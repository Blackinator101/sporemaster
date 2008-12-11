using System;
using System.IO;
using Gibbed.Spore.Helpers;

namespace Gibbed.Spore.Properties
{
	[PropertyDefinitionAttribute("bbox", "bboxes", 57)]
	class BoundingBoxProperty : ComplexProperty
	{
		public float MinX;
		public float MinY;
		public float MinZ;
		public float MaxX;
		public float MaxY;
		public float MaxZ;

		public override void ReadProp(Stream input, bool array)
		{
			this.MinX = input.ReadF32();
			this.MinY = input.ReadF32();
			this.MinZ = input.ReadF32();
			this.MaxX = input.ReadF32();
			this.MaxY = input.ReadF32();
			this.MaxZ = input.ReadF32();
		}

		public override void WriteProp(Stream output, bool array)
		{
            output.WriteF32(this.MinX);
            output.WriteF32(this.MinY);
            output.WriteF32(this.MinZ);
            output.WriteF32(this.MaxX);
            output.WriteF32(this.MaxY);
            output.WriteF32(this.MaxZ);
        }

		public override void WriteXML(System.Xml.XmlWriter output)
		{
			output.WriteStartElement("min");
			output.WriteValue(string.Format("{0}, {1}, {2}", this.MinX, this.MinY, this.MinZ));
			output.WriteEndElement();

			output.WriteStartElement("max");
			output.WriteValue(string.Format("{0}, {1}, {2}", this.MaxX, this.MaxY, this.MaxZ));
			output.WriteEndElement();
		}

		public override void ReadXML(System.Xml.XmlReader input)
		{
            input.ReadToDescendant("min");
            var s = input.ReadString().Split(new char[] { ',' });
            if (s.Length != 3) throw new FormatException("Bounding box vector value formatted incorrectly.");
            this.MinX = float.Parse(s[0].Trim());
            this.MinY = float.Parse(s[1].Trim());
            this.MinZ = float.Parse(s[2].Trim());

            input.ReadToNextSibling("max");
            s = input.ReadString().Split(new char[] { ',' });
            if (s.Length != 3) throw new FormatException("Bounding box vector value formatted incorrectly.");
            this.MaxX = float.Parse(s[0].Trim());
            this.MaxY = float.Parse(s[1].Trim());
            this.MaxZ = float.Parse(s[2].Trim());
		}
	}
}
