using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EditorPdf.Core;
namespace EditorPdf.App;

public sealed class PdfPaginator(PdfDocument document, Size size, IReadOnlyList<int>? pages = null, Rect? imageableArea = null, bool grayscale = false) : DocumentPaginator, IDocumentPaginatorSource
{
    public override bool IsPageCountValid => true;
    public override int PageCount => pages?.Count ?? document.Count;
    public override Size PageSize { get; set; } = size;
    public override IDocumentPaginatorSource Source => this;
    public DocumentPaginator DocumentPaginator => this;
    public override DocumentPage GetPage(int pageNumber)
    {
        if (pageNumber < 0 || pageNumber >= PageCount)
            return DocumentPage.Missing;
        var raster = document.Render(pages == null ? pageNumber : pages[pageNumber], Math.Clamp((int)(PageSize.Width * 2), 600, 2400));
        var bitmap = BitmapSource.Create(raster.Width, raster.Height, 96, 96, PixelFormats.Bgra32, null, raster.Pixels, raster.Stride);
        bitmap.Freeze();
        BitmapSource source = grayscale ? new FormatConvertedBitmap(bitmap, PixelFormats.Gray8, null, 0) : bitmap;
        var area = imageableArea ?? new Rect(PageSize);
        var page = new Canvas { Width = PageSize.Width, Height = PageSize.Height, Background = Brushes.White };
        var image = new Image { Source = source, Width = area.Width, Height = area.Height, Stretch = Stretch.Uniform };
        Canvas.SetLeft(image, area.Left);
        Canvas.SetTop(image, area.Top);
        page.Children.Add(image);
        page.Measure(PageSize);
        page.Arrange(new Rect(PageSize));
        page.UpdateLayout();
        return new DocumentPage(page, PageSize, new Rect(PageSize), area);
    }
}
