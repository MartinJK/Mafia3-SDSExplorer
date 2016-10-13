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

        private static object DeserializeNodeEntry(Stream input)
        {
            var name_ = input.ReadStringU8(3);
            input.Seek(1, SeekOrigin.Current); // 0 terminator of string

            var nodeType = input.ReadValueU8();
            var unk1 = input.ReadValueU8();

            switch (nodeType)
            {
                case 1:
                {
                    var nameLength = input.ReadValueU8();
                    var childCount = input.ReadValueU16();
                    var attributeCount = input.ReadValueU8();
                    input.ReadValueU32();

                    var name = input.ReadString(nameLength + 1, true, Encoding.UTF8);

                    var node = new NodeEntry()
                    {
                        Name = name,
                    };

                    var children = new List<object>();
                    for (ushort i = 0; i < childCount; i++)
                    {
                        children.Add(DeserializeNodeEntry(input));
                    }

                    if (children.Count == 1 && children[0] is DataValue)
                    {
                        node.Value = (DataValue)children[0];
                    }
                    else
                    {
                        foreach (var child in children)
                        {
                            node.Children.Add((NodeEntry)child);
                        }
                    }

                    for (byte i = 0; i < attributeCount; i++)
                    {
                        var child = DeserializeNodeEntry(input);

                        if (child is NodeEntry)
                        {
                            var data = (NodeEntry)child;

                            if (data.Children.Count != 0 ||
                                data.Attributes.Count != 0)
                            {
                                throw new FormatException();
                            }

                            var attribute = new AttributeEntry()
                            {
                                Name = data.Name,
                                Value = data.Value,
                            };
                            node.Attributes.Add(attribute);
                        }
                        else
                        {
                            node.Attributes.Add((AttributeEntry)child);
                        }
                    }

                    return node;
                }

                case 4:
                {
                    var valueType = input.ReadValueU8();
                    if (valueType == 0)
                    {
                        var valueLength = input.ReadValueU16();
                        var value = input.ReadString(valueLength + 1, true, Encoding.UTF8);
                        return new DataValue(DataType.String, value);
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                }

                case 5:
                {
                    var nameLength = input.ReadValueU8();
                    var name = input.ReadString(nameLength + 1, true, Encoding.UTF8);

                    var attribute = new NodeEntry()
                    {
                        Name = name,
                    };

                    attribute.Value = (DataValue)DeserializeNodeEntry(input);
                    return attribute;
                }

                default:
                {
                    throw new NotImplementedException();
                }
            }
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
