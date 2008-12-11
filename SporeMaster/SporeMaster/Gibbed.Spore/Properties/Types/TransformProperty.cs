using System;
using System.IO;
using Gibbed.Spore.Helpers;

namespace Gibbed.Spore.Properties
{
	[PropertyDefinitionAttribute("transform", "transforms", 56)]
	public class TransformProperty : ComplexProperty
	{
		public uint Flags;  //< looks like a bitfield, e.g. 0x6000300
		public float[] Matrix = new float[12];  //< I assume this is a 4x3 homogenous transform matrix, just because there are 12 floats :-)

		public override void ReadProp(Stream input, bool array)
		{
			this.Flags = input.ReadU32BE();
            for (int i = 0; i < 12; i++)
                this.Matrix[i] = input.ReadF32();
		}

		public override void WriteProp(Stream output, bool array)
		{
            output.WriteU32BE(this.Flags);
            for (int i = 0; i < 12; i++)
                output.WriteF32(this.Matrix[i]);
        }

		public override void WriteXML(System.Xml.XmlWriter output)
		{
            output.WriteAttributeString("flags", "0x" + this.Flags.ToString("X8"));
            // This is pretty much a wild guess!  But at least it preserves the data.
            output.WriteAttributeString("pos", string.Format("{0},{1},{2}", this.Matrix[0], this.Matrix[1], this.Matrix[2]));
            output.WriteAttributeString("vx", string.Format("{0},{1},{2}", this.Matrix[3], this.Matrix[6], this.Matrix[9]));
            output.WriteAttributeString("vy", string.Format("{0},{1},{2}", this.Matrix[4], this.Matrix[7], this.Matrix[10]));
            output.WriteAttributeString("vz", string.Format("{0},{1},{2}", this.Matrix[5], this.Matrix[8], this.Matrix[11]));
		}

		public override void ReadXML(System.Xml.XmlReader input)
		{
            if (!input.HasAttributes)
            {
                // Legacy Gibbed.Spore format
                var s = input.ReadString().Split(new char[] { ' ' });
                if (s.Length != 13) throw new FormatException("Legacy transform format incorrect.");
                this.Flags = s[0].GetHexNumber();
                for (int i = 0; i < 12; i++)
                    this.Matrix[i] = float.Parse(s[i + 1]);
                return;
            }
            this.Flags = input.GetAttribute("Flags").GetHexNumber();
            parseV(input.GetAttribute("pos"), out Matrix[0], out Matrix[1], out Matrix[2]);
            parseV(input.GetAttribute("vx"), out Matrix[3], out Matrix[6], out Matrix[9]);
            parseV(input.GetAttribute("vy"), out Matrix[4], out Matrix[7], out Matrix[10]);
            parseV(input.GetAttribute("vz"), out Matrix[5], out Matrix[8], out Matrix[11]);
		}
 
        static void parseV(string input, out float x, out float y, out float z)
        {
            var s = input.Split(new char[] { ',' });
            if (s.Length != 3) throw new FormatException("Bad vector format in transform property.");
            x = float.Parse(s[0]);
            y = float.Parse(s[1]);
            z = float.Parse(s[2]);
        }

    }
}
