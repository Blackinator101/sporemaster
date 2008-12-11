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
    class PackagePack
    {
        public PackagePack(Stream output, string sourceFolder, PleaseWait progress)
        {
            NameRegistry.Files.UsedHashes = new List<UInt32>();

            var group_dirs = Directory.GetDirectories(sourceFolder);
            var file_query = from d in group_dirs
                                where d != sourceFolder + "\\sporemaster\\"
                                from f in Directory.GetFileSystemEntries(d)
                                where !f.EndsWith(".search_index")  // < these might appear in group directories if there are indexable files in subdirectories
                                select f;
            var files = file_query.ToList();
            files.Add(sourceFolder + "\\sporemaster\\names.txt");

            if (progress != null) progress.beginTask(1.0, files.Count);

            DatabasePackedFile dbf = new DatabasePackedFile();
            dbf.Version = new Version(2, 0);
            dbf.WriteHeader(output, 0, 0);

            var rw4_hash = NameRegistry.Types.toHash("rw4");

            uint size, start = (uint)output.Position;
            foreach( var f in files ) {
                string relativePath = f.Substring(sourceFolder.Length + 1);
                bool additionalOutputFiles;
                byte[] autoLocale = null;
                do
                {
                    additionalOutputFiles = false;

                    var parts = relativePath.Split(new char[] { '\\' });
                    if (parts.Length != 2) continue;
                    var group = parts[0];
                    parts = parts[1].Split(new char[] { '.' }, 2);
                    var instance = parts[0];
                    var extension = parts[1];
                    var index = new DatabaseIndex();
                    index.GroupId = NameRegistry.Groups.toHash(group);
                    index.InstanceId = NameRegistry.Files.toHash(instance);

                    try
                    {
                        if (relativePath == "sporemaster\\names.txt")
                        {
                            writeNamesFile(output);
                        }
                        else if (autoLocale != null)
                        {
                            output.Write(autoLocale, 0, autoLocale.Length);
                        }
                        else if (extension == "prop.xml")
                        {
                            extension = "prop";
                            writePropFile(group, instance, f, output, out autoLocale);
                            if (autoLocale.Length != 0)
                            {
                                additionalOutputFiles = true;
                                relativePath = "locale~\\auto_" + group + "_" + instance + ".locale";
                            }
                        }
                        else if (NameRegistry.Types.toHash(extension)==rw4_hash && Directory.Exists(f))
                        {
                            writeRW4File(f, output);
                        }
                        else
                            writeBinaryFile(f, output);
                    }
                    catch (Exception e)
                    {
                        throw new Exception("Error packing file '" + relativePath + "'.", e);
                    }

                    size = (uint)output.Position - start;

                    index.TypeId = NameRegistry.Types.toHash(extension);
                    index.Compressed = false;
                    index.Flags = 1;
                    index.DecompressedSize = size;
                    index.CompressedSize = size | 0x80000000;
                    index.Offset = start;
                    dbf.Indices.Add(index);
                    start += size;
                } while (additionalOutputFiles);

                progress.addProgress(1.0);
            }

            dbf.WriteIndex(output);
            size = (uint)output.Position - start;
            output.Seek(0, SeekOrigin.Begin);
            dbf.WriteHeader(output, (int)start, (int)size);

            output.Close();

            if (progress != null) progress.endTask();
        }
        void writeRW4File(string inputDirName, Stream output)
        {
            var files = Directory.GetFiles( inputDirName );
            if (files.Contains(inputDirName + "\\model.mesh.xml"))
            {
                // ModelPack requires a stream beginning at position 0
                using (var stream = new MemoryStream())
                {
                    new RenderWare4.ModelPack(inputDirName + "\\model.mesh.xml", stream);
                    byte[] data = stream.GetBuffer();
                    output.Write(data, 0, (int)stream.Length);
                }
            }
            else if (files.Contains(inputDirName + "\\texture.dds"))
            {
                using (var stream = new MemoryStream())
                {
                    new RenderWare4.DDSPack(inputDirName + "\\texture.dds", stream);
                    byte[] data = stream.GetBuffer();
                    output.Write(data, 0, (int)stream.Length);
                }
            }
            else if (files.Contains(inputDirName + "\\raw.rw4"))
            {
                writeBinaryFile(inputDirName + "\\raw.rw4", output);
            }
            else
                throw new Exception("Nothing present from which I can build a rw4 file.");

        }
        void writePropFile(string groupName, string instanceName, string inputFileName, Stream output, out byte[] locale)
        {
            // Convert .prop.xml to binary .prop file and write it to output
            var reader = XmlReader.Create( File.OpenText(inputFileName) );
            var file = new PropertyFile();
            file.ReadXML( reader );
            reader.Close();
            locale = generateLocale(file, groupName, instanceName);
            file.Write(output);
		}
        byte[] generateLocale(PropertyFile file, string group, string instance)
        {
            // Create a .locale file from placeholder text if there are any <text> 
            //   tags with no instance or table ID
            var locale = new StringBuilder();
            uint locale_index = 0;
            var locale_name = "auto_" + group + "_" + instance;
            foreach (var prop1 in file.Values.Values)
            {
                var arr = prop1 as ArrayProperty;
                if (arr == null) continue;
                foreach (var prop in arr.Values)
                {
                    var text = prop as TextProperty;
                    if (text != null && text.InstanceId == 0 && text.TableId == 0)
                    {
                        if (locale_index == 0) locale.Append("# This file generated from " + group + "/" + instance + ".prop.xml by SporeMaster.\n");
                        text.TableId = NameRegistry.Files.toHash(locale_name);
                        text.InstanceId = ++locale_index;
                        locale.AppendFormat("0x{0:X8} {1}\n", text.InstanceId, text.PlaceholderText);
                    }
                }
            }
            return Encoding.UTF8.GetBytes( locale.ToString() );
        }
        void writeBinaryFile(string inputFileName, Stream output)
        {
            var data = File.ReadAllBytes(inputFileName);
            output.Write(data, 0, data.Length);
        }
        void writeNamesFile(Stream output)
        {
            foreach( var h in NameRegistry.Files.UsedHashes ) {
                var name = NameRegistry.Files.toName(h);
                if (name.StartsWith("#")) continue;
                string line;
                if (name.FNV() == h)
                    line = name;
                else
                    line = name + "\t0x" + h.ToString("X8");
                var bytes = Encoding.UTF8.GetBytes(line + "\r\n");
                output.Write(bytes,0, bytes.Length);
            }
        }
    }
}
