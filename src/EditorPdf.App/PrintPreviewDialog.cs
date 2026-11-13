using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using EditorPdf.Core;
namespace EditorPdf.App;

// The same ticket, selected pages and paginator drive the preview and print job.
public sealed class PrintPreviewDialog : Window
{
    readonly PdfDocument document;
    readonly ComboBox printers = new() { MinWidth = 220, DisplayMemberPath = "FullName" };
    readonly ComboBox orientation = new() { ItemsSource = new[] { Ui.T("Vertical"), Ui.T("Horizontal") }, SelectedIndex = 0 };
    readonly ComboBox paper = new() { DisplayMemberPath = "Label" };
    readonly ComboBox color = new() { ItemsSource = new[] { Ui.T("Color"), Ui.T("Blanco y negro") }, SelectedIndex = 0 };
    readonly TextBox ranges = new() { Text = "", ToolTip = Ui.T("Vacío: todas. Ejemplo: 1-3,5") };
    static readonly int[] SheetCounts = [1, 2, 4, 6, 9, 16];
    int pagesPerSheet = 1;
    bool columnsFirst;
    readonly CheckBox sheetBorders = new() { Content = Ui.T("Dibujar bordes"), Margin = new Thickness(0, 8, 0, 0), IsEnabled = false };
    readonly ComboBox duplex = new() { DisplayMemberPath = "Label", IsEnabled = false };
    readonly WrapPanel layoutCards = new();
    readonly StackPanel orderCards = new() { Orientation = Orientation.Horizontal };
    readonly TextBox copies = new() { Text = "1" };
    readonly TextBlock counter = new() { Margin = new Thickness(12,0,12,0) };
    readonly TextBlock error = new() { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Firebrick, Margin = new Thickness(0,12,0,0) };
    readonly Image image = new() { Stretch = Stretch.Uniform, Margin = new Thickness(16) };
    readonly Button print = new() { Content = Ui.T("Imprimir"), IsDefault = true, MinWidth = 110 };
    readonly Button previous = new() { Content = "‹", ToolTip = Ui.T("Página anterior") };
    readonly Button next = new() { Content = "›", ToolTip = Ui.T("Página siguiente") };
    readonly DispatcherTimer refresh = new() { Interval = TimeSpan.FromMilliseconds(180) };
    LocalPrintServer? server;
    PrintQueueCollection? queueCollection;
    PrintQueue[] queues = [];
    PrintTicket? baseTicket, ticket;
    PrintQueue? queue;
    bool updating;
    int previewIndex;
    int[] selectedPages = [];
    Size sheet = new(793.7,1122.5);
    Rect printable = new(24,24,745.7,1074.5);
    bool monochrome;
    public PdfPaginator? CurrentPaginator { get; private set; }
    public IReadOnlyList<int> SelectedPages => selectedPages;
    public Size SheetSize => sheet;
    public bool Grayscale => monochrome;
    public int PreviewIndex => previewIndex;
    public BitmapSource? PreviewImage => image.Source as BitmapSource;
    public string StatusMessage => error.Text;
    public bool CanPrint => print.IsEnabled;
    public int PrinterCount => queues.Length;
    public string ValidationDetails { get; private set; } = "";
    sealed record DuplexOption(Duplexing Mode, string Label);
    public sealed record PaperOption(PageMediaSize Media, string Label);

    public PrintPreviewDialog(Window owner, PdfDocument document, bool loadPrinters = true)
    {
        this.document = document;
        ranges.Text=document.Count==1 ? "1" : $"1-{document.Count}";
        if(owner.IsVisible) Owner=owner;
        Title=Ui.T("Impresión"); Width=1040; Height=780; MinWidth=820; MinHeight=580;
        WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var root=new DockPanel { Margin=new Thickness(16) };
        var footer=new StackPanel { Orientation=Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Right };
        var cancel=new Button { Content=Ui.T("Cancelar"), IsCancel=true, MinWidth=110 };
        footer.Children.Add(print); footer.Children.Add(cancel); DockPanel.SetDock(footer,Dock.Bottom); root.Children.Add(footer);
        var controls=new StackPanel { Width=240, Margin=new Thickness(0,0,18,0) };
        void Field(string label,Control control) { controls.Children.Add(new Label { Content=Ui.T(label), Target=control, Margin=new Thickness(0,5,0,4) }); controls.Children.Add(control); }
        Field("Impresora",printers); Field("Orientación",orientation); Field("Papel",paper); Field("Copias",copies); Field("Modo de color",color); Field("Páginas",ranges);
        Field("Doble cara", duplex);
        controls.Children.Add(new Label { Content = Ui.T("Páginas por hoja") });
        controls.Children.Add(layoutCards);
        controls.Children.Add(new Label { Content = Ui.T("Orden en la hoja") });
        controls.Children.Add(orderCards);
        controls.Children.Add(sheetBorders);
        controls.Children.Add(error);
        var scroll=new ScrollViewer { Content=controls, VerticalScrollBarVisibility=ScrollBarVisibility.Auto, HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled };
        DockPanel.SetDock(scroll,Dock.Left); root.Children.Add(scroll);
        var preview=new DockPanel();
        var navigation=new StackPanel { Orientation=Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Center };
        navigation.Children.Add(previous); navigation.Children.Add(counter); navigation.Children.Add(next);
        DockPanel.SetDock(navigation,Dock.Top); preview.Children.Add(navigation);
        preview.Children.Add(new Border { Background=new SolidColorBrush(Color.FromRgb(220,225,232)), Child=image, Margin=new Thickness(0,8,0,8) });
        root.Children.Add(preview); Content=root;
        paper.ItemsSource=new[] { new PaperOption(new PageMediaSize(PageMediaSizeName.ISOA4,793.7,1122.5),"A4"), new PaperOption(new PageMediaSize(PageMediaSizeName.NorthAmericaLetter,816,1056),"Letter") };
        paper.SelectedIndex=0;
        refresh.Tick+=(_,_)=>{ refresh.Stop(); UpdatePreview(); };
        printers.SelectionChanged+=(_,_)=>SelectPrinter();
        foreach(var combo in new[] { orientation,paper,color }) combo.SelectionChanged+=(_,_)=>Schedule();
        ranges.TextChanged+=(_,_)=>{ previewIndex=0; Schedule(); };
        sheetBorders.Checked += (_, _) => Schedule();
        sheetBorders.Unchecked += (_, _) => Schedule();
        duplex.SelectionChanged += (_, _) => Schedule();
        duplex.ItemsSource = new[] { new DuplexOption(Duplexing.OneSided, Ui.T("Una cara")) };
        duplex.SelectedIndex = 0;
        copies.TextChanged+=(_,_)=>Schedule();
        previous.Click+=(_,_)=>{ if(previewIndex>0) { previewIndex--; RenderPreview(); } };
        next.Click+=(_,_)=>{ if(previewIndex+1<(CurrentPaginator?.PageCount ?? 0)) { previewIndex++; RenderPreview(); } };
        print.Click+=(_,_)=>PrintNow();
        Closed+=(_,_)=>{ refresh.Stop(); foreach(var q in queues) q.Dispose(); queueCollection?.Dispose(); server?.Dispose(); };
        if(loadPrinters) LoadPrinters();
        UpdatePreview();
    }
    void Schedule() { if(updating) return; print.IsEnabled=false; refresh.Stop(); refresh.Start(); }
    void LoadPrinters()
    {
        try
        {
            server=new LocalPrintServer();
            queueCollection=server.GetPrintQueues(new[] { EnumeratedPrintQueueTypes.Local,EnumeratedPrintQueueTypes.Connections });
            queues=queueCollection.ToArray();
            printers.ItemsSource=queues;
            using var defaultQueue=server.DefaultPrintQueue;
            string? defaultName=defaultQueue?.FullName;
            printers.SelectedItem=queues.FirstOrDefault(q=>q.FullName==defaultName) ?? queues.FirstOrDefault();
            if(queues.Length==0) error.Text=Ui.T("No hay impresoras instaladas.");
        }
        catch(Exception ex) when(ex is PrintSystemException or System.UnauthorizedAccessException)
        { error.Text=Ui.T("No se pudieron cargar las impresoras.")+" "+ex.Message; }
    }
    void SelectPrinter()
    {
        if(printers.SelectedItem is not PrintQueue selected) return;
        queue=null; baseTicket=null;
        updating=true;
        try
        {
            var defaults=selected.UserPrintTicket ?? selected.DefaultPrintTicket;
            defaults=selected.MergeAndValidatePrintTicket(defaults,null).ValidatedPrintTicket;
            var caps=selected.GetPrintCapabilities(defaults);
            var sides = new List<DuplexOption> { new(Duplexing.OneSided, Ui.T("Una cara")) };
            if (caps.DuplexingCapability.Contains(Duplexing.TwoSidedLongEdge))
                sides.Add(new(Duplexing.TwoSidedLongEdge, Ui.T("Doble cara · borde largo")));
            if (caps.DuplexingCapability.Contains(Duplexing.TwoSidedShortEdge))
                sides.Add(new(Duplexing.TwoSidedShortEdge, Ui.T("Doble cara · borde corto")));
            duplex.ItemsSource = sides;
            duplex.SelectedIndex = 0;
            duplex.IsEnabled = sides.Count > 1;
            var papers=caps.PageMediaSizeCapability.Where(m=>m.Width>0 && m.Height>0).Select(m=>new PaperOption(m,m.PageMediaSizeName switch
            { PageMediaSizeName.ISOA4=>"A4", PageMediaSizeName.ISOA3=>"A3", PageMediaSizeName.ISOA5=>"A5", PageMediaSizeName.NorthAmericaLetter=>"Letter", PageMediaSizeName.NorthAmericaLegal=>"Legal", _=>$"{m.Width*25.4/96:0} × {m.Height*25.4/96:0} mm" })).ToArray();
            if(papers.Length==0) throw new InvalidOperationException(Ui.T("La impresora no informa del tamaño de papel."));
            paper.ItemsSource=papers;
            paper.SelectedItem=papers.FirstOrDefault(p=>p.Media.PageMediaSizeName==defaults.PageMediaSize?.PageMediaSizeName) ?? papers[0];
            orientation.SelectedIndex=defaults.PageOrientation is PageOrientation.Landscape or PageOrientation.ReverseLandscape ? 1 : 0;
            color.SelectedIndex=defaults.OutputColor is OutputColor.Monochrome or OutputColor.Grayscale ? 1 : 0;
            copies.Text="1";
            queue=selected; baseTicket=defaults.Clone();
            error.Text="";
        }
        catch(Exception ex) when(ex is not OutOfMemoryException) { error.Text=ex.Message; }
        finally { updating=false; }
        UpdatePreview();
    }
    public void SetPreviewOptions(string pages, bool landscape, bool blackAndWhite, int copyCount=1, int perSheet=1, bool downFirst=false, bool drawBorders=false)
    {
        updating=true;
        ranges.Text=pages; orientation.SelectedIndex=landscape ? 1 : 0; color.SelectedIndex=blackAndWhite ? 1 : 0; copies.Text=copyCount.ToString();
        pagesPerSheet = SheetCounts.Contains(perSheet) ? perSheet : 1; columnsFirst = downFirst; sheetBorders.IsChecked = drawBorders;
        updating=false; previewIndex=0; UpdatePreview();
    }
    void UpdatePreview()
    {
        refresh.Stop();
        try
        {
            selectedPages=string.IsNullOrWhiteSpace(ranges.Text) ? Enumerable.Range(0,document.Count).ToArray() : Session.ParseRanges(ranges.Text,document.Count);
            if(selectedPages.Length==0) throw new ArgumentException(Ui.T("Selecciona páginas para imprimir."));
            if(!int.TryParse(copies.Text,out int count) || count<1 || count>999) throw new ArgumentException(Ui.T("Indica entre 1 y 999 copias."));
            var chosen=(PaperOption)paper.SelectedItem;
            bool landscape=orientation.SelectedIndex==1;
            monochrome=color.SelectedIndex==1;
            double width=chosen.Media.Width!.Value,height=chosen.Media.Height!.Value;
            sheet=landscape ? new Size(Math.Max(width,height),Math.Min(width,height)) : new Size(Math.Min(width,height),Math.Max(width,height));
            printable=new Rect(24,24,sheet.Width-48,sheet.Height-48);
            if(queue!=null && baseTicket!=null)
            {
                var desired=baseTicket.Clone(); desired.CopyCount=count;
                desired.PagesPerSheet=1;
                desired.PageScalingFactor=100;
                if (duplex.SelectedItem is DuplexOption side)
                    desired.Duplexing = side.Mode;
                desired.PageMediaSize=chosen.Media;
                desired.PageOrientation=landscape ? PageOrientation.Landscape : PageOrientation.Portrait;
                var caps=queue.GetPrintCapabilities(desired);
                desired.OutputColor=monochrome ? (caps.OutputColorCapability.Contains(OutputColor.Grayscale) ? OutputColor.Grayscale : OutputColor.Monochrome) : OutputColor.Color;
                if(caps.OutputColorCapability.Count>0 && !caps.OutputColorCapability.Contains(desired.OutputColor.Value))
                    throw new InvalidOperationException(Ui.T("La impresora no admite el modo de color seleccionado."));
                var validation=queue.MergeAndValidatePrintTicket(baseTicket,desired);
                ticket=validation.ValidatedPrintTicket;
                ValidationDetails=$"Orientation {desired.PageOrientation}/{ticket.PageOrientation}; Paper {desired.PageMediaSize.PageMediaSizeName}/{ticket.PageMediaSize?.PageMediaSizeName}; Copies {desired.CopyCount}/{ticket.CopyCount}; Color {desired.OutputColor}/{ticket.OutputColor}";
                bool colorMatches=monochrome ? ticket.OutputColor is OutputColor.Monochrome or OutputColor.Grayscale : ticket.OutputColor==OutputColor.Color;
                if(ticket.PageOrientation!=desired.PageOrientation || ticket.PageMediaSize?.PageMediaSizeName!=desired.PageMediaSize.PageMediaSizeName || ticket.CopyCount!=desired.CopyCount || !colorMatches || (ticket.Duplexing ?? Duplexing.OneSided) != desired.Duplexing || (ticket.PagesPerSheet ?? 1)!=1 || (ticket.PageScalingFactor ?? 100)!=100)
                    throw new InvalidOperationException(Ui.T("La impresora no admite esta combinación de opciones."));
                caps=queue.GetPrintCapabilities(ticket);
                if(caps.OrientedPageMediaWidth is >0 && caps.OrientedPageMediaHeight is >0) sheet=new Size(caps.OrientedPageMediaWidth.Value,caps.OrientedPageMediaHeight.Value);
                var area=caps.PageImageableArea ?? throw new InvalidOperationException(Ui.T("La impresora no informa del área imprimible."));
                printable=Rect.Intersect(new Rect(sheet),new Rect(area.OriginWidth,area.OriginHeight,area.ExtentWidth,area.ExtentHeight));
                if(printable.IsEmpty || printable.Width<=0 || printable.Height<=0) throw new InvalidOperationException(Ui.T("El área imprimible no es válida."));
            }
            int countPerSheet = pagesPerSheet;
            var (columns, rows) = PdfPaginator.Layout(countPerSheet, sheet.Width > sheet.Height);
            UpdateLayoutCards(countPerSheet);
            sheetBorders.IsEnabled = countPerSheet > 1;
            CurrentPaginator = new PdfPaginator(document, sheet, selectedPages, printable, monochrome, columns, rows,
                columnsFirst, sheetBorders.IsEnabled && sheetBorders.IsChecked == true);
            previewIndex = Math.Clamp(previewIndex, 0, CurrentPaginator.PageCount - 1);
            RenderPreview();
            if(queue!=null) error.Text="";
            print.IsEnabled=queue!=null;
        }
        catch(Exception ex) when(ex is not OutOfMemoryException)
        { CurrentPaginator=null; image.Source=null; counter.Text=""; error.Text=Ui.T(ex.Message); print.IsEnabled=false; previous.IsEnabled=next.IsEnabled=false; }
    }
    void UpdateLayoutCards(int selected)
    {
        layoutCards.Children.Clear();
        orderCards.Children.Clear();
        bool landscape = sheet.Width > sheet.Height;
        foreach (int count in SheetCounts)
        {
            string title = $"{count}";
            var button = LayoutCard(count, landscape, columnsFirst, title, count == selected);
            button.ToolTip = $"{title} · {Ui.T("Páginas por hoja")}";
            button.Click += (_, _) => { pagesPerSheet = count; Schedule(); };
            layoutCards.Children.Add(button);
        }
        foreach (bool orderByColumns in new[] { false, true })
        {
            string title = Ui.T(orderByColumns ? "Por columnas" : "Por filas");
            var button = LayoutCard(selected, landscape, orderByColumns, title, columnsFirst == orderByColumns);
            button.IsEnabled = selected > 1;
            button.Click += (_, _) => { columnsFirst = orderByColumns; Schedule(); };
            orderCards.Children.Add(button);
        }
    }

    static Button LayoutCard(int count, bool landscape, bool columnsFirst, string label, bool selected)
    {
        var (columns, rows) = PdfPaginator.Layout(count, landscape);
        var grid = new Grid { Width = landscape ? 47 : 38, Height = landscape ? 32 : 42, Margin = new Thickness(0, 0, 0, 3) };
        for (int i = 0; i < columns; i++) grid.ColumnDefinitions.Add(new ColumnDefinition());
        for (int i = 0; i < rows; i++) grid.RowDefinitions.Add(new RowDefinition());
        for (int i = 0; i < count; i++)
        {
            var (column, row) = PdfPaginator.Cell(i, columns, rows, columnsFirst);
            var cell = new Border { Background = Brushes.White, BorderBrush = Brushes.SteelBlue,
                BorderThickness = new Thickness(.7), Margin = new Thickness(.6) };
            cell.Child = new Viewbox { MaxHeight = 12, Child = new TextBlock { Text = $"{i + 1}", Foreground = Brushes.MidnightBlue } };
            Grid.SetColumn(cell, column);
            Grid.SetRow(cell, row);
            grid.Children.Add(cell);
        }
        var stack = new StackPanel();
        stack.Children.Add(grid);
        stack.Children.Add(new TextBlock { Text = label, HorizontalAlignment = HorizontalAlignment.Center });
        var button = new Button { Content = stack, Padding = new Thickness(5), Margin = new Thickness(2),
            Background = selected ? new SolidColorBrush(Color.FromRgb(218, 234, 255)) : Brushes.White,
            BorderBrush = selected ? Brushes.RoyalBlue : Brushes.LightSlateGray };
        System.Windows.Automation.AutomationProperties.SetName(button, label);
        return button;
    }

    void RenderPreview()
    {
        if(CurrentPaginator==null) return;
        var page=CurrentPaginator.GetPage(previewIndex);
        // Render exactly the paginator used by PrintNow, including paper and printer margins.
        double scale=Math.Min(1,1100/Math.Max(sheet.Width,sheet.Height));
        var bitmap=new RenderTargetBitmap((int)Math.Ceiling(sheet.Width*scale),(int)Math.Ceiling(sheet.Height*scale),96*scale,96*scale,PixelFormats.Pbgra32);
        bitmap.Render(page.Visual); bitmap.Freeze(); image.Source=bitmap;
        counter.Text = $"{Ui.T("Hoja")} {previewIndex + 1} / {CurrentPaginator.PageCount}";
        previous.IsEnabled=previewIndex>0; next.IsEnabled=previewIndex+1<(CurrentPaginator?.PageCount ?? 0);
    }
    void PrintNow()
    {
        UpdatePreview();
        if(!print.IsEnabled || queue==null || ticket==null || CurrentPaginator==null) return;
        try
        {
            // No second settings dialog: this job uses the settings shown beside the preview.
            var job=new PrintDialog { PrintQueue=queue,PrintTicket=ticket };
            print.IsEnabled=false;
            job.PrintDocument(CurrentPaginator,"Editor PDF");
            DialogResult=true;
        }
        catch(Exception ex) when(ex is not OutOfMemoryException) { error.Text=ex.Message; print.IsEnabled=true; }
    }
}
