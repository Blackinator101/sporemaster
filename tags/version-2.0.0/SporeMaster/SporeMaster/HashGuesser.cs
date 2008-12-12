using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using Gibbed.Spore.Helpers;
using System.IO;

namespace SporeMaster
{
    class HashGuesser : INotifyPropertyChanged
    {
        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged(String info)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(info));
            }
        }
        #endregion

        public class Result
        {
            public string Text { get; set; }
        };

        public List<Result> Results { get; set; }

        Nullable<UInt32> target_hash;
        string prefix = "", suffix = "", contains = "";
        bool isEnabled = false;

        public Nullable<UInt32> HashCode { get { return target_hash; } set { target_hash = value; guess();  } }
        public string Prefix { get { return prefix; } set { prefix = value; guess(); } }
        public string Suffix { get { return suffix; } set { suffix = value; guess(); } }
        public string Contains { get { return contains; } set { contains = value; guess(); } }
        public bool Enabled { get { return isEnabled; } set { isEnabled = value; guess(); } }

        private UInt32 //fnv_base = 0x811c9dc5,
                       //fnv_step = 0x1000193,
                       fnv_unstep = 0x359c449b;   // fnv_unstep * fnv_step = 1 (mod 2^32)

        private List<string> left, right;
        private Dictionary<UInt32, string> left_hash;
        string left_hash_prefix;

        void guess()
        {
            Results = new List<Result>();
            if (target_hash == null || !isEnabled) { NotifyPropertyChanged("results"); return; }

            if (left == null) left = read("guess_left.txt", 5000000);
            if (right == null) right = read("guess_right.txt", 5000000);

            if (left_hash == null || prefix != left_hash_prefix)
            {
                left_hash_prefix = prefix;
                recalc_left();
            }

            UInt32 th = FNV_rev(target_hash.Value, suffix);
            if (contains != "")
            {
                if (left_hash.ContainsKey(FNV_rev(th, contains)))
                    foreach (var l in left_hash[FNV_rev(th, contains)].Split(new char[] { '\0' }))
                        Results.Add(new Result { Text = prefix + l + contains + suffix });
                /*foreach (var r in right)
                {
                    UInt32 h = th;
                    for (int i = r.Length - 1; i >= 0; i--)
                    {
                        h ^= r[i];
                        h *= fnv_unstep;
                        UInt32 h2 = FNV_rev(h, contains);
                        for (int j = i - 1; j >= 0; j--)
                        {
                            h ^= r[j];
                            h *= fnv_unstep;
                        }
                        if (left_hash.ContainsKey(h2))
                            foreach (var l in left_hash[h2].Split(new char[] { '\0' }))
                                Results.Add(new Result { Text = prefix + l + r.Substring(0, i) + contains + r.Substring(i) + suffix });
                    }
                }*/
            }
            foreach (var r in right)
            {
                UInt32 h = FNV_rev(th, r);
                if (left_hash.ContainsKey(h))
                    foreach (var l in left_hash[h].Split(new char[] { '\0' }))
                        Results.Add(new Result { Text = prefix + l + r + suffix });
                if (contains != "")
                {
                    h = FNV_rev(h, contains);
                    if (left_hash.ContainsKey(h))
                        foreach (var l in left_hash[h].Split(new char[] { '\0' }))
                            Results.Add(new Result { Text = prefix + l + contains + r + suffix });
                }
            }

            Results = (from r in Results
                       group r by r.Text into g
                       where g.Key.Contains(contains)
                       orderby g.Key.Length
                       select new Result { Text = g.Key }
                       ).ToList();

            NotifyPropertyChanged("results");
        }

        /*UInt32 FNV(UInt32 h, string chars) {
            for (int i = 0; i < chars.Length; i++)
            {
                h *= fnv_step;
                h ^= chars[i];
            }
            return h;
        }*/
        UInt32 FNV_rev(UInt32 h, string chars)
        {
            for (int i = chars.Length - 1; i >= 0; i--)
            {
                h ^= chars[i];
                h *= fnv_unstep;
            }
            return h;
        }

        void recalc_left()
        {
            left_hash = new Dictionary<uint, string>();
            foreach( var hi in left ) {
                string line = prefix + hi;
                var h = line.FNV();
                if (left_hash.ContainsKey(h))
                    left_hash[h] = left_hash[h] + "\0" + hi;
                else
                    left_hash.Add(h, hi);
            }
        }

        List<string> read(string name, int max)
        {
            List<string> result = new List<string>();
            using (var read_right = new StreamReader(name))
            {
                while (max-- != 0)
                {
                    string line = read_right.ReadLine();
                    if (line == null) break;
                    result.Add(line);
                }
            }
            return result;
        }

        public HashGuesser()
        {
            Results = new List<Result>();
        }

    };
}
