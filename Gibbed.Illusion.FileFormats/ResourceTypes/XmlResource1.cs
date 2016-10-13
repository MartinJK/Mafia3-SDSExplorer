using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Gibbed.Helpers;
using System.Xml;
using System.Diagnostics;

namespace Gibbed.Illusion.FileFormats.ResourceTypes
{
    public class XmlResource1
    {
        public static void Serialize(Stream output, string content)
        {
            throw new NotImplementedException();
        }

        public static string Deserialize(Stream input)
        {
            var pos = input.Position;
            input.Seek(0, SeekOrigin.Begin);
            unsafe
            {
                var buffer = new byte[input.Length];
                input.Read(buffer, 0, buffer.Length);
                TypedReference tr = __makeref(buffer);
                IntPtr ptr = **(IntPtr**)(&tr);
                string hex = ptr.ToString("X");
                string hexOutput = String.Format("Data: 0x{0:X}", hex);
                Debug.WriteLine(hexOutput);
            }
            input.Seek(2, SeekOrigin.Begin);

            var name = input.ReadStringU8(3);
            input.Seek(1, SeekOrigin.Current); // 0 terminator of string
            
            var root = (NodeEntry)DeserializeNodeEntry(input);
            root.Name = name;

            var settings = new XmlWriterSettings();
            settings.Indent = true;

            var output = new StringBuilder();
            var writer = XmlWriter.Create(output, settings);

            writer.WriteStartDocument();
            WriteXmlNode(writer, root);
            writer.WriteEndDocument();

            writer.Flush();
            return output.ToString();
        }

        private static void WriteXmlNode(XmlWriter writer, NodeEntry node)
        {
            writer.WriteStartElement(node.Name);

            foreach (var attribute in node.Attributes)
            {
                writer.WriteStartAttribute(attribute.Name);
                writer.WriteValue(
                    attribute.Value == null ?
                    "" : attribute.Value.ToString());
                writer.WriteEndAttribute();
            }

            foreach (var child in node.Children)
            {
                WriteXmlNode(writer, child);
            }

            if (node.Value != null)
            {
                writer.WriteValue(node.Value.ToString());
            }

            writer.WriteEndElement();
        }

        private static object DeserializeNodeEntryInternal(NodeEntry previousNode, Stream input)
        {
            var node = new NodeEntry()
            {
                Name = "undefined",
            };

            byte[] unkBytes = new byte[6];
            input.Read(unkBytes, 0, unkBytes.Length);

            byte[] stringArr = new byte[255];
            bool read = true;
            char index = (char)0;
            while (read)
            {
                byte[] reader = new byte[1];
                input.Read(reader, 0, 1);
                read = reader[0] != 0;
                stringArr[index] = reader[0];
                if (index == 0 && !read)
                {
                    read = true;
                }

                if (index == 1 && !read)
                {
                    break;
                }
                index++;
            }

            var name = Encoding.ASCII.GetString(stringArr);
            name = name.Trim('\0');
            name = name.Replace("\0", "");
            node.Name = name;
            
            var nodeType = input.ReadValueU8();
            var subNotes = input.ReadValueU8();
            if (nodeType == 4)
            {
                DeserializeNodeEntryInternal(node, input);
            }

            double num;
            if (double.TryParse(name, out num) || name.Contains(":"))
            {
                previousNode.Value = new DataValue(DataType.String, name);
            }
            else
            {
                children.Add(node);
            }
            return node;
        }

        private static List<object> children;

        private static object DeserializeNodeEntry(Stream input)
        {
            children = new List<object>();

            var name_ = input.ReadStringU8(3); // File path
            input.Seek(1, SeekOrigin.Current); // 0 terminator of string
            input.Seek(18, SeekOrigin.Current); // 0 terminator of string

            var node = new NodeEntry();

            var nodeType = input.ReadValueU8();
            var subNotes = input.ReadValueU8();
            
            if (nodeType == 4)
            {
                byte[] unkBytes = new byte[6];
                input.Read(unkBytes, 0, unkBytes.Length);

                byte[] stringArr = new byte[255];
                bool read = true;
                char index = (char)0;
                while(read)
                {
                    byte[] reader = new byte[1];
                    input.Read(reader, 0, 1);
                    read = reader[0] != 0;
                    stringArr[index] = reader[0];
                    if(index == 0 && !read)
                    {
                        read = true;
                    }

                    if(index == 1 && !read)
                    {
                        break;
                    }
                    index++;
                }

                var name = Encoding.ASCII.GetString(stringArr);
                name = name.Trim('\0');
                name = name.Replace("\0", "");

                nodeType = input.ReadValueU8();
                subNotes = input.ReadValueU8();

                if(nodeType == 4)
                {
                    DeserializeNodeEntryInternal(node, input);
                }
            }

            foreach (var child in children)
            {
                node.Children.Add((NodeEntry)child);
            }

            return node;
        }

        private class NodeEntry
        {
            public string Name;
            public DataValue Value;
            public List<NodeEntry> Children = new List<NodeEntry>();
            public List<AttributeEntry> Attributes = new List<AttributeEntry>();
        }

        private class AttributeEntry
        {
            public string Name;
            public DataValue Value;
        }

        private enum DataType
        {
            String = 0,
        }

        private class DataValue
        {
            public DataType Type;
            public object Value;

            public DataValue(DataType type, object value)
            {
                this.Type = type;
                this.Value = value;
            }

            public override string ToString()
            {
                return this.Value.ToString();
            }
        }
    }
}
