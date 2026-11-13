using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EditorPdf.Core;
namespace EditorPdf.App;

public sealed class PdfPaginator(PdfDocument document, Size size, IReadOnlyList<int>? pages = null, Rect? imageableArea = null, bool grayscale = false, int columns = 1, int rows = 1, bool downFirst = false, bool borders = false) : DocumentPaginator, IDocumentPaginatorSource
{
    public override bool IsPageCountValid => true;
    int PagesPerSheet => columns * rows;
    int DocumentPageCount => pages?.Count ?? document.Count;
    public override int PageCount => (DocumentPageCount + PagesPerSheet - 1) / PagesPerSheet;
    public override Size PageSize { get; set; } = size;
    public override IDocumentPaginatorSource Source => this;
    public DocumentPaginator DocumentPaginator => this;
    public override DocumentPage GetPage(int pageNumber)
    {
        if (pageNumber < 0 || pageNumber >= PageCount)
            return DocumentPage.Missing;
        var area = imageableArea ?? new Rect(PageSize);
        var sheet = new Canvas { Width = PageSize.Width, Height = PageSize.Height, Background = Brushes.White };
        double cellWidth = area.Width / columns, cellHeight = area.Height / rows;
        for (int cell = 0; cell < PagesPerSheet; cell++)
        {
            int selectedIndex = pageNumber * PagesPerSheet + cell;
            if (selectedIndex >= DocumentPageCount) break;
            int column = downFirst ? cell / rows : cell % columns;
            int row = downFirst ? cell % rows : cell / columns;
            var slot = new Rect(area.Left + column * cellWidth, area.Top + row * cellHeight, cellWidth, cellHeight);
            if (PagesPerSheet > 1) slot.Inflate(-Math.Min(4, cellWidth / 8), -Math.Min(4, cellHeight / 8));
            AddPage(sheet, pages == null ? selectedIndex : pages[selectedIndex], slot);
        }
        sheet.Measure(PageSize);
        sheet.Arrange(new Rect(PageSize));
        sheet.UpdateLayout();
        return new DocumentPage(sheet, PageSize, new Rect(PageSize), area);
    }

    void AddPage(Canvas sheet, int pageIndex, Rect slot)
    {
        var raster = document.Render(pageIndex, Math.Clamp((int)(slot.Width * 2), 600, 2400));
        var bitmap = BitmapSource.Create(raster.Width, raster.Height, 96, 96, PixelFormats.Bgra32, null, raster.Pixels, raster.Stride);
        bitmap.Freeze();
        BitmapSource source = grayscale ? new FormatConvertedBitmap(bitmap, PixelFormats.Gray8, null, 0) : bitmap;
        var tile = new Border
        {
            Width = slot.Width, Height = slot.Height,
            BorderBrush = Brushes.DimGray, BorderThickness = new Thickness(borders ? .7 : 0),
            Child = new Image { Source = source, Stretch = Stretch.Uniform }
        };
        Canvas.SetLeft(tile, slot.Left);
        Canvas.SetTop(tile, slot.Top);
        sheet.Children.Add(tile);
    }
}
