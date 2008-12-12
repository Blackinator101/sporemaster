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
using System.ComponentModel;
using Gibbed.Spore.Helpers;

namespace SporeMaster
{
    /// <summary>
    /// Interaction logic for NameEditor.xaml
    /// </summary>
    public partial class NameEditor : UserControl
    {
        HashGuesser guesser { get; set; }

        public NameEditor()
        {
            InitializeComponent();
            if (LicenseManager.UsageMode != LicenseUsageMode.Designtime)
            {
                guesser = new HashGuesser();
                this.Width = double.NaN; ;
                this.Height = double.NaN; ;
                GuessPanel.DataContext = guesser;
            }
        }

        public void EditName(string name, Nullable<UInt32> hash)
        {
            AliasName.Text = name != null ? name : "";
            AliasHash.Text = hash != null ? hash.Value.ToString("X8") : "";
        }

        private void AliasName_TextChanged(object sender, TextChangedEventArgs e)
        {
            update();
        }

        private UInt32 parseHash(string hash, out bool okSoFar, out bool done)
        {
            UInt32 h;
            bool parses = UInt32.TryParse(hash, System.Globalization.NumberStyles.AllowHexSpecifier, null, out h);
            okSoFar = hash == "" || parses;
            done = parses && hash.Length == 8;
            return h;
        }

        private Brush LightGreen = new SolidColorBrush(new Color{R=0xD8,G=0xFF,B=0xD8,A=0xFF});

        private void AliasHash_TextChanged(object sender, TextChangedEventArgs e)
        {
            GuessHash.Text = AliasHash.Text;
            bool okSoFar, done;
            parseHash(AliasHash.Text, out okSoFar, out done);
            AliasHash.Background = done ? LightGreen : okSoFar ? Brushes.White : Brushes.Yellow;
            update();
        }

        private void update() {
            if (guesser == null) return;  // design time!
            var name = AliasName.Text;
            var hash = name.FNV();
            var altname = NameRegistry.Files.toName(hash);
            SaveName.IsEnabled = name.Length > 0;
            if (name.EndsWith("~"))
            {
                if (NameRegistry.Files.checkName(name, out hash))
                    AliasNameStatus.Text = "Known alias.";
                else
                    AliasNameStatus.Text = "Unknown alias.";
                SaveName.IsEnabled = false;
            }
            else if (altname == name)
                AliasNameStatus.Text = "Already known.";
            else if (!altname.StartsWith("#"))
                AliasNameStatus.Text = "Already defined as '" + altname + "'.";
            else if (IsHashUsed(hash))
            {
                AliasNameStatus.Inlines.Clear();
                AliasNameStatus.Inlines.Add(AliasNameStatusLink);
                AliasNameStatusLink.Inlines.Clear();
                AliasNameStatusLink.Inlines.Add("Used by Spore!");
            }
            else
            {
                AliasNameStatus.Text = "Not used by Spore.";
            }
            AliasNameHashesTo.Text = hash.ToString("X8");

            if (HashColumn.IsEnabled)
            {
                if (AliasHash.Text.Length!=8 || !UInt32.TryParse( AliasHash.Text, System.Globalization.NumberStyles.AllowHexSpecifier, null, out hash )) {
                    AliasHashStatus.Text = "Not a valid hash!";
                    SaveAlias.IsEnabled = false;
                } else {
                    UInt32 h2;
                    SaveAlias.IsEnabled = AliasName.Text.EndsWith("~") && !NameRegistry.Files.checkName(AliasName.Text, out h2);
                    altname = NameRegistry.Files.toName(hash);
                    if (!altname.StartsWith("#")) {
                        AliasHashStatus.Text = "Already defined as '" + altname + "'.";
                        if (altname.FNV() == hash)
                            SaveAlias.IsEnabled = false;
                    } else if (IsHashUsed(hash))
                    {
                        AliasHashStatus.Inlines.Clear();
                        AliasHashStatus.Inlines.Add(AliasHashStatusLink);
                        AliasHashStatusLink.Inlines.Clear();
                        AliasHashStatusLink.Inlines.Add("Used by Spore!");
                    } else {
                        AliasHashStatus.Text = "Not used by Spore.";
                    }
                    if (hash == AliasName.Text.FNV())
                        SaveAlias.IsEnabled = false;
                }
            }
        }


        bool IsHashUsed(UInt32 hash)
        {
            return DirectoryTree.Tree.Search( new SearchSpec("#" + hash.ToString("X8")) );
        }

        private void AliasNameStatusLink_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.Instance.SearchFiles(AliasName.Text.FNV().ToString("X8"));
        }

        private void AliasHashStatusLink_Click(object sender, RoutedEventArgs e)
        {
            UInt32 hash;
            if (UInt32.TryParse(AliasHash.Text, System.Globalization.NumberStyles.AllowHexSpecifier, null, out hash))
                MainWindow.Instance.SearchFiles(hash.ToString("X8"));
        }

        private void SaveName_Click(object sender, RoutedEventArgs e)
        {
            var hash = AliasName.Text.FNV();
            var oldName = NameRegistry.Files.toName(hash);
            NameRegistry.Files.addName( AliasName.Text, hash, true );
            NameRegistry.Files.save();
            rename(new UInt32[] { hash }, new string[] { oldName });
            update();
        }

        private void SaveAlias_Click(object sender, RoutedEventArgs e)
        {
            UInt32 hash;
            if (UInt32.TryParse(AliasHash.Text, System.Globalization.NumberStyles.AllowHexSpecifier, null, out hash) &&
                AliasName.Text.EndsWith("~"))
            {
                var oldName = NameRegistry.Files.toName(hash);
                NameRegistry.Files.addName( AliasName.Text, hash, true );
                NameRegistry.Files.save();
                rename(new UInt32[] { hash }, new string[] { oldName });
                update();
            }
        }

        private void UserControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
                update();
        }

        private void rename(UInt32[] hashes, string[] oldName)
        {
            new PleaseWait(MainWindow.Instance, "Renaming", delegate (PleaseWait progress)
            {
                Renamer.rename(hashes, oldName, progress);
            }).ShowDialog();
        }

        private void GuessHash_TextChanged(object sender, TextChangedEventArgs e)
        {
            GuessHash.Text = AliasHash.Text;

            bool okSoFar, done;
            UInt32 h = parseHash(GuessHash.Text, out okSoFar, out done);

            if (done) guesser.HashCode = h;
            else guesser.HashCode = null;
            GuessHash.Background = done ? LightGreen : okSoFar ? Brushes.White : Brushes.Yellow;
        }

        private void GuessResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var r = (GuessResults.SelectedItem as HashGuesser.Result);
            if (r != null)
                AliasName.Text = r.Text;
        }

    }
}
