using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using EditorPdf.App;
using EditorPdf.Core;

static class CanvasChecks
{
    public static void Run(MainWindow window, string folder, string fixture, Action<string, int, int, double> capture)
    {
        Session Session() => (Session)typeof(MainWindow).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
        var session = Session();
        ((TabItem)window.FindName("EditMode")).IsSelected = true;
        window.BeginInlineText(new Point(90, 180));
        window.InlineTextValue = "Texto escrito sobre la pagina";
        if (session.Document.Text(0).Contains("Texto escrito sobre la pagina"))
            throw new Exception("Un borrador sin confirmar no debe modificar el PDF");
        if (!((Canvas)window.FindName("Overlay")).Children.OfType<TextBox>().Any(t => t.Text.Contains("Texto escrito")))
            throw new Exception("El texto no está visible en el lienzo mientras se escribe");
        capture("texto-en-pagina", 1280, 780, 1);
        typeof(MainWindow).GetMethod("SaveWorkspace", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, null);
        var recovery = (string)typeof(MainWindow).GetField("recoveryFolder", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(window)!;
        var recovered = new MainWindow(recovery, false, _ => { }, e => throw e);
        typeof(MainWindow).GetMethod("RestoreWorkspace", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(recovered, null);
        if (recovered.InlineTextValue != "Texto escrito sobre la pagina")
            throw new Exception("No se recupera el borrador en página");
        typeof(MainWindow).GetMethod("DisposeWorkspace", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(recovered, null);
        window.FinishInlineText();
        if (!session.Document.Text(0).Contains("Texto escrito sobre la pagina"))
            throw new Exception("No se confirma texto sobre el PDF");
        session.Undo();
        if (session.Document.Text(0).Contains("Texto escrito sobre la pagina"))
            throw new Exception("No se deshace el texto insertado");
        session.Redo();
        session.Execute(d => d.AddImage(0, 4, 4, 16, Enumerable.Range(0, 64).Select(i => (byte)(i % 4 is 1 or 3 ? 255 : 0)).ToArray(), 100, 400, 100, 60));
        typeof(MainWindow).GetMethod("Refresh", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, null);
        var objects = (ListBox)window.FindName("ObjectsList");
        var row = objects.Items.Cast<MainWindow.ObjectRow>().Last(o => o.Value.Type == 3);
        objects.SelectedItem = row;
        capture("imagen-seleccionada", 1280, 780, 1);
        var overlay = (Canvas)window.FindName("Overlay");
        var handles = overlay.Children.OfType<System.Windows.Controls.Primitives.Thumb>().ToArray();
        if (handles.Length != 4)
            throw new Exception("La imagen debe tener cuatro esquinas interactivas");
        if (handles.Any(h => !h.IsHitTestVisible || !ReferenceEquals(System.Windows.Media.VisualTreeHelper.GetParent(h), overlay)))
            throw new Exception("Las esquinas no están conectadas al lienzo interactivo");
        var image = (Image)window.FindName("PageImage");
        var original = session.Document;
        var raster = SHA256.HashData(original.Render(0, 600).Pixels);
        window.BeginObjectGesture(row.Value, new Point(100, 200));
        window.UpdateObjectGesture(new Point(145, 230));
        window.RenderGesturePreview();
        if (!ReferenceEquals(original, session.Document) || !raster.SequenceEqual(SHA256.HashData(session.Document.Render(0, 600).Pixels)))
            throw new Exception("La vista previa modifica el documento antes de soltar");
        if (image.Source is not BitmapSource)
            throw new Exception("No se renderiza vista previa del arrastre");
        var previewImage = (BitmapSource)image.Source;
        byte[] previewPixels = new byte[previewImage.PixelWidth * previewImage.PixelHeight * 4];
        previewImage.CopyPixels(previewPixels, previewImage.PixelWidth * 4, 0);
        var referenceImage = session.Document.Render(0, previewImage.PixelWidth);
        if (SHA256.HashData(previewPixels).SequenceEqual(SHA256.HashData(referenceImage.Pixels)))
            throw new Exception("La imagen no se mueve visualmente durante el arrastre");
        capture("movimiento-en-vivo", 1280, 780, 1);
        window.CancelCanvasGesture();
        if (!ReferenceEquals(original, session.Document))
            throw new Exception("Cancelar el arrastre modifica la sesión");
        var textRow = objects.Items.Cast<MainWindow.ObjectRow>().First(o => o.Value.Text == "Texto escrito sobre la pagina");
        objects.SelectedItem = textRow;
        window.BeginObjectGesture(textRow.Value, new Point(150, 200));
        window.UpdateObjectGesture(new Point(175, 210));
        window.RenderGesturePreview();
        window.CommitObjectGesture();
        if (Math.Abs(session.Document.Objects(0).Single(o => o.Text == textRow.Value.Text).Left - textRow.Value.Left) < 10)
            throw new Exception("No se mueve el texto con vista previa");
        row = objects.Items.Cast<MainWindow.ObjectRow>().Last(o => o.Value.Type == 3);
        objects.SelectedItem = row;
        window.BeginObjectGesture(row.Value, new Point(100, 200));
        window.UpdateObjectGesture(new Point(145, 230));
        window.RenderGesturePreview();
        window.CommitObjectGesture();
        var moved = session.Document.Objects(0).Last(o => o.Type == 3);
        if (Math.Abs(moved.Left - row.Value.Left) < 10)
            throw new Exception("El arrastre no cambia la posición al soltar");
        objects.SelectedItem = objects.Items.Cast<MainWindow.ObjectRow>().Last(o => o.Value.Type == 3);
        var p1 = session.Document.ToDevice(0, (int)image.Width, (int)image.Height, moved.Left, moved.Bottom);
        var p2 = session.Document.ToDevice(0, (int)image.Width, (int)image.Height, moved.Right, moved.Top);
        var start = new Point(Math.Max(p1.X, p2.X), Math.Max(p1.Y, p2.Y));
        window.BeginObjectGesture(moved, start, "SE");
        window.UpdateObjectGesture(start + new Vector(Math.Abs(p1.X - p2.X) * .5, Math.Abs(p1.Y - p2.Y) * .5));
        window.RenderGesturePreview();
        capture("esquinas-redimensionar", 1280, 780, 1);
        window.CommitObjectGesture();
        var resized = session.Document.Objects(0).Last(o => o.Type == 3);
        if (Math.Abs((resized.Right - resized.Left) / (moved.Right - moved.Left) - 1.5) > .04)
            throw new Exception("Las esquinas no escalan la imagen proporcionalmente");
        typeof(MainWindow).GetMethod("CanvasContextOpening", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, [image, null]);
        if (!image.ContextMenu!.Items.OfType<MenuItem>().Any(i => Equals(i.Header, "Traer al frente")))
            throw new Exception("Falta menú contextual de orden");
        session.SaveNew(Path.Combine(folder, "edicion-canvas.pdf"));
        var picker = new PageSelectionDialog(session.Document, "Elegir páginas", 0, session.Document.Count);
        picker.SetSelection(false);
        picker.Choices[1].Chosen = true;
        if (!picker.SelectedPages.SequenceEqual([1]))
            throw new Exception("El selector visual no conserva la selección");
        CapturePicker(picker, Path.Combine(folder, "selector-paginas.png"));
        window.ExtractSelectedPages(picker.SelectedPages, Path.Combine(folder, "extraido-visual.pdf"));
        using (var extracted = new PdfDocument(File.ReadAllBytes(Path.Combine(folder, "extraido-visual.pdf"))))
            if (extracted.Count != 1 || !extracted.Text(0).Contains("Segunda pagina"))
                throw new Exception("La extracción visual no guarda las páginas elegidas");
        int count = session.Document.Count;
        window.InsertSelectedPages(fixture, [1], 0);
        if (session.Document.Count != count + 1 || !session.Document.Text(0).Contains("Segunda pagina"))
            throw new Exception("Insertar páginas elegidas visualmente falla");
        window.BeginInlineText(new Point(50, 150));
        window.InlineTextValue = "Cancelar este borrador";
        window.CancelInlineText();
        if (session.Document.Text(0).Contains("Cancelar este borrador"))
            throw new Exception("Cancelar conserva texto no confirmado");
        var editableText = session.Document.Objects(0).First(o => o.Type == 1 && o.Text.Contains("Segunda pagina"));
        Console.WriteLine($"CHECK texto original: fuente={editableText.Font}, tamaño={editableText.Size:0.##}, visual={editableText.VisualSize:0.##}, alto={editableText.Top - editableText.Bottom:0.##}");
        window.BeginInlineText(new Point(55, 80), editableText.Text, editableText.Index);
        var editor = overlay.Children.OfType<TextBox>().Single();
        if (Math.Abs(editor.FontSize - editableText.Size * image.Width / session.Document.Info(0).Width) > .2 || Math.Abs(Canvas.GetLeft(editor) - session.Document.ToDevice(0, (int)image.Width, (int)image.Height, editableText.Left, editableText.Top).X) > 3)
            throw new Exception("El editor directo cambia el tamaño o la posición del texto original");
        capture("texto-original-en-sitio", 1280, 780, 1);
        window.InlineTextValue = editableText.Text + " editado";
        window.FinishInlineText();
        if (!session.Document.Objects(0).Any(o => o.Text == editableText.Text + " editado"))
            throw new Exception("La edición directa del texto original no se guarda: " + string.Join(" | ", session.Document.Objects(0).Where(o => o.Type == 1).Select(o => o.Text)));
        var chosen = objects.Items.Cast<MainWindow.ObjectRow>().First(o => o.Value.Text == editableText.Text + " editado");
        objects.SelectedItem = chosen;
        var fonts = (ComboBox)window.FindName("FontChoice");
        if (fonts.SelectedIndex != 0 || (string)fonts.SelectedItem != "Conservar original")
            throw new Exception("La fuente original debe mantenerse al seleccionar texto");
        fonts.SelectedItem = "Liberation Sans";
        ((TextBox)window.FindName("TextValue")).Text = "Numero 12345";
        typeof(MainWindow).GetMethod("ApplyTextClick", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, [window, new RoutedEventArgs()]);
        if (!session.Document.Objects(0).Any(o => o.Text == "Numero 12345" && o.Font.Contains("Liberation", StringComparison.OrdinalIgnoreCase)))
            throw new Exception("La fuente elegida no sustituye la original ni conserva el número");
        using (var fontDocument = new PdfDocument(File.ReadAllBytes(fixture)))
        {
            int n = 0;
            foreach (var family in PdfDocument.IncludedFontFamilies)
                fontDocument.AddText(0, "Fuente " + ++n, 14, 40, 300 + n * 25, 0, family, false, false);
            fontDocument.AddText(0, "Negrita cursiva", 14, 40, 490, 0, "Liberation Sans", true, true);
            var saved = Path.Combine(folder, "fuentes-incluidas.pdf");
            fontDocument.SaveNew(saved);
            using var reopened = new PdfDocument(File.ReadAllBytes(saved));
            if (!reopened.Text(0).Contains("Fuente 10") || !reopened.Text(0).Contains("Negrita cursiva"))
                throw new Exception("Las fuentes incluidas no sobreviven al guardado");
        }
        var sizeField = (TextBox)window.FindName("FontSizeValue");
        sizeField.Text = "14";
        typeof(MainWindow).GetMethod("AdjustFontSize", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, [1]);
        if (sizeField.Text != "15") throw new Exception("La flecha no aumenta el tamaño de letra");
        var colors = new ColorPickerDialog(window, 0x123456);
        var swatches = (System.Windows.Controls.Primitives.UniformGrid)typeof(ColorPickerDialog).GetField("palette", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(colors)!;
        if (swatches.Children.Count != 144) throw new Exception("Faltan colores en la paleta visual");
        ((Button)swatches.Children[swatches.Children.Count - 1]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (colors.SelectedColor == 0x123456) throw new Exception("La paleta no selecciona colores");
        CaptureDialog(colors, Path.Combine(folder, "selector-color.png"), 475, 440);
        var paragraphDialog = new ParagraphEditorDialog(window, "Una línea\nOtra línea", 240, 2);
        if (paragraphDialog.ParagraphText != "Una línea\nOtra línea" || paragraphDialog.FontChoice != null || paragraphDialog.ParagraphWidth != 240)
            throw new Exception("El editor de párrafos no presenta el texto y la fuente original");
        CaptureDialog(paragraphDialog, Path.Combine(folder, "editor-parrafo.png"), 650, 480);
        Console.WriteLine("PASS lienzo: texto visible, recuperación de borrador, movimiento con vista previa, cancelación, esquinas, menú contextual y selección visual de páginas");
    }
    static void CapturePicker(PageSelectionDialog picker, string path)
    {
        var root = (FrameworkElement)picker.Content;
        picker.Content = null;
        root.Measure(new Size(900, 620));
        root.Arrange(new Rect(0, 0, 900, 620));
        root.UpdateLayout();
        var bitmap = new RenderTargetBitmap(900, 620, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(root);
        var png = new PngBitmapEncoder();
        png.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path);
        png.Save(file);
    }
    static void CaptureDialog(Window dialog, string path, int width, int height)
    {
        var root = (FrameworkElement)dialog.Content;
        dialog.Content = null;
        var host = new Border { Background = System.Windows.Media.Brushes.White, Child = root };
        host.Measure(new Size(width, height));
        host.Arrange(new Rect(0, 0, width, height));
        host.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(host);
        var png = new PngBitmapEncoder();
        png.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path);
        png.Save(file);
    }
}
