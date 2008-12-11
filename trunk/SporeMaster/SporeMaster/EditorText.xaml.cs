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
    /// Interaction logic for EditorText.xaml
    /// </summary>
    public partial class EditorText : UserControl, IEditor
    {
        string editing;
        int editorSaveUndoCount = 0;

        public EditorText()
        {
            InitializeComponent();

            if (System.ComponentModel.LicenseManager.UsageMode != System.ComponentModel.LicenseUsageMode.Designtime)
            {
                this.Width = double.NaN; ;
                this.Height = double.NaN; ;
                Editor.Document.HighlightingStrategy = ICSharpCode.TextEditor.Document.HighlightingStrategyFactory.CreateHighlightingStrategyForFile("foo.xml");
                Editor.IndentStyle = ICSharpCode.TextEditor.Document.IndentStyle.Smart;
                Editor.TabIndent = 2;
                Editor.Document.TextEditorProperties.IndentationSize = 2;
                Editor.ConvertTabsToSpaces = true;
            }
        }

        public void Open(string path, bool read_only)
        {
            if (path.EndsWith("\\")) path = null;

            if (editing != path)
            {
                Save();
                editing = null;
                Editor.Document.UndoStack.ClearAll();
                editorSaveUndoCount = Editor.Document.UndoStack.UndoItemCount;

                if (path != null)
                {
                    try
                    {
                        Editor.Document.TextContent = File.ReadAllText(path);
                    }
                    catch (Exception exc)
                    {
                        Editor.Document.TextContent = "Unable to load file: " + path + ".\n\n" + exc.ToString();
                        Editor.IsReadOnly = true;
                        Editor.BackColor = System.Drawing.Color.Yellow;
                        Editor.Refresh();
                        return;
                    }
                    if (!read_only) editing = path;
                }
                else
                    Editor.Document.TextContent = "";

                Editor.Refresh();
            }
            Editor.IsReadOnly = read_only;
            Editor.BackColor = read_only ? System.Drawing.Color.LightGray : System.Drawing.Color.White;
        }

        public void Search(string search)
        {
            Editor.Document.MarkerStrategy.RemoveAll(new Predicate<ICSharpCode.TextEditor.Document.TextMarker>(delegate { return true; }));
            if (search != "" && Editor.Document.TextLength != 0)
            {
                bool anyvisible = false;
                string lower = Editor.Document.TextContent.ToLowerInvariant();
                int pos = 0;
                int firstmatch_row = -1, firstmatch_column = -1;
                var view = Editor.ActiveTextAreaControl.TextArea.TextView;
                int topRow = view.FirstVisibleLine;
                int bottomRow = topRow + view.VisibleLineCount;
                int leftColumn = Editor.ActiveTextAreaControl.HScrollBar.Value - Editor.ActiveTextAreaControl.HScrollBar.Minimum;
                int rightColumn = leftColumn + view.VisibleColumnCount;

                while ((pos = lower.IndexOf(search, pos)) != -1)
                {
                    Editor.Document.MarkerStrategy.AddMarker(new ICSharpCode.TextEditor.Document.TextMarker(
                        pos, search.Length, ICSharpCode.TextEditor.Document.TextMarkerType.SolidBlock,
                        System.Drawing.Color.Red, System.Drawing.Color.White));
                    if (!anyvisible)
                    {
                        int line = Editor.Document.GetLineNumberForOffset(pos);
                        int col = pos - Editor.Document.GetLineSegmentForOffset(pos).Offset;
                        if (firstmatch_row < 0) { firstmatch_row = line; firstmatch_column = col; }
                        anyvisible = (line >= topRow && line < bottomRow && col >= leftColumn && col < rightColumn);
                    }
                    pos += search.Length;
                }
                if (!anyvisible && firstmatch_row >= 0)
                    Editor.ActiveTextAreaControl.ScrollTo(firstmatch_row, firstmatch_column);
            }
            Editor.Refresh();
        }

        public void Save()
        {
            if (editing != null && Editor.Document.UndoStack.UndoItemCount != editorSaveUndoCount)
            {
                editorSaveUndoCount = Editor.Document.UndoStack.UndoItemCount;
                File.WriteAllText(editing, Editor.Document.TextContent);
            }
        }

        public string GetSelectedText()
        {
            if (!Editor.ActiveTextAreaControl.SelectionManager.HasSomethingSelected)
                return null;
            return Editor.ActiveTextAreaControl.SelectionManager.SelectedText;
        }
    }
}
