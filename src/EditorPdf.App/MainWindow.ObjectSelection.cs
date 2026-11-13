using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using EditorPdf.Core;

namespace EditorPdf.App;

public partial class MainWindow
{
    byte[]? copiedObjects;

    void CopySelectedObjects(bool cut)
    {
        var indices = ObjectSelection().Select(obj => obj.Index).ToArray();
        if (indices.Length == 0) return;
        var copy = Pdf.CopyObjects(page, indices);
        if (cut) DeleteSelectedObjects();
        copiedObjects = copy;
        Status.Text = Ui.T(cut ? "Objetos cortados. Elige una página y pega." : "Objetos copiados.");
    }

    void PasteSelectedObjects()
    {
        if (copiedObjects is not { } copy || Pdf.Count == 0) return;
        int[] added = [];
        Change(document => added = document.PasteObjects(page, copy));
        RestoreObjectSelection(added);
    }

    void MoveSelectionToPage(int destination)
    {
        if (destination == page) return;
        int source = page;
        int[] indices = ObjectSelection().Select(obj => obj.Index).ToArray();
        if (indices.Length == 0) return;
        byte[] copy = Pdf.CopyObjects(source, indices);
        int[] added = [];
        Change(document =>
        {
            added = document.PasteObjects(destination, copy);
            foreach (int index in indices.OrderDescending()) document.RemoveObject(source, index);
        }, destination);
        RestoreObjectSelection(added);
    }

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
        var selectedIndices = ObjectSelection().Select(obj => obj.Index).ToHashSet();
        var paragraph = areaSelection?.Where(obj => selectedIndices.Contains(obj.Index)).ToArray();
        if (paragraph?.Length > 0)
        {
            var edit = new ToolButton { Icon = "Paragraph", IconOnly = true, Content = "Editar texto seleccionado", ToolTip = Ui.T("Editar texto seleccionado") };
            edit.Click += (_, _) => Run(() => EditParagraphObjects(paragraph));
            Canvas.SetLeft(edit, Math.Clamp(SelectionBounds(ObjectSelection()).Right + 5, 0, Math.Max(0, Overlay.Width - 34)));
            Canvas.SetTop(edit, Math.Clamp(SelectionBounds(ObjectSelection()).Top, 0, Math.Max(0, Overlay.Height - 34)));
            Overlay.Children.Add(edit);
        }
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
