using System;
using System.IO;
using System.Text;
using Gibbed.Spore.Helpers;

namespace Gibbed.Spore.Properties
{
	[PropertyDefinition("text", "texts", 34)]
	public class TextProperty : ComplexProperty
	{
		public uint TableId;
		public uint InstanceId;
		public string PlaceholderText;

		public override void ReadProp(Stream input, bool array)
		{
			if (array == true)
			{
				this.TableId = input.ReadU32();
				this.InstanceId = input.ReadU32();

				int size = (int)input.Length - 8;

				byte[] data = new byte[size];
				input.Read(data, 0, size);

				if (((size - 8) % 2) != 0)
				{
					throw new Exception("array size is not a multiple of two");
				}

				int end = 0;
				for (int i = 0; i < size - 8; i += 2)
				{
					if (data[i] == 0 && data[i + 1] == 0)
					{
						end = i;
						break;
					}
				}

				this.PlaceholderText = Encoding.Unicode.GetString(data, 0, end);
			}
			else
			{
				throw new NotImplementedException();
			}
		}

		public override void WriteProp(Stream output, bool array)
		{
			if (array == true)
			{
				output.WriteU32(this.TableId);
				output.WriteU32(this.InstanceId);
				byte[] data = Encoding.Unicode.GetBytes(this.PlaceholderText);
				output.Write(data, 0, data.Length);
				
				// For some reason text from real properties are minimum 512 bytes for text, so
				// I'll emulate that here, not sure if it is needed though.
				if (data.Length < 512)
				{
					data = new byte[512 - data.Length];
					output.Write(data, 0, data.Length);
				}
			}
			else
			{
				throw new NotImplementedException();
			}
		}

		public override void WriteXML(System.Xml.XmlWriter output)
		{
            if (this.TableId != 0)
			    output.WriteAttributeString("tableid", SporeMaster.NameRegistry.Files.toName(this.TableId));
            if (this.InstanceId != 0)
			    output.WriteAttributeString("instanceid", SporeMaster.NameRegistry.Files.toName(this.InstanceId));
			output.WriteValue(this.PlaceholderText);
		}

		public override void ReadXML(System.Xml.XmlReader input)
		{
            var tid = input.GetAttribute("tableid");
            var iid = input.GetAttribute("instanceid");
            this.TableId = tid==null ? 0 : SporeMaster.NameRegistry.Files.toHash( tid );
			this.InstanceId = iid==null ? 0 : SporeMaster.NameRegistry.Files.toHash( iid );
			this.PlaceholderText = input.ReadString();
		}
	}
}
