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
using System.Windows.Media.Media3D;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SporeMaster
{
    /// <summary>
    /// Interaction logic for EditorModel.xaml
    /// </summary>
    public partial class EditorModel : UserControl, IEditor
    {
        public EditorModel()
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
            Errors.Text = "";
            meshMain.TriangleIndices.Clear();
            meshMain.Positions.Clear();
            meshMain.Normals.Clear();
            wireframe.Points.Clear();
            if (filename != null) {
                try
                {
                    var imp = new RenderWare4.OgreXmlReader(filename + "model.mesh.xml");
                    foreach (var v in imp.vertices)
                    {
                        meshMain.Positions.Add(new Point3D(v.x, v.y, v.z));
                        meshMain.Normals.Add(new Vector3D(
                            RenderWare4.Vertex.UnpackNormal(v.normal, 0),
                            RenderWare4.Vertex.UnpackNormal(v.normal, 1),
                            RenderWare4.Vertex.UnpackNormal(v.normal, 2)));
                    }
                    foreach (var t in imp.triangles)
                    {
                        meshMain.TriangleIndices.Add((int)t.i);
                        meshMain.TriangleIndices.Add((int)t.j);
                        meshMain.TriangleIndices.Add((int)t.k);
                    }
                    wireframe.MakeWireframe(Model);
                }
                catch (Exception e)
                {
                    Errors.Text = e.ToString();
                }
            }
        }
        public void Save()
        {
        }
        public void Search(string search_string)
        {
        }
        public string GetSelectedText()
        {
            return "";
        }

        private void OnViewportMouseUp(object sender, MouseButtonEventArgs e)
        {

        }

        private void OnViewportMouseDown(object sender, MouseButtonEventArgs e)
        {

        }

        private void OnViewportMouseMove(object sender, MouseEventArgs e)
        {

        }

        private void Zoom_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            double s = -Math.Pow(2.0, -Zoom.Value);
            camMain.Position = new Point3D(camMain.LookDirection.X * s, camMain.LookDirection.Y * s, camMain.LookDirection.Z * s);
        }
    }
}
