using System;
using System.Collections.Generic;
using System.Text;
using VTreeView;

namespace VTreeViewTest
{

    public class RandomTreeNode : TreeNode
    {




        private List<TreeNode> children = null;

        public override ICollection<TreeNode> Children
        {
            get
            {
                if (children == null)
                    updateChildren();

                return children;
            }
        }

        private void updateChildren()
        {
            children = new List<TreeNode>();
            if ((this.GetHashCode() & 1) == 1)
                return;
        
            for (int i = 0; i < 1500; i++)
            {
                children.Add(new RandomTreeNode() { Name = i.ToString() });
            }


        }


        private string _name = "";
        public override string Name
        {
            get { return _name; }
            set
            {
                _name = value;
            }

        }

    }

    

}
