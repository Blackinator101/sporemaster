using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace SporeMaster
{
    class FullTextIndex
    {
        string fullpath;
        byte[] data;
        int[] suffix_array;
        DateTime lastModified;
        static Dictionary<string, DirCache> dirCaches = new Dictionary<string,DirCache>();

        class DirCache
        {
            public Dictionary<string, FullTextIndex> cache = new Dictionary<string,FullTextIndex>();
            BinaryWriter writer;
            string filename;
            int totalsize = 0;
            int storedsize = 0;

            public void Dispose()
            {
                if (writer != null) writer.Close();
                writer = null;
                cache = null;
            }

            public void read(string filename)
            {
                this.filename = filename;
                if (!File.Exists(filename)) return;

                try {
                    using (var file = File.OpenRead(filename)) {
                        var reader = new BinaryReader(file);
                        int fileLength = (int)reader.BaseStream.Length;
                        int pos = 0;
                        while (pos < fileLength)
                        {
                            var name = reader.ReadString();
                            pos += name.Length >= 128 * 128 ? 3 : name.Length >= 128 ? 2 : 1;
                            pos += name.Length;
                            var len = reader.ReadInt32();
                            if (len<0 || (ulong)pos + (ulong)12 + (ulong)len * (ulong)5 > (ulong)fileLength)
                                throw new Exception("Bad length in index file.");
                            pos += 4;
                            DateTime lastModified = DateTime.FromFileTimeUtc(reader.ReadInt64());
                            pos += 8;
                            var data = new byte[len];
                            reader.Read(data, 0, len);
                            pos += len;
                            var suffix_array = new int[len];
                            for (int i = 0; i < len; i++)
                            {
                                suffix_array[i] = reader.ReadInt32();
                                if (suffix_array[i] < 0 || suffix_array[i] >= len)
                                    throw new Exception("Out of bounds data in suffix array.");
                            }
                            pos += len * 4;

                            cache[name] = new FullTextIndex { data = data, suffix_array = suffix_array, lastModified = lastModified };
                            totalsize += cache[name].data.Length;
                        }
                    }
                }
                catch (Exception)
                {
                    File.Delete(filename);
                    this.storedsize = 0;
                    cache = new Dictionary<string, FullTextIndex>();
                    totalsize = 0;
                }
            }

            public void Add(FullTextIndex fti)
            {
                var fname = Path.GetFileName( fti.fullpath );
                if (cache.ContainsKey(fname))
                    this.totalsize -= cache[fname].data.Length;
                cache[fname] = fti;
                var oldtotalsize = this.totalsize;
                this.totalsize += fti.data.Length;
                this.storedsize += fti.data.Length;
                if (this.storedsize > this.totalsize * 2 && this.storedsize > this.totalsize + 16384)
                {
                    this.storedsize = totalsize;
                    writer.Close();
                    writer = null;
                    File.Delete(filename);
                    foreach (var i in cache.Values)
                        write(i);
                    writer.Flush();
                }
                else
                {
                    write(fti);
                    if (this.totalsize / 1000000 > oldtotalsize / 1000000)
                        writer.Flush();
                }
            }

            private void write( FullTextIndex fti ) {
                if (writer == null)
                {
                    writer = new BinaryWriter(File.Create(filename));
                }
                writer.Write(Path.GetFileName(fti.fullpath));
                writer.Write((Int32)fti.data.Length);
                writer.Write(fti.lastModified.ToFileTimeUtc());
                writer.Write(fti.data, 0, fti.data.Length);
                for (int i = 0; i < fti.data.Length; i++)
                    writer.Write(fti.suffix_array[i]);
            }
        };

        public static void ResetPath(string path)
        {
            foreach (var d in dirCaches.Keys.ToArray())
            {
                if (d == path || d.StartsWith(path + "\\"))
                {
                    dirCaches[d].Dispose();
                    dirCaches.Remove(d);
                }
            }
        }

        private FullTextIndex() { }

        public FullTextIndex(string path)
        {
            this.fullpath = path;
            var dir = Path.GetDirectoryName(path);
            var fname = Path.GetFileName(path);
            this.lastModified = File.GetLastWriteTimeUtc(path);

            DirCache dcache;
            if (!dirCaches.TryGetValue(dir, out dcache)) {
                dcache = new DirCache();
                dcache.read(dir + ".search_index");
                dirCaches[dir] = dcache;
            }

            if (dcache.cache.ContainsKey(fname))
            {
                var fti = dcache.cache[fname];
                if (fti.lastModified == this.lastModified)
                {
                    this.data = fti.data;
                    this.suffix_array = fti.suffix_array;
                    return;
                }
            }

            try
            {
                data = System.Text.Encoding.UTF8.GetBytes(File.ReadAllText(path).ToLowerInvariant());
            }
            catch (IOException) { return; }
            catch (UnauthorizedAccessException) { return;  }

            if (suffix_array == null) {
                var temp = new int[data.Length + 3];
                for (int i = 0; i < data.Length; i++)
                    temp[i] = data[i];
                temp[data.Length] = temp[data.Length + 1] = temp[data.Length + 2] = 0;
                suffix_array = new int[data.Length];
                for (int i = 0; i < data.Length; i++)
                    suffix_array[i] = i;
                suffixArray(temp, suffix_array, data.Length, 255);

                dcache.Add(this);
            }
        }

        public bool Contains(byte[] search)
        {
            if (data == null) return false;
            return Contains_Suffix(search);
        }

        private bool Contains_Suffix(byte[] search)
        {
            int begin = 0, end = suffix_array.Length;
            while (begin != end)
            {
                int mid = (begin + end) >> 1;
                int p = suffix_array[mid];
                int i;
                for(i=0; i<search.Length; i++)
                    if (p+i>=data.Length || search[i] > data[p + i])
                    {
                        begin = mid + 1;
                        goto continue_while;
                    } else if (search[i] < data[p + i])
                    {
                        end = mid;
                        goto continue_while;
                    }
                return true;

                continue_while: ;
            }
            return false;
        }

        private bool Contains_Linear(byte[] search) {
            int end = data.Length - search.Length;
            int j;
            for (int i = 0; i < end; i++) {
                for (j = 0; j < search.Length; j++)
                    if (data[i + j] != search[j])
                        goto fail;
                return true;
                fail: ;
            };
            return false;
        }

        #region Simple Linear Work Suffix Array Construction, Kärkkäinen and Sanders

        bool leq(int a1, int a2, int b1, int b2)  // lexicographic order
        { return (a1 < b1 || a1 == b1 && a2 <= b2); } // for pairs

        bool leq(int a1, int a2, int a3, int b1, int b2, int b3)
        { return (a1 < b1 || a1 == b1 && leq(a2, a3, b2, b3)); } // and triples

        // stably sort a[0..n-1] to b[0..n-1] with keys in 0..K from r
        static void radixPass(int[] a, int[] b, int[] r, int ri, int n, int K)
        {// count occurrences
            unsafe
            {
                int[] c = new int[K + 1]; // counter array
                for (int i = 0; i <= K; i++) c[i] = 0; // reset counters
                for (int i = 0; i < n; i++) c[r[ri + a[i]]]++; // count occurrences
                for (int i = 0, sum = 0; i <= K; i++) // exclusive prefix sums
                { int t = c[i]; c[i] = sum; sum += t; }
                for (int i = 0; i < n; i++) b[c[r[ri + a[i]]]++] = a[i]; // sort
            }
        }

        // find the suffix array SA of s[0..n-1] in {1..K}ˆn
        // require s[n]=s[n+1]=s[n+2]=0, n>=2
        void suffixArray(int[] s, int[] SA, int n, int K)
        {
	        int n0=(n+2)/3, n1=(n+1)/3, n2=n/3, n02=n0+n2;
	        int[] s12 = new int[n02 + 3]; s12[n02]= s12[n02+1]= s12[n02+2]=0;
	        int[] SA12 = new int[n02 + 3]; SA12[n02]=SA12[n02+1]=SA12[n02+2]=0;
	        int[] s0 = new int[n0];
	        int[] SA0 = new int[n0];
	        // generate positions of mod 1 and mod 2 suffixes
	        // the "+(n0-n1)" adds a dummy mod 1 suffix if n%3 == 1
	        for (int i=0, j=0; i < n+(n0-n1); i++) if (i%3 != 0) s12[j++] = i;
	        // lsb radix sort the mod 1 and mod 2 triples
	        radixPass(s12 , SA12, s, +2, n02, K);
	        radixPass(SA12, s12 , s, +1, n02, K);
	        radixPass(s12 , SA12, s , +0, n02, K);
	        // find lexicographic names of triples
	        int name = 0, c0 = -1, c1 = -1, c2 = -1;
	        for(int i=0; i<n02; i++) {
		        if (s[SA12[i]] != c0 || s[SA12[i]+1] != c1 || s[SA12[i]+2] != c2)
		        {name++; c0 = s[SA12[i]]; c1 = s[SA12[i]+1]; c2 = s[SA12[i]+2]; }
		        if (SA12[i]%3==1){s12[SA12[i]/3] = name; } // left half
		        else {s12[SA12[i]/3 + n0] = name; } // right half
	        }
	        // recurse if names are not yet unique
	        if (name < n02) {
		        suffixArray(s12, SA12, n02, name);
		        // store unique names in s12 using the suffix array
		        for(int i=0; i<n02; i++) s12[SA12[i]]=i+1;
	        } else // generate the suffix array of s12 directly
		        for(int i=0; i<n02; i++) SA12[s12[i] - 1] = i;
	        // stably sort the mod 0 suffixes from SA12 by their first character
	        for (int i=0, j=0; i < n02; i++) if (SA12[i] < n0) s0[j++] = 3*SA12[i];
	        radixPass(s0, SA0, s, +0, n0, K);
	        // merge sorted SA0 suffixes and sorted SA12 suffixes
	        for (int p=0, t=n0-n1, k=0; k < n; k++) {
		        int i = (SA12[t] < n0 ? SA12[t]*3+1:(SA12[t] - n0)*3+2); // pos of current offset 12 suffix
		        int j = SA0[p]; // pos of current offset 0 suffix
		        if (SA12[t] < n0 ? // different compares for mod 1 and mod 2 suffixes
			        leq(s[i], s12[SA12[t] + n0], s[j], s12[j/3]) :
		        leq(s[i],s[i+1],s12[SA12[t]-n0+1], s[j],s[j+1],s12[j/3+n0]))
		        {// suffix from SA12 is smaller
			        SA[k] = i; t++;
			        if (t == n02) // done --- only SA0 suffixes left
				        for (k++; p < n0; p++, k++) SA[k] = SA0[p];
		        } else {// suffix from SA0 is smaller
			        SA[k] = j; p++;
			        if (p == n0) // done --- only SA12 suffixes left
				        for (k++; t < n02; t++, k++) SA[k] = (SA12[t] < n0 ? SA12[t]*3+1:(SA12[t] - n0)*3+2);
		        }
	        }
        }
        #endregion
    }
}
