using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using EditorPdf.Core;

namespace EditorPdf.App;

public partial class MainWindow
{
    PdfObject[] ObjectSelection() => ObjectsList.SelectedItems.Cast<ObjectRow>().Select(row => row.Value).OrderBy(obj => obj.Index).ToArray();

    Rect SelectionBounds(IEnumerable<PdfObject> objects)
    {
        Rect bounds = Rect.Empty;
        foreach (var obj in objects) bounds.Union(DeviceBounds(obj));
        return bounds;
    }

    void RestoreObjectSelection(int[] indices)
    {
        ObjectsList.SelectedItems.Clear();
        foreach (var row in ObjectsList.Items.Cast<ObjectRow>())
            if (indices.Contains(row.Value.Index)) ObjectsList.SelectedItems.Add(row);
    }

    void DrawGroupSelection()
    {
        resizeHandles.Clear();
        selectionOutline = new Rectangle { Stroke = Brushes.RoyalBlue, StrokeThickness = 1.5, StrokeDashArray = new DoubleCollection { 3, 2 }, IsHitTestVisible = false };
        Overlay.Children.Add(selectionOutline);
        textToolbar = null;
        if (Pdf.ObjectEditBlock == null)
        {
            var actions = new StackPanel { Orientation = Orientation.Horizontal };
            foreach (var (label, icon, action) in new (string, string, Action)[]
            {
                ("Duplicar selección", "Duplicate", DuplicateSelectedObjects),
                ("Eliminar selección", "Delete", DeleteSelectedObjects)
            })
            {
                var button = new ToolButton { Icon = icon, IconOnly = true, Content = label, ToolTip = Ui.T(label) };
                button.Click += (_, _) => Run(action);
                actions.Children.Add(button);
            }
            textToolbar = new Border { Background = Brushes.White, BorderBrush = Brushes.RoyalBlue, BorderThickness = new Thickness(1), Child = actions };
            Overlay.Children.Add(textToolbar);
        }
        PositionSelection(SelectionBounds(ObjectSelection()));
    }

    void DeleteSelectedObjects()
    {
        var indices = ObjectSelection().Select(obj => obj.Index).OrderDescending().ToArray();
        if (indices.Length == 0) return;
        Change(document =>
        {
            foreach (int index in indices) document.RemoveObject(page, index);
        });
    }

    void DuplicateSelectedObjects()
    {
        var selected = ObjectSelection();
        if (selected.Length == 0) return;
        int[] copies = [];
        Change(document =>
        {
            var added = new List<int>();
            foreach (var obj in selected)
            {
                if (obj.Type == 3) document.DuplicateImage(page, obj.Index);
                else document.DuplicateText(page, obj.Index);
                added.Add(document.Objects(page).Last().Index);
            }
            copies = added.ToArray();
        });
        RestoreObjectSelection(copies);
    }
}
