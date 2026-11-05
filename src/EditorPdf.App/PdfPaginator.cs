using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EditorPdf.Core;
namespace EditorPdf.App;

public sealed class PdfPaginator(PdfDocument document, Size size, IReadOnlyList<int>? pages = null) : DocumentPaginator, IDocumentPaginatorSource
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
        var page = new FixedPage { Width = PageSize.Width, Height = PageSize.Height };
        page.Children.Add(new Image { Source = bitmap, Width = PageSize.Width, Height = PageSize.Height, Stretch = Stretch.Uniform });
        page.Measure(PageSize);
        page.Arrange(new Rect(PageSize));
        page.UpdateLayout();
        return new DocumentPage(page, PageSize, new Rect(PageSize), new Rect(PageSize));
    }
}
