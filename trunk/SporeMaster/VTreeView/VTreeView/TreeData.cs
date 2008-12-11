using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections;

namespace VTreeView
{
    public class TreeData : INotifyPropertyChanged
    {
        private ObservableCollection<TreeNode> items = new ObservableCollection<TreeNode>();
        public ObservableCollection<TreeNode> Items
        {
            get { return items; }
        }

        public void AddRootItem(TreeNode TN)
        {
            TN.Level = 0;
            TN.PropertyChanged +=new PropertyChangedEventHandler(TN_PropertyChanged);
            this.items.Add(TN);

            if (TN.IsExpanded)
            { 
                PopulateChildren(TN);
            }
        }
        public void AddRootItems(IEnumerable<TreeNode> TNs)
        {
            var myEnumerator = TNs.GetEnumerator();

            while (myEnumerator.MoveNext())
            {
                AddRootItem(myEnumerator.Current);
            }
        }

        public void ClearAll()
        {
            this.items.Clear();
        }

        private void PopulateChildren(TreeNode TN)
        {
            if (items.Contains(TN))
            {
                int index = this.items.IndexOf(TN);
                if (index == this.items.Count - 1 || this.items[index + 1].Level <= TN.Level)
                {
                    IEnumerable myList = TN.Children;
                    int offset = 0;
                    foreach (TreeNode TN2 in myList)
                    {
                        TN2.PropertyChanged += new PropertyChangedEventHandler(TN_PropertyChanged);
                        TN2.Level = TN.Level + 1;
                        this.items.Insert(index + offset + 1, TN2);

                        offset++;
                    }

                    foreach (TreeNode TN2 in myList)
                    {
                        if ((TN2.IsExpanded) && (TN.HasChildren))
                        {
                            PopulateChildren(TN2);
                        }
                    }
                }
            }
        }

        void TN_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsExpanded")
            {
                TreeNode TN = (TreeNode)sender;
                if (TN.IsExpanded)
                {
                    this.PopulateChildren(TN);
                }
                else
                {
                    this.ClearChildren(TN);
                }

                if (NodeExpandedChanaged != null)
                    NodeExpandedChanaged(this, new NodeExpandedChanagedEventArgs(TN));
            }
        }

        private void ClearChildren(TreeNode TN)
        {
            if (items.Contains(TN))
            {
                int indexToRemove = this.items.IndexOf(TN) + 1;
                while ((indexToRemove < this.items.Count) && (this.items[indexToRemove].Level > TN.Level))
                {
                    items[indexToRemove].PropertyChanged -= new PropertyChangedEventHandler(TN_PropertyChanged);
                    items.RemoveAt(indexToRemove);
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public void RemoveNode(TreeNode TN)
        {
            ClearChildren(TN);

            if (items.Contains(TN))
            {
                this.items.Remove(TN);
            }
        }

        public void UpdateChildren(TreeNode TN)
        {
            TN.UpdateChildren();

            if (TN.HasChildren == false)
            {
                TN.IsExpanded = false;
            }
            else if (TN.IsExpanded)
            {
                if (items.Contains(TN))
                {
                    ClearChildren(TN);
                    PopulateChildren(TN);
                }
            }
        }

        public event NodeExpandedChanagedEventHandler NodeExpandedChanaged;

        public delegate void NodeExpandedChanagedEventHandler(object sender, NodeExpandedChanagedEventArgs e);
        public class NodeExpandedChanagedEventArgs : EventArgs
        {
            public NodeExpandedChanagedEventArgs(TreeNode tN)
            {
                _tN = tN;
            }

            private TreeNode _tN;
            public TreeNode TN
            {
                get { return _tN; }
            }
        }
    }
}
