using System;
using System.Collections.Generic;
using System.Text;
using VTreeView;
using Microsoft.Win32;

namespace VTreeViewTest
{
    public class KeyTreeNode : TreeNode
    {
        private RegistryKey key;
        public RegistryKey Key
        {
            get { return key; }
            set { key = value; }
        }




        public override ICollection<TreeNode> Children
        {
            get
            {
                List<TreeNode> children = new List<TreeNode>();

                if (key != null)
                {
                    string[] subKeyNames = key.GetSubKeyNames();

                    for (int i = 0; i < subKeyNames.Length; i++)
                    {
                        children.Add(new KeyTreeNode() { Key = key.OpenSubKey(subKeyNames[i]) });
                    }

                }

                return children;
            }
        }

        public override bool HasChildren
        {
            get
            {
                if (key != null)
                {
                    return (key.SubKeyCount > 0);
                }
                else
                {
                    return false;
                }
            }
        }

        public override string Name
        {
            get
            {
                return key.Name.Substring(key.Name.LastIndexOf('\\') + 1);
            }
        }
    }
}
