using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Gibbed.Spore.Helpers;
using SporeMaster.RenderWare4;

namespace DecodeRW4
{

    class Program
    {
        static void CreateModel(string src_name, string out_name)
        {
            using (var stream = File.Create(out_name))
                new ModelPack(src_name, stream);

            Console.WriteLine("Checking model:");
            var tm = new RW4Model();
            using (var stream = File.Open(out_name, FileMode.Open))
                tm.Read(stream);
            foreach (var s in tm.Sections)
                Console.WriteLine(String.Format("  #{0} 0x{1:x} - 0x{2:x}: 0x{3:x} {4}", s.number, s.pos, s.pos + s.size, s.type_code, s.obj.ToString().Substring("SporeMaster.RenderWare4.".Length)));
        }

        static void Main(string[] args)
        {
            //DoConvert();
        }

        static void DoConvert() {
            Dictionary<string, int> errors = new Dictionary<string, int>();
            int okCount = 0;
            string[] files;
            string dir = "c:\\my\\proj\\mods\\spore\\spore.unpacked\\";

            files = Directory.GetFiles(dir, "*.#2F4E681B", SearchOption.AllDirectories);
            foreach (var f in files)
            {
                var outdir = "c:\\my\\proj\\mods\\spore\\rw4test\\" + Path.GetFileNameWithoutExtension(f) + ".rw4";
                try
                {
                    Console.Write(f.Substring(dir.Length + 1) + ": ");
                    using (var stream = File.OpenRead(f))
                    {
                        Directory.CreateDirectory(outdir);
                        var up = new ModelUnpack(stream,
                                    outdir);
                        okCount++;
                        Console.WriteLine(up.Type);
                    }
                }
                catch (SporeMaster.RenderWare4.ModelFormatException e)
                {
                    System.Console.WriteLine(e.Message);
                    if (errors.ContainsKey(e.exception_type)) errors[e.exception_type]++;
                    else errors[e.exception_type] = 1;
                    try { Directory.Delete(outdir); }
                    catch (System.IO.IOException) { }
                }
                catch (Exception e)
                {
                    System.Console.WriteLine(e.Message);
                    if (errors.ContainsKey(e.Message)) errors[e.Message]++;
                    else errors[e.Message] = 1;
                    try { Directory.Delete(outdir); }
                    catch (System.IO.IOException) { }
                }
            }

            System.Console.WriteLine(String.Format("Converted {0}/{1} files.", okCount, files.Length));
            if (errors.Count() != 0)
            {
                System.Console.WriteLine("Errors:");
                foreach (var e in errors)
                    System.Console.WriteLine(String.Format("     {0}: {1}", e.Key, e.Value));
            }
        }
    }
}
