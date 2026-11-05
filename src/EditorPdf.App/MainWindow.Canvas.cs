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
        PositionSelection(DeviceBounds(obj));
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
    public void BeginInlineText(Point point, string initial = "", int? originalIndex = null)
    {
        RequireObjectEditing();
        FinishInlineText();
        string family = FontChoice.SelectedItem as string ?? "Noto Sans";
        bool bold = BoldChoice.IsChecked == true, italic = ItalicChoice.IsChecked == true, substitute = SubstituteFont.IsChecked == true;
        addTextArmed = false;
        PageImage.Cursor = Cursors.Arrow;
        ObjectsList.SelectedItem = null;
        Overlay.Children.Clear();
        resizeHandles.Clear();
        const float size = 14;
        double scale = PageImage.Width / Pdf.Info(page).Width;
        var baseline = Pdf.ToPage(page, (int)PageImage.Width, (int)PageImage.Height, (int)point.X, (int)(point.Y + size * scale * 1.15));
        inlineDraft = new InlineTextDraft(initial, (float)baseline.X, (float)baseline.Y, size, page, originalIndex, family, bold, italic, substitute);
        inlineText = new TextBox { Text = initial, FontFamily = family == "Noto Sans" ? FontFamily : new FontFamily(new Uri(AppContext.BaseDirectory), "./assets/fonts/#" + family), FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, FontStyle = italic ? FontStyles.Italic : FontStyles.Normal, FontSize = size * scale, Padding = new Thickness(1), Margin = new Thickness(0), MinWidth = 140, MinHeight = size * scale * 1.5, MaxWidth = Math.Max(140, PageImage.Width - point.X), Background = new SolidColorBrush(Color.FromArgb(225, 255, 255, 255)), BorderBrush = Brushes.RoyalBlue, BorderThickness = new Thickness(1), AcceptsReturn = false, MaxLength = 10000, ToolTip = "Intro: aplicar · Esc: cancelar" };
        System.Windows.Automation.AutomationProperties.SetName(inlineText, "Texto sobre la página");
        Canvas.SetLeft(inlineText, point.X);
        Canvas.SetTop(inlineText, point.Y);
        Overlay.Children.Add(inlineText);
        inlineText.TextChanged += (_, _) => { if (inlineDraft != null && inlineText != null) inlineDraft = inlineDraft with { Text = inlineText.Text }; inlineRecoveryTimer.Stop(); inlineRecoveryTimer.Start(); };
        inlineText.LostKeyboardFocus += (_, _) => { if (inlineText != null) Run(FinishInlineText); };
        inlineText.PreviewKeyDown += (_, e) => { if (e.Key == Key.Enter) { Run(FinishInlineText); e.Handled = true; } else if (e.Key == Key.Escape) { CancelInlineText(); e.Handled = true; } };
        inlineText.Focus();
        inlineText.CaretIndex = inlineText.Text.Length;
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
    void DisposeCanvas()
    {
        canvasPreviewTimer.Stop();
        inlineRecoveryTimer.Stop();
        ClearGesture();
        inlineText = null;
        inlineDraft = null;
    }
}

public sealed record InlineTextDraft(string Text, float X, float Y, float Size, int Page, int? OriginalIndex = null, string Family = "Noto Sans", bool Bold = false, bool Italic = false, bool Substitute = false);
