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
using Microsoft.Win32;
using System.Windows.Threading;

namespace VTreeViewTest
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class Window1 : Window
    {
        public Window1()
        {
            InitializeComponent();
            this.Loaded += new RoutedEventHandler(Window1_Loaded);
        }


        void Window1_Loaded(object sender, RoutedEventArgs e)
        {

            RandomTreeNode root = new RandomTreeNode() { Name = "Root" };
            //KeyTreeNode root = new KeyTreeNode() { Key = Registry.CurrentUser };


            //myTreeView.ItemsSource = root.Children;
            myVTreeView.Data.AddRootItems(root.Children);

            #region Comparison Code
            DT = new DispatcherTimer();
            DT.Interval = new TimeSpan(0, 0, 2);
            DT.Tick += new EventHandler(DT_Tick);
            DT.Start(); 
            #endregion
        }

        #region Comparison Code
        DispatcherTimer DT;
        void DT_Tick(object sender, EventArgs e)
        {
            TB1.Text = "TreeView: " + GetVisualCount(myTreeView).ToString() + " (Visuals)";
            TB2.Text = "VTreeView:  " + GetVisualCount(myVTreeView).ToString() + " (Visuals)";

        }
        private static int GetVisualCount(DependencyObject visual)
        {
            int visualCount = 1;
            int childCount = VisualTreeHelper.GetChildrenCount(visual);

            for (int i = 0; i < childCount; i++)
            {
                DependencyObject childVisual = VisualTreeHelper.GetChild(visual, i);
                visualCount += GetVisualCount(childVisual);
            }

            return visualCount;
        } 
        #endregion
    }
}
