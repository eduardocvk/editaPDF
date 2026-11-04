using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EditorPdf.Core;
using EditorPdf.App;
static class UiTests
{
    [STAThread]
    static void Main(string[] args)
    {
        string folder = Path.GetFullPath(args[0]);
        Directory.CreateDirectory(folder);
        var app = new App();
        app.InitializeComponent();
        var externalLinks = new List<Uri>();
        var window = new MainWindow(Path.Combine(folder, "recovery"), false, uri => externalLinks.Add(uri));
        window.OpenPaths([Path.GetFullPath(args[1])]);
        var session = (Session)typeof(MainWindow).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
        var root = (FrameworkElement)window.Content;
        window.Content = null;
        var host = new Border { Background = new SolidColorBrush(Color.FromRgb(243, 245, 248)), Child = root };
        void Capture(string name, int width, int height, double scale = 1)
        {
            host.Measure(new Size(width, height));
            host.Arrange(new Rect(0, 0, width, height));
            host.UpdateLayout();
            var image = new RenderTargetBitmap((int)(width * scale), (int)(height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            image.Render(host);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using var f = File.Create(Path.Combine(folder, name + ".png"));
            encoder.Save(f);
            Console.WriteLine("PASS WPF render " + name);
        }
        Capture("organizar-1280", 1280, 780);
        Capture("organizar-920-150pct", 920, 560, 1.5);
        ((Button)window.FindName("UndoButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        ((RadioButton)window.FindName("EditMode")).IsChecked = true;
        var list = (ListBox)window.FindName("ObjectsList");
        list.SelectedIndex = 1;
        Capture("editar-1280", 1280, 780);
        ((TextBox)window.FindName("TextValue")).Text = "Edicion visual comprobada";
        typeof(MainWindow).GetMethod("ApplyTextClick", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, [window, new RoutedEventArgs()]);
        if (!session.Document.Text(0).Contains("Edicion visual comprobada"))
            throw new Exception("La edicion desde controles no se aplico");
        if (((TextBox)window.FindName("TextValue")).Text.Length != 0)
            throw new Exception("El panel conserva propiedades de un objeto no seleccionado");
        ((Button)window.FindName("UndoButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (!session.Document.Text(0).Contains("Texto original compatible 123"))
            throw new Exception("Deshacer de UI fallo");
        ((Button)window.FindName("RedoButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (!session.Document.Text(0).Contains("Edicion visual comprobada"))
            throw new Exception("Rehacer de UI fallo");
        session.SaveNew(Path.Combine(folder, "ui-editado.pdf"));
        Capture("resultado-920-200pct", 920, 560, 2);
        var paginator = new PdfPaginator(session.Document, new Size(793, 1122));
        if (paginator.PageCount != 2 || paginator.GetPage(1) == System.Windows.Documents.DocumentPage.Missing)
            throw new Exception("Paginacion de impresion fallo");
        if (!LocalPrinting.IsLocalPort("USB001") || !LocalPrinting.IsLocalPort("PORTPROMPT:") || LocalPrinting.IsLocalPort("192.0.2.1") || LocalPrinting.IsLocalPort(@"\\server\printer"))
            throw new Exception("Filtro de impresoras locales fallo");
        Console.WriteLine("PASS preparacion de impresion por paginas y filtro local");
        if (externalLinks.Count != 0)
            throw new Exception("Se intentó abrir un enlace externo sin pulsación");
        var banner = (Button)window.FindName("FirmaPdfBanner");
        if (!banner.IsEnabled)
            throw new Exception("El banner FirmaPDF no está habilitado");
        banner.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (externalLinks.Count != 1 || externalLinks[0].AbsoluteUri != "https://apps.microsoft.com/detail/9P5ZK5W4J9ZJ")
            throw new Exception("El banner no abre exclusivamente la dirección oficial fija");
        Console.WriteLine("PASS banner FirmaPDF: pulsacion explicita y URL fija sin datos; apertura de navegador sustituida solo en prueba");
        session.Dispose();
        Console.WriteLine("PASS UI texto original, deshacer, rehacer y guardado");
    }
}
