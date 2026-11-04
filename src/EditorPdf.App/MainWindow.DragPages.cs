using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
namespace EditorPdf.App;

public partial class MainWindow
{
    const string PageDragFormat = "EditorPDF.Page";
    sealed record PageDrag(string Document, int Page);
    static PageDrag? ReadPageDrag(IDataObject data)
    {
        if (data.GetData(PageDragFormat) is not string value)
            return null;
        var parts = value.Split(':');
        return parts.Length == 2 && Guid.TryParseExact(parts[0], "N", out _) && int.TryParse(parts[1], out int page) ? new PageDrag(parts[0], page) : null;
    }
    Point? thumbnailStart; int thumbnailFrom; long lastDragScroll;
    AdornerLayer? dropLayer; InsertionLine? dropLine;
    void ThumbnailMouseDown(object sender, MouseButtonEventArgs e)
    {
        var item = ItemsControl.ContainerFromElement(PagesList, e.OriginalSource as DependencyObject) as ListBoxItem;
        if (item?.DataContext is not Thumb thumb)
        {
            thumbnailStart = null;
            return;
        }
        thumbnailStart = e.GetPosition(PagesList);
        thumbnailFrom = thumb.Index;
    }
    void ThumbnailMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            thumbnailStart = null;
            return;
        }
        if (thumbnailStart is not { } start || activeDocument == null)
            return;
        var point = e.GetPosition(PagesList);
        if (Math.Abs(point.X - start.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(point.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;
        thumbnailStart = null;
        try
        {
            DragDrop.DoDragDrop(PagesList, new DataObject(PageDragFormat, activeDocument.Id + ":" + thumbnailFrom.ToString(System.Globalization.CultureInfo.InvariantCulture)), DragDropEffects.Move);
        }
        finally { ClearInsertionLine(); }
    }
    int InsertionAt(DragEventArgs e, bool show)
    {
        var item = ItemsControl.ContainerFromElement(PagesList, e.OriginalSource as DependencyObject) as ListBoxItem;
        if (item?.DataContext is not Thumb thumb)
        {
            if (show)
                ClearInsertionLine();
            return Pdf.Count;
        }
        bool after = e.GetPosition(item).Y >= item.ActualHeight / 2;
        if (show)
        {
            ClearInsertionLine();
            dropLayer = AdornerLayer.GetAdornerLayer(item);
            if (dropLayer != null)
            {
                dropLine = new InsertionLine(item, after);
                dropLayer.Add(dropLine);
            }
        }
        return thumb.Index + (after ? 1 : 0);
    }
    void ThumbnailDragOver(object sender, DragEventArgs e)
    {
        if (ReadPageDrag(e.Data) is not { } drag)
            return;
        e.Handled = true;
        if (activeDocument?.Id != drag.Document || Pdf.EditBlock != null)
        {
            e.Effects = DragDropEffects.None;
            ClearInsertionLine();
            return;
        }
        e.Effects = DragDropEffects.Move;
        InsertionAt(e, true);
        if (Environment.TickCount64 - lastDragScroll > 80)
        {
            lastDragScroll = Environment.TickCount64;
            var scroll = FindScroll(PagesList);
            double y = e.GetPosition(PagesList).Y;
            if (y < 25)
                scroll?.ScrollToVerticalOffset(scroll.VerticalOffset - 20);
            else if (y > PagesList.ActualHeight - 25)
                scroll?.ScrollToVerticalOffset(scroll.VerticalOffset + 20);
        }
    }
    static ScrollViewer? FindScroll(DependencyObject parent)
    {
        if (parent is ScrollViewer viewer)
            return viewer;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            if (FindScroll(VisualTreeHelper.GetChild(parent, i)) is { } result)
                return result;
        return null;
    }
    void ThumbnailDrop(object sender, DragEventArgs e)
    {
        if (ReadPageDrag(e.Data) is not { } drag)
            return;
        e.Handled = true;
        ClearInsertionLine();
        if (activeDocument?.Id != drag.Document)
            return;
        int insertion = InsertionAt(e, false);
        Run(() => MovePageToInsertion(drag.Page, insertion));
    }
    public void MovePageToInsertion(int from, int insertion)
    {
        if (from < 0 || from >= Pdf.Count || insertion < 0 || insertion > Pdf.Count)
            throw new ArgumentOutOfRangeException(nameof(insertion));
        int to = insertion > from ? insertion - 1 : insertion;
        if (to == from)
            return;
        Change(d => d.Move(from, to), to);
    }
    void ThumbnailDragLeave(object sender, DragEventArgs e) => ClearInsertionLine();
    void ClearInsertionLine()
    {
        if (dropLine != null)
            dropLayer?.Remove(dropLine);
        dropLine = null;
        dropLayer = null;
    }
    sealed class InsertionLine : Adorner
    {
        readonly bool after;
        public InsertionLine(UIElement element, bool after) : base(element) { this.after = after; IsHitTestVisible = false; }
        protected override void OnRender(DrawingContext context)
        {
            double y = after ? AdornedElement.RenderSize.Height - 1 : 1;
            context.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(35, 94, 188)), 3), new Point(3, y), new Point(AdornedElement.RenderSize.Width - 3, y));
        }
    }
}
