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
        var groupText = objects.Items.Cast<MainWindow.ObjectRow>().First(o => o.Value.Text == "Texto escrito sobre la pagina");
        var groupImage = objects.Items.Cast<MainWindow.ObjectRow>().Last(o => o.Value.Type == 3);
        objects.SelectedItems.Clear();
        objects.SelectedItems.Add(groupText);
        objects.SelectedItems.Add(groupImage);
        capture("seleccion-multiple",1280,780,1);
        window.BeginObjectGesture(groupText.Value, new Point(120,200));
        window.UpdateObjectGesture(new Point(150,220));
        window.RenderGesturePreview();
        window.CommitObjectGesture();
        var afterDrag = objects.SelectedItems.Cast<MainWindow.ObjectRow>().Select(o => o.Value).ToArray();
        if(afterDrag.Length != 2 || afterDrag.Any(o => Math.Abs(o.Left - (o.Index == groupText.Value.Index ? groupText.Value.Left : groupImage.Value.Left)) < 10))
            throw new Exception("El arrastre no desplaza los dos objetos seleccionados");
        int countBeforeCopy = session.Document.Objects(0).Count;
        typeof(MainWindow).GetMethod("DuplicateSelectedObjects", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, null);
        if(objects.SelectedItems.Count != 2 || session.Document.Objects(0).Count != countBeforeCopy + 2)
            throw new Exception("No se duplica el grupo completo");
        typeof(MainWindow).GetMethod("DeleteSelectedObjects", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, null);
        if(session.Document.Objects(0).Count != countBeforeCopy)
            throw new Exception("No se elimina el grupo completo");
        session.Undo();
        if(session.Document.Objects(0).Count != countBeforeCopy + 2)
            throw new Exception("Deshacer no recupera el grupo en una operación");
        session.Undo(); session.Undo();
        typeof(MainWindow).GetMethod("Refresh", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, null);
        Console.WriteLine("PASS selección múltiple mixta: arrastre, duplicación, borrado y deshacer");
        var toMove = objects.Items.Cast<MainWindow.ObjectRow>().Where(o => o.Value.Type == 3 || o.Value.Text == "Texto escrito sobre la pagina").ToArray();
        objects.UnselectAll();
        foreach (var item in toMove) objects.SelectedItems.Add(item);
        int targetBefore = session.Document.Objects(1).Count;
        typeof(MainWindow).GetMethod("MoveSelectionToPage", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, [1]);
        if (session.Document.Objects(1).Count != targetBefore + toMove.Length ||
            session.Document.Objects(0).Any(o => o.Text == "Texto escrito sobre la pagina"))
            throw new Exception("El grupo no se ha trasladado entre páginas");
        using (var reopenedMove = new PdfDocument(session.Document.SaveBytes()))
            if (!reopenedMove.Text(1).Contains("Texto escrito sobre la pagina") || !reopenedMove.Objects(1).Any(o => o.Type == 3))
                throw new Exception("Los objetos trasladados no sobreviven al guardado");
        session.Undo();
        ((ListBox)window.FindName("PagesList")).SelectedIndex = 0;
        typeof(MainWindow).GetMethod("Refresh", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, null);
        Console.WriteLine("PASS traslado de texto e imagen entre páginas, reapertura y deshacer");
        var setZoom = typeof(MainWindow).GetMethod("SetZoom", BindingFlags.NonPublic | BindingFlags.Instance)!;
        setZoom.Invoke(window, [.5]);
        capture("paginas-en-paralelo", 1280, 780, 1);
        if (((Button)window.FindName("AdjacentPage")).Visibility != Visibility.Visible)
            throw new Exception("No aparecen dos páginas cuando caben");
        setZoom.Invoke(window, [1.0]);
        if (((Button)window.FindName("AdjacentPage")).Visibility != Visibility.Collapsed)
            throw new Exception("La segunda página no se oculta al ampliar");
        var cutRows = objects.Items.Cast<MainWindow.ObjectRow>().Where(o => o.Value.Type == 3 || o.Value.Text == "Texto escrito sobre la pagina").ToArray();
        foreach (var item in cutRows) objects.SelectedItems.Add(item);
        var copyCommand = typeof(MainWindow).GetMethod("CopySelectedObjects", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var pasteCommand = typeof(MainWindow).GetMethod("PasteSelectedObjects", BindingFlags.NonPublic | BindingFlags.Instance)!;
        copyCommand.Invoke(window, [true]);
        if (session.Document.Objects(0).Any(o => o.Text == "Texto escrito sobre la pagina"))
            throw new Exception("Cortar conserva el texto en origen");
        ((ListBox)window.FindName("PagesList")).SelectedIndex = 1;
        pasteCommand.Invoke(window, null);
        if (objects.SelectedItems.Count != cutRows.Length || !session.Document.Text(1).Contains("Texto escrito sobre la pagina"))
            throw new Exception("Pegar no recupera todos los objetos cortados");
        session.Undo();
        session.Undo();
        ((ListBox)window.FindName("PagesList")).SelectedIndex = 0;
        typeof(MainWindow).GetMethod("Refresh", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, null);
        Console.WriteLine("PASS vista doble según zoom y portapapeles entre páginas");
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
        var beforeUnchanged = session.Document;
        window.BeginInlineText(new Point(55, 80), editableText.Text, editableText.Index);
        window.FinishInlineText();
        if (!ReferenceEquals(beforeUnchanged, session.Document))
            throw new Exception("Cerrar un texto sin cambios intenta reescribir la fuente original");
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
        objects.SelectedItem = objects.Items.Cast<MainWindow.ObjectRow>().First(o => o.Value.Text == "Numero 12345");
        var bar = overlay.Children.OfType<Border>().SingleOrDefault(b => System.Windows.Automation.AutomationProperties.GetName(b) == "Barra contextual de texto");
        if (bar == null || bar.Child is not StackPanel barContent || !barContent.Children.OfType<Button>().Any(b => Equals(b.ToolTip, "Aplicar propiedades de texto")))
            throw new Exception("Falta la barra contextual junto al texto seleccionado");
        capture("barra-contextual", 1280, 780, 1);
        var beforeToolbarNoOp = session.Document;
        barContent.Children.OfType<Button>().First(b => Equals(b.ToolTip, "Aplicar propiedades de texto")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (!ReferenceEquals(beforeToolbarNoOp, session.Document))
            throw new Exception("La barra contextual reescribe texto aunque no se cambió ninguna propiedad");
        float previousSize = session.Document.Objects(0).First(o => o.Text == "Numero 12345").Size;
        ((TextBox)window.FindName("FontSizeValue")).Text = (previousSize + 1).ToString(System.Globalization.CultureInfo.CurrentCulture);
        barContent.Children.OfType<Button>().First(b => Equals(b.ToolTip, "Aplicar propiedades de texto")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (!session.Document.Objects(0).Any(o => o.Text == "Numero 12345" && Math.Abs(o.Size - previousSize - 1) < .05))
            throw new Exception("La barra contextual no aplica el nuevo tamaño al PDF");
        objects.SelectedItem = objects.Items.Cast<MainWindow.ObjectRow>().First(o => o.Value.Text == "Numero 12345");
        var styleBar = overlay.Children.OfType<Border>().Single(b => System.Windows.Automation.AutomationProperties.GetName(b) == "Barra contextual de texto");
        var styleControls = (StackPanel)styleBar.Child;
        styleControls.Children.OfType<Button>().First(b => Equals(b.ToolTip, "Negrita")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        styleControls.Children.OfType<Button>().First(b => Equals(b.ToolTip, "Aplicar propiedades de texto")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (!session.Document.Objects(0).Any(o => o.Text == "Numero 12345" && o.Font.Contains("Bold", StringComparison.OrdinalIgnoreCase)))
            throw new Exception("La barra contextual no aplica negrita real al PDF");
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
        using(var printDocument = new PdfDocument())
        {
            printDocument.Blank(0); printDocument.AddText(0,"First page",16,40,700);
            printDocument.Blank(1); printDocument.AddText(1,"Second page",16,40,700);
            printDocument.AddImage(1,2,2,8,Enumerable.Range(0,16).Select(i=>(byte)(i%4 is 2 or 3 ? 255 : 0)).ToArray(),40,500,120,80);
            var printing = new PrintPreviewDialog(window,printDocument,false);
            if(printing.SelectedPages.Count!=printDocument.Count) throw new Exception("Impresión no comienza con todas las páginas");
            using(var xps=new System.Windows.Xps.Packaging.XpsDocument(Path.Combine(folder,"print-serialization.xps"),FileAccess.ReadWrite))
            {
                System.Windows.Xps.Packaging.XpsDocument.CreateXpsDocumentWriter(xps).Write(printing.CurrentPaginator!);
                if(xps.GetFixedDocumentSequence().DocumentPaginator.PageCount!=printDocument.Count) throw new Exception("Serialización XPS incompleta");
            }
            var saved=Path.Combine(folder,"overwrite-copy.pdf");
            using(var writable=new Session())
            {
                writable.Execute(d=>d.Blank(0)); writable.SaveNew(saved);
                writable.Execute(d=>d.Blank(1)); writable.SaveNew(saved,true);
                using var reopened=new PdfDocument(File.ReadAllBytes(saved));
                if(reopened.Count!=2) throw new Exception("Sobrescritura confirmada no actualiza copia");
            }
            printing.SetPreviewOptions("2",false,false);
            if(!printing.SelectedPages.SequenceEqual([1]) || printing.CurrentPaginator?.PageCount!=1 || printing.PreviewImage==null || printing.CanPrint)
                throw new Exception("La vista previa no respeta el rango o permite imprimir sin impresora");
            var portrait=printing.PreviewImage;
            printing.SetPreviewOptions("2",true,false);
            if(printing.SheetSize.Width<=printing.SheetSize.Height || printing.PreviewImage!.PixelWidth<=printing.PreviewImage.PixelHeight || portrait.PixelWidth==printing.PreviewImage.PixelWidth)
                throw new Exception("La orientación no actualiza la vista previa");
            CaptureDialog(printing,Path.Combine(folder,"impresion-horizontal-pagina-2.png"),1040,780);
            var gray = new PrintPreviewDialog(window,printDocument,false);
            gray.SetPreviewOptions("2,1",false,true,2);
            if(!gray.SelectedPages.SequenceEqual([1,0]) || !gray.Grayscale || gray.CurrentPaginator?.PageCount!=2)
                throw new Exception("La vista previa no respeta el orden y el modo gris");
            var printRaster=gray.PreviewImage!;
            var pixels=new byte[printRaster.PixelWidth*printRaster.PixelHeight*4]; printRaster.CopyPixels(pixels,printRaster.PixelWidth*4,0);
            for(int i=0;i<pixels.Length;i+=4) if(pixels[i]!=pixels[i+1] || pixels[i]!=pixels[i+2]) throw new Exception("La vista previa de blanco y negro conserva color");
            gray.SetPreviewOptions("2,1", true, false, perSheet: 2, downFirst: true, drawBorders: true);
            var tiled = gray.CurrentPaginator!;
            if (tiled.PageCount != 1 || ((Canvas)tiled.GetPage(0).Visual).Children.Count != 2)
                throw new Exception("Dos páginas no caben en una sola hoja");
            using (var xps = new System.Windows.Xps.Packaging.XpsDocument(Path.Combine(folder,"dos-paginas-por-hoja.xps"),FileAccess.ReadWrite))
            {
                System.Windows.Xps.Packaging.XpsDocument.CreateXpsDocumentWriter(xps).Write(tiled);
                if (xps.GetFixedDocumentSequence().DocumentPaginator.PageCount != 1) throw new Exception("El trabajo XPS no conserva una hoja");
            }
            CaptureDialog(gray,Path.Combine(folder,"impresion-dos-por-hoja.png"),1040,780);
            gray.SetPreviewOptions("999",false,false);
            if(gray.CurrentPaginator!=null || gray.PreviewImage!=null || gray.CanPrint) throw new Exception("Un rango inválido conserva una vista previa imprimible");
            Console.WriteLine("PASS impresión unificada: rango, orden, orientación, gris y bloqueo de rango inválido");
        }
        window.SetInterfaceLanguage("en",false);
        capture("english-interface",1280,780,1);
        if(Ui.T("Añadir texto")!="Add text") throw new Exception("No se traduce la interfaz al inglés");
        window.SetInterfaceLanguage("es",false);
        var textForArea = session.Document.Objects(0).First(o => o.Type == 1 && !string.IsNullOrWhiteSpace(o.Text) && o.HorizontalText);
        var cornerA = session.Document.ToDevice(0, (int)image.Width, (int)image.Height, textForArea.Left, textForArea.Top);
        var cornerB = session.Document.ToDevice(0, (int)image.Width, (int)image.Height, textForArea.Right, textForArea.Bottom);
        var area = new Rect(new Point(Math.Min(cornerA.X, cornerB.X) - 4, Math.Min(cornerA.Y, cornerB.Y) - 4), new Point(Math.Max(cornerA.X, cornerB.X) + 4, Math.Max(cornerA.Y, cornerB.Y) + 4));
        typeof(MainWindow).GetMethod("ArmParagraphSelection", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, null);
        typeof(MainWindow).GetMethod("StartParagraphSelection", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, [area.TopLeft, false]);
        typeof(MainWindow).GetMethod("UpdateParagraphSelection", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, [area.BottomRight]);
        if (!overlay.Children.OfType<System.Windows.Shapes.Rectangle>().Any(r => r.Width > 0 && r.Height > 0))
            throw new Exception("El recuadro de selección de párrafo no aparece al arrastrar");
        var chosenArea = (PdfObject[])typeof(MainWindow).GetMethod("ParagraphObjectsIn", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, [area])!;
        if (!chosenArea.Any(o => o.Index == textForArea.Index))
            throw new Exception("El recuadro no incluye el texto seleccionado");
        capture("parrafo-recuadro", 1280, 780, 1);
        typeof(MainWindow).GetMethod("CancelParagraphSelection", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, null);
        typeof(MainWindow).GetMethod("StartParagraphSelection", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, [area.TopLeft, true]);
        typeof(MainWindow).GetMethod("CompleteParagraphSelection", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, [area.BottomRight]);
        if (objects.SelectedItems.Count == 0 || !overlay.Children.OfType<ToolButton>().Any(b => Equals(b.ToolTip, "Editar texto seleccionado")))
            throw new Exception("La selección de texto por arrastre no ofrece el botón contextual de edición");
        typeof(MainWindow).GetMethod("CancelParagraphSelection", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, null);
        Console.WriteLine("PASS lienzo: texto visible, movimiento con vista previa, cancelación, esquinas, menú contextual y selección visual de páginas");
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
