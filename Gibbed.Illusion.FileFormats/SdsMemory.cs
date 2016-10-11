using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Gibbed.Illusion.FileFormats
{
    public class SdsMemory
    {
        public DataStorage.SDSFile Header;
        private string Platform;

        public List<DataStorage.ResourceTypeReference> ResourceTypes =
            new List<DataStorage.ResourceTypeReference>();

        public List<Entry> Entries =
            new List<Entry>();

        public class Entry
        {
            public DataStorage.FileHeader Header;
            public uint TypeId { get { return this.Header.TypeId; } }
            public string Description { get; internal set; }
            public MemoryStream Data;
        }
    }
}
