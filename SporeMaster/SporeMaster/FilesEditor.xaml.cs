using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.ComponentModel;
using Path = System.IO.Path;

namespace SporeMaster
{
    /// <summary>
    /// Interaction logic for FilesEditor.xaml
    /// </summary>
    public partial class FilesEditor : UserControl
    {
        // MainWindow::loadConfiguration sets these directly:
        public HashSet<string> fullTextExtensions;
        public string WinMergeEXE;

        private DirectoryTree files = DirectoryTree.Tree;
        private DirectoryTreeWatcher[] watcher = new DirectoryTreeWatcher[2];
        private DirectoryTree lastSelectedFile = null;

        private string[] _Path = new string[2];
        private string LeftPath { get { return _Path[0]; } }
        private string RightPath { get { return _Path[1]; } }
        private Window Window { get { return MainWindow.Instance; } }

        IEditor currentEditor;

        public FilesEditor()
        {
            InitializeComponent();

            if (LicenseManager.UsageMode != LicenseUsageMode.Designtime)
            {
                this.Width = double.NaN;
                this.Height = double.NaN;
            }
        }

        public void SetPath(int side, string value, PleaseWait progress)
        {
            if (_Path[side] == value) return;
            _Path[side] = value;
            if (watcher[side] != null) watcher[side].Dispose();
            if (_Path[side] != null && Directory.Exists(_Path[side]))
                watcher[side] = new DirectoryTreeWatcher(files, side, _Path[side], Dispatcher, update, fullTextExtensions, progress);
        }

        public void SearchFiles(string search)
        {
            FileSearch.Text = search;
            update();
        }

        public string GetEditorSelection()
        {
            return currentEditor==null ? "" : currentEditor.GetSelectedText();
        }


        void update() {
            files.Search(FileSearch.Text);
            if (ShowRightOnly.IsChecked == true)
                files.SearchRightPresent();
            DirTree.Data.ClearAll();
            DirTree.Data.AddRootItem(files);
            if (IsVisible)
            {
                if (lastSelectedFile != null && lastSelectedFile.Parent != null && 
                    !lastSelectedFile.LeftPresent && !lastSelectedFile.RightPresent)
                {
                    // File has been deleted or renamed.  Check for renaming
                    UInt32 instance = NameRegistry.Files.toHash(lastSelectedFile.BaseName);
                    UInt32 group = NameRegistry.Groups.toHash(lastSelectedFile.Parent.Path);
                    UInt32 ext = NameRegistry.Types.toHash(lastSelectedFile.FileType);
                    string newpath = NameRegistry.Groups.toName(group) + "\\" + NameRegistry.Files.toName(instance) + "." + NameRegistry.Types.toName(ext);
                    lastSelectedFile = files.getFile(newpath, false);
                }
                DirTree.SelectedItem = lastSelectedFile;
                if (DirTree.SelectedItem != null)
                {
                    DirTree.ScrollIntoView(DirTree.SelectedItem);
                }
                /*if (editing != null && DirTree.SelectedItem != null &&
                    editing == RightPath + "\\" + (DirTree.SelectedItem as DirectoryTree).Path)
                {
                    if (System.IO.File.GetLastWriteTimeUtc(editing) > 
                }*/
                updateEditorSearch();
            }
        }

        void FileSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            update();
        }

        private void editDocument(string path, bool read_only)
        {
            // Select an editor
            if (path != null && path.EndsWith(".png"))
            {
                currentEditor = ImageEditor;
                TabImageEditor.IsSelected = true;
            }
            else if (path != null && path.EndsWith(".rw4\\") && File.Exists(path + "model.mesh.xml"))
            {
                currentEditor = ModelEditor;
                TabModelEditor.IsSelected = true;
            }
            else
            {
                currentEditor = TextEditor;
                TabTextEditor.IsSelected = true;
            }

            currentEditor.Open(path, read_only);
            updateEditorSearch();
        }

        private void Tree_SelectedItemChanged(object sender, SelectionChangedEventArgs e)
        {
            var s = (sender as VTreeView.VTreeView).SelectedItem as DirectoryTree;
            SelectedFilePanel.IsEnabled = (s != null);
            if (s != null)
            {
                lastSelectedFile = s;
                SelectedFileLabel.Text = s.Path;
                SelectedFile_Open.IsEnabled = s.LeftPresent;
                SelectedFile_Erase.IsEnabled = s.RightPresent;
                var f = s.IsFolder ? s : s.Parent;
                SelectedFile_ExploreLeft.IsEnabled = f.LeftPresent;
                SelectedFile_ExploreRight.IsEnabled = f.RightPresent;
                SelectedFile_Save.IsEnabled = false;
                if (!s.LeftPresent && !s.RightPresent)
                {
                    editDocument(null, true);
                }
                else if (s.RightPresent)
                {
                    editDocument(this.RightPath + "\\" + s.Path + (s.IsFolder?"\\":""), false);
                }
                else
                {
                    editDocument(this.LeftPath + "\\" + s.Path + (s.IsFolder ? "\\" : ""), true);
                    if (!s.IsFolder)
                        SelectedFile_Save.IsEnabled = true;
                }
                DirTree.ScrollIntoView(s);
            }
            else
            {
                if (!lastSelectedFile.IsSelected) 
                    lastSelectedFile = null;
                //SelectedFileLabel.Text = "";
            }
        }

        void updateEditorSearch()
        {
            string search = FileSearch.Text.ToLowerInvariant();
            if (currentEditor != null) currentEditor.Search(search);
        }

        void SelectedFile_Open_Click(object sender, RoutedEventArgs e)
        {
            if (currentEditor != null) currentEditor.Save();
            var n = (DirTree.SelectedItem as DirectoryTree);
            var l = watcher[0].Path + "\\" + n.Path;
            var r = watcher[1].Path + "\\" + n.Path;
            if (!n.RightPresent)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(r));
                System.Diagnostics.Process.Start(WinMergeEXE, "/u /wl /s \"" + l + "\" \"" + l + "\" \"" + r + "\"");
            } else if (!n.LeftPresent) {
                l = "Not Present in Spore";
                System.Diagnostics.Process.Start(WinMergeEXE, "/u /wl /s \"" + l + "\" \"" + r + "\"");
            } else
                System.Diagnostics.Process.Start(WinMergeEXE, "/u /wl /s \"" + l + "\" \"" + r + "\"" );
        }

        void SelectedFile_Save_Click(object sender, RoutedEventArgs e)
        {
            if (currentEditor != null) currentEditor.Save();

            var n = (DirTree.SelectedItem as DirectoryTree);
            if (n.LeftPresent && !n.RightPresent && !n.IsFolder)
            {
                var l = watcher[0].Path + "\\" + n.Path;
                var r = watcher[1].Path + "\\" + n.Path;
                Directory.CreateDirectory(Path.GetDirectoryName(r));
                System.IO.File.Copy(l, r);
                editDocument(r, false);
            }
        }

        void SelectedFile_Erase_Click(object sender, RoutedEventArgs e)
        {
            var n = (DirTree.SelectedItem as DirectoryTree);
            var r = watcher[1].Path + "\\" + n.Path;
            if (MessageBox.Show(this.Window, "Are you sure you want to delete " + r + "?", "Delete", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                if (n.IsFolder)
                    System.IO.Directory.Delete(r, true);
                else
                {
                    System.IO.File.Delete(r);
                    if (File.Exists(r + ".search_index"))
                        File.Delete(r + ".search_index");
                }
            }
        }

        void ShowRightOnly_Changed(object sender, RoutedEventArgs e)
        {
            update();
        }

        void SelectedFile_Explorer_Click(object sender, RoutedEventArgs e)
        {
            if (currentEditor != null) currentEditor.Save();
            var n = (DirTree.SelectedItem as DirectoryTree);
            int side = (sender == SelectedFile_ExploreLeft) ? 0 : 1;
            var p = watcher[side].Path + "\\" + n.Path;
            if (!n.IsFolder) p = Path.GetDirectoryName(p);
            System.Diagnostics.Process.Start(p);
        }

        void DirTree_KeyDown(object sender, KeyEventArgs e)
        {
            var n = (DirTree.SelectedItem as DirectoryTree);
            if (e.Key == Key.Up)
            {
                e.Handled = true;
                if (DirTree.SelectedIndex > 0)
                    DirTree.SelectedIndex = DirTree.SelectedIndex - 1;
            }
            if (e.Key == Key.Down)
            {
                e.Handled = true;
                DirTree.SelectedIndex = DirTree.SelectedIndex + 1;
            }
            if (e.Key == Key.Right)
            {
                if (n != null && n.IsFolder && !n.IsExpanded)
                    n.IsExpanded = true;
                e.Handled = true;
            }
            if (e.Key == Key.Left)
            {
                if (n != null)
                {
                    if (n.IsExpanded)
                        n.IsExpanded = false;
                    else if (n.Parent != null)
                    {
                        if (n.Parent.Parent != null)
                            n.Parent.IsExpanded = false;
                        DirTree.SelectedItem = n.Parent;
                    }
                }
                e.Handled = true;
            }
        }

        private void UserControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            update();
            if (currentEditor != null) currentEditor.Save();
        }
    }
}
