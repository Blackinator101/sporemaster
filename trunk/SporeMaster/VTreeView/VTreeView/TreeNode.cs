using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel;

namespace VTreeView
{
    public abstract class TreeNode : INotifyPropertyChanged
    {

        public TreeNode()
        {

        }

        private int level = 0;
        public int Level
        {
            get { return level; }
            set { level = value; }
        }

        private bool _isSelected = false;
        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                if (value != _isSelected)
                {
                    _isSelected = value;
                    OnPropertyChanged("IsSelected");
                }
            }
        }

        private bool isExpanded = false;
        public virtual bool IsExpanded
        {
            get { return isExpanded; }
            set
            {
                if (value != isExpanded)
                {
                    isExpanded = value;
                    OnPropertyChanged("IsExpanded");
                }
            }
        }

        public abstract ICollection<TreeNode> Children
        {
            get;
        }

        public virtual bool HasChildren
        {
            get
            {
                return (Children.Count > 0);
            }
        }

        public virtual string Name
        {
            get
            {
                return this.ToString();
            }
            set
            { 
            
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public virtual void UpdateChildren()
        {
            OnPropertyChanged("HasChildren");
        }

    }
}
