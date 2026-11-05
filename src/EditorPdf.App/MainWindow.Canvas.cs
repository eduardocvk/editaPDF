using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using EditorPdf.Core;
using ResizeThumb = System.Windows.Controls.Primitives.Thumb;
namespace EditorPdf.App;

public partial class MainWindow
{
    bool addTextArmed;
    TextBox? inlineText;
    InlineTextDraft? inlineDraft;
    Rectangle? selectionOutline;
    Border? textToolbar;
    ComboBox? toolbarFont;
    TextBox? toolbarSize;
    Border? toolbarColorSwatch;
    readonly List<ResizeThumb> resizeHandles = [];
    PdfDocument? gesturePreview;
    PdfObject? gestureObject;
    Rect gestureStartRect, gestureRect;
    Point gestureStartPoint;
    string? resizeCorner;
    bool gestureChanged;
    readonly DispatcherTimer canvasPreviewTimer = new() { Interval = TimeSpan.FromMilliseconds(35) };
    readonly DispatcherTimer inlineRecoveryTimer = new() { Interval = TimeSpan.FromMilliseconds(700) };
    Point contextPoint;

    void InitializeCanvas()
    {
        canvasPreviewTimer.Tick += (_, _) => { canvasPreviewTimer.Stop(); Run(RenderGesturePreview); };
        inlineRecoveryTimer.Tick += (_, _) => { inlineRecoveryTimer.Stop(); Recover(); };
        PageImage.MouseMove += CanvasMouseMove;
        PageImage.PreviewMouseRightButtonDown += CanvasRightClick;
        PageImage.ContextMenuOpening += CanvasContextOpening;
        PageImage.ContextMenu = new ContextMenu();
        ObjectsList.ContextMenu = PageImage.ContextMenu;
        ObjectsList.ContextMenuOpening += CanvasContextOpening;
        PageImage.LostMouseCapture += (_, _) => { if (gestureObject != null && resizeCorner == null && Mouse.LeftButton == MouseButtonState.Pressed) CancelCanvasGesture(); };
        PageImage.LostMouseCapture += (_, _) => { if (paragraphStart != null && Mouse.LeftButton == MouseButtonState.Pressed) CancelParagraphSelection(); };
    }
    Rect DeviceBounds(PdfObject obj)
    {
        var points = new[] { Device(obj.Left, obj.Bottom), Device(obj.Left, obj.Top), Device(obj.Right, obj.Bottom), Device(obj.Right, obj.Top) };
        return new Rect(new Point(points.Min(p => p.X), points.Min(p => p.Y)), new Point(points.Max(p => p.X), points.Max(p => p.Y)));
    }
    Point Device(double x, double y)
    {
        var p = Pdf.ToDevice(page, (int)PageImage.Width, (int)PageImage.Height, x, y);
        return new Point(p.X, p.Y);
    }
    void DrawObjectSelection(PdfObject obj)
    {
        Overlay.Children.Clear();
        resizeHandles.Clear();
        selectionOutline = new Rectangle { Stroke = Brushes.RoyalBlue, StrokeThickness = 1.5, IsHitTestVisible = false };
        Overlay.Children.Add(selectionOutline);
        if (obj.Type == 3 && EditMode.IsSelected && Pdf.ObjectEditBlock == null)
        {
            foreach (string corner in new[] { "NW", "NE", "SW", "SE" })
            {
                var handle = new ResizeThumb { Width = 11, Height = 11, Background = Brushes.White, BorderBrush = Brushes.RoyalBlue, BorderThickness = new Thickness(1.5), Cursor = corner is "NW" or "SE" ? Cursors.SizeNWSE : Cursors.SizeNESW, Tag = corner, Focusable = true, ToolTip = "Arrastra para redimensionar · Mayús: proporción libre" };
                System.Windows.Automation.AutomationProperties.SetName(handle, "Redimensionar imagen " + corner);
                handle.DragStarted += (_, _) => Run(() => BeginObjectGesture(obj, Mouse.GetPosition(PageImage), corner));
                handle.DragDelta += (_, _) => Run(() => UpdateObjectGesture(Mouse.GetPosition(PageImage), Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)));
                handle.DragCompleted += (_, e) => Run(() => { if (e.Canceled) CancelCanvasGesture(); else CommitObjectGesture(); });
                resizeHandles.Add(handle);
                Overlay.Children.Add(handle);
            }
        }
        if (obj.Type == 1 && EditMode.IsSelected && Pdf.ObjectEditBlock == null)
            BuildTextToolbar(obj);
        PositionSelection(DeviceBounds(obj));
    }
    void BuildTextToolbar(PdfObject obj)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        toolbarFont = new ComboBox { Width = 138, ItemsSource = FontChoice.ItemsSource, SelectedIndex = FontChoice.SelectedIndex, ToolTip = "Fuente del texto", Margin = new Thickness(0, 0, 4, 0) };
        toolbarFont.SelectionChanged += (_, _) => { if (toolbarFont.SelectedIndex >= 0) FontChoice.SelectedIndex = toolbarFont.SelectedIndex; };
        row.Children.Add(toolbarFont);
        toolbarSize = new TextBox { Width = 45, Text = FontSizeValue.Text, Margin = new Thickness(0, 0, 2, 0), Padding = new Thickness(2), ToolTip = "Tamaño en puntos" };
        toolbarSize.TextChanged += (_, _) => FontSizeValue.Text = toolbarSize.Text;
        row.Children.Add(toolbarSize);
        Button Small(string label, string tip, Action click)
        {
            var button = new Button { Content = label, MinWidth = 25, MinHeight = 25, Padding = new Thickness(2), Margin = new Thickness(1, 0, 0, 0), ToolTip = tip };
            System.Windows.Automation.AutomationProperties.SetName(button, tip);
            button.Click += (_, _) => click();
            row.Children.Add(button);
            return button;
        }
        Small("A", "Reducir tamaño de texto", () => { AdjustFontSize(-1); toolbarSize.Text = FontSizeValue.Text; }).FontSize = 10;
        Small("A", "Aumentar tamaño de texto", () => { AdjustFontSize(1); toolbarSize.Text = FontSizeValue.Text; }).FontSize = 17;
        Button? boldButton = null;
        boldButton = Small("B", "Negrita", () => { BoldChoice.IsChecked = BoldChoice.IsChecked != true; if (FontChoice.SelectedIndex == 0) ChooseStyleFont(obj); boldButton!.Background = BoldChoice.IsChecked == true ? new SolidColorBrush(Color.FromRgb(221, 234, 255)) : Brushes.White; });
        boldButton.FontWeight = FontWeights.Bold;
        boldButton.Background = BoldChoice.IsChecked == true ? new SolidColorBrush(Color.FromRgb(221, 234, 255)) : Brushes.White;
        Button? italicButton = null;
        italicButton = Small("I", "Cursiva", () => { ItalicChoice.IsChecked = ItalicChoice.IsChecked != true; if (FontChoice.SelectedIndex == 0) ChooseStyleFont(obj); italicButton!.Background = ItalicChoice.IsChecked == true ? new SolidColorBrush(Color.FromRgb(221, 234, 255)) : Brushes.White; });
        italicButton.FontStyle = FontStyles.Italic;
        italicButton.Background = ItalicChoice.IsChecked == true ? new SolidColorBrush(Color.FromRgb(221, 234, 255)) : Brushes.White;
        var color = Small("", "Elegir color de texto", () => { PickColorClick(this, new RoutedEventArgs()); });
        toolbarColorSwatch = new Border { Width = 20, Height = 14, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), Background = new SolidColorBrush(Color.FromRgb((byte)(obj.FillRgb >> 16), (byte)(obj.FillRgb >> 8), (byte)obj.FillRgb)) };
        color.Content = toolbarColorSwatch;
        Small("✓", "Aplicar propiedades de texto", () => Run(ApplyContextTextProperties));
        textToolbar = new Border { Background = Brushes.White, BorderBrush = Brushes.RoyalBlue, BorderThickness = new Thickness(1), Padding = new Thickness(4), Child = row, Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 5, Opacity = .18 }, LayoutTransform = new ScaleTransform(Math.Clamp(zoom, .35, 1), Math.Clamp(zoom, .35, 1)) };
        System.Windows.Automation.AutomationProperties.SetName(textToolbar, "Barra contextual de texto");
        Canvas.SetZIndex(textToolbar, 10);
        Overlay.Children.Add(textToolbar);
    }
    void ChooseStyleFont(PdfObject obj)
    {
        string name = obj.Font;
        if (name.Length > 7 && name[6] == '+') name = name[7..];
        name = name.Split('-')[0];
        string family = PdfDocument.IncludedFontFamilies.FirstOrDefault(f => f != "Noto Sans" && f.Replace(" ", "").Equals(name, StringComparison.OrdinalIgnoreCase)) ?? "Liberation Sans";
        FontChoice.SelectedItem = family;
        if (toolbarFont != null) toolbarFont.SelectedItem = family;
        Status.Text = $"Se usará {family} para el estilo elegido.";
    }
    void ApplyContextTextProperties()
    {
        if (Selected is not { } selection || selection.Value.Type != 1) throw new InvalidOperationException("Selecciona texto.");
        var original = selection.Value;
        string value = TextValue.Text;
        bool requestedBold = BoldChoice.IsChecked == true, requestedItalic = ItalicChoice.IsChecked == true;
        bool originalBold = original.Font.Contains("Bold", StringComparison.OrdinalIgnoreCase), originalItalic = original.Font.Contains("Italic", StringComparison.OrdinalIgnoreCase) || original.Font.Contains("Oblique", StringComparison.OrdinalIgnoreCase);
        if (FontChoice.SelectedIndex == 0 && (requestedBold != originalBold || requestedItalic != originalItalic)) ChooseStyleFont(original);
        bool substitute = FontChoice.SelectedIndex > 0;
        float size = Number(FontSizeValue.Text);
        string colorText = ColorValue.Text.Trim();
        if (colorText.Length != 7 || colorText[0] != '#' || !uint.TryParse(colorText.AsSpan(1), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out uint rgb))
            throw new ArgumentException("Selecciona un color válido.");
        bool textChange = substitute || value != original.Text;
        bool styleChange = Math.Abs(size - original.Size) > .001 || rgb != original.FillRgb;
        if (!textChange && !styleChange) return;
        int index = original.Index;
        Change(d =>
        {
            if (textChange)
            {
                string family = substitute ? (string)FontChoice.SelectedItem : "Noto Sans";
                d.ReplaceText(page, index, value, substitute, family, BoldChoice.IsChecked == true, ItalicChoice.IsChecked == true);
                if (substitute) index = d.Objects(page).Last(o => o.Type == 1).Index;
            }
            if (styleChange) d.SetTextStyle(page, index, size, rgb);
        });
        SelectObject(index);
    }
    void PositionSelection(Rect bounds)
    {
        if (selectionOutline == null)
            return;
        selectionOutline.Width = Math.Max(2, bounds.Width);
        selectionOutline.Height = Math.Max(2, bounds.Height);
        Canvas.SetLeft(selectionOutline, bounds.Left);
        Canvas.SetTop(selectionOutline, bounds.Top);
        foreach (var handle in resizeHandles)
        {
            string corner = (string)handle.Tag;
            Canvas.SetLeft(handle, (corner.EndsWith('W') ? bounds.Left : bounds.Right) - 5.5);
            Canvas.SetTop(handle, (corner.StartsWith('N') ? bounds.Top : bounds.Bottom) - 5.5);
        }
        if (textToolbar != null && Overlay.Children.Contains(textToolbar))
        {
            double scale = Math.Clamp(zoom, .35, 1);
            Canvas.SetLeft(textToolbar, Math.Clamp(bounds.Left, 0, Math.Max(0, Overlay.Width - 370 * scale)));
            Canvas.SetTop(textToolbar, bounds.Top >= 46 * scale ? bounds.Top - 43 * scale : Math.Min(Overlay.Height - 36 * scale, bounds.Bottom + 6));
        }
    }
    void RequireObjectEditing()
    {
        RequireDocument();
        if (Pdf.ObjectEditBlock is { } reason)
            throw new InvalidOperationException(reason);
    }
    public void BeginObjectGesture(PdfObject obj, Point pointer, string? corner = null)
    {
        RequireObjectEditing();
        FinishInlineText();
        CancelCanvasGesture(false);
        if (corner != null && (obj.Right <= obj.Left || obj.Top <= obj.Bottom))
            throw new InvalidOperationException("Esta imagen no tiene un recuadro redimensionable.");
        gesturePreview = new PdfDocument(Pdf.SaveBytes());
        gestureObject = obj;
        resizeCorner = corner;
        gestureStartPoint = pointer;
        gestureStartRect = gestureRect = DeviceBounds(obj);
        gestureChanged = false;
    }
    public void UpdateObjectGesture(Point pointer, bool freeAspect = false)
    {
        if (gestureObject == null)
            return;
        Vector delta = pointer - gestureStartPoint;
        if (resizeCorner == null)
            gestureRect = new Rect(gestureStartRect.TopLeft + delta, gestureStartRect.Size);
        else
        {
            bool west = resizeCorner.EndsWith('W'), north = resizeCorner.StartsWith('N');
            double w = Math.Max(8, gestureStartRect.Width + delta.X * (west ? -1 : 1));
            double h = Math.Max(8, gestureStartRect.Height + delta.Y * (north ? -1 : 1));
            if (!freeAspect)
            {
                double rw = w / Math.Max(1, gestureStartRect.Width), rh = h / Math.Max(1, gestureStartRect.Height);
                double factor = Math.Abs(rw - 1) >= Math.Abs(rh - 1) ? rw : rh;
                w = gestureStartRect.Width * factor;
                h = gestureStartRect.Height * factor;
            }
            gestureRect = new Rect(west ? gestureStartRect.Right - w : gestureStartRect.Left, north ? gestureStartRect.Bottom - h : gestureStartRect.Top, w, h);
        }
        gestureChanged = (gestureRect.TopLeft - gestureStartRect.TopLeft).Length > 1 || Math.Abs(gestureRect.Width - gestureStartRect.Width) > 1 || Math.Abs(gestureRect.Height - gestureStartRect.Height) > 1;
        PositionSelection(gestureRect);
        if (!canvasPreviewTimer.IsEnabled)
            canvasPreviewTimer.Start();
    }
    (float Dx, float Dy, float Sx, float Sy) GestureTransform()
    {
        if (resizeCorner == null)
        {
            var delta = gestureRect.TopLeft - gestureStartRect.TopLeft;
            var start = Pdf.ToPage(page, (int)PageImage.Width, (int)PageImage.Height, (int)Math.Round(gestureStartPoint.X), (int)Math.Round(gestureStartPoint.Y));
            var end = Pdf.ToPage(page, (int)PageImage.Width, (int)PageImage.Height, (int)Math.Round(gestureStartPoint.X + delta.X), (int)Math.Round(gestureStartPoint.Y + delta.Y));
            return ((float)(end.X - start.X), (float)(end.Y - start.Y), 1, 1);
        }
        var a = Pdf.ToPage(page, (int)PageImage.Width, (int)PageImage.Height, (int)Math.Round(gestureRect.Left), (int)Math.Round(gestureRect.Top));
        var b = Pdf.ToPage(page, (int)PageImage.Width, (int)PageImage.Height, (int)Math.Round(gestureRect.Right), (int)Math.Round(gestureRect.Bottom));
        var obj = gestureObject!;
        return ((float)Math.Min(a.X, b.X) - obj.Left, (float)Math.Min(a.Y, b.Y) - obj.Bottom,
            resizeCorner == null ? 1 : (float)Math.Abs(a.X - b.X) / (obj.Right - obj.Left),
            resizeCorner == null ? 1 : (float)Math.Abs(a.Y - b.Y) / (obj.Top - obj.Bottom));
    }
    public void RenderGesturePreview()
    {
        canvasPreviewTimer.Stop();
        if (gestureObject == null || gesturePreview == null)
            return;
        if (!gestureChanged)
        {
            PageImage.Source = Bitmap(Pdf.Render(page, Math.Min(1400, (int)PageImage.Width)));
            XValue.Text = gestureObject.Left.ToString("0.##");
            YValue.Text = gestureObject.Bottom.ToString("0.##");
            WValue.Text = (gestureObject.Right - gestureObject.Left).ToString("0.##");
            HValue.Text = (gestureObject.Top - gestureObject.Bottom).ToString("0.##");
            return;
        }
        var t = GestureTransform();
        var previous = gesturePreview.Objects(page).Single(o => o.Index == gestureObject.Index);
        gesturePreview.Transform(page, gestureObject.Index, gestureObject.Left + t.Dx - previous.Left, gestureObject.Bottom + t.Dy - previous.Bottom,
            (gestureObject.Right - gestureObject.Left) * t.Sx / (previous.Right - previous.Left), (gestureObject.Top - gestureObject.Bottom) * t.Sy / (previous.Top - previous.Bottom));
        PageImage.Source = Bitmap(gesturePreview.RenderPreview(page, Math.Min(1400, (int)PageImage.Width)));
        var value = gesturePreview.Objects(page).Single(o => o.Index == gestureObject.Index);
        XValue.Text = value.Left.ToString("0.##");
        YValue.Text = value.Bottom.ToString("0.##");
        WValue.Text = (value.Right - value.Left).ToString("0.##");
        HValue.Text = (value.Top - value.Bottom).ToString("0.##");
    }
    public void CommitObjectGesture()
    {
        if (gestureObject == null)
            return;
        int index = gestureObject.Index;
        bool changed = gestureChanged;
        var t = GestureTransform();
        ClearGesture();
        PageImage.ReleaseMouseCapture();
        if (changed)
            Change(d => d.Transform(page, index, t.Dx, t.Dy, t.Sx, t.Sy));
        else
            ShowPage();
        SelectObject(index);
        if (Selected is { } selected) DrawObjectSelection(selected.Value);
    }
    void ClearGesture()
    {
        canvasPreviewTimer.Stop();
        gestureObject = null;
        gesturePreview?.Dispose();
        gesturePreview = null;
        resizeCorner = null;
        gestureChanged = false;
    }
    public void CancelCanvasGesture(bool redraw = true)
    {
        if (gestureObject == null)
            return;
        int index = gestureObject.Index;
        ClearGesture();
        if (Mouse.Captured is ResizeThumb handle && resizeHandles.Contains(handle))
            handle.CancelDrag();
        PageImage.ReleaseMouseCapture();
        if (redraw)
        {
            ShowPage();
            SelectObject(index);
            if (Selected is { } selected) DrawObjectSelection(selected.Value);
        }
    }
    void SelectObject(int index)
    {
        ObjectsList.SelectedItem = ObjectsList.Items.Cast<ObjectRow>().FirstOrDefault(o => o.Value.Index == index);
        if (Selected is { } selected) DrawObjectSelection(selected.Value);
    }
    void CanvasMouseMove(object sender, MouseEventArgs e)
    {
        if (paragraphStart != null && e.LeftButton == MouseButtonState.Pressed)
            UpdateParagraphSelection(e.GetPosition(PageImage));
        if (gestureObject != null && resizeCorner == null && e.LeftButton == MouseButtonState.Pressed)
            Run(() => UpdateObjectGesture(e.GetPosition(PageImage)));
    }
    void ArmTextInsertion()
    {
        RequireObjectEditing();
        FinishInlineText();
        CancelCanvasGesture();
        EditMode.IsSelected = true;
        addTextArmed = true;
        PageImage.Cursor = Cursors.IBeam;
        Status.Text = "Haz clic en la página para escribir.";
    }
    public void BeginInlineText(Point point, string initial = "", int? originalIndex = null, Point? caretPoint = null)
    {
        RequireObjectEditing();
        FinishInlineText();
        var originalObject = originalIndex is int selectedIndex ? Pdf.Objects(page).FirstOrDefault(o => o.Index == selectedIndex && o.Type == 1) : null;
        if (originalIndex != null && originalObject == null) throw new InvalidOperationException("El texto original ya no está disponible.");
        if (originalObject != null && (!originalObject.HorizontalText || Pdf.Info(page).Rotation != 0))
            throw new InvalidOperationException("Este texto girado se edita desde el panel de propiedades.");
        if (originalObject != null) point = DeviceBounds(originalObject).TopLeft;
        bool substitute = originalIndex != null && FontChoice.SelectedIndex > 0;
        string family = FontChoice.SelectedIndex > 0 ? (string)FontChoice.SelectedItem : "Noto Sans";
        bool bold = BoldChoice.IsChecked == true, italic = ItalicChoice.IsChecked == true;
        addTextArmed = false;
        PageImage.Cursor = Cursors.Arrow;
        if (originalObject == null) ObjectsList.SelectedItem = null;
        Overlay.Children.Clear();
        resizeHandles.Clear();
        float size = originalObject?.VisualSize is > 0 ? originalObject.VisualSize : 14;
        double scale = PageImage.Width / Pdf.Info(page).Width;
        var baseline = Pdf.ToPage(page, (int)PageImage.Width, (int)PageImage.Height, (int)point.X, (int)(point.Y + size * scale * 1.15));
        inlineDraft = new InlineTextDraft(initial, (float)baseline.X, (float)baseline.Y, size, page, originalIndex, family, bold, italic, substitute, originalObject?.Text);
        FontFamily displayFamily = family == "Noto Sans" ? FontFamily : new FontFamily(new Uri(AppContext.BaseDirectory), "./assets/fonts/#" + family);
        if (originalObject != null && !substitute)
        {
            string name = originalObject.Font;
            if (name.Length > 7 && name[6] == '+') name = name[7..];
            name = name.Split('-')[0];
            name = name.StartsWith("Helvetica", StringComparison.OrdinalIgnoreCase) ? "Arial" : name.StartsWith("Times", StringComparison.OrdinalIgnoreCase) ? "Times New Roman" : name.StartsWith("Courier", StringComparison.OrdinalIgnoreCase) ? "Courier New" : name;
            var included = PdfDocument.IncludedFontFamilies.FirstOrDefault(f => f.Replace(" ", "").Equals(name, StringComparison.OrdinalIgnoreCase));
            if (included != null) displayFamily = included == "Noto Sans" ? FontFamily : new FontFamily(new Uri(AppContext.BaseDirectory), "./assets/fonts/#" + included);
            else if (!string.IsNullOrWhiteSpace(name)) displayFamily = new FontFamily(name);
        }
        double textWidth = originalObject == null ? 140 : Math.Max(30, DeviceBounds(originalObject).Width + 10);
        inlineText = new TextBox { Text = initial, FontFamily = displayFamily, FontWeight = bold || originalObject?.Font.Contains("Bold", StringComparison.OrdinalIgnoreCase) == true ? FontWeights.Bold : FontWeights.Normal, FontStyle = italic || originalObject?.Font.Contains("Italic", StringComparison.OrdinalIgnoreCase) == true ? FontStyles.Italic : FontStyles.Normal, Foreground = originalObject == null ? Brushes.Black : new SolidColorBrush(Color.FromRgb((byte)(originalObject.FillRgb >> 16), (byte)(originalObject.FillRgb >> 8), (byte)originalObject.FillRgb)), FontSize = size * scale, Padding = new Thickness(1), Margin = new Thickness(0), MinWidth = textWidth, MinHeight = size * scale * 1.3, MaxWidth = Math.Max(textWidth, PageImage.Width - point.X), Background = Brushes.Transparent, BorderBrush = Brushes.RoyalBlue, BorderThickness = new Thickness(1), AcceptsReturn = false, MaxLength = 10000, ToolTip = "Intro: aplicar · Esc: cancelar" };
        if (originalObject != null)
        {
            using var blanked = new PdfDocument(Pdf.SaveBytes());
            blanked.RemoveObject(page, originalObject.Index);
            PageImage.Source = Bitmap(blanked.Render(page, (int)PageImage.Width));
        }
        System.Windows.Automation.AutomationProperties.SetName(inlineText, "Texto sobre la página");
        Canvas.SetLeft(inlineText, point.X);
        Canvas.SetTop(inlineText, point.Y);
        Overlay.Children.Add(inlineText);
        inlineText.TextChanged += (_, _) => { if (inlineDraft != null && inlineText != null) inlineDraft = inlineDraft with { Text = inlineText.Text }; inlineRecoveryTimer.Stop(); inlineRecoveryTimer.Start(); };
        inlineText.LostKeyboardFocus += (_, _) => { if (inlineText != null) Run(FinishInlineText); };
        inlineText.PreviewKeyDown += (_, e) => { if (e.Key == Key.Enter) { Run(FinishInlineText); e.Handled = true; } else if (e.Key == Key.Escape) { CancelInlineText(); e.Handled = true; } };
        inlineText.Focus();
        if (caretPoint is { } clicked)
            Dispatcher.BeginInvoke(() => { if (inlineText != null) { inlineText.UpdateLayout(); inlineText.CaretIndex = Math.Max(0, inlineText.GetCharacterIndexFromPoint(new Point(clicked.X - point.X, clicked.Y - point.Y), true)); } });
        else inlineText.CaretIndex = inlineText.Text.Length;
        Status.Text = "Escribe en la página · Intro para aplicar · Esc para cancelar.";
    }
    public string? InlineTextValue
    {
        get => inlineText?.Text; set
        {
            if (inlineText != null)
                inlineText.Text = value ?? "";
        }
    }
    public void FinishInlineText()
    {
        if (inlineText == null || inlineDraft == null)
            return;
        var draft = inlineDraft with
        {
            Text = inlineText.Text
        };
        if (draft.OriginalIndex is int unchangedIndex && draft.Text == (draft.OriginalText ?? Pdf.Objects(draft.Page).FirstOrDefault(o => o.Index == unchangedIndex)?.Text))
        {
            inlineRecoveryTimer.Stop();
            inlineText = null;
            inlineDraft = null;
            ShowPage();
            SelectObject(unchangedIndex);
            Recover();
            return;
        }
        // Validate on a disposable candidate first; leave the editor intact on an error.
        if (!string.IsNullOrWhiteSpace(draft.Text))
        {
            using var candidate = new PdfDocument(Pdf.SaveBytes());
            if (draft.OriginalIndex is int original) candidate.ReplaceText(draft.Page, original, draft.Text, draft.Substitute, draft.Family, draft.Bold, draft.Italic);
            else candidate.AddText(draft.Page, draft.Text, draft.Size, draft.X, draft.Y, Pdf.Info(draft.Page).Rotation, draft.Family, draft.Bold, draft.Italic);
        }
        inlineRecoveryTimer.Stop();
        inlineText = null;
        inlineDraft = null;
        Overlay.Children.Clear();
        if (!string.IsNullOrWhiteSpace(draft.Text))
        {
            var originalDocument = Pdf;
            try
            {
                if (draft.OriginalIndex is int original) Change(d => d.ReplaceText(draft.Page, original, draft.Text, draft.Substitute, draft.Family, draft.Bold, draft.Italic));
                else Change(d => d.AddText(draft.Page, draft.Text, draft.Size, draft.X, draft.Y, Pdf.Info(draft.Page).Rotation, draft.Family, draft.Bold, draft.Italic));
            }
            catch
            {
                if (ReferenceEquals(Pdf, originalDocument))
                {
                    var position = Device(draft.X, draft.Y);
                    BeginInlineText(new Point(position.X, position.Y - draft.Size * PageImage.Width / Pdf.Info(page).Width * 1.15), draft.Text, draft.OriginalIndex);
                    inlineDraft = draft;
                }
                throw;
            }
            ObjectsList.SelectedItem = ObjectsList.Items.Cast<ObjectRow>().LastOrDefault();
        }
        else
        {
            ShowPage();
            Recover();
        }
    }
    public void CancelInlineText()
    {
        inlineRecoveryTimer.Stop();
        inlineText = null;
        inlineDraft = null;
        addTextArmed = false;
        PageImage.Cursor = Cursors.Arrow;
        Overlay.Children.Clear();
        if (Pdf.Count > 0)
            ShowPage();
        Recover();
    }
    void CanvasRightClick(object sender, MouseButtonEventArgs e)
    {
        if (Pdf.Count == 0)
            return;
        Run(() =>
        {
            FinishInlineText();
            CancelCanvasGesture();
            EditMode.IsSelected = true;
            contextPoint = e.GetPosition(PageImage);
            var p = Pdf.ToPage(page, (int)PageImage.Width, (int)PageImage.Height, (int)contextPoint.X, (int)contextPoint.Y);
            ObjectsList.SelectedItem = ObjectsList.Items.Cast<ObjectRow>().LastOrDefault(o => p.X >= o.Value.Left && p.X <= o.Value.Right && p.Y >= o.Value.Bottom && p.Y <= o.Value.Top);
        });
    }
    void CanvasContextOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender == ObjectsList && Selected is { } objectRow)
            contextPoint = DeviceBounds(objectRow.Value).TopLeft;
        var menu = PageImage.ContextMenu!;
        menu.Items.Clear();
        void Item(string label, Action action, bool enabled = true, string icon = "Check")
        {
            var item = new MenuItem { Header = label, IsEnabled = enabled, Icon = ToolButton.MakeIcon(icon) };
            item.Click += (_, _) => Run(action);
            menu.Items.Add(item);
        }
        bool editable = Pdf.Count > 0 && Pdf.ObjectEditBlock == null;
        Item("Añadir texto aquí", () => BeginInlineText(contextPoint), editable, "Text");
        Item("Insertar imagen…", () => AddImageClick(sender, new RoutedEventArgs()), editable, "Image");
        if (Selected is { } row)
        {
            menu.Items.Add(new Separator());
            if (row.Value.Type == 1) Item("Editar texto", () => BeginInlineText(DeviceBounds(row.Value).TopLeft, row.Value.Text, row.Value.Index), editable, "Text");
            if (row.Value.Type == 1) Item("Editar párrafo…", ArmParagraphSelection, editable, "Text");
            Item("Traer al frente", () => OrderSelectedObject(1, true), editable, "Up");
            Item("Adelantar", () => OrderSelectedObject(1), editable, "Up");
            Item("Atrasar", () => OrderSelectedObject(-1), editable, "Down");
            Item("Enviar al fondo", () => OrderSelectedObject(-1, true), editable, "Down");
            menu.Items.Add(new Separator());
            Item("Propiedades", () => (row.Value.Type == 1 ? TextValue : XValue).Focus(), true, "Position");
            Item("Eliminar objeto", () => Change(d => d.RemoveObject(page, row.Value.Index)), editable, "Delete");
            Item("Deseleccionar", () => ObjectsList.SelectedItem = null, true, "Check");
        }
        menu.Items.Add(new Separator());
        Item("Deshacer", () => UndoClick(sender, new RoutedEventArgs()), session.CanUndo, "Down");
        Item("Rehacer", () => RedoClick(sender, new RoutedEventArgs()), session.CanRedo, "Up");
    }
    public void OrderSelectedObject(int direction, bool extreme = false)
    {
        if (Selected is not { } row)
            return;
        int target = row.Value.Index;
        Change(d => target = d.OrderObject(page, row.Value.Index, direction, extreme));
        SelectObject(target);
    }
    void EditParagraphClick(object sender, RoutedEventArgs e) => Run(ArmParagraphSelection);
    void DisposeCanvas()
    {
        canvasPreviewTimer.Stop();
        inlineRecoveryTimer.Stop();
        ClearGesture();
        inlineText = null;
        inlineDraft = null;
    }
}

public sealed record InlineTextDraft(string Text, float X, float Y, float Size, int Page, int? OriginalIndex = null, string Family = "Noto Sans", bool Bold = false, bool Italic = false, bool Substitute = false, string? OriginalText = null);
