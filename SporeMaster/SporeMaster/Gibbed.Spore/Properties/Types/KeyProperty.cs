using System;
using System.IO;
using Gibbed.Spore.Helpers;
using SporeMaster;

namespace Gibbed.Spore.Properties
{
	[PropertyDefinition("key", "keys", 32)]
	public class KeyProperty : Property
	{
		public uint TypeId;
		public uint GroupId;
		public uint InstanceId;

		public override void ReadProp(Stream input, bool array)
		{
			this.InstanceId = input.ReadU32();
			this.TypeId = input.ReadU32();
			this.GroupId = input.ReadU32();

			if (array == false)
			{
				input.Seek(4, SeekOrigin.Current);
			}
		}

		public override void WriteProp(Stream output, bool array)
		{
			output.WriteU32(this.InstanceId);
			output.WriteU32(this.TypeId);
			output.WriteU32(this.GroupId);

			if (array == false)
			{
				output.WriteU32(0); // junk
			}
		}

		public override void WriteXML(System.Xml.XmlWriter output)
		{
			if (this.GroupId != 0)
				output.WriteAttributeString("groupid", NameRegistry.Groups.toName(this.GroupId));

			output.WriteAttributeString("instanceid", NameRegistry.Files.toName(this.InstanceId));

			if (this.TypeId != 0)
				output.WriteAttributeString("typeid", NameRegistry.Types.toName(this.TypeId));
            //output.WriteAttributeString("target", NameRegistry.getFileName( this.GroupId, this.InstanceId, this.TypeId ));
		}

		public override void ReadXML(System.Xml.XmlReader input)
		{
            string instanceText = input.GetAttribute("instanceid");
            if (instanceText == null)
                throw new Exception("instanceid cannot be null for key");

            this.GroupId = NameRegistry.Groups.toHash( input.GetAttribute("groupid") );
            this.InstanceId = NameRegistry.Files.toHash( instanceText );
            this.TypeId = NameRegistry.Types.toHash(input.GetAttribute("typeid"));
		}
	}
}
