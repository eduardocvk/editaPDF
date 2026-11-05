using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EditorPdf.Core;
namespace EditorPdf.App;

public sealed class PrintPreviewDialog : Window
{
    public PrintPreviewDialog(Window owner, PdfDocument document)
    {
        if(owner.IsVisible) Owner = owner; Title = Ui.T("Vista previa de impresión"); Width = 880; Height = 760;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new DockPanel();
        var tools = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8) };
        var image = new Image { Stretch = Stretch.Uniform, Margin = new Thickness(18) };
        var label = new TextBlock { Margin = new Thickness(12,0,12,0) };
        int page = 0;
        void Render() { var r = document.Render(page, 1100); var bitmap = BitmapSource.Create(r.Width,r.Height,96,96,PixelFormats.Bgra32,null,r.Pixels,r.Stride); bitmap.Freeze(); image.Source=bitmap; label.Text=$"{page+1} / {document.Count}"; }
        var previous = new Button { Content = "‹", ToolTip = Ui.T("Página anterior") };
        var next = new Button { Content = "›", ToolTip = Ui.T("Página siguiente") };
        previous.Click += (_,_) => { if(page>0) { page--; Render(); } };
        next.Click += (_,_) => { if(page<document.Count-1) { page++; Render(); } };
        var print = new Button { Content = Ui.T("Imprimir…"), IsDefault = true };
        print.Click += (_,_) => DialogResult = true;
        tools.Children.Add(previous); tools.Children.Add(label); tools.Children.Add(next); tools.Children.Add(print);
        DockPanel.SetDock(tools,Dock.Top); root.Children.Add(tools);
        root.Children.Add(new Border { Child=image, Background=Brushes.LightGray }); Content=root; Render();
    }
}
