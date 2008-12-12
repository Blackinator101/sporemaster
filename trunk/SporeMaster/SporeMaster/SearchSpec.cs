using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SporeMaster
{
    public class SearchSpec
    {
        // Represents a search phrase typed by the user or a search being done programmatically e.g. by the
        //   names editor.  This class is not a search engine; DirectoryTree, FullTextIndex and various IEditor
        //   implementations all do searching.

        public struct Sequence
        {
            public string as_lower;
            public byte[] as_utf8;

            public Sequence(string x)
            {
                this.as_lower = x.ToLowerInvariant();
                this.as_utf8 = Encoding.UTF8.GetBytes(this.as_lower);
            }
        };

        public Sequence[] require_all;

        public static SearchSpec all = new SearchSpec("");

        public SearchSpec(string phrase)
        {
            require_all = (
                from word in phrase.Split(' ')
                where word != ""
                select new Sequence(word)
                ).ToArray();
        }
    }
}
