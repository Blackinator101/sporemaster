using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace VTreeView
{
    public class RowExpander : System.Windows.Controls.Control
    {
        static RowExpander()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(RowExpander), new FrameworkPropertyMetadata(typeof(RowExpander)));
        }
    }
}
