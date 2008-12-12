using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;

namespace SporeMaster
{
    class Renamer
    {
        // Renamer.rename() searches through DirectoryTree.Tree, renaming and rebuilding files
        //   to reflect a change in the NameRegistry.
        public static void rename(UInt32[] hashes, string[] oldNames, PleaseWait progress)
        {
            // TODO: do something with progress bar
            var nameBytes = from n in oldNames select Encoding.UTF8.GetBytes(n.ToLowerInvariant());
            DirectoryTree.Tree.Search( SearchSpec.all );
            var renamer = new Renamer
            {
                oldNames = nameBytes.ToArray(),
                hashes = hashes,
                leftPath = MainWindow.Instance.LeftPath + "\\",
                rightPath = MainWindow.Instance.RightPath + "\\"
            };

            renamer.renameTree( DirectoryTree.Tree );

            DirectoryTree.Tree.Search(SearchSpec.all);

            if (renamer.errors.Count != 0)
            {
                string s = "There were errors updating the following files:\n";
                foreach (var e in renamer.errors)
                    s += "    " + e + "\n";
                MessageBox.Show(s, "Warning");
            }
        }

        byte[][] oldNames;
        UInt32[] hashes;
        string leftPath, rightPath;
        List<String> errors = new List<String>();

        void renameTree(DirectoryTree node)
        {
            foreach (var c in node.Children)
                renameTree(c as DirectoryTree);

            if (node.FullName.EndsWith(".prop.xml"))
            {
                if (node.LeftPresent)
                    foreach (var oldName in oldNames)
                        if (node.LeftContains(oldName))
                        {
                            if ((new RebuildPropertyFile(leftPath + node.Path)).Failed)
                                errors.Add(leftPath + node.Path);
                            break;
                        }
                if (node.RightPresent)
                    foreach (var oldName in oldNames)
                        if (node.RightContains(oldName))
                        {
                            if ((new RebuildPropertyFile(rightPath + node.Path)).Failed)
                                errors.Add(rightPath + node.Path);
                            break;
                        }
            }

            try
            {
                var hash = NameRegistry.Files.toHash(node.BaseName);
                if (hashes.Contains(hash))
                {
                    string newPath = node.Parent.Path + "\\" +
                                     NameRegistry.Files.toName(hash);
                    string ext = node.FileType;
                    if (ext != "") newPath += "." + ext;
                    if (newPath != node.Path)
                    {
                        if (node.LeftPresent)
                            System.IO.Directory.Move(leftPath + node.Path, leftPath + newPath);
                        if (node.RightPresent)
                            System.IO.Directory.Move(rightPath + node.Path, rightPath + newPath);
                    }
                }
            }
            catch (Exception)
            {
                if (node.LeftPresent) errors.Add(leftPath + node.Path);
                if (node.RightPresent) errors.Add(rightPath + node.Path);
            }
        }
    }
}
