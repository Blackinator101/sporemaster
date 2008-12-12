using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace VTreeView
{
    /// <summary>
    /// Follow steps 1a or 1b and then 2 to use this custom control in a XAML file.
    ///
    /// Step 1a) Using this custom control in a XAML file that exists in the current project.
    /// Add this XmlNamespace attribute to the root element of the markup file where it is 
    /// to be used:
    ///
    ///     xmlns:MyNamespace="clr-namespace:VTreeView"
    ///
    ///
    /// Step 1b) Using this custom control in a XAML file that exists in a different project.
    /// Add this XmlNamespace attribute to the root element of the markup file where it is 
    /// to be used:
    ///
    ///     xmlns:MyNamespace="clr-namespace:VTreeView;assembly=VTreeView"
    ///
    /// You will also need to add a project reference from the project where the XAML file lives
    /// to this project and Rebuild to avoid compilation errors:
    ///
    ///     Right click on the target project in the Solution Explorer and
    ///     "Add Reference"->"Projects"->[Select this project]
    ///
    ///
    /// Step 2)
    /// Go ahead and use your control in the XAML file.
    ///
    ///     <MyNamespace:CustomControl1/>
    ///
    /// </summary>
    /// 

    public class VTreeView : ListBox
    {
        public VTreeView()
        {
            this.MouseDoubleClick += new MouseButtonEventHandler(VTreeView_MouseDoubleClick);
            _data = new TreeData();
            this.ItemsSource = _data.Items;
        }

        static VTreeView()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(VTreeView), new FrameworkPropertyMetadata(typeof(VTreeView)));
        }

        private TreeData _data;

        public TreeData Data
        {
            get { return _data; }
        }

        private bool doubleClickExpand = true;
        public bool DoubleClickExpand
        {
            get { return doubleClickExpand; }
            set { doubleClickExpand = value; }
        }


        void VTreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DoubleClickExpand)
            {
                FrameworkElement fe = e.OriginalSource as FrameworkElement;
                if (fe != null)
                {
                    TreeNode TN = (TreeNode)fe.DataContext;
                    if (TN != null)
                    {
                        TN.IsExpanded = !TN.IsExpanded;
                        e.Handled = true;
                    }
                }
            }
        }

        ScrollViewer m_scrollViewer;
        public ScrollViewer ScrollViewer
        {
            get
            {
                if (m_scrollViewer == null)
                {
                    DependencyObject border = VisualTreeHelper.GetChild(this, 0);
                    if (border != null)
                    {
                        m_scrollViewer = VisualTreeHelper.GetChild(border, 0) as ScrollViewer;
                    }
                }

                return m_scrollViewer;
            }
        }
    }
}
