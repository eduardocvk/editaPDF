using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using EditorPdf.Core;

namespace EditorPdf.App;

public partial class MainWindow
{
    bool paragraphArmed;
    bool paragraphPassive;
    Point? paragraphStart;
    Rectangle? paragraphOutline;
    PdfObject[]? areaSelection;

    void ArmParagraphSelection()
    {
        RequireObjectEditing();
        FinishInlineText();
        CancelCanvasGesture();
        EditMode.IsSelected = true;
        addTextArmed = false;
        ObjectsList.SelectedItem = null;
        Overlay.Children.Clear();
        paragraphArmed = true;
        paragraphPassive = false;
        PageImage.Cursor = Cursors.Cross;
        Status.Text = Ui.T("Arrastra un recuadro alrededor de todo el párrafo · Esc cancela.");
    }

    void CancelParagraphSelection()
    {
        paragraphArmed = false;
        paragraphPassive = false;
        paragraphStart = null;
        paragraphOutline = null;
        areaSelection = null;
        PageImage.ReleaseMouseCapture();
        PageImage.Cursor = Cursors.Arrow;
        Overlay.Children.Clear();
    }

    void StartParagraphSelection(Point point, bool passive = false)
    {
        paragraphArmed = true;
        paragraphPassive = passive;
        paragraphStart = point;
        paragraphOutline = new Rectangle { Stroke = Brushes.RoyalBlue, StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 4, 3 }, Fill = new SolidColorBrush(Color.FromArgb(32, 65, 105, 225)), IsHitTestVisible = false };
        Overlay.Children.Clear();
        Overlay.Children.Add(paragraphOutline);
        PageImage.CaptureMouse();
        UpdateParagraphSelection(point);
    }

    void UpdateParagraphSelection(Point point)
    {
        if (paragraphStart is not { } start || paragraphOutline == null) return;
        double x = Math.Clamp(point.X, 0, PageImage.Width), y = Math.Clamp(point.Y, 0, PageImage.Height);
        Canvas.SetLeft(paragraphOutline, Math.Min(start.X, x));
        Canvas.SetTop(paragraphOutline, Math.Min(start.Y, y));
        paragraphOutline.Width = Math.Abs(x - start.X);
        paragraphOutline.Height = Math.Abs(y - start.Y);
    }

    PdfObject[] ParagraphObjectsIn(Rect deviceRect)
    {
        var a = Pdf.ToPage(page, (int)PageImage.Width, (int)PageImage.Height, (int)deviceRect.Left, (int)deviceRect.Top);
        var b = Pdf.ToPage(page, (int)PageImage.Width, (int)PageImage.Height, (int)deviceRect.Right, (int)deviceRect.Bottom);
        return ParagraphSelection.InArea(Pdf.Objects(page), (float)Math.Min(a.X, b.X), (float)Math.Min(a.Y, b.Y), (float)Math.Max(a.X, b.X), (float)Math.Max(a.Y, b.Y));
    }

    void CompleteParagraphSelection(Point point)
    {
        if (paragraphStart is not { } start) return;
        var area = new Rect(start, new Point(Math.Clamp(point.X, 0, PageImage.Width), Math.Clamp(point.Y, 0, PageImage.Height)));
        bool passive = paragraphPassive;
        CancelParagraphSelection();
        if (area.Width < 8 || area.Height < 8) { Status.Text = Ui.T("Selecciona el texto dibujando un rectángulo alrededor."); return; }
        var group = ParagraphObjectsIn(area);
        if (passive)
        {
            var enclosed = Pdf.Objects(page).Where(obj => area.Contains(DeviceBounds(obj))).Select(obj => obj.Index).ToArray();
            areaSelection = group.Where(obj => enclosed.Contains(obj.Index)).ToArray();
            RestoreObjectSelection(enclosed);
            if (enclosed.Length != 1 || areaSelection.Length == 0) return;
            paragraphOutline = new Rectangle { Width = area.Width, Height = area.Height, Stroke = Brushes.RoyalBlue, StrokeThickness = 1.5, StrokeDashArray = new DoubleCollection { 4, 3 }, IsHitTestVisible = false };
            Canvas.SetLeft(paragraphOutline, area.Left);
            Canvas.SetTop(paragraphOutline, area.Top);
            Overlay.Children.Add(paragraphOutline);
            var edit = new ToolButton { Icon = "Paragraph", IconOnly = true, Content = Ui.T("Editar texto seleccionado"), ToolTip = Ui.T("Editar texto seleccionado"), Padding = new Thickness(5), MinWidth = 29, MinHeight = 29 };
            edit.Click += (_, _) => Run(EditAreaSelection);
            Canvas.SetLeft(edit, Math.Min(area.Right + 5, Overlay.Width - 34));
            Canvas.SetTop(edit, Math.Clamp(area.Top, 0, Overlay.Height - 34));
            Overlay.Children.Add(edit);
            Status.Text = (Ui.Current.Language=="en" ? $"{group.Length} selected fragments. Use the button next to the rectangle to edit." : $"{group.Length} fragmentos seleccionados. Pulsa el botón junto al recuadro para editarlos.");
            return;
        }
        if (group.Length == 0) { Status.Text = Ui.T("No hay texto compatible dentro del recuadro."); return; }
        EditParagraphObjects(group);
    }

    void EditAreaSelection()
    {
        if (areaSelection is not { Length: > 0 } group) return;
        CancelParagraphSelection();
        EditParagraphObjects(group);
    }

    void EditParagraphObjects(PdfObject[] group)
    {
        bool mixed = group.Select(o => (o.Font, o.Size, o.FillRgb)).Distinct().Count() > 1;
        var info = Pdf.Info(page);
        float width = Math.Min(info.Width - group.Min(o => o.Left) - 10, Math.Max(30, group.Max(o => o.Right) - group.Min(o => o.Left)));
        var dialog = new ParagraphEditorDialog(this, ParagraphSelection.Compose(group), width, group.Length, mixed, width);
        Mouse.OverrideCursor = null;
        if (dialog.ShowDialog() != true) return;
        var indices = group.Select(o => o.Index).ToArray();
        Change(d => d.ReplaceParagraph(page, indices, dialog.ParagraphText, dialog.ParagraphWidth, dialog.FontChoice, alignment: dialog.Alignment));
        SelectObject(indices.Min());
    }
}
