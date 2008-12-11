using System;
using System.Collections.Generic;
using System.Linq;
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
using System.IO;

namespace SporeMaster
{
    /// <summary>
    /// Interaction logic for EditorImage.xaml
    /// </summary>
    public partial class EditorImage : UserControl, IEditor
    {
        public EditorImage()
        {
            InitializeComponent();
            if (System.ComponentModel.LicenseManager.UsageMode != System.ComponentModel.LicenseUsageMode.Designtime)
            {
                this.Width = double.NaN; ;
                this.Height = double.NaN; ;
            }
        }

        public void Open(string filename, bool read_only)
        {
            var data = File.ReadAllBytes( filename );
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = new MemoryStream(data);
            bitmap.EndInit();
            image.Source = bitmap;
        }
        public void Save()
        {
        }
        public void Search(string search_string)
        {
        }
        public string GetSelectedText(){
            return "";
        }
    }
}
