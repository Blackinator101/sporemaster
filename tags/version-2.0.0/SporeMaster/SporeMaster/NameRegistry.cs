using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using Gibbed.Spore.Helpers;

namespace SporeMaster
{
    class NameRegistry
    {
        private Dictionary<UInt32, String> hash_name = new Dictionary<UInt32,string>();
        private Dictionary<String, UInt32> name_hash = new Dictionary<string, UInt32>();

        private HashSet<UInt32> usedHashes = new HashSet<UInt32>();
        private string filename = null;

        public static NameRegistry Types;
        public static NameRegistry Files;
        public static NameRegistry Groups;
        public static NameRegistry Properties;

        public IEnumerable<UInt32> UsedHashes {
            get { return usedHashes.AsEnumerable(); }
            set { usedHashes.Clear(); usedHashes.UnionWith(value); }
        }

        public bool IsHashUsed(UInt32 hash)
        {
            return usedHashes.Contains(hash);
        }

        public static string getFileName(UInt32 GroupId, UInt32 InstanceId, UInt32 TypeId)
        {
            return NameRegistry.Groups.toName(GroupId) + "/" + NameRegistry.Files.toName(InstanceId) + "." + NameRegistry.Types.toName(TypeId);
        }

        public static void parseFileName(string filename, out UInt32 GroupId, out UInt32 InstanceId, out UInt32 TypeId)
        {
            var m = Regex.Match(filename, "([^/\\]*)[/\\]([^.]*).(*)");
            if (!m.Success) throw new ArgumentException("Invalid file reference.");
            GroupId = Groups.toHash(m.Groups[1].ToString());
            InstanceId = Files.toHash(m.Groups[2].ToString());
            TypeId = Types.toHash(m.Groups[3].ToString());
        }

        public NameRegistry()
        {
        }

        public NameRegistry(string registryFile)
        {
            this.filename = registryFile;
            readRegistryFile(registryFile, true);
        }

        public void save()
        {
            writeRegistryFile(this.filename);
        }

        public void readRegistryFile( string registryFile, bool overrideExisting ) {
            readRegistry(new StreamReader(registryFile), overrideExisting, null);
        }

        public void readRegistry( TextReader reader, bool overrideExisting, List<UInt32> outNewHashes ) {
            while (true)
            {
                string line = reader.ReadLine();
                if (line == null)
                {
                    break;
                }
                if (line.StartsWith("#")) continue;
                string name;
                UInt32 hash;
                if (line.Contains("\t")) {
                    var s = line.Split( new Char[] {'\t'} );
                    name = s[0];
                    if (s[1].StartsWith("0x"))
                        hash = UInt32.Parse( s[1].Substring(2), System.Globalization.NumberStyles.AllowHexSpecifier );
                    else
                        hash = UInt32.Parse( s[1] );
                } else {
                    name = line;
                    hash = name.FNV();
                }

                if (overrideExisting || !name_hash.ContainsKey( name ))
                    if (addName( name, hash, overrideExisting ) && outNewHashes!=null)
                        outNewHashes.Add( hash );
            }

            reader.Close();
        }

        public void writeRegistryFile(string registryFile)
        {
            var names = new List<string>();
            
            // First insert 'obsolete' names
            foreach (var i in name_hash)
                if (hash_name[i.Value] != i.Key)
                    names.Add(i.Key);

            names.Add("### Previous items are 'obsolete' ###");

            // Now insert the names that actually are used
            names.AddRange( hash_name.Values );

            // Write the file
            var output = File.Create(registryFile + ".part");
            foreach( var name in names ) {
                string line;
                if (name_hash.ContainsKey(name) && name_hash[name] != name.FNV())
                    line = name + "\t0x" + name_hash[name].ToString("X8") + "\r\n";
                else
                    line = name + "\r\n";
                var b = Encoding.UTF8.GetBytes( line );
                output.Write(b, 0, b.Length);
            }
            output.Close();  
            
            // And move it into place
            File.Replace(registryFile + ".part", registryFile, registryFile + ".backup");
            File.Delete(registryFile + ".backup");
        }

        public bool addName(string name, UInt32 hash, bool overrideExisting)
        {
            if (hash != name.FNV())
                name_hash[name] = hash;
            else if (name_hash.ContainsKey(name))
                name_hash.Remove(name);

            if (!hash_name.ContainsKey(hash) || 
                (overrideExisting && hash_name[hash].FNV()!=hash) )  //< Even if overrideExisting, don't override a true name with an alias.  Not sure if this matters!
            {
                hash_name[hash] = name;
                return true;
            }
            return false;
        }

        public string toName( UInt32 hash ) {
            if (hash_name.ContainsKey(hash))
                return hash_name[hash];
            return "#" + hash.ToString("X8");
        }

        public UInt32 toHash(string name)
        {
            UInt32 hash = toHashImpl(name);
            usedHashes.Add(hash);
            return hash;
        }

        private UInt32 toHashImpl(string name)
        {
            UInt32 hash;
            if (name == null) return 0;
            if (checkName(name, out hash))
                return hash;
            if (name.EndsWith("~"))
                throw new Exception("Cannot hash name '" + name + "': ~ is reserved for aliases");
            hash = name.FNV();
            hash_name[hash] = name;
            return hash;
        }

        public bool checkName(string name, out UInt32 hash)
        {
            if (name == null) { hash = 0; return false; }
            if (name.StartsWith("#"))
            {
                hash = UInt32.Parse(name.Substring(1), System.Globalization.NumberStyles.AllowHexSpecifier);
                return true;
            }
            if (name.StartsWith("0x"))
            {
                hash = UInt32.Parse(name.Substring(2), System.Globalization.NumberStyles.AllowHexSpecifier);
                return true;
            }
            if (name_hash.ContainsKey(name))
            {
                hash = name_hash[name];
                return true;
            }
            hash = 0;
            return false;
        }
    }
}
