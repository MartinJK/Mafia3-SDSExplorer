using System;
using System.IO;
using System.Text;
using Gibbed.Helpers;
using System.Collections.Generic;

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

        public int parent;
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

        public int checkSum; // //+0xC // FNVHash32 from bytes of dwMagic, dwVersion and dwPlatform

        public uint ResourceTypeTableOffset;
        public uint BlockTableOffset;

        public int xmlOffset; // always zero?
        public int slotRamRequired; // offset / size of something?
        public int slotVRamRequired; // always zero?
        public int otherRamRequired; // offset / size of something?
        public int otherVRamRequired; // always zero?
        public int someFlag; // 1

        public int unk_30; // always zero?
        public int unk_34; // always zero?
        public int unk_38; // always zero?
        public int unk_3C; // always zero?

        public int dataCount;
        public int archiveHeaderChecksum; // FNVHash32 of the all header bytes

        public SDSResourceInfo resourceInfo;
        public SDSResourceType[] resourceTypes;

        public SDSChunkInfo chunkInfo;
        public List<SDSChunk> chunkData;

        public void Deserialize(Stream input, bool littleEndian)
        {
            this.resourceInfo = new SDSResourceInfo();
            this.chunkInfo = new SDSChunkInfo();

            this.magic = (int)input.ReadValueU32(littleEndian);
            this.version = (int)input.ReadValueU32(littleEndian);
            this.type = (int)input.ReadValueU32(littleEndian);

            this.checkSum = (int)input.ReadValueU32(littleEndian);

            this.ResourceTypeTableOffset = input.ReadValueU32(littleEndian);
            this.BlockTableOffset = input.ReadValueU32(littleEndian);

            this.xmlOffset = (int)input.ReadValueU32(littleEndian);
            this.slotRamRequired = (int)input.ReadValueU32(littleEndian);
            this.slotVRamRequired = (int)input.ReadValueU32(littleEndian);
            this.otherRamRequired = (int)input.ReadValueU32(littleEndian);
            this.otherVRamRequired = (int)input.ReadValueU32(littleEndian);
            this.someFlag = (int)input.ReadValueU32(littleEndian);
            this.unk_30 = (int)input.ReadValueU32(littleEndian);
            this.unk_34 = (int)input.ReadValueU32(littleEndian);
            this.unk_38 = (int)input.ReadValueU32(littleEndian);
            this.unk_3C = (int)input.ReadValueU32(littleEndian);

            this.dataCount = (int)input.ReadValueU32(littleEndian);
            this.archiveHeaderChecksum = (int)input.ReadValueU32(littleEndian);
        }

        public object Clone()
        {
            return new SDSFile()
            {
                magic = this.magic,
                version = this.version,
                type = this.type,
                checkSum = this.checkSum,
                ResourceTypeTableOffset = this.ResourceTypeTableOffset,
                BlockTableOffset = this.BlockTableOffset,
                xmlOffset = this.xmlOffset,
                slotRamRequired = this.slotRamRequired,
                slotVRamRequired = this.slotVRamRequired, // always zero?
                otherRamRequired = this.otherRamRequired, // offset / size of something?
                otherVRamRequired = this.otherVRamRequired, // always zero?
                someFlag = this.someFlag, // 1
                unk_30 = this.unk_30, // always zero?
                unk_34 = this.unk_34, // always zero?
                unk_38 = this.unk_38, // always zero?
                unk_3C = this.unk_3C, // always zero?
                dataCount = this.dataCount, // 0x14 (v20 / v1.4?)
                archiveHeaderChecksum = this.archiveHeaderChecksum,
                resourceInfo = this.resourceInfo,
                resourceTypes = this.resourceTypes,
                chunkInfo = this.chunkInfo,
                chunkData = this.chunkData
            };
        }
    };
}
