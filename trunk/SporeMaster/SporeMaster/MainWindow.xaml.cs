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
using Microsoft.Win32;
using Path = System.IO.Path;

namespace SporeMaster
{
    public partial class MainWindow : Window
    {
        private const string ConfigFile = "SporeMaster Configuration.xml";

        public static MainWindow Instance = null;

        public string LeftPath = "spore.unpacked";
        public string RightPath = null;
        string UnpackedFolderFormat = "{0}{1}.unpacked";

        public MainWindow()
        {
            Instance = this;
            InitializeComponent();

            loadConfiguration();

            Registry.SetValue("HKEY_CURRENT_USER\\Software\\Thingamahoochie\\WinMerge\\Backup", "EnableFile", (UInt32)0, RegistryValueKind.DWord);

            if (Directory.Exists(LeftPath))
                tabControl.SelectedIndex = 1;
            new PleaseWait(null, "Updating Index...", delegate(PleaseWait progress)
            {
                NameRegistry.Files = NameRegistry.Groups = new NameRegistry("reg_file.txt");
                NameRegistry.Types = new NameRegistry("reg_type.txt");
                NameRegistry.Properties = new NameRegistry("reg_property.txt");
                if (!File.Exists("Not present in Spore"))
                {
                    File.WriteAllText("Not Present in Spore", "");
                    File.SetAttributes("Not Present in Spore", FileAttributes.ReadOnly);
                }
                FilesEditor.SetPath(0, LeftPath, progress);
            }).ShowDialog();
        }

        void loadConfiguration()
        {
            if (!File.Exists(ConfigFile))
                Directory.SetCurrentDirectory(Path.GetDirectoryName(typeof(MainWindow).Assembly.Location));

            using (var cfg = System.Xml.XmlTextReader.Create(ConfigFile))
            {
                cfg.ReadToFollowing("SporeMasterConfig");
                while (cfg.Read())
                {
                    if (!cfg.IsStartElement()) continue;
                    if (cfg.Name == "Packages")
                    {
                        PackageList.Items.Clear();
                        var subtree = cfg.ReadSubtree();
                        while (subtree.Read())
                        {
                            if (subtree.IsStartElement() && subtree.Name == "Package")
                                PackageList.Items.Add(makeListItem(subtree.ReadString()));
                        }
                    }
                    else if (cfg.Name == "FullTextIndexExtensions")
                    {
                        FilesEditor.fullTextExtensions = new HashSet<string>();
                        var subtree = cfg.ReadSubtree();
                        while (subtree.Read())
                        {
                            if (subtree.IsStartElement() && subtree.Name == "Extension")
                                FilesEditor.fullTextExtensions.Add(subtree.ReadString());
                        }
                    }
                    else if (cfg.Name == "WinMerge")
                    {
                        FilesEditor.WinMergeEXE = cfg.ReadString();
                    }
                    else if (cfg.Name == "UnpackedFolderFormat")
                    {
                        UnpackedFolderFormat = cfg.ReadString();
                    }
                }
            }
        }

        void saveConfiguration()
        {
            using (var cfg = new System.Xml.XmlTextWriter(ConfigFile, Encoding.UTF8))
            {
                cfg.Formatting = System.Xml.Formatting.Indented;
                cfg.WriteStartDocument();
                cfg.WriteStartElement("SporeMasterConfig");
                cfg.WriteStartElement("Packages");
                foreach (var p in PackageList.Items)
                    cfg.WriteElementString("Package", (p as ListBoxItem).Content as string);
                cfg.WriteEndElement();
                cfg.WriteStartElement("FullTextIndexExtensions");
                foreach (var ext in FilesEditor.fullTextExtensions)
                    cfg.WriteElementString("Extension", ext);
                cfg.WriteEndElement();
                cfg.WriteElementString("WinMerge", FilesEditor.WinMergeEXE);
                cfg.WriteElementString("UnpackedFolderFormat", UnpackedFolderFormat);
                cfg.WriteEndElement();
                cfg.WriteEndDocument();
            }
        }

        public void SearchFiles(string search)
        {
            TabFilesEditor.IsSelected = true;
            FilesEditor.SearchFiles(search);
        }

        void PackageList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Btn_EditPackage.IsEnabled = Btn_RemovePackage.IsEnabled = (PackageList.SelectedIndex != -1);
            Btn_PackageUp.IsEnabled = (PackageList.SelectedIndex > 0);
            Btn_PackageDown.IsEnabled = (PackageList.SelectedIndex != -1 && PackageList.SelectedIndex < PackageList.Items.Count - 1);
        }

        void Btn_NewPackage_Click(object sender, RoutedEventArgs e)
        {
            var open = new Microsoft.Win32.OpenFileDialog();
            open.Filter = "Spore packages|*.package";
            open.Multiselect = true;
            open.RestoreDirectory = true;
            if (open.ShowDialog(this) == true)
            {
                foreach (var f in open.FileNames)
                {
                    PackageList.Items.Add(makeListItem(f));
                }
            }
            saveConfiguration();
        }

        void Btn_EditPackage_Click(object sender, RoutedEventArgs e)
        {
            int i = PackageList.SelectedIndex;
            var open = new Microsoft.Win32.OpenFileDialog();
            open.Filter = "Spore packages|*.package";
            open.FileName = PackageList.SelectedItem as string;
            open.RestoreDirectory = true;
            if (open.ShowDialog(this) == true)
            {
                PackageList.Items.RemoveAt(i);
                PackageList.Items.Insert(i, makeListItem(open.FileName));
                PackageList.SelectedIndex = i;
                PackageList.Focus();
            }
            saveConfiguration();
        }

        void Btn_RemovePackage_Click(object sender, RoutedEventArgs e)
        {
            PackageList.Items.Remove(PackageList.SelectedItem);
            saveConfiguration();
        }

        void Btn_PackageUp_Click(object sender, RoutedEventArgs e)
        {
            int i = PackageList.SelectedIndex;
            string f = (PackageList.Items[i] as ListViewItem).Content as string;
            PackageList.Items.RemoveAt(i);
            PackageList.Items.Insert(i - 1, makeListItem(f));
            PackageList.SelectedIndex = i - 1;
            PackageList.Focus();
            saveConfiguration();
        }
        void Btn_PackageDown_Click(object sender, RoutedEventArgs e)
        {
            int i = PackageList.SelectedIndex;
            string f = (PackageList.Items[i] as ListViewItem).Content as string;
            PackageList.Items.RemoveAt(i);
            PackageList.Items.Insert(i + 1, makeListItem(f));
            PackageList.SelectedIndex = i + 1;
            PackageList.Focus();
            saveConfiguration();
        }

        ListViewItem makeListItem(string filename)
        {
            var l = new ListViewItem();
            l.Content = filename;
            return l;
        }

        void Btn_Unpack_Click(object sender, RoutedEventArgs e)
        {
            var filenames = (from ListViewItem item in PackageList.Items
                             select item.Content as String).ToArray();

            new PleaseWait(null, "Unpacking", delegate(PleaseWait progress)
            {
                try
                {
                    FilesEditor.SetPath(0, null, progress);
                    Directory.CreateDirectory(LeftPath);
                    var streams = (from f in filenames
                                   select File.OpenRead(f) as Stream).ToArray();
                    progress.beginTask(0.3, 1.0);
                    var b = new PackageUnpack(streams, LeftPath, progress);
                    progress.endTask();
                    progress.beginTask(0.7, 1.0);
                    FilesEditor.SetPath(0, LeftPath, progress);
                    progress.endTask();
                }
                catch (Exception exc)
                {
                    progress.complete();
                    MessageBox.Show(exc.ToString(), "Error unpacking packages");
                }

            }).ShowDialog();
        }

        void NewMod_Click(object sender, RoutedEventArgs e)
        {
            var open = new Microsoft.Win32.SaveFileDialog();
            open.Filter = "Spore packages|*.package";
            open.RestoreDirectory = true;
            if (open.ShowDialog(this) == true)
            {
                ModPath.Text = open.FileName;
                Directory.CreateDirectory(buildRightPath(ModPath.Text));
                checkModReady();
            }
        }

        void OpenMod_Click(object sender, RoutedEventArgs e)
        {
            var open = new Microsoft.Win32.OpenFileDialog();
            open.Filter = "Spore packages|*.package";
            open.FileName = ModPath.Text;
            open.RestoreDirectory = true;
            if (open.ShowDialog(this) == true)
            {
                ModPath.Text = open.FileName;
                checkModReady();
            }
        }

        void Pack_Mod_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string mod = ModPath.Text;
                if (File.Exists(mod))
                {
                    if (MessageBox.Show("This will overwrite\n  '" + ModPath.Text + "'\nwith your changes.", "Warning", MessageBoxButton.OKCancel) != MessageBoxResult.OK)
                        return;
                }

                new PleaseWait(null, "Packing Mod", delegate(PleaseWait progress)
                {
                    var stream = File.Create(mod);
                    new PackagePack(stream, buildRightPath(mod), progress);
                }).ShowDialog();

                checkModReady();
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.ToString(), "Error packing mod");
            }
        }
        void Unpack_Mod_Click(object sender, RoutedEventArgs e)
        {
            // TODO: PleaseWait
            try
            {
                string path = buildRightPath(ModPath.Text);
                if (Directory.Exists(path))
                {
                    if (MessageBox.Show("This will overwrite any changes you have made\nsince the last time you packed this mod!\n\nAre you sure?", "Warning", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                        return;
                }
                FilesEditor.SetPath(1, null, null);
                var stream = File.OpenRead(ModPath.Text);
                var b = new PackageUnpack(new Stream[] { stream }, path, null);
                checkModReady();
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.ToString(), "Error unpacking mod");
            }
        }

        string buildRightPath(string modPath)
        {
            string dirname = Path.GetDirectoryName(modPath);
            if (dirname != "") dirname += "\\";
            return String.Format(UnpackedFolderFormat, dirname, Path.GetFileName(modPath));
        }

        void checkModReady()
        {
            FilesEditor.SetPath(1, null, null);
            if (RightPath != null)
                FullTextIndex.ResetPath(RightPath);
            if (ModPath.Text != "")
            {
                RightPath = buildRightPath(ModPath.Text);
                Unpack_Mod.IsEnabled = File.Exists(ModPath.Text);
                if (Directory.Exists(RightPath))
                {
                    FilesEditor.SetPath(1, RightPath, null);
                    TabFilesEditor.IsEnabled = true;
                    Pack_Mod.IsEnabled = true;
                    Unpack_Mod_Doc.Text = "You have unpacked this mod.  Edit it in the Files tab.";
                    Pack_Mod_Doc.Text = "You must pack the mod to see changes in Spore.";
                }
                else
                {
                    TabFilesEditor.IsEnabled = false;
                    Pack_Mod.IsEnabled = false;
                    Unpack_Mod_Doc.Text = "You must unpack this mod before editing it.";
                    Pack_Mod_Doc.Text = "";
                }
            }
            else
            {
                Unpack_Mod.IsEnabled = false;
                Pack_Mod.IsEnabled = false;
                Unpack_Mod_Doc.Text = "";
                Pack_Mod_Doc.Text = "";
                RightPath = null;
            }
        }

        void tabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.OriginalSource != tabControl)
                return;
            switch (tabControl.SelectedIndex)
            {
                case 3: Tab_NameEditor_init(); break;
            }
        }

        void Tab_NameEditor_init()
        {
            string text = FilesEditor.GetEditorSelection();
            if (text != null) {
                UInt32 hash;
                if (text.StartsWith("#"))
                {
                    if (UInt32.TryParse(text.Substring(1), System.Globalization.NumberStyles.AllowHexSpecifier, null, out hash))
                    {
                        NameEditor.EditName(null, hash);
                        return;
                    }
                } 
                else if (UInt32.TryParse(text, out hash) && hash != 0)
                {
                    NameEditor.EditName(null, hash);
                    return;
                }
                else if (text.Length == 8 && UInt32.TryParse(text, System.Globalization.NumberStyles.AllowHexSpecifier, null, out hash))
                {
                    NameEditor.EditName(null, hash);
                    return;
                }
                else if (!text.Contains("\n") && !text.Contains(" "))
                {
                    NameEditor.EditName(text, null);
                    return;
                }
            }
            var n = (FilesEditor.DirTree.SelectedItem as DirectoryTree);
            if (n != null)
            {
                string s = n.FullName.Split(new char[] { '.' }, 2)[0];
                if (s.StartsWith("#"))
                    NameEditor.EditName(null, NameRegistry.Files.toHash(s));
                else
                    NameEditor.EditName(s, NameRegistry.Files.toHash(s));
            }
        }
    }
}