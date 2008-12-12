using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows.Controls;
using System.Windows.Media;

namespace SporeMaster
{
    class DirectoryTree : VTreeView.TreeNode {
        public static DirectoryTree Tree = new DirectoryTree();

        DirectoryTree parent;
        string fullname, fullnamelower;
        bool visible = true;
        SortedList<string, DirectoryTree> children = null;  //< null means a file node (cannot have children); empty means an empty folder
        int leftPresent, rightPresent;  // Total number of present leaf nodes descended from this one, on each side
        FullTextIndex leftFullText, rightFullText;

        public DirectoryTree() {
            fullname = "";
            fullnamelower = "";
            children = new SortedList<string, DirectoryTree>(new ChildComparer());
            IsExpanded = true;
        }
        private DirectoryTree(DirectoryTree parent, string fullname)
        {
            this.parent = parent;
            this.fullname = fullname;
            this.fullnamelower = fullname.ToLower();
        }

        public string FullName { get { return fullname; } }
        public string BaseName { get { return fullname.Split(new char[] { '.' }, 2)[0]; } }
        public string FileType { get { return fullname.Contains('.') ? fullname.Split(new char[] { '.' }, 2)[1] : ""; } }

        public Brush LeftBrush { get { return this.getPresent(0)!=0 ? Brushes.Black : Brushes.LightGray; } }
        public Brush RightBrush { get { return this.getPresent(1)!=0 ? Brushes.Black : Brushes.LightGray; } }

        public bool LeftPresent { get { return leftPresent != 0; } }
        public bool RightPresent { get { return rightPresent != 0; } }

        public bool IsFolder { get { return children != null; } }

        public DirectoryTree Parent { get { return parent; } }

        public string Path
        {
            get
            {
                return parent!=null && parent.parent!=null ? parent.Path + "\\" + fullname : fullname;
            }
        }

        public int getPresent(int side) { return side==1 ? rightPresent : leftPresent; }
        public void incPresent(int side)
        {
            if (side == 1) ++rightPresent;
            else ++leftPresent;
            if (parent!=null) parent.incPresent(side);
        }
        public void decPresent(int side)
        {
            if (getPresent(side) == 0) return;
            if (children!=null) {
                foreach( var c in children.Values )
                    c.decPresent(side);
            }
            if (getPresent(side) == 1) {
                // Decrement the count for this and all parents
                for(var p = this; p != null; p=p.parent) {
                    if (side == 1) { if (--p.rightPresent != 0) return; }
                    else { if (--p.leftPresent != 0) return; }
                }
            }
            // TODO: if (leftPresent==0 && rightPresent==0) Erase();
        }

        public DirectoryTree getFile( string relativePath, bool create ) {
            if (relativePath == "") return this;
            var pathSplit = relativePath.Split(new char[]{'\\'}, 2);
            string pathNext = pathSplit[0], pathRest= pathSplit.Length==2 ? pathSplit[1] : "";

            DirectoryTree t;
            if (children.TryGetValue( pathNext, out t ))
                return t.getFile(pathRest, create);

            if (!create) return null;

            t = new DirectoryTree(this, pathNext);
            if (pathRest != "")
                t.children = new SortedList<string, DirectoryTree>(new ChildComparer());  //< This is a folder node!
            children.Add(pathNext, t);

            return t.getFile(pathRest, create);
        }

        public void SearchRightPresent()
        {
            visible = visible && RightPresent;
            if (children != null)
                foreach (var c in children.Values)
                    c.SearchRightPresent();
        }

        void makeChildrenVisible()
        {
            visible = true;
            if (children != null)
                foreach (var c in children.Values)
                    c.makeChildrenVisible();
        }

        public bool Search(SearchSpec search)
        {
            makeChildrenVisible();
            foreach(var word in search.require_all)
                SearchInternal(word);
            return visible;
        }
        public bool LeftContains(byte[] bytes)
        {
            return leftFullText != null && leftFullText.Contains(bytes);
        }
        public bool RightContains(byte[] bytes)
        {
            return rightFullText != null && rightFullText.Contains(bytes);
        }
        private bool SearchInternal( SearchSpec.Sequence seq )
        {
            if (!visible) return false;  //< optimization
            if (fullnamelower.Contains(seq.as_lower) || LeftContains(seq.as_utf8) || RightContains(seq.as_utf8))
            {
                return true;
            }
            else
            {
                bool found = false;
                if (children != null)
                    foreach (var c in children.Values)
                        if (c.SearchInternal(seq))
                            found = true;
                if (!found) visible = false;
                return found;
            }
        }

        public void LoadFullText(int side, string path)
        {
            FullTextIndex i = new FullTextIndex(path);
            if (side == 1) rightFullText = i;
            else leftFullText = i;
        }

        #region TreeNode interface
        private static Collection<VTreeView.TreeNode> noTreeNodes = new Collection<VTreeView.TreeNode>();

        public override ICollection<VTreeView.TreeNode> Children
        {
            get
            {
                if (children==null) return noTreeNodes;
                var c = new Collection<VTreeView.TreeNode>();
                foreach (var i in children.Values)
                    if (i.visible && (i.leftPresent+i.rightPresent != 0))
                        c.Add(i);
                return c;
            }
        }
        #endregion

        class ChildComparer : IComparer<string>
        {
            public int Compare(string x, string y)
            {
                bool xs = x.Length>0 && x[0] == '#';
                bool ys = y.Length>0 && y[0] == '#';
                if (xs && !ys) return 1;
                if (ys && !xs) return -1;
                return x.CompareTo(y);
            }
        }
    }
}
