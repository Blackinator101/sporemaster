using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Gibbed.Spore.Helpers;
using Gibbed.Spore.Package;
using Gibbed.Spore.Properties;
using System.Xml;

namespace SporeMaster
{
    class PackageUnpack
    {
        public PackageUnpack(Stream[] packageFiles, string destinationFolder, PleaseWait progress)
        {
            double e = 0.0;
            FullTextIndex.ResetPath(destinationFolder);
            if (Directory.Exists(destinationFolder))
            {
                if (progress != null) progress.beginTask(e = 0.25, 1.0);
                Directory.Delete(destinationFolder, true);
                if (progress != null) progress.endTask();
            }
            Directory.CreateDirectory(destinationFolder);

            double totalSize = 0;
            for (int i = 0; i < packageFiles.Length; i++)
            {
                totalSize += packageFiles[i].Length;
            }

            if (progress != null)
                progress.beginTask(1.0 - e, totalSize);

            foreach (var filestream in packageFiles)
            {
                DatabasePackedFile db = new DatabasePackedFile();
                db.Read(filestream);
                var DatabaseFiles = db.Indices.ToArray();

                var group_sporemaster = "sporemaster".FNV();
                var instance_names = "names".FNV();
                foreach (var dbf in DatabaseFiles)
                {
                    if (dbf.GroupId == group_sporemaster && dbf.InstanceId == instance_names)
                    {
                        byte[] data = unpack(filestream, dbf);
                        readNamesFile(data);
                    }
                }

                var locale_type = NameRegistry.Types.toHash("locale");
                var prop_type = NameRegistry.Types.toHash("prop");
                var rw4_type = NameRegistry.Types.toHash("rw4");

                foreach (var dbf in DatabaseFiles) {
                    // skip over automatically generated locale files
                    if (!(dbf.TypeId == locale_type && (
                            NameRegistry.Groups.toName(dbf.InstanceId).StartsWith("auto_") || 
                            NameRegistry.Groups.toName(dbf.InstanceId).EndsWith("_auto"))))
                    {
                        var fn = Path.Combine(destinationFolder, NameRegistry.getFileName(dbf.GroupId, dbf.InstanceId, dbf.TypeId));
                        Directory.CreateDirectory(Path.GetDirectoryName(fn));
                        byte[] data = unpack(filestream, dbf);

                        if (dbf.TypeId == prop_type)
                            writePropFile(data, fn);
                        else if (dbf.TypeId == rw4_type)
                            writeRW4File(data, fn);
                        else
                            writeBinaryFile(data, fn);
                    }
                    if (progress != null)
                        progress.addProgress(dbf.CompressedSize);
                }
                filestream.Close();
            }

            if (progress != null)
                progress.endTask();
        }

        private void readNamesFile(byte[] data)
        {
            var newHashes = new List<UInt32>();
            NameRegistry.Files.readRegistry(new StreamReader(new MemoryStream(data)), false, newHashes);
            if (newHashes.Count!=0)
                Renamer.rename(newHashes.ToArray(), 
                    (from h in newHashes select "#" + h.ToString("X8")).ToArray(),
                    null);
            NameRegistry.Files.save();
        }

        private void writeBinaryFile(byte[] data, string fn)
        {
            using (var output = File.Create(fn))
                output.Write(data, 0, data.Length);
        }

        private void writePropFile(byte[] data, string fn)
        {
			PropertyFile file = new PropertyFile();
            file.Read(new MemoryStream(data));
            stripGeneratedLocaleReferences(file);
            var output = File.Create(fn + ".xml");
            XmlTextWriter writer = new XmlTextWriter(output, Encoding.UTF8);
            writer.Formatting = Formatting.Indented;
            file.WriteXML(writer);
            writer.Flush();
            output.Close();
        }

        private void writeRW4File(byte[] data, string fn)
        {
            Directory.CreateDirectory(fn);
            writeBinaryFile(data, fn + "\\raw.rw4");

            try
            {
                using (var stream = new MemoryStream(data))
                    new SporeMaster.RenderWare4.ModelUnpack(stream, fn);
            }
            catch (Exception e)
            {
                File.WriteAllText(fn + "\\conversion_error.txt", e.ToString());
            }
        }

        private void stripGeneratedLocaleReferences(PropertyFile file)
        {
            // Effectively, we are undoing the work of PackagePack.generateLocale()
            foreach (var prop1 in file.Values.Values)
            {
                var arr = prop1 as ArrayProperty;
                if (arr == null) continue;
                foreach (var prop in arr.Values)
                {
                    var text = prop as TextProperty;
                    if (text != null && 
                            (NameRegistry.Files.toName(text.TableId).StartsWith("auto_") ||
                             NameRegistry.Files.toName(text.TableId).EndsWith("_auto")))
                    {
                        text.InstanceId = 0;
                        text.TableId = 0;
                    }
                }
            }
        }

        private byte[] unpack(Stream archive, DatabaseIndex index)
        {
            if (index.Compressed)
            {
                archive.Seek(index.Offset, SeekOrigin.Begin);
                return archive.RefPackDecompress(index.CompressedSize, index.DecompressedSize);
            }
            else
            {
                archive.Seek(index.Offset, SeekOrigin.Begin);
                byte[] d = new byte[index.DecompressedSize];
                archive.Read(d, 0, d.Length);
                return d;
            }
        }
    }
}
