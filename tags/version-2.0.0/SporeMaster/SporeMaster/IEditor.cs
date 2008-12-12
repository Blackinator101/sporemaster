using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SporeMaster
{
    interface IEditor
    {
        void Open(string path, bool read_only);
        void Search(SearchSpec search);
        void Save();
        string GetSelectedText();
        
    }
}
