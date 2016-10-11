using System;
using System.IO;
using System.Text;
using Gibbed.Helpers;

namespace Gibbed.Illusion.FileFormats.DataStorage
{
    // Copyright and thanks to CarLuver96 (http://forum.xentax.com/viewtopic.php?f=10&t=15336)
    public class SDSChunkData
    {
        public int memorySize; // size in memory? (e.g. uncompressed size)

        public int bufferOffset; // offset to start of data

        public int memorySize_2; // SDSData.unk_04 again?

        public short unk_0A; // number of something? (this format is confusing)
        public short unk_0C; // 0x80F?

        public short bufferSize; // this might be an int, but unlikely
                                 /*
                                     <<< Data padding >>>
                                 */
                                 /* @ bufferOffset */
        public byte[] buffer;
    };

    public class SDSChunk
    {
        public int dataSize; // size of SDSChunkData
        public char dataType; // reference to SDSDataInfo.dataType

        public SDSChunkData data;
    }

    public class SDSChunkInfo
    {
        public int magic; // 0x6C7A4555 ('UEzl')

        public int alignment; // 0x10000? (never seems to change)
        public char flags; // 4?

        public SDSChunk[] data;
    };

    public class SDSResourceInfo
    {
        public int count; // how many data types there are
    }

    public class SDSResourceType
    {
        public int typeIndex; // local index to reference this type

        public int strLen;
        public byte[] typeName; // no null-terminator

        public int reserved; // possibly a guard? (always zero?)
    }

    public class SDSFile : ICloneable
    {
        public int magic; // 0x534453 ('SDS\0')

        public int version; // 0x14 (v20 / v1.4?)

        public int type;
        /*enum SDSType : int
        {
            PC = 0x4350,
            
            PS4 = 0x345350,
            XB1 = 0x314258,
        };*/

        public int unk_0C; // 0x5DE53FDE

        public uint ResourceTypeTableOffset;
        public uint BlockTableOffset;

        public int unk_18; // always zero?
        public int unk_1C; // offset / size of something?
        public int unk_20; // always zero?
        public int unk_24; // offset / size of something?
        public int unk_28; // always zero?
        public int unk_2C; // 1
        public int unk_30; // always zero?
        public int unk_34; // always zero?
        public int unk_38; // always zero?
        public int unk_3C; // always zero?

        public int chunkCount; // how many data chunks? (educated guess)
        public int reserved; // checksum, hash, or key of some sort?

        public int dataCount;

        public SDSResourceInfo resourceInfo;
        public SDSResourceType[] resourceTypes;

        public SDSChunkInfo chunkInfo;
        public SDSChunk[] chunkData;

        public void Deserialize(Stream input, bool littleEndian)
        {
            this.resourceInfo = new SDSResourceInfo();
            this.chunkInfo = new SDSChunkInfo();

            this.magic = (int)input.ReadValueU32(littleEndian);
            this.version = (int)input.ReadValueU32(littleEndian);
            this.type = (int)input.ReadValueU32(littleEndian);

            this.unk_0C = (int)input.ReadValueU32(littleEndian);

            this.ResourceTypeTableOffset = input.ReadValueU32(littleEndian);
            this.BlockTableOffset = input.ReadValueU32(littleEndian);

            this.unk_18 = (int)input.ReadValueU32(littleEndian);
            this.unk_1C = (int)input.ReadValueU32(littleEndian);
            this.unk_20 = (int)input.ReadValueU32(littleEndian);
            this.unk_24 = (int)input.ReadValueU32(littleEndian);
            this.unk_28 = (int)input.ReadValueU32(littleEndian);
            this.unk_2C = (int)input.ReadValueU32(littleEndian);
            this.unk_30 = (int)input.ReadValueU32(littleEndian);
            this.unk_34 = (int)input.ReadValueU32(littleEndian);
            this.unk_38 = (int)input.ReadValueU32(littleEndian);
            this.unk_3C = (int)input.ReadValueU32(littleEndian);

            this.chunkCount = (int)input.ReadValueU32(littleEndian);
            this.reserved = (int)input.ReadValueU32(littleEndian);
        }

        public object Clone()
        {
            return new SDSFile()
            {
                magic = this.magic,
                version = this.version,
                type = this.type,
                unk_0C = this.unk_0C,
                ResourceTypeTableOffset = this.ResourceTypeTableOffset,
                BlockTableOffset = this.BlockTableOffset,
                unk_18 = this.unk_18,
                unk_1C = this.unk_1C,
                unk_20 = this.unk_20, // always zero?
                unk_24 = this.unk_24, // offset / size of something?
                unk_28 = this.unk_28, // always zero?
                unk_2C = this.unk_2C, // 1
                unk_30 = this.unk_30, // always zero?
                unk_34 = this.unk_34, // always zero?
                unk_38 = this.unk_38, // always zero?
                unk_3C = this.unk_3C, // always zero?
                chunkCount = this.chunkCount, // 0x14 (v20 / v1.4?)
                reserved = this.reserved,
                dataCount = this.dataCount,
                resourceInfo = this.resourceInfo,
                resourceTypes = this.resourceTypes,
                chunkInfo = this.chunkInfo,
                chunkData = this.chunkData
            };
        }
    };

    public class ArchiveHeader : ICloneable
    {
        public uint ResourceTypeTableOffset;
        public uint BlockTableOffset;
        public uint XmlOffset;
        public uint SlotRamRequired;
        public uint SlotVramRequired;
        public uint OtherRamRequired;
        public uint OtherVramRequired;
        public uint Unknown1C; // flags of some sort : see note 2
        public byte[] Unknown20;
        public uint FileCount;

        public void Serialize(Stream output, bool littleEndian)
        {
            output.WriteValueU32(this.ResourceTypeTableOffset, littleEndian);
            output.WriteValueU32(this.BlockTableOffset, littleEndian);
            output.WriteValueU32(this.XmlOffset, littleEndian);
            output.WriteValueU32(this.SlotRamRequired, littleEndian);
            output.WriteValueU32(this.SlotVramRequired, littleEndian);
            output.WriteValueU32(this.OtherRamRequired, littleEndian);
            output.WriteValueU32(this.OtherVramRequired, littleEndian);
            output.WriteValueU32(this.Unknown1C, littleEndian);
            output.Write(this.Unknown20, 0, this.Unknown20.Length);
            output.WriteValueU32(this.FileCount, littleEndian);
        }

        public void Deserialize(Stream input, bool littleEndian)
        {
            this.ResourceTypeTableOffset = input.ReadValueU32(littleEndian);
            this.BlockTableOffset = input.ReadValueU32(littleEndian);
            this.XmlOffset = input.ReadValueU32(littleEndian);
            this.SlotRamRequired = input.ReadValueU32(littleEndian);
            this.SlotVramRequired = input.ReadValueU32(littleEndian);
            this.OtherRamRequired = input.ReadValueU32(littleEndian);
            this.OtherVramRequired = input.ReadValueU32(littleEndian);
            this.Unknown1C = input.ReadValueU32(littleEndian);
            this.Unknown20 = new byte[16];
            input.Read(this.Unknown20, 0, this.Unknown20.Length);
            this.FileCount = input.ReadValueU32(littleEndian);
        }

        public object Clone()
        {
            return new ArchiveHeader()
            {
                ResourceTypeTableOffset = this.ResourceTypeTableOffset,
                BlockTableOffset = this.BlockTableOffset,
                XmlOffset = this.XmlOffset,
                SlotRamRequired = this.SlotRamRequired,
                SlotVramRequired = this.SlotVramRequired,
                OtherRamRequired = this.OtherRamRequired,
                OtherVramRequired = this.OtherVramRequired,
                Unknown1C = this.Unknown1C,
                Unknown20 = (byte[])this.Unknown20.Clone(),
                FileCount = this.FileCount,
            };
        }
    }
}
