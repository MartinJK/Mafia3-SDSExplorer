using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.XPath;
using Gibbed.Helpers;
using System.Diagnostics;
using ComponentAce.Compression.Libs.zlib;

namespace Gibbed.Illusion.FileFormats
{
    // SDS = SluttyDataStorage :D?
    public class SdsReader
    {
        public DataStorage.SDSFile Header;
        private string Platform;
        private Stream FileStream;
        private Stream DataStream;
        private BlockStream BlockStream;

        public List<DataStorage.ResourceTypeReference> ResourceTypes =
            new List<DataStorage.ResourceTypeReference>();

        public string Xml;

        public List<Entry> Entries =
            new List<Entry>();

        public SdsReader()
        {
            this.FileStream = null;
            this.DataStream = null;
            this.BlockStream = null;
        }

        public bool Open(string path)
        {
            if (this.BlockStream != null)
            {
                this.BlockStream.FreeLoadedBlocks();
            }

            if (this.DataStream != null)
            {
                if (this.FileStream != this.DataStream)
                {
                    this.DataStream.Close();
                }
            }

            if (this.FileStream != null)
            {
                this.FileStream.Close();
            }

            var input = File.Open(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite);

            this.Initialize(input);

            return true;
        }

        public void Close()
        {
            this.BlockStream.FreeLoadedBlocks();
            this.FileStream.Close();
        }

        private void Initialize(Stream input)
        {
            Stream data = input;

            if (input.Length >= (0x90 + 15))
            {
                input.Seek(0x90, SeekOrigin.Begin);
                byte[] fsfh = new byte[15];
                input.Read(fsfh, 0, fsfh.Length);

                // "tables/fsfh.bin"
                if (FNV.Hash64(fsfh, 0, fsfh.Length) == 0x39DD22E69C74EC6F)
                {
                    input.Seek(0x10000, SeekOrigin.Begin);

                    FileFormats.TEA.Setup setup = null;

                    byte[] encryptedHeader = new byte[8];
                    input.Read(encryptedHeader, 0, encryptedHeader.Length);

                    byte[] decryptedHeader = new byte[8];
                    foreach (var current in FileFormats.TEA.Mafia3.Setups)
                    {
                        Array.Copy(
                            encryptedHeader,
                            decryptedHeader,
                            encryptedHeader.Length);

                        FileFormats.TEA.Decrypt(
                            decryptedHeader,
                            0,
                            8,
                            FileFormats.TEA.Mafia3.Keys,
                            current.Sum,
                            current.Delta,
                            current.Rounds);

                        if (BitConverter.ToUInt32(decryptedHeader, 0) == 0x00534453)
                        {
                            setup = current;
                            break;
                        }
                    }

                    if (setup == null)
                    {
                        throw new InvalidOperationException();
                    }

                    input.Seek(0x10000, SeekOrigin.Begin);

                    data = new MemoryStream();

                    long left = input.Length - input.Position;
                    byte[] buffer = new byte[0x10000];
                    while (left > 0)
                    {
                        int block = (int)(Math.Min(left, buffer.Length));
                        input.Read(buffer, 0, block);
                        FileFormats.TEA.Decrypt(buffer, 0, block, FileFormats.TEA.Mafia3.Keys, setup.Sum, setup.Delta, setup.Rounds);
                        data.Write(buffer, 0, block);
                        left -= block;
                    }

                    data.Position = 0;
                }
            }

            bool littleEndian;
            {
                data.Seek(8, SeekOrigin.Begin);
                var platform = data.ReadString(4, true, Encoding.ASCII);

                littleEndian = platform == "PC";
            }

            // header
            data.Seek(0, SeekOrigin.Begin);
            unsafe
            {
                var buffer = new byte[data.Length];
                data.Read(buffer, 0, buffer.Length);
                TypedReference tr = __makeref(buffer);
                IntPtr ptr = **(IntPtr**)(&tr);
                string hex = ptr.ToString("X");
                string hexOutput = String.Format("Data: 0x{0:X}", hex);
                Debug.WriteLine(hexOutput);
            }

            data.Seek(0, SeekOrigin.Begin);
            {
                var memory = data.ReadToMemoryStreamSafe(
                    12, littleEndian);
                var magic = memory.ReadString(4, true, Encoding.ASCII);
                var version = memory.ReadValueU32(littleEndian);
                var platform = memory.ReadString(4, true, Encoding.ASCII);

                if (magic != "SDS")
                {
                    throw new FormatException("not an SDS archive");
                }

                if (version != 20 && version != 19)
                {
                    throw new FormatException("unsupported SDS archive version");
                }

                this.Platform = platform;
            }

            data.Seek(0, SeekOrigin.Begin);
            DataStorage.SDSFile file = new DataStorage.SDSFile();
            var mem = data; // data.ReadToMemoryStreamSafe(data.Length, littleEndian);
            file.Deserialize(mem, littleEndian);

            file.resourceInfo.count = (int)input.ReadValueU32(littleEndian);

            file.resourceTypes = new DataStorage.SDSResourceType[file.resourceInfo.count];

            for (var i = 0; i < file.resourceInfo.count; ++i)
            {
                file.resourceTypes[i] = new DataStorage.SDSResourceType();
                file.resourceTypes[i].typeIndex = (int)input.ReadValueU32(littleEndian);
                file.resourceTypes[i].strLen = (int)input.ReadValueU32(littleEndian);
                file.resourceTypes[i].typeName = new byte[file.resourceTypes[i].strLen];
                input.Read(file.resourceTypes[i].typeName, 0, file.resourceTypes[i].typeName.Length);
                var realTypename = Encoding.ASCII.GetString(file.resourceTypes[i].typeName);
                file.resourceTypes[i].reserved = (int)input.ReadValueU32(littleEndian);

                var type = new DataStorage.ResourceTypeReference();
                type.Id = (uint)file.resourceTypes[i].typeIndex;
                type.Name = realTypename;
                this.ResourceTypes.Add(type);
            }

            file.chunkInfo.magic = (int)input.ReadValueU32(littleEndian);
            file.chunkInfo.alignment = (int)input.ReadValueU32(littleEndian);

            var tmp = new byte[1];
            input.Read(tmp, 0, tmp.Length);
            file.chunkInfo.flags = (char)tmp[0];

            if (file.chunkInfo.magic != 0x6C7A4555 || file.chunkInfo.alignment != 0x10000 || file.chunkInfo.flags != 4)
            {
                throw new InvalidOperationException();
            }

            file.chunkData = new DataStorage.SDSChunk[file.chunkCount];

            var blockStream = new BlockStream(input);
            long virtualOffset = 0;
            for (var i = 0; i < file.chunkCount; ++i)
            {
                file.chunkData[i] = new DataStorage.SDSChunk();
                file.chunkData[i].dataSize = (int)input.ReadValueU32(littleEndian);

                var tmp2 = new byte[1];
                input.Read(tmp2, 0, tmp2.Length);
                file.chunkData[i].dataType = (char)tmp2[0];

                uint size = (uint)file.chunkData[i].dataSize;
                bool compressed = file.chunkData[i].dataType != 0;
                if (size == 0)
                {
                    break;
                }

                if (compressed == true)
                {
                    var compressionInfo = new DataStorage.CompressedBlockHeader();
                    compressionInfo.Deserialize(input, littleEndian);

                    if (compressionInfo.Unknown04 != 32 ||
                            compressionInfo.Unknown08 != 65536 ||
                            compressionInfo.Unknown0C != 135200769)
                    {
                        throw new InvalidOperationException();
                    }

                    if (size - 32 != compressionInfo.CompressedSize)
                    {
                        throw new InvalidOperationException();
                    }

                    blockStream.AddCompressedBlock(
                        virtualOffset,
                        compressionInfo.UncompressedSize,
                        data.Position,
                        compressionInfo.CompressedSize);

                    input.Seek(compressionInfo.CompressedSize, SeekOrigin.Current);
                }
                else
                {
                    blockStream.AddUncompressedBlock(
                           virtualOffset,
                           size,
                           data.Position);

                    input.Seek(size, SeekOrigin.Current);
                }

                virtualOffset += file.chunkInfo.alignment;
                /*file.dataChunk[i].data.memorySize = (int)input.ReadValueU32(littleEndian);
                file.dataChunk[i].data.bufferOffset = (int)input.ReadValueU32(littleEndian);
                file.dataChunk[i].data.memorySize_2 = (int)input.ReadValueU32(littleEndian);

                file.dataChunk[i].data.unk_0A = (short)input.ReadValueU16(littleEndian);
                file.dataChunk[i].data.unk_0C = (short)input.ReadValueU16(littleEndian);

                file.dataChunk[i].data.bufferSize = (short)input.ReadValueU16(littleEndian);

                file.dataChunk[i].data.buffer = new byte[file.dataChunk[i].data.bufferSize];
                input.Read(file.dataChunk[i].data.buffer, 0, file.dataChunk[i].data.bufferSize);*/
            }

            blockStream.Seek(0, SeekOrigin.Begin);
            {
                this.Entries.Clear();
                for (uint i = 0; i < file.chunkCount; i++)
                {
                    var position = blockStream.Position;
                    var memory = blockStream.ReadToMemoryStreamSafe(26, littleEndian);

                    var fileHeader = new DataStorage.FileHeader();
                    fileHeader.Deserialize(memory, littleEndian);
                    /*
                    string description = descriptions == null ? null : descriptions[(int)i];
                    if (string.IsNullOrEmpty(description) == true ||
                        description == "not available")
                    {
                        description = string.Format("{0:X8}", position);
                    }
                    */
                    string description = "not available";

                    this.Entries.Add(new Entry()
                    {
                        Header = fileHeader,
                        Description = description,
                        Offset = blockStream.Position,
                        Size = fileHeader.Size - 30,
                    });

                    blockStream.Seek(position + fileHeader.Size, SeekOrigin.Begin);
                }
            }
            
            this.Header = file;
            this.BlockStream = blockStream;
            
            this.DataStream = data;
            this.FileStream = input;

            this.BlockStream.FreeLoadedBlocks();
        }

        public void ExportData(Stream output)
        {
            this.BlockStream.SaveUncompressed(output);
        }

        public void ExportData(string outputPath)
        {
            using (var output = File.Create(outputPath))
            {
                this.BlockStream.SaveUncompressed(output);
            }

            this.BlockStream.FreeLoadedBlocks();
        }

        public class Entry
        {
            public DataStorage.FileHeader Header;
            public long Offset;
            public uint Size;
            public uint TypeId { get { return this.Header.TypeId; } }
            public string Description { get; internal set; }
        }

        public void ExportEntry(Entry entry, string outputPath)
        {
            using (var output = File.Create(outputPath))
            {
                this.BlockStream.Seek(entry.Offset, SeekOrigin.Begin);
                long left = entry.Size;
                byte[] buffer = new byte[0x10000];
                while (left > 0)
                {
                    int block = (int)(Math.Min(left, buffer.Length));
                    this.BlockStream.Read(buffer, 0, block);
                    output.Write(buffer, 0, block);
                    left -= block;
                }
            }

            this.BlockStream.FreeLoadedBlocks();
        }

        public MemoryStream GetEntry(Entry entry)
        {
            this.BlockStream.Seek(entry.Offset, SeekOrigin.Begin);

            var memory = new MemoryStream();
            {
                long left = entry.Size;
                byte[] buffer = new byte[0x10000];
                while (left > 0)
                {
                    int block = (int)(Math.Min(left, buffer.Length));
                    this.BlockStream.Read(buffer, 0, block);
                    memory.Write(buffer, 0, block);
                    left -= block;
                }
                memory.Position = 0;
            }

            //this.BlockStream.FreeLoadedBlocks();
            return memory;
        }

        public SdsMemory LoadToMemory()
        {
            var memory = new SdsMemory();

            memory.Header = (DataStorage.SDSFile)this.Header.Clone();

            memory.ResourceTypes.Clear();
            foreach (var resourceType in this.ResourceTypes)
            {
                memory.ResourceTypes.Add((DataStorage.ResourceTypeReference)resourceType.Clone());
            }

            memory.Entries.Clear();
            foreach (var entry in this.Entries)
            {
                memory.Entries.Add(new SdsMemory.Entry()
                {
                    Header = (DataStorage.FileHeader)entry.Header.Clone(),
                    Description = entry.Description,
                    Data = this.GetEntry(entry),
                });
            }

            //memory.Xml = this.Xml;

            return memory;
        }
    }
}
