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
        public Hashes.M3HashList hashList;

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
            this.hashList = null;
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

            hashList = new Hashes.M3HashList();
            hashList.Initialize();

            // little endian?
            bool littleEndian;
            {
                data.Seek(8, SeekOrigin.Begin);
                var platform = data.ReadString(4, true, Encoding.ASCII);

                littleEndian = platform == "PC";
            }

            // header
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

            // Read resources count info
            file.resourceInfo.count = (int)data.ReadValueU32(littleEndian);

            // Read resources types
            file.resourceTypes = new DataStorage.SDSResourceType[file.resourceInfo.count];

            // Read resources info
            for (var i = 0; i < file.resourceInfo.count; ++i)
            {
                file.resourceTypes[i] = new DataStorage.SDSResourceType();
                file.resourceTypes[i].typeIndex = (int)data.ReadValueU32(littleEndian);
                file.resourceTypes[i].strLen = (int)data.ReadValueU32(littleEndian);
                file.resourceTypes[i].typeName = new byte[file.resourceTypes[i].strLen];
                data.Read(file.resourceTypes[i].typeName, 0, file.resourceTypes[i].typeName.Length);
                var realTypename = Encoding.ASCII.GetString(file.resourceTypes[i].typeName);
                file.resourceTypes[i].parent = (int)data.ReadValueU32(littleEndian);

                var type = new DataStorage.ResourceTypeReference();
                type.Id = (uint)file.resourceTypes[i].typeIndex;
                type.Name = realTypename;
                this.ResourceTypes.Add(type);
            }

            // Read chunk info
            file.chunkInfo.magic = (int)data.ReadValueU32(littleEndian);
            file.chunkInfo.alignment = (int)data.ReadValueU32(littleEndian);

            var tmp = new byte[1];
            data.Read(tmp, 0, tmp.Length);
            file.chunkInfo.flags = (char)tmp[0];

            if (file.chunkInfo.magic != 0x6C7A4555
                || file.chunkInfo.alignment != 0x10000 && file.chunkInfo.alignment != 0x00004000
                || file.chunkInfo.flags != 4)
            {
                throw new InvalidOperationException();
            }
            
            // Read chunk data
            var blockStream = new BlockStream(data);
            long virtualOffset = 0;
            var index = 0;
            file.chunkData = new List<DataStorage.SDSChunk>();
            while (true)
            {
                var i = index;
                var chunk = new DataStorage.SDSChunk();
                chunk.dataSize = (int)data.ReadValueU32(littleEndian);

                var tmp2 = new byte[1];
                data.Read(tmp2, 0, tmp2.Length);
                chunk.dataType = (char)tmp2[0];

                uint size = (uint)chunk.dataSize;
                bool compressed = chunk.dataType != 0;
                if (size == 0)
                {
                    break;
                }

                if (compressed == true)
                {
                    var compressionInfo = new DataStorage.CompressedBlockHeader();
                    compressionInfo.Deserialize(data, littleEndian);

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

                    data.Seek(compressionInfo.CompressedSize, SeekOrigin.Current);
                }
                else
                {
                    blockStream.AddUncompressedBlock(
                           virtualOffset,
                           size,
                           data.Position);

                    data.Seek(size, SeekOrigin.Current);
                }

                file.chunkData.Add(chunk);
                ++index;
                virtualOffset += file.chunkInfo.alignment;
            }

            // Read files
            blockStream.Seek(0, SeekOrigin.Begin);
            {
                this.Entries.Clear();
                for (uint i = 0; i < file.dataCount; i++)
                {
                    var position = blockStream.Position;
                    var memory = blockStream.ReadToMemoryStreamSafe(36, littleEndian);

                    var fileHeader = new DataStorage.FileHeader();
                    fileHeader.Deserialize(memory, littleEndian);

                    var Name = memory.ReadValueU32(littleEndian);
                    string description = hashList.GetStringByHash(Name);
                    blockStream.Seek(blockStream.Position - 4, SeekOrigin.Begin);

                    if (fileHeader.TypeId == 0) // Flash/XML
                    {
                        var blockPosition = blockStream.Position;
                        var memoryName = blockStream.ReadToMemoryStreamSafe(255, littleEndian);
                        memoryName.Seek(memoryName.Position + 2, SeekOrigin.Begin);

                        var Name_ = memoryName.ReadStringU16(littleEndian);
                        description = Name_;
                        blockStream.Seek(blockPosition, SeekOrigin.Begin);
                    }

                    if (fileHeader.TypeId == 1) // MemFile
                    {
                        var blockPosition = blockStream.Position;
                        var memoryName = blockStream.ReadToMemoryStreamSafe(255, littleEndian);
                        memoryName.Seek(memoryName.Position + 6, SeekOrigin.Begin);

                        var Name_ = memoryName.ReadStringU32(littleEndian);
                        description = Name_;
                        blockStream.Seek(blockPosition, SeekOrigin.Begin);
                    }

                    this.Entries.Add(new Entry()
                    {
                        Header = fileHeader,
                        Description = description,
                        Offset = blockStream.Position,// Data offset
                        Size = fileHeader.Size - 36,
                    });

                    blockStream.Seek(position + (fileHeader.Size), SeekOrigin.Begin);
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
                byte[] buffer = new byte[0x10000]; // - 30];
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
