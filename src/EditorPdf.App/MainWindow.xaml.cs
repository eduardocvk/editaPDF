using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using EditorPdf.Core;
using Microsoft.Win32;
namespace EditorPdf.App;

public partial class MainWindow : Window
{
    readonly Action<Uri> openExternal;
    readonly bool useFirmaBridge;
    readonly Action<Exception>? reportError;
    public static Uri FirmaPdfStoreUri { get; } = new("https://apps.microsoft.com/detail/9P5ZK5W4J9ZJ");
    readonly string recoveryFolder; readonly bool restoreOnLoad; Session session = new(); int page; double zoom = 1; bool refreshing;
    public static string RecoveryFolder => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EditorPDF", "Recovery");
    PdfDocument Pdf => session.Document; ObjectRow? Selected => ObjectsList.SelectedItems.Count == 1 ? ObjectsList.SelectedItem as ObjectRow : null;
    public MainWindow(string? localRecovery = null, bool restore = true, Action<Uri>? externalLinkOpener = null, Action<Exception>? errorReporter = null)
    {
        recoveryFolder = localRecovery ?? RecoveryFolder;
        restoreOnLoad = restore;
        reportError = errorReporter;
        useFirmaBridge = externalLinkOpener == null;
        openExternal = externalLinkOpener ?? (uri => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }));
        InitializeComponent();
        if(useFirmaBridge && FirmaPdfBridge.FindExecutable()!=null && FirmaPdfBanner.Content is StackPanel banner)
        {
            banner.Children.OfType<TextBlock>().First().Text=Ui.T("Firma con tu certificado digital");
            banner.Children.OfType<TextBlock>().Last().Text=Ui.T("Firma este PDF ↗");
            FirmaPdfBanner.ToolTip=Ui.T("Guardar una copia y abrirla en FirmaPDF");
        }
        FontChoice.ItemsSource = new[] { Ui.T("Conservar original") }.Concat(PdfDocument.IncludedFontFamilies).ToArray();
        FontChoice.SelectedIndex = 1;
        InitializeWorkspace();
        InitializeCanvas();
        DocumentScroll.SizeChanged += (_, _) => UpdateAdjacentPage();
        Icon = BitmapFrame.Create(new Uri(System.IO.Path.Combine(AppContext.BaseDirectory, "assets", "EditorPDF.ico")));
        HeaderLogo.Source = new BitmapImage(new Uri(System.IO.Path.Combine(AppContext.BaseDirectory, "assets", "logo.png")));
        FontFamily = new FontFamily(new Uri(AppContext.BaseDirectory), "./assets/#Noto Sans");
        Loaded += (_, _) => Refresh();
    }
    void ChangeLanguage(object sender, RoutedEventArgs e)
    {
        if(sender is not MenuItem { Tag: string language }) return;
        Run(() => SetInterfaceLanguage(language));
    }
    public void SetInterfaceLanguage(string language, bool persist = true)
    {
        FinishInlineText();
        Ui.Current.SetLanguage(language,persist);
        int selected=FontChoice.SelectedIndex;
        FontChoice.ItemsSource=new[] { Ui.T("Conservar original") }.Concat(PdfDocument.IncludedFontFamilies).ToArray();
        FontChoice.SelectedIndex=selected;
        Status.Text=Ui.T("Selecciona el texto dibujando un rectángulo alrededor.");
        if(Selected is { } row) DrawObjectSelection(row.Value);
    }
    void Run(Action work, string? success = null)
    {
        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            work();
            if (success != null)
                Status.Text = success;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { Status.Text = Ui.T(ex.Message); if (reportError != null) reportError(ex); else MessageBox.Show(this, Ui.T(ex.Message), "Editor PDF", MessageBoxButton.OK, MessageBoxImage.Warning); }
        finally { Mouse.OverrideCursor = null; }
    }
    void RequireDocument()
    {
        if (Pdf.Count == 0)
            throw new InvalidOperationException(Ui.T("Abre un PDF primero."));
    }
    bool Recover() => true;
    void Change(Action<PdfDocument> mutation, int? selectedPage = null)
    {
        FinishInlineText();
        RequireDocument();
        session.Execute(mutation);
        if (selectedPage.HasValue)
            page = selectedPage.Value;
        Refresh();
        Status.Text = Ui.T("Cambios sin guardar");
        Recover();
    }
    static BitmapSource Bitmap(Raster r)
    {
        var b = BitmapSource.Create(r.Width, r.Height, 96, 96, PixelFormats.Bgra32, null, r.Pixels, r.Stride);
        b.Freeze();
        return b;
    }
    public sealed class Thumb(PdfDocument doc, int index)
    {
        public int Index => index;
        BitmapSource? cached; public string Label => $"{index + 1}"; public BitmapSource Image => cached ??= Bitmap(doc.Render(index, 140));
        public override string ToString() => $"Página {index + 1}";
    }
    public sealed record ObjectRow(PdfObject Value)
    {
        public string Label => Value.Type == 3 ? Ui.T("Imagen") : Value.Type == 2 ? Ui.T("Forma vectorial") : string.IsNullOrWhiteSpace(Value.Text) ? "Texto (sin mapa Unicode)" : Value.Text.Replace("\r", " ").Replace("\n", " ");
    }
    void Refresh()
    {
        if (!IsInitialized)
            return;
        refreshing = true;
        try
        {
            page = Math.Clamp(page, 0, Math.Max(0, Pdf.Count - 1));
            PagesList.ItemsSource = Enumerable.Range(0, Pdf.Count).Select(i => new Thumb(Pdf, i)).ToArray();
            PagesList.SelectedIndex = Pdf.Count > 0 ? page : -1;
            UndoButton.IsEnabled = session.CanUndo;
            RedoButton.IsEnabled = session.CanRedo;
            EmptyHint.Visibility = Pdf.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            UpdateTabHeaders();
            EditableCopyPanel.Visibility = Pdf.Signed || Pdf.Protected ? Visibility.Visible : Visibility.Collapsed;
            EditableCopyButton.Visibility = Pdf.Signed ? Visibility.Collapsed : Visibility.Visible;
            RemoveSignaturesButton.Visibility = Pdf.Signed ? Visibility.Visible : Visibility.Collapsed;
            ShowPage();
        }
        finally { refreshing = false; }
    }
    void ShowPage()
    {
        if (paragraphArmed || areaSelection != null) CancelParagraphSelection();
        CancelCanvasGesture(false);
        Overlay.Children.Clear();
        if (Pdf.Count == 0)
        {
            PageImage.Source = null;
            ObjectsList.ItemsSource = null;
            UpdateAdjacentPage();
            return;
        }
        var info = Pdf.Info(page);
        var raster = Pdf.Render(page, (int)(info.Width * zoom));
        PageImage.Source = Bitmap(raster);
        PageImage.Width = raster.Width;
        PageImage.Height = raster.Height;
        Overlay.Width = raster.Width;
        Overlay.Height = raster.Height;
        Paper.Width = raster.Width;
        Paper.Height = raster.Height;
        ObjectsList.ItemsSource = Pdf.Objects(page).Select(x => new ObjectRow(x)).ToArray();
        UpdateAdjacentPage();
        ZoomLabel.Text = $"{zoom * 100:0} %";
        Status.Text = (Ui.Current.Language=="en" ? $"Page {page+1} of {Pdf.Count}" : $"Página {page+1} de {Pdf.Count}") + (Pdf.EditBlock != null ? (Ui.Current.Language=="en" ? " · Read only" : " · Solo lectura") : "") + (session.Dirty ? (Ui.Current.Language=="en" ? " · Unsaved changes" : " · Cambios sin guardar") : "");
        if (Pdf.EditBlock != null)
            Status.ToolTip = Pdf.EditBlock;
        else
            Status.ToolTip = null;
    }
    bool LeaveCurrent()
    {
        if (!session.Dirty)
            return true;
        var result = MessageBox.Show(this, Ui.T("¿Guardar una copia de los cambios antes de continuar?"), Ui.T("Cambios sin guardar"), MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        if (result == MessageBoxResult.Cancel)
            return false;
        if (result == MessageBoxResult.No)
            return true;
        return SaveCopy();
    }
    public void OpenPaths(string[] paths) => Run(() => OpenDocuments(paths));
    string[] PickPdfs(bool multiple = true)
    {
        var dialog = new OpenFileDialog { Filter = "PDF (*.pdf)|*.pdf", Multiselect = multiple, CheckFileExists = true };
        return dialog.ShowDialog(this) == true ? dialog.FileNames : [];
    }
    void OpenClick(object sender, RoutedEventArgs e)
    {
        var files = PickPdfs();
        if (files.Length > 0)
            OpenPaths(files);
    }
    void AppendClick(object sender, RoutedEventArgs e) => Run(() =>
    {
        FinishInlineText(); CancelCanvasGesture(false); CheckDocumentCapacity();
        using var composer=new MergeComposerDialog(this,documents.OrderBy(d=>d==activeDocument ? 0 : 1));
        Mouse.OverrideCursor=null;
        if(composer.ShowDialog()!=true) return;
        OpenComposition(composer);
    });
    public WorkspaceDocument OpenComposition(MergeComposerDialog composer)
    {
        CheckDocumentCapacity();
        var next=composer.TakeDocument();
        var document=new WorkspaceDocument(next,Ui.T("Unión de PDF"));
        documents.Add(document); SwitchDocument(document);
        foreach(var empty in documents.Where(d=>d!=document && d.Session.Document.Count==0 && !d.Session.Dirty).ToArray())
        { documents.Remove(empty); empty.Dispose(); }
        return document;
    }
    bool SaveCopy()
    {
        FinishInlineText();
        RequireDocument();
        var d = new SaveFileDialog { Filter = "PDF (*.pdf)|*.pdf", FileName = "documento_editado.pdf", AddExtension = true, OverwritePrompt = true };
        if (d.ShowDialog(this) != true)
            return false;
        session.SaveNew(d.FileName, true);
        Refresh();
        Recover();
        Status.Text = Ui.T("Copia guardada y comprobada.");
        return true;
    }
    void SaveClick(object sender, RoutedEventArgs e) => Run(() => SaveCopy());
    void BlankClick(object sender, RoutedEventArgs e) => Run(() => { if (Pdf.Count == 0) { session.Execute(d => d.Blank(0)); Refresh(); Recover(); } else Change(d => d.Blank(page + 1), page + 1); });
    void DuplicateClick(object sender, RoutedEventArgs e) => Run(() => Change(d => d.Duplicate(page), page + 1));
    void RotateClick(object sender, RoutedEventArgs e) => Run(() => Change(d => d.Rotate(page)));
    void DeletePageClick(object sender, RoutedEventArgs e) => Run(() => DeletePage(page));

    void DeletePage(int index)
    {
        FinishInlineText();
        var impact = Pdf.ReviewPageDeletion(index);
        var details = new List<string>();
        if (impact.Annotations > 0) details.Add($"{Ui.T("Anotaciones o campos de la página que se eliminarán")} · {impact.Annotations}");
        if (impact.Links > 0) details.Add($"{Ui.T("Enlaces internos")} · {impact.Links}");
        if (impact.Bookmarks > 0) details.Add($"{Ui.T("Marcadores")} · {impact.Bookmarks}");
        if (impact.NamedDestinations > 0) details.Add($"{Ui.T("Destinos con nombre")} · {impact.NamedDestinations}");
        if (impact.OtherDestinations > 0) details.Add($"{Ui.T("Otros destinos")} · {impact.OtherDestinations}");
        if (details.Count > 0)
        {
            var separator = Environment.NewLine;
            var text = $"{Ui.T("Eliminar página")} {index + 1}" + separator + separator + string.Join(separator, details);
            if (impact.Links + impact.Bookmarks + impact.NamedDestinations + impact.OtherDestinations > 0)
                text += separator + separator + string.Format(Ui.T("Las referencias pasarán a la página {0} del documento actual."), impact.ReplacementPage);
            Mouse.OverrideCursor = null;
            if (MessageBox.Show(this, text, Ui.T("Eliminar página"), MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
                return;
        }
        Change(d => d.Delete(index), Math.Min(index, Pdf.Count - 2));
    }
    void MoveUpClick(object sender, RoutedEventArgs e) => Run(() => { if (page > 0) { int from = page; Change(d => d.Move(from, from - 1), from - 1); } });
    void MoveDownClick(object sender, RoutedEventArgs e) => Run(() => { if (page < Pdf.Count - 1) { int from = page; Change(d => d.Move(from, from + 1), from + 1); } });
    void UndoClick(object sender, RoutedEventArgs e) => Run(() => { session.Undo(); Refresh(); Recover(); });
    void RedoClick(object sender, RoutedEventArgs e) => Run(() => { session.Redo(); Refresh(); Recover(); });
    void PageActionClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is not Button { DataContext: Thumb thumb, Tag: string action }) return;
        Run(() =>
        {
            int index = thumb.Index;
            if (index < 0 || index >= Pdf.Count) return;
            switch (action)
            {
                case "up" when index > 0: Change(d => d.Move(index, index - 1), index - 1); break;
                case "down" when index < Pdf.Count - 1: Change(d => d.Move(index, index + 1), index + 1); break;
                case "duplicate": Change(d => d.Duplicate(index), index + 1); break;
                case "rotate": Change(d => d.Rotate(index), index); break;
                case "delete": DeletePage(index); break;
            }
        });
    }
    void DocumentMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && Pdf.Count > 0)
        {
            e.Handled = true;
            Run(() =>
            {
                Point before = e.GetPosition(PageImage);
                Point pointer = e.GetPosition(DocumentScroll);
                double width = PageImage.Width, height = PageImage.Height;
                SetZoom(zoom * Math.Pow(1.2, e.Delta / 120.0));
                DocumentScroll.UpdateLayout();
                Point after = PageImage.TranslatePoint(new Point(before.X * PageImage.Width / width, before.Y * PageImage.Height / height), DocumentScroll);
                DocumentScroll.ScrollToHorizontalOffset(DocumentScroll.HorizontalOffset + after.X - pointer.X);
                DocumentScroll.ScrollToVerticalOffset(DocumentScroll.VerticalOffset + after.Y - pointer.Y);
            });
            return;
        }
        if (Pdf.Count < 2 || Keyboard.Modifiers != ModifierKeys.None || paragraphStart != null || gestureObject != null) return;
        bool next = e.Delta < 0;
        bool atEdge = next ? DocumentScroll.VerticalOffset + DocumentScroll.ViewportHeight >= DocumentScroll.ExtentHeight - 2
            : DocumentScroll.VerticalOffset <= 2;
        if (!atEdge || (next ? page >= Pdf.Count - 1 : page <= 0)) return;
        e.Handled = true;
        Run(() =>
        {
            FinishInlineText();
            page += next ? 1 : -1;
            refreshing = true;
            PagesList.SelectedIndex = page;
            refreshing = false;
            ShowPage();
            Dispatcher.BeginInvoke(() => DocumentScroll.ScrollToVerticalOffset(next ? 0 : DocumentScroll.ScrollableHeight), System.Windows.Threading.DispatcherPriority.Loaded);
        });
    }
    void PageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (refreshing || PagesList.SelectedIndex < 0)
            return;
        int requestedPage = e.AddedItems.OfType<Thumb>().LastOrDefault()?.Index ?? PagesList.SelectedIndex;
        Run(() => { FinishInlineText(); CancelCanvasGesture(false); page = requestedPage; ShowPage(); });
    }
    void ModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized || ModeTabs == null || e.Source != ModeTabs)
            return;
        if (inlineText != null)
            Run(FinishInlineText);
        if (paragraphArmed || areaSelection != null) CancelParagraphSelection();
        CancelCanvasGesture(false);
        Overlay.Children.Clear();
        if (Selected is { } selected)
            Mark(selected.Value);
    }
    void ObjectChanged(object sender, SelectionChangedEventArgs e)
    {
        Overlay.Children.Clear();
        if (Selected is not { } row)
        {
            TextValue.Clear();
            TextValue.IsEnabled = false;
            FontLabel.Text = "";
            FontSizeValue.Clear();
            ColorValue.Clear();
            FontSizeValue.IsEnabled = ColorValue.IsEnabled = false;
            FontChoice.SelectedIndex = 1;
            BoldChoice.IsChecked = false;
            ItalicChoice.IsChecked = false;
            foreach (var field in new[] { XValue, YValue, WValue, HValue })
            {
                field.Clear();
                field.IsEnabled = false;
            }
            if (ObjectsList.SelectedItems.Count > 1) DrawGroupSelection();
            return;
        }
        var o = row.Value;
        TextValue.Text = o.Text;
        TextValue.IsEnabled = o.Type == 1;
        FontChoice.SelectedIndex = 0;
        BoldChoice.IsChecked = o.Type == 1 && o.Font.Contains("Bold", StringComparison.OrdinalIgnoreCase);
        ItalicChoice.IsChecked = o.Type == 1 && (o.Font.Contains("Italic", StringComparison.OrdinalIgnoreCase) || o.Font.Contains("Oblique", StringComparison.OrdinalIgnoreCase));
        XValue.IsEnabled = YValue.IsEnabled = true;
        WValue.IsEnabled = HValue.IsEnabled = o.Type == 3;
        FontLabel.Text = o.Type == 1 ? o.Font : "";
        FontSizeValue.IsEnabled = ColorValue.IsEnabled = o.Type == 1;
        FontSizeValue.Text = o.Type == 1 ? o.Size.ToString("0.##", CultureInfo.CurrentCulture) : "";
        ColorValue.Text = o.Type == 1 ? $"#{o.FillRgb:X6}" : "";
        if (o.Type == 1) ColorSwatch.Background = new SolidColorBrush(Color.FromRgb((byte)(o.FillRgb >> 16), (byte)(o.FillRgb >> 8), (byte)o.FillRgb));
        XValue.Text = o.Left.ToString("0.##", CultureInfo.CurrentCulture);
        YValue.Text = o.Bottom.ToString("0.##", CultureInfo.CurrentCulture);
        WValue.Text = (o.Right - o.Left).ToString("0.##", CultureInfo.CurrentCulture);
        HValue.Text = (o.Top - o.Bottom).ToString("0.##", CultureInfo.CurrentCulture);
        Mark(o);
    }
    void Mark(PdfObject o) => DrawObjectSelection(o);
    void AddTextClick(object sender, RoutedEventArgs e) => Run(ArmTextInsertion);
    void ApplyTextClick(object sender, RoutedEventArgs e) => Run(() => { if (Selected is not { } o || o.Value.Type != 1) throw new InvalidOperationException(Ui.T("Selecciona un objeto de texto.")); var text = TextValue.Text; bool bold = BoldChoice.IsChecked == true, italic = ItalicChoice.IsChecked == true; bool originalBold = o.Value.Font.Contains("Bold", StringComparison.OrdinalIgnoreCase), originalItalic = o.Value.Font.Contains("Italic", StringComparison.OrdinalIgnoreCase) || o.Value.Font.Contains("Oblique", StringComparison.OrdinalIgnoreCase); if (FontChoice.SelectedIndex == 0 && (bold != originalBold || italic != originalItalic)) ChooseStyleFont(o.Value); bool substitute = FontChoice.SelectedIndex > 0; if (!substitute && text == o.Value.Text) return; string family = substitute ? (string)FontChoice.SelectedItem : "Noto Sans"; Change(d => d.ReplaceText(page, o.Value.Index, text, substitute, family, bold, italic)); });
    void ApplyStyleClick(object sender, RoutedEventArgs e) => Run(() =>
    {
        if (Selected is not { } row || row.Value.Type != 1)
            throw new InvalidOperationException(Ui.T("Selecciona un objeto de texto."));
        string color = ColorValue.Text.Trim();
        if (color.Length != 7 || color[0] != '#' || !uint.TryParse(color.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgb))
            throw new ArgumentException("Usa un color #RRGGBB, por ejemplo #235EBC.");
        float size = Number(FontSizeValue.Text);
        Change(d => d.SetTextStyle(page, row.Value.Index, size, rgb));
    });
    void PickColorClick(object sender, RoutedEventArgs e) => Run(() =>
    {
        uint original = uint.TryParse(ColorValue.Text.TrimStart('#'), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb) ? rgb : 0;
        var picker = new ColorPickerDialog(this, original);
        Mouse.OverrideCursor = null;
        if (picker.ShowDialog() != true) return;
        ColorValue.Text = $"#{picker.SelectedColor:X6}";
        ColorSwatch.Background = new SolidColorBrush(Color.FromRgb((byte)(picker.SelectedColor >> 16), (byte)(picker.SelectedColor >> 8), (byte)picker.SelectedColor));
        if (toolbarColorSwatch != null) toolbarColorSwatch.Background = ColorSwatch.Background;
    });
    void FontSizeUp(object sender, RoutedEventArgs e) => AdjustFontSize(1);
    void FontSizeDown(object sender, RoutedEventArgs e) => AdjustFontSize(-1);
    void AdjustFontSize(int delta)
    {
        float current = float.TryParse(FontSizeValue.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value) ? value : 14;
        FontSizeValue.Text = Math.Clamp(current + delta, 1, 512).ToString("0.##", CultureInfo.CurrentCulture);
    }
    void DeleteObjectClick(object sender, RoutedEventArgs e) => Run(() => { if (Selected is not { } o) throw new InvalidOperationException(Ui.T("Selecciona texto o imagen.")); Change(d => d.RemoveObject(page, o.Value.Index)); });
    static float Number(string text)
    {
        if (!float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var n) || !float.IsFinite(n))
            throw new ArgumentException(Ui.T("Introduce una medida válida."));
        return n;
    }
    void ApplyPositionClick(object sender, RoutedEventArgs e) => Run(() => { if (Selected is not { } row) throw new InvalidOperationException(Ui.T("Selecciona texto o imagen.")); var o = row.Value; float x = Number(XValue.Text), y = Number(YValue.Text); if (o.Type == 2) { Change(d => d.Transform(page, o.Index, x - o.Left, y - o.Bottom)); return; } float w = Number(WValue.Text), h = Number(HValue.Text); if (w <= 0 || h <= 0 || o.Right <= o.Left || o.Top <= o.Bottom) throw new ArgumentException("Ancho y alto deben ser mayores que cero."); if (o.Type == 1 && (Math.Abs(w - (o.Right - o.Left)) > 0.05 || Math.Abs(h - (o.Top - o.Bottom)) > 0.05)) throw new InvalidOperationException(Ui.T("El cambio de tamaño mediante ancho y alto está disponible para imágenes. El texto puede moverse.")); Change(d => d.Transform(page, o.Index, x - o.Left, y - o.Bottom, o.Type == 3 ? w / (o.Right - o.Left) : 1, o.Type == 3 ? h / (o.Top - o.Bottom) : 1)); });
    void AddImageClick(object sender, RoutedEventArgs e) => Run(() => { RequireDocument(); var dialog = new OpenFileDialog { Filter = "Imágenes|*.png;*.jpg;*.jpeg;*.bmp" }; if (dialog.ShowDialog(this) != true) return; BitmapSource image; using (var file = File.OpenRead(LocalFiles.Validate(dialog.FileName))) { var decoder = BitmapDecoder.Create(file, BitmapCreateOptions.IgnoreImageCache, BitmapCacheOption.OnLoad); image = new FormatConvertedBitmap(decoder.Frames[0], PixelFormats.Bgra32, null, 0); } if ((long)image.PixelWidth * image.PixelHeight > 25_000_000) throw new InvalidOperationException(Ui.T("La imagen supera 25 megapíxeles.")); int stride = image.PixelWidth * 4; var pixels = new byte[stride * image.PixelHeight]; image.CopyPixels(pixels, stride, 0); FinishInlineText(); float width = Math.Min(240, Pdf.Info(page).Width - 80), height = width * image.PixelHeight / image.PixelWidth; Change(d => d.AddImage(page, image.PixelWidth, image.PixelHeight, stride, pixels, 40, Math.Max(20, Pdf.Info(page).Height - height - 60), width, height)); ObjectsList.SelectedItem = ObjectsList.Items.Cast<ObjectRow>().LastOrDefault(); });
    void PageMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (EditMode.IsSelected != true || Pdf.Count == 0)
            return;
        Run(() =>
        {
            PageImage.Focus();
            var point = e.GetPosition(PageImage);
            if (areaSelection != null)
            {
                areaSelection = null;
                if (ObjectsList.SelectedItems.Count > 1) { Overlay.Children.Clear(); DrawGroupSelection(); }
            }
            if (paragraphArmed) { StartParagraphSelection(point); e.Handled = true; return; }
            if (Keyboard.Modifiers == ModifierKeys.Shift && Pdf.ObjectEditBlock == null && !addTextArmed)
            {
                FinishInlineText();
                StartParagraphSelection(point, passive: true);
                e.Handled = true;
                return;
            }
            if (addTextArmed)
            {
                BeginInlineText(point);
                e.Handled = true;
                return;
            }
            FinishInlineText();
            var hit = HitObject(point);
            if (e.ClickCount == 2 && Keyboard.Modifiers == ModifierKeys.None && hit?.Value.Type == 1 && Pdf.ObjectEditBlock == null)
            {
                var bounds = DeviceBounds(hit.Value);
                BeginInlineText(bounds.TopLeft, hit.Value.Text, hit.Value.Index, point);
                e.Handled = true;
                return;
            }
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && hit != null)
            {
                if (ObjectsList.SelectedItems.Contains(hit)) ObjectsList.SelectedItems.Remove(hit);
                else ObjectsList.SelectedItems.Add(hit);
                e.Handled = true;
                return;
            }
            if (hit == null || !ObjectsList.SelectedItems.Contains(hit)) ObjectsList.SelectedItem = hit;
            if (hit == null && Pdf.ObjectEditBlock == null)
            {
                StartParagraphSelection(point, passive: true);
                e.Handled = true;
                return;
            }
            if (hit != null && Pdf.ObjectEditBlock == null)
            {
                BeginObjectGesture(hit.Value, point);
                PageImage.CaptureMouse();
            }
        });
    }
    void PageMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (paragraphStart != null) { Run(() => CompleteParagraphSelection(e.GetPosition(PageImage))); e.Handled = true; return; }
        if (gestureObject != null && resizeCorner == null)
        {
            if (gestureChanged && adjacentIndex >= 0 && new Rect(AdjacentPage.RenderSize).Contains(e.GetPosition(AdjacentPage)))
            {
                int destination = adjacentIndex;
                Run(() => { CancelCanvasGesture(); MoveSelectionToPage(destination); });
            }
            else Run(CommitObjectGesture);
        }
    }
    void SetZoom(double value)
    {
        if (paragraphArmed) CancelParagraphSelection();
        FinishInlineText();
        CancelCanvasGesture(false);
        zoom = Math.Clamp(value, 0.25, 4);
        if (Pdf.Count > 0)
            ShowPage();
    }
    void ZoomOut(object sender, RoutedEventArgs e) => Run(() => SetZoom(zoom / 1.2)); void ZoomIn(object sender, RoutedEventArgs e) => Run(() => SetZoom(zoom * 1.2)); void FitClick(object sender, RoutedEventArgs e) => Run(() => { RequireDocument(); SetZoom((DocumentScroll.ViewportWidth - 65) / Pdf.Info(page).Width); });
    int matchIndex = -1;
    string matchQuery = "";
    void SearchChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if(SearchBack==null || SearchNext==null || SearchCount==null) return;
        SearchBack.Visibility=SearchNext.Visibility=Visibility.Collapsed; SearchCount.Text=""; matchQuery=""; matchIndex=-1;
    }
    void SearchPrevious(object sender, RoutedEventArgs e) => Run(() => FindMatch(-1));
    void SearchClick(object sender, RoutedEventArgs e) => Run(() => FindMatch(1));
    void FindMatch(int direction)
    {
        RequireDocument(); string query=SearchBox.Text;
        if(string.IsNullOrWhiteSpace(query)) { SearchCount.Text=""; return; }
        SearchBack.Visibility=SearchNext.Visibility=Visibility.Visible;
        var matches=new List<(int Page,int Offset)>();
        for(int i=0;i<Pdf.Count;i++) { string text=Pdf.Text(i); int at=0; while(at<text.Length) { int found=text.IndexOf(query,at,StringComparison.OrdinalIgnoreCase); if(found<0) break; matches.Add((i,found)); at=found+query.Length; } }
        if(query!=matchQuery) { matchQuery=query; matchIndex=-1; }
        if(matches.Count==0) { SearchCount.Text="0"; Status.Text=Ui.T("No se encontró el texto."); return; }
        matchIndex=matchIndex<0 ? (direction<0 ? matches.Count-1 : 0) : (matchIndex+direction+matches.Count)%matches.Count;
        page=matches[matchIndex].Page; refreshing=true; PagesList.SelectedIndex=page; refreshing=false; ShowPage(); SearchCount.Text=$"{matchIndex+1}/{matches.Count}";
        int occurrence=matches.Take(matchIndex).Count(m=>m.Page==page);
        var candidates=new List<ObjectRow>();
        foreach(var item in ObjectsList.Items.Cast<ObjectRow>())
        {
            int offset=0;
            while(offset<item.Value.Text.Length) { int found=item.Value.Text.IndexOf(query,offset,StringComparison.OrdinalIgnoreCase); if(found<0) break; candidates.Add(item); offset=found+query.Length; }
        }
        var row=candidates.ElementAtOrDefault(occurrence) ?? candidates.FirstOrDefault();
        if(row!=null) { ObjectsList.SelectedItem=row; var bounds=DeviceBounds(row.Value); DocumentScroll.ScrollToVerticalOffset(Math.Max(0,bounds.Top-80)); }
    }
    void SearchKeys(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SearchClick(sender, e);
            e.Handled = true;
        }
    }
    void PrintClick(object sender, RoutedEventArgs e) => Run(() => { RequireDocument(); if (LocalPrinting.Print(this, Pdf)) Status.Text = Ui.T("Documento enviado a la impresora local seleccionada."); });
    void FirmaPdfClick(object sender, RoutedEventArgs e) => Run(() =>
    {
        var executable=useFirmaBridge ? FirmaPdfBridge.FindExecutable() : null;
        if(executable==null) { openExternal(FirmaPdfStoreUri); return; }
        FinishInlineText(); RequireDocument();
        var save=new SaveFileDialog { Filter="PDF (*.pdf)|*.pdf",FileName=SuggestedSigningFileName(),Title=Ui.T("Guardar copia para FirmaPDF") };
        if(save.ShowDialog(this)!=true) return;
        Pdf.SaveNew(save.FileName);
        FirmaPdfBridge.Open(executable,save.FileName);
    });
    void PrivacyClick(object sender, RoutedEventArgs e) => MessageBox.Show(this,
        Ui.Current.Language=="en" ? "PDF files are processed locally. No telemetry, remote processing or automatic session recovery. Documents are saved only when you choose to save them. Printing uses Windows. The FirmaPDF button saves a copy you select and opens the installed app, or opens its Store page if it is unavailable. AGPL-3.0-or-later; source code and licenses included."
        : "Los PDF se procesan localmente. Sin telemetría, procesamiento remoto ni recuperación automática de sesión. Los documentos solo se guardan cuando lo solicitas. La impresión utiliza Windows. El botón FirmaPDF guarda una copia elegida por ti y abre la aplicación instalada, o su ficha de Store si no está disponible. AGPL-3.0-or-later; código y licencias incluidos.", Ui.T("Privacidad"),MessageBoxButton.OK,MessageBoxImage.Information);
    void KeysPressed(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && (inlineText != null || addTextArmed || gestureObject != null || paragraphArmed || areaSelection != null))
        {
            if (paragraphArmed || areaSelection != null) CancelParagraphSelection();
            else if (inlineText != null || addTextArmed)
                CancelInlineText();
            else
                CancelCanvasGesture();
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Escape && ObjectSelection().Length > 0 && Keyboard.FocusedElement is not TextBox)
        {
            ObjectsList.UnselectAll();
            e.Handled = true;
            return;
        }
        if (inlineText != null && e.Key == Key.Enter)
        {
            Run(FinishInlineText);
            e.Handled = true;
            return;
        }
        if (inlineText != null && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && e.Key is Key.Tab or Key.W or Key.O or Key.P or Key.F)
            Run(FinishInlineText);
        if (inlineText != null && e.Key != Key.S)
            return;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && gestureObject != null)
            CancelCanvasGesture();
        bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control), shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        if (ctrl)
        {
            bool objectKeys = Keyboard.FocusedElement is not TextBox &&
                (PageImage.IsKeyboardFocused || ObjectsList.IsKeyboardFocusWithin || PagesList.IsKeyboardFocusWithin || AdjacentPage.IsKeyboardFocused);
            if (objectKeys && e.Key is Key.C or Key.X && ObjectSelection().Length > 0)
            {
                Run(() => CopySelectedObjects(e.Key == Key.X));
                e.Handled = true;
            }
            else if (objectKeys && e.Key == Key.V && copiedObjects != null && Pdf.Count > 0)
            {
                Run(PasteSelectedObjects);
                e.Handled = true;
            }
            else if (e.Key == Key.D && (ObjectsList.IsKeyboardFocusWithin || PageImage.IsKeyboardFocused) && ObjectSelection().Length > 0)
            {
                Run(DuplicateSelectedObjects);
                e.Handled = true;
            }
            else if (e.Key == Key.W)
            {
                if (activeDocument != null)
                    Run(() => CloseDocument(activeDocument));
                e.Handled = true;
            }
            else if (e.Key == Key.Tab)
            {
                int next = (documents.IndexOf(activeDocument!) + (shift ? -1 : 1) + documents.Count) % documents.Count;
                Run(() => { SwitchDocument(documents[next]); Recover(); });
                e.Handled = true;
            }
            else if (e.Key == Key.O)
            {
                OpenClick(sender, e);
                e.Handled = true;
            }
            else if (e.Key == Key.S)
            {
                SaveClick(sender, e);
                e.Handled = true;
            }
            else if (e.Key == Key.P)
            {
                PrintClick(sender, e);
                e.Handled = true;
            }
            else if (e.Key == Key.F)
            {
                SearchBox.Focus();
                SearchBox.SelectAll();
                e.Handled = true;
            }
            else if (e.Key == Key.Z && Keyboard.FocusedElement is not TextBox)
            {
                if (shift)
                    RedoClick(sender, e);
                else
                    UndoClick(sender, e);
                e.Handled = true;
            }
            else if (e.Key == Key.Y && Keyboard.FocusedElement is not TextBox)
            {
                RedoClick(sender, e);
                e.Handled = true;
            }
        }
        else if (Keyboard.FocusedElement is not TextBox)
        {
            if (e.Key == Key.Delete && ObjectSelection().Length > 0 &&
                !PagesList.IsKeyboardFocusWithin && !DocumentTabs.IsKeyboardFocusWithin)
            {
                Run(DeleteSelectedObjects);
                e.Handled = true;
            }
            else if (e.Key == Key.PageDown && page < Pdf.Count - 1)
            {
                PagesList.SelectedIndex = page + 1;
                e.Handled = true;
            }
            else if (e.Key == Key.PageUp && page > 0)
            {
                PagesList.SelectedIndex = page - 1;
                e.Handled = true;
            }
        }
    }
    void FilesDropped(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
            OpenPaths(files.Where(x => System.IO.Path.GetExtension(x).Equals(".pdf", StringComparison.OrdinalIgnoreCase)).ToArray());
    }
    void WindowClosing(object? sender, CancelEventArgs e)
    {
        bool finished = false;
        Run(() => finished = CanCloseWorkspace());
        e.Cancel = !finished;
    }
    string? Prompt(string title, string label, string initial)
    {
        var input = new TextBox { Text = initial, MinWidth = 320, Margin = new Thickness(0, 7, 0, 12) };
        var panel = new StackPanel { Margin = new Thickness(18) };
        var dialog = new Window { Title = title, Owner = this, SizeToContent = SizeToContent.WidthAndHeight, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = panel };
        panel.Children.Add(new Label { Content = label, Target = input });
        panel.Children.Add(input);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = Ui.T("Cancelar"), IsCancel = true };
        var accept = new Button { Content = Ui.T("Aceptar"), IsDefault = true };
        accept.Click += (_, _) => dialog.DialogResult = true;
        buttons.Children.Add(cancel);
        buttons.Children.Add(accept);
        panel.Children.Add(buttons);
        dialog.Loaded += (_, _) => { input.Focus(); input.SelectAll(); };
        return dialog.ShowDialog() == true ? input.Text : null;
    }
}
