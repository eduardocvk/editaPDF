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
    readonly Action<Exception>? reportError;
    public static Uri FirmaPdfStoreUri { get; } = new("https://apps.microsoft.com/detail/9P5ZK5W4J9ZJ");
    readonly string recoveryFolder; readonly bool restoreOnLoad; Session session = new(); int page; double zoom = 1; bool refreshing; int searchPage = -1, searchOffset; string lastQuery = "";
    public static string RecoveryFolder => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EditorPDF", "Recovery");
    PdfDocument Pdf => session.Document; ObjectRow? Selected => ObjectsList.SelectedItem as ObjectRow;
    public MainWindow(string? localRecovery = null, bool restore = true, Action<Uri>? externalLinkOpener = null, Action<Exception>? errorReporter = null)
    {
        recoveryFolder = localRecovery ?? RecoveryFolder;
        restoreOnLoad = restore;
        reportError = errorReporter;
        openExternal = externalLinkOpener ?? (uri => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }));
        InitializeComponent();
        FontChoice.ItemsSource = new[] { "Conservar original" }.Concat(PdfDocument.IncludedFontFamilies).ToArray();
        FontChoice.SelectedIndex = 1;
        InitializeWorkspace();
        InitializeCanvas();
        Icon = BitmapFrame.Create(new Uri(System.IO.Path.Combine(AppContext.BaseDirectory, "assets", "EditorPDF.ico")));
        HeaderLogo.Source = new BitmapImage(new Uri(System.IO.Path.Combine(AppContext.BaseDirectory, "assets", "logo.png")));
        FontFamily = new FontFamily(new Uri(AppContext.BaseDirectory), "./assets/#Noto Sans");
        Loaded += (_, _) => { if (restoreOnLoad && HasRecovery() && MessageBox.Show(this, "Hay documentos de una sesión local anterior. ¿Recuperarlos?", "Recuperar sesión", MessageBoxButton.YesNo) == MessageBoxResult.Yes) Run(RestoreWorkspace, "Sesión recuperada."); else Refresh(); };
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
        catch (Exception ex) when (ex is not OutOfMemoryException) { Status.Text = ex.Message; if (reportError != null) reportError(ex); else MessageBox.Show(this, ex.Message, "Editor PDF", MessageBoxButton.OK, MessageBoxImage.Warning); }
        finally { Mouse.OverrideCursor = null; }
    }
    void RequireDocument()
    {
        if (Pdf.Count == 0)
            throw new InvalidOperationException("Abre un PDF primero.");
    }
    bool Recover()
    {
        try
        {
            SaveWorkspace();
            return true;
        }
        catch (Exception ex) { Status.Text = "Los cambios siguen en memoria; no se pudo guardar la recuperación local: " + ex.Message; if (reportError != null) reportError(ex); else MessageBox.Show(this, Status.Text, "Recuperación local", MessageBoxButton.OK, MessageBoxImage.Warning); return false; }
    }
    void Change(Action<PdfDocument> mutation, int? selectedPage = null)
    {
        FinishInlineText();
        RequireDocument();
        session.Execute(mutation);
        if (selectedPage.HasValue)
            page = selectedPage.Value;
        Refresh();
        Status.Text = "Cambios sin guardar";
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
        public string Label => Value.Type == 3 ? "Imagen" : string.IsNullOrWhiteSpace(Value.Text) ? "Texto (sin mapa Unicode)" : Value.Text.Replace("\r", " ").Replace("\n", " ");
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
        ZoomLabel.Text = $"{zoom * 100:0} %";
        Status.Text = $"Página {page + 1} de {Pdf.Count}" + (Pdf.EditBlock != null ? " · Solo lectura" : "") + (session.Dirty ? " · Cambios sin guardar" : "");
        if (Pdf.EditBlock != null)
            Status.ToolTip = Pdf.EditBlock;
        else
            Status.ToolTip = null;
    }
    bool LeaveCurrent()
    {
        if (!session.Dirty)
            return true;
        var result = MessageBox.Show(this, "¿Guardar una copia de los cambios antes de continuar?", "Cambios sin guardar", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
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
    void AppendClick(object sender, RoutedEventArgs e) => Run(() => AppendPaths(PickPdfs()));
    bool SaveCopy()
    {
        FinishInlineText();
        RequireDocument();
        var d = new SaveFileDialog { Filter = "PDF (*.pdf)|*.pdf", FileName = "documento_editado.pdf", AddExtension = true, OverwritePrompt = true };
        if (d.ShowDialog(this) != true)
            return false;
        session.SaveNew(d.FileName);
        Refresh();
        Recover();
        Status.Text = "Copia guardada y comprobada.";
        return true;
    }
    void SaveClick(object sender, RoutedEventArgs e) => Run(() => SaveCopy());
    void BlankClick(object sender, RoutedEventArgs e) => Run(() => { if (Pdf.Count == 0) { session.Execute(d => d.Blank(0)); Refresh(); Recover(); } else Change(d => d.Blank(page + 1), page + 1); });
    void DuplicateClick(object sender, RoutedEventArgs e) => Run(() => Change(d => d.Duplicate(page), page + 1));
    void RotateClick(object sender, RoutedEventArgs e) => Run(() => Change(d => d.Rotate(page)));
    void DeletePageClick(object sender, RoutedEventArgs e) => Run(() => Change(d => d.Delete(page)));
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
                case "delete": Change(d => d.Delete(index), Math.Min(index, Pdf.Count - 2)); break;
            }
        });
    }
    void DocumentMouseWheel(object sender, MouseWheelEventArgs e)
    {
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
        int requestedPage = PagesList.SelectedIndex;
        Run(() => { FinishInlineText(); CancelCanvasGesture(false); page = requestedPage; refreshing = true; PagesList.SelectedIndex = page; refreshing = false; ShowPage(); });
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
    void ApplyTextClick(object sender, RoutedEventArgs e) => Run(() => { if (Selected is not { } o || o.Value.Type != 1) throw new InvalidOperationException("Selecciona un objeto de texto."); var text = TextValue.Text; bool bold = BoldChoice.IsChecked == true, italic = ItalicChoice.IsChecked == true; bool originalBold = o.Value.Font.Contains("Bold", StringComparison.OrdinalIgnoreCase), originalItalic = o.Value.Font.Contains("Italic", StringComparison.OrdinalIgnoreCase) || o.Value.Font.Contains("Oblique", StringComparison.OrdinalIgnoreCase); if (FontChoice.SelectedIndex == 0 && (bold != originalBold || italic != originalItalic)) ChooseStyleFont(o.Value); bool substitute = FontChoice.SelectedIndex > 0; if (!substitute && text == o.Value.Text) return; string family = substitute ? (string)FontChoice.SelectedItem : "Noto Sans"; Change(d => d.ReplaceText(page, o.Value.Index, text, substitute, family, bold, italic)); });
    void ApplyStyleClick(object sender, RoutedEventArgs e) => Run(() =>
    {
        if (Selected is not { } row || row.Value.Type != 1)
            throw new InvalidOperationException("Selecciona un objeto de texto.");
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
    void DeleteObjectClick(object sender, RoutedEventArgs e) => Run(() => { if (Selected is not { } o) throw new InvalidOperationException("Selecciona texto o imagen."); Change(d => d.RemoveObject(page, o.Value.Index)); });
    static float Number(string text)
    {
        if (!float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var n) || !float.IsFinite(n))
            throw new ArgumentException("Introduce una medida válida.");
        return n;
    }
    void ApplyPositionClick(object sender, RoutedEventArgs e) => Run(() => { if (Selected is not { } row) throw new InvalidOperationException("Selecciona texto o imagen."); var o = row.Value; float x = Number(XValue.Text), y = Number(YValue.Text), w = Number(WValue.Text), h = Number(HValue.Text); if (w <= 0 || h <= 0 || o.Right <= o.Left || o.Top <= o.Bottom) throw new ArgumentException("Ancho y alto deben ser mayores que cero."); if (o.Type == 1 && (Math.Abs(w - (o.Right - o.Left)) > 0.05 || Math.Abs(h - (o.Top - o.Bottom)) > 0.05)) throw new InvalidOperationException("El cambio de tamaño mediante ancho y alto está disponible para imágenes. El texto puede moverse."); Change(d => d.Transform(page, o.Index, x - o.Left, y - o.Bottom, o.Type == 3 ? w / (o.Right - o.Left) : 1, o.Type == 3 ? h / (o.Top - o.Bottom) : 1)); });
    void AddImageClick(object sender, RoutedEventArgs e) => Run(() => { RequireDocument(); var dialog = new OpenFileDialog { Filter = "Imágenes|*.png;*.jpg;*.jpeg;*.bmp" }; if (dialog.ShowDialog(this) != true) return; BitmapSource image; using (var file = File.OpenRead(LocalFiles.Validate(dialog.FileName))) { var decoder = BitmapDecoder.Create(file, BitmapCreateOptions.IgnoreImageCache, BitmapCacheOption.OnLoad); image = new FormatConvertedBitmap(decoder.Frames[0], PixelFormats.Bgra32, null, 0); } if ((long)image.PixelWidth * image.PixelHeight > 25_000_000) throw new InvalidOperationException("La imagen supera 25 megapíxeles."); int stride = image.PixelWidth * 4; var pixels = new byte[stride * image.PixelHeight]; image.CopyPixels(pixels, stride, 0); FinishInlineText(); float width = Math.Min(240, Pdf.Info(page).Width - 80), height = width * image.PixelHeight / image.PixelWidth; Change(d => d.AddImage(page, image.PixelWidth, image.PixelHeight, stride, pixels, 40, Math.Max(20, Pdf.Info(page).Height - height - 60), width, height)); ObjectsList.SelectedItem = ObjectsList.Items.Cast<ObjectRow>().LastOrDefault(); });
    void PageMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (EditMode.IsSelected != true || Pdf.Count == 0)
            return;
        Run(() =>
        {
            var point = e.GetPosition(PageImage);
            if (areaSelection != null) CancelParagraphSelection();
            if (paragraphArmed) { StartParagraphSelection(point); e.Handled = true; return; }
            if (addTextArmed)
            {
                BeginInlineText(point);
                e.Handled = true;
                return;
            }
            FinishInlineText();
            var p = Pdf.ToPage(page, (int)PageImage.Width, (int)PageImage.Height, (int)point.X, (int)point.Y);
            var hit = ObjectsList.Items.Cast<ObjectRow>().LastOrDefault(x => p.X >= x.Value.Left - 3 && p.X <= x.Value.Right + 3 && p.Y >= x.Value.Bottom - 3 && p.Y <= x.Value.Top + 3);
            if (e.ClickCount == 2 && hit?.Value.Type == 1 && Pdf.ObjectEditBlock == null)
            {
                var bounds = DeviceBounds(hit.Value);
                BeginInlineText(bounds.TopLeft, hit.Value.Text, hit.Value.Index, point);
                e.Handled = true;
                return;
            }
            ObjectsList.SelectedItem = hit;
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
            Run(CommitObjectGesture);
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
    void SearchClick(object sender, RoutedEventArgs e) => Run(() => { RequireDocument(); string q = SearchBox.Text; if (string.IsNullOrWhiteSpace(q)) return; if (q != lastQuery) { searchPage = page; searchOffset = 0; lastQuery = q; } for (int k = 0; k <= Pdf.Count; k++) { int i = (Math.Max(0, searchPage) + k) % Pdf.Count; var text = Pdf.Text(i); int at = text.IndexOf(q, k == 0 ? Math.Min(searchOffset, text.Length) : 0, StringComparison.OrdinalIgnoreCase); if (at < 0) continue; page = i; searchPage = i; searchOffset = at + q.Length; refreshing = true; PagesList.SelectedIndex = page; refreshing = false; ShowPage(); var row = ObjectsList.Items.Cast<ObjectRow>().FirstOrDefault(x => x.Value.Text.Contains(q, StringComparison.OrdinalIgnoreCase)); if (row != null) ObjectsList.SelectedItem = row; Status.Text = $"Encontrado en página {i + 1}."; return; } Status.Text = "No se encontró el texto."; searchPage = page; searchOffset = 0; });
    void SearchKeys(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SearchClick(sender, e);
            e.Handled = true;
        }
    }
    void PrintClick(object sender, RoutedEventArgs e) => Run(() => { RequireDocument(); if (LocalPrinting.Print(this, Pdf)) Status.Text = "Documento enviado a la impresora local seleccionada."; });
    void FirmaPdfClick(object sender, RoutedEventArgs e) => Run(() => openExternal(FirmaPdfStoreUri));
    void PrivacyClick(object sender, RoutedEventArgs e) => MessageBox.Show(this, "Procesamiento local. Sin telemetría, descargas, JavaScript PDF ni servicios remotos.\n\nLa recuperación guarda una copia del PDF en tu perfil local. No se guardan contraseñas. La impresión solo admite puertos locales compatibles.\n\nEl banner FirmaPDF solo abre su ficha en el navegador al pulsarlo, sin documentos, rutas ni datos de sesión.\n\nAGPL-3.0-or-later. Licencias y fuentes incluidas.", "Privacidad", MessageBoxButton.OK, MessageBoxImage.Information);
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
            if (e.Key == Key.W)
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
            if (e.Key == Key.PageDown && page < Pdf.Count - 1)
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
        var cancel = new Button { Content = "Cancelar", IsCancel = true };
        var accept = new Button { Content = "Aceptar", IsDefault = true };
        accept.Click += (_, _) => dialog.DialogResult = true;
        buttons.Children.Add(cancel);
        buttons.Children.Add(accept);
        panel.Children.Add(buttons);
        dialog.Loaded += (_, _) => { input.Focus(); input.SelectAll(); };
        return dialog.ShowDialog() == true ? input.Text : null;
    }
}
