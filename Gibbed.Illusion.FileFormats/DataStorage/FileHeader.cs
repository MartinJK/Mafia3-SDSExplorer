using System;
using System.IO;
using Gibbed.Helpers;

namespace Gibbed.Illusion.FileFormats.DataStorage
{
    public class FileHeader : ICloneable
    {
        public uint TypeId;
        public uint Size; // includes headers (such as the first 30 bytes)
        public ushort Version;
        public uint SlotRamRequired;
        public uint SlotVramRequired;
        public uint OtherRamRequired;
        public uint OtherVramRequired;
        public ushort _f1E;
        public uint _f20;

        public void Serialize(Stream output, bool littleEndian)
        {
            output.WriteValueU32(this.TypeId, littleEndian);
            output.WriteValueU32(this.Size, littleEndian);
            output.WriteValueU16(this.Version, littleEndian);
            output.WriteValueU32(this.SlotRamRequired, littleEndian);
            output.WriteValueU32(this.SlotVramRequired, littleEndian);
            output.WriteValueU32(this.OtherRamRequired, littleEndian);
            output.WriteValueU32(this.OtherVramRequired, littleEndian);
            output.WriteValueU16(this._f1E, littleEndian);
            output.WriteValueU32(this._f20, littleEndian);
        }

        public void Deserialize(Stream input, bool littleEndian)
        {
            this.TypeId = input.ReadValueU32(littleEndian);
            this.Size = input.ReadValueU32(littleEndian);
            this.Version = input.ReadValueU16(littleEndian);
            this.SlotRamRequired = input.ReadValueU32(littleEndian);
            this.SlotVramRequired = input.ReadValueU32(littleEndian);
            this.OtherRamRequired = input.ReadValueU32(littleEndian);
            this.OtherVramRequired = input.ReadValueU32(littleEndian);
            this._f1E = input.ReadValueU16(littleEndian);
            this._f20 = input.ReadValueU32(littleEndian);
        }

        public object Clone()
        {
            return new FileHeader()
            {
                TypeId = this.TypeId,
                Size = this.Size,
                Version = this.Version,
                SlotRamRequired = this.SlotRamRequired,
                SlotVramRequired = this.SlotVramRequired,
                OtherRamRequired = this.OtherRamRequired,
                OtherVramRequired = this.OtherVramRequired,
                _f1E = this._f1E,
                _f20 = this._f20
            };
        }
    }
}
