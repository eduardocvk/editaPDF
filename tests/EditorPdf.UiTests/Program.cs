using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EditorPdf.Core;
using EditorPdf.App;
static class UiTests
{
    [STAThread]
    static void Main(string[] args)
    {
        if(args.FirstOrDefault()=="--optimize-file")
        {
            var original=File.ReadAllBytes(args[1]);
            using var source=new PdfDocument(original);
            var result=PdfImageOptimizer.Optimize(original,image=>ReduceSizeDialog.Encode(image,45));
            using var copy=new PdfDocument(result.Bytes); ReduceSizeDialog.Validate(source,copy); copy.SaveNew(args[2]);
            Console.WriteLine($"PASS private local document: {source.Count} pages, {result.Reduced}/{result.Images} images reduced; {original.Length} -> {new FileInfo(args[2]).Length} bytes");
            return;
        }
        if(args.FirstOrDefault()=="--reduction")
        {
            var reductionApp=new Application();
            reductionApp.Resources=(ResourceDictionary)Application.LoadComponent(new Uri("/EditorPDF;component/Theme.xaml",UriKind.Relative));
            Directory.CreateDirectory(args[1]); ReductionChecks.Run(args[1],args[2]); return;
        }
        if(args.FirstOrDefault()=="--diagnose-print")
        {
            var printingApp=new Application();
            printingApp.Resources=(ResourceDictionary)Application.LoadComponent(new Uri("/EditorPDF;component/Theme.xaml",UriKind.Relative));
            using var sample=new PdfDocument(); sample.Blank(0); sample.AddText(0,"Print settings test",16,40,700);
            var owner=new Window(); var printing=new PrintPreviewDialog(owner,sample);
            Console.WriteLine($"Printers={printing.PrinterCount}; CanPrint={printing.CanPrint}; Sheet={printing.SheetSize.Width:0}x{printing.SheetSize.Height:0}; Status={printing.StatusMessage}");
            Console.WriteLine(printing.ValidationDetails);
            printing.SetPreviewOptions("1",true,true,2);
            Console.WriteLine($"Landscape: CanPrint={printing.CanPrint}; Sheet={printing.SheetSize.Width:0}x{printing.SheetSize.Height:0}; Status={printing.StatusMessage}; {printing.ValidationDetails}");
            var printers=(ComboBox)typeof(PrintPreviewDialog).GetField("printers",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(printing)!;
            var pdfPrinter=printers.Items.Cast<System.Printing.PrintQueue>().FirstOrDefault(q=>q.Name.Contains("Microsoft Print to PDF",StringComparison.OrdinalIgnoreCase));
            if(pdfPrinter!=null)
            {
                printers.SelectedItem=pdfPrinter;
                printing.SetPreviewOptions("1",false,false);
                Console.WriteLine($"PDF printer: CanPrint={printing.CanPrint}; Status={printing.StatusMessage}; {printing.ValidationDetails}");
            }
            printing.Close(); owner.Close();
            return;
        }
        if (args.FirstOrDefault() == "--diagnose-ui-import")
        {
            var diagnosticApp = new Application();
            diagnosticApp.Resources = (ResourceDictionary)Application.LoadComponent(new Uri("/EditorPDF;component/Theme.xaml", UriKind.Relative));
            var diagnosticWindow = new MainWindow(args[3], false, _ => { }, ex => throw ex);
            try
            {
                diagnosticWindow.OpenDocuments([args[1]]);
                diagnosticWindow.AppendPaths([args[2]]);
                Console.WriteLine("PASS unión con render y flujo WPF");
            }
            catch (Exception ex) { Console.WriteLine(ex.Message); Environment.ExitCode = 1; }
            typeof(MainWindow).GetMethod("DisposeWorkspace", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(diagnosticWindow, null);
            return;
        }
        if (args.Length == 0)
            args = ["--interactive", Path.Combine(AppContext.BaseDirectory, "preview-recovery"), Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "structure-probe-02", "fixtures", "anotaciones.pdf"))];
        if (args.FirstOrDefault() == "--interactive")
        {
            var preview = new Application();
            preview.Resources = (ResourceDictionary)Application.LoadComponent(new Uri("/EditorPDF;component/Theme.xaml", UriKind.Relative));
            var interactive = new MainWindow(Path.GetFullPath(args[1]), false);
            interactive.OpenDocuments(args.Skip(2).Select(Path.GetFullPath).ToArray());
            preview.Run(interactive);
            return;
        }
        string folder = Path.GetFullPath(args[0]);
        Ui.Current.SetLanguage("es", false);
        Directory.CreateDirectory(folder);
        var app = new Application();
        app.Resources = (ResourceDictionary)Application.LoadComponent(new Uri("/EditorPDF;component/Theme.xaml", UriKind.Relative));
        var externalLinks = new List<Uri>();
        var window = new MainWindow(Path.Combine(folder, "recovery"), false, uri => externalLinks.Add(uri), ex => throw new Exception("Error UI", ex));
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
        Capture("paginas-1280", 1280, 780);
        Capture("paginas-920-150pct", 920, 560, 1.5);
        var pageList = (ListBox)window.FindName("PagesList");
        var documentScroll = (ScrollViewer)window.FindName("DocumentScroll");
        documentScroll.ScrollToBottom();
        documentScroll.UpdateLayout();
        var wheel = new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, -120) { RoutedEvent = UIElement.PreviewMouseWheelEvent };
        typeof(MainWindow).GetMethod("DocumentMouseWheel", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, [documentScroll, wheel]);
        if (pageList.SelectedIndex != 1 || !wheel.Handled)
            throw new Exception("La rueda no avanza a la página siguiente desde el borde inferior");
        pageList.SelectedIndex = 0;
        var rotatePage = new ToolButton { DataContext = pageList.Items[1], Tag = "rotate" };
        int rotationBefore = session.Document.Info(1).Rotation;
        typeof(MainWindow).GetMethod("PageActionClick", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, [rotatePage, new RoutedEventArgs(Button.ClickEvent)]);
        if (session.Document.Info(1).Rotation == rotationBefore)
            throw new Exception("El botón de girar la miniatura no actúa sobre su página");
        session.Undo();
        typeof(MainWindow).GetMethod("Refresh", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, null);
        pageList.SelectedIndex = 0;
        var tools = (DockPanel)window.FindName("PropertiesPanel");
        var tabs = (TabControl)window.FindName("ModeTabs");
        if (!tools.Children.Contains(tabs) || Grid.GetColumn(tools) != 4 || tabs.Items.Count != 1 || !ReferenceEquals(tabs.Items[0], window.FindName("EditMode")))
            throw new Exception("El panel derecho debe contener solo las herramientas de edición");
        ((Button)window.FindName("UndoButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        ((TabItem)window.FindName("EditMode")).IsSelected = true;
        if (!ReferenceEquals(tabs.SelectedItem, window.FindName("EditMode")))
            throw new Exception("No se pudo activar la pestaña Editar");
        var list = (ListBox)window.FindName("ObjectsList");
        var boldToggle = (ToggleButton)window.FindName("BoldChoice");
        var italicToggle = (ToggleButton)window.FindName("ItalicChoice");
        if (!Equals(boldToggle.Content, "B") || !Equals(italicToggle.Content, "I"))
            throw new Exception("Negrita y cursiva deben mostrarse como botones de icono");
        boldToggle.IsChecked = true;
        italicToggle.IsChecked = true;
        if (!boldToggle.IsChecked.GetValueOrDefault() || !italicToggle.IsChecked.GetValueOrDefault())
            throw new Exception("Negrita y cursiva no se pueden activar");
        Capture("estilo-botones-activos", 1280, 780);
        boldToggle.IsChecked = italicToggle.IsChecked = false;
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
        if (banner.ActualHeight < 60 || banner.Content is not StackPanel bannerContent || bannerContent.Children.OfType<TextBlock>().Count() != 2)
            throw new Exception("El banner debe mostrar la invitación y el nombre de FirmaPDF");
        banner.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (externalLinks.Count != 1 || externalLinks[0].AbsoluteUri != "https://apps.microsoft.com/detail/9P5ZK5W4J9ZJ")
            throw new Exception("El banner no abre exclusivamente la dirección oficial fija");
        Console.WriteLine("PASS banner FirmaPDF: pulsacion explicita y URL fija sin datos; apertura de navegador sustituida solo en prueba");
        var docTabs = (TabControl)window.FindName("DocumentTabs");
        var first = (WorkspaceDocument)docTabs.Items[0];
        var fixtures = Path.GetDirectoryName(Path.GetFullPath(args[1]))!;
        window.OpenDocuments([Path.Combine(fixtures, "anotaciones.pdf"), Path.Combine(fixtures, "destinos.pdf")]);
        if (docTabs.Items.Count != 3 || docTabs.Items.Cast<WorkspaceDocument>().Any(d => d.Session.Document.Count != 2))
            throw new Exception("Abrir varios debe crear documentos independientes");
        var second = (WorkspaceDocument)docTabs.Items[1];
        ((TabItem)window.FindName("EditMode")).IsSelected = true;
        host.Measure(new Size(1280, 780));
        host.Arrange(new Rect(0, 0, 1280, 780));
        host.UpdateLayout();
        var thumbnails = (ListBox)window.FindName("PagesList");
        thumbnails.ScrollIntoView(thumbnails.Items[1]);
        host.UpdateLayout();
        var dropTarget = (ListBoxItem)thumbnails.ItemContainerGenerator.ContainerFromIndex(1);
        var pageData = new DataObject("EditorPDF.Page", second.Id + ":0");
        var drop = (DragEventArgs)Activator.CreateInstance(typeof(DragEventArgs), BindingFlags.Instance | BindingFlags.NonPublic, null, [pageData, DragDropKeyStates.LeftMouseButton, DragDropEffects.Move, dropTarget, new Point(10, dropTarget.ActualHeight - 2)], System.Globalization.CultureInfo.InvariantCulture)!;
        drop.RoutedEvent = DragDrop.DropEvent;
        dropTarget.RaiseEvent(drop);
        if (!drop.Handled)
            throw new Exception("El evento de soltar no llegó al organizador de miniaturas");
        if (!second.Session.Document.Text(1).Contains("Texto original compatible"))
            throw new Exception("Mover al soltar miniatura no conserva las paginas");
        typeof(MainWindow).GetMethod("SetZoom", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, [1.5]);
        ((TextBox)window.FindName("SearchBox")).Text = "busqueda de la segunda pestaña";
        ((ListBox)window.FindName("ObjectsList")).SelectedIndex = 1;
        ((TextBox)window.FindName("TextValue")).Text = "Borrador sin aplicar";
        docTabs.SelectedIndex = 0;
        if (!ReferenceEquals(first.Session, typeof(MainWindow).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)) || !first.Session.Document.Text(0).Contains("Edicion visual comprobada"))
            throw new Exception("Cambiar pestaña pierde el documento");
        ((Button)window.FindName("UndoButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (!first.Session.Document.Text(0).Contains("Texto original compatible") || !second.Session.Document.Text(1).Contains("Texto original compatible"))
            throw new Exception("El historial se mezcla entre pestañas");
        ((Button)window.FindName("RedoButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        docTabs.SelectedIndex = 1;
        if (((TextBox)window.FindName("TextValue")).Text != "Borrador sin aplicar" || !((TextBox)window.FindName("SearchBox")).Text.Contains("segunda pestaña") || second.View.Page != 1 || Math.Abs(second.View.Zoom - 1.5) > 0.01)
            throw new Exception("La pestaña pierde posicion o borrador");
        Capture("documentos-1280", 1280, 780);
        second.Session.SaveNew(Path.Combine(folder, "ui-reordenado.pdf"));
        if(Directory.Exists(Path.Combine(folder,"recovery"))) throw new Exception("La interfaz no debe guardar recuperación de sesión");
        Console.WriteLine("CHECK cerrar tercera pestaña sin cambios");
        typeof(MainWindow).GetMethod("SwitchDocument", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, [docTabs.Items[2]]);
        if ((bool)typeof(MainWindow).GetMethod("HasUnappliedProperties", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, null)! || ((WorkspaceDocument)docTabs.Items[2]).Session.Dirty)
            throw new Exception("La tercera pestaña no debe tener cambios ni borradores pendientes");
        typeof(MainWindow).GetMethod("CloseDocument", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, [docTabs.Items[2]]);
        if (docTabs.Items.Count != 2)
            throw new Exception("Cerrar documento no conserva los demás");
        if (window.Icon == null || ((Image)window.FindName("HeaderLogo")).Source == null)
            throw new Exception("Falta el logo de la app");
        window.OpenDocuments([Path.Combine(fixtures, "firma-sintetica.pdf")]);
        if (((FrameworkElement)window.FindName("EditableCopyPanel")).Visibility != Visibility.Visible)
            throw new Exception("Falta la opción de copia editable en PDF firmados");
        Capture("firmado-1280", 1280, 780);
        window.OpenEditableCopy(Path.Combine(fixtures, "firma-sintetica.pdf"));
        var editable = (WorkspaceDocument)docTabs.SelectedItem;
        if (editable.Session.Document.EditBlock != null || ((FrameworkElement)window.FindName("EditableCopyPanel")).Visibility != Visibility.Collapsed)
            throw new Exception("La copia sin firma sigue bloqueada");
        ((TabItem)window.FindName("EditMode")).IsSelected = true;
        var editableObjects = (ListBox)window.FindName("ObjectsList");
        editableObjects.SelectedItem = editableObjects.Items.Cast<MainWindow.ObjectRow>().First(o => o.Value.Text == "Texto original compatible 123");
        ((TextBox)window.FindName("FontSizeValue")).Text = "23";
        ((TextBox)window.FindName("ColorValue")).Text = "#B42318";
        ((Button)window.FindName("ApplyStyleButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (!editable.Session.Document.Objects(0).Any(o => o.Text == "Texto original compatible 123" && Math.Abs(o.Size - 23) < .01 && o.FillRgb == 0xB42318))
            throw new Exception("El estilo del texto original no se aplica desde el panel");
        editableObjects.SelectedItem = editableObjects.Items.Cast<MainWindow.ObjectRow>().First(o => o.Value.Text == "Texto original compatible 123");
        Capture("estilo-copia-1280", 1280, 780);
        int oldTabs = docTabs.Items.Count;
        Console.WriteLine("CHECK union desde Explorador");
        window.MergeDocuments([Path.Combine(fixtures, "externo.pdf"), Path.Combine(fixtures, "anotaciones.pdf")]);
        if (docTabs.Items.Count != oldTabs + 1 || ((WorkspaceDocument)docTabs.SelectedItem).Session.Document.Count != 4)
            throw new Exception("La unión del Explorador debe abrir una pestaña nueva con todos los PDF");
        Console.WriteLine("CHECK integracion del Explorador");
        CanvasChecks.Run(window, folder, Path.GetFullPath(args[1]), Capture);
        ShellChecks.Run(Path.GetFullPath(args[1]), folder);
        PageTransferChecks.Run(folder,fixtures);
        ReductionChecks.Run(folder,fixtures);
        Console.WriteLine("PASS documentos en pestañas: sesiones e historial independientes, orden por insercion, borradores, sin recuperación persistente y cierre");
        typeof(MainWindow).GetMethod("DisposeWorkspace", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, null);
        Console.WriteLine("PASS UI texto original, deshacer, rehacer y guardado");
    }
}
