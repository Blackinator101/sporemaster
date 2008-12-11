using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;
using Gibbed.Spore.Properties;

namespace SporeMaster
{
    class RebuildPropertyFile
    {
        bool failed = false;
        public RebuildPropertyFile(string path)
        {
            try
            {
                var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
                var reader = XmlReader.Create(stream);
                var file = new PropertyFile();
                file.ReadXML(reader);
                reader.Close();
                stream.Close();

                stream = new FileStream(path + ".part", FileMode.Create, FileAccess.Write);
                XmlTextWriter writer = new XmlTextWriter(stream, Encoding.UTF8);
                writer.Formatting = Formatting.Indented;
                file.WriteXML(writer);
                writer.Close();
                stream.Close();
            }
            catch (Exception)
            {
                failed = true;
                if (File.Exists(path + ".part")) File.Delete(path + ".part");
                return;
            }
            File.Replace(path + ".part", path, path + ".backup");
            File.Delete(path + ".backup");
        }
        public bool Failed { get { return failed; } }
    }
}
