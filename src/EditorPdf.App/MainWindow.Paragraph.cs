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
    Point? paragraphStart;
    Rectangle? paragraphOutline;

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
        PageImage.Cursor = Cursors.Cross;
        Status.Text = "Arrastra un recuadro alrededor de todo el párrafo · Esc cancela.";
    }

    void CancelParagraphSelection()
    {
        paragraphArmed = false;
        paragraphStart = null;
        paragraphOutline = null;
        PageImage.ReleaseMouseCapture();
        PageImage.Cursor = Cursors.Arrow;
        Overlay.Children.Clear();
    }

    void StartParagraphSelection(Point point)
    {
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
        var area = new Rect(start, point);
        CancelParagraphSelection();
        if (area.Width < 8 || area.Height < 8) { Status.Text = "Arrastra alrededor de las líneas del párrafo."; return; }
        var group = ParagraphObjectsIn(area);
        if (group.Length == 0) { Status.Text = "No hay texto compatible dentro del recuadro."; return; }
        bool mixed = group.Select(o => (o.Font, o.Size, o.FillRgb)).Distinct().Count() > 1;
        var info = Pdf.Info(page);
        float width = Math.Min(info.Width - group.Min(o => o.Left) - 10, Math.Max(200, group.Max(o => o.Right) - group.Min(o => o.Left)));
        var dialog = new ParagraphEditorDialog(this, ParagraphSelection.Compose(group), width, group.Length, mixed);
        Mouse.OverrideCursor = null;
        if (dialog.ShowDialog() != true) return;
        var indices = group.Select(o => o.Index).ToArray();
        Change(d => d.ReplaceParagraph(page, indices, dialog.ParagraphText, dialog.ParagraphWidth, dialog.FontChoice));
        SelectObject(indices.Min());
    }
}
