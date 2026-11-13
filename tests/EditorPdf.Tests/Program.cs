using EditorPdf.Core;
using System.Security.Cryptography;
using System.Diagnostics.Tracing;
using System.Text;
if (args.FirstOrDefault() == "--diagnose-paragraph")
{
    using var document = new PdfDocument(LocalFiles.Read(args[1]));
    Console.WriteLine($"Pages={document.Count} ObjectEditBlocked={document.ObjectEditBlock != null}");
    for (int i = 0; i < document.Count; i++)
    {
        var texts = document.Objects(i).Where(o => o.Type == 1 && !string.IsNullOrWhiteSpace(o.Text)).ToArray();
        var bands = texts.GroupBy(o => (int)Math.Round(o.Top / 5) * 5).Where(g => g.Count() > 1).ToArray();
        var sample = bands.FirstOrDefault();
        int selected = sample == null ? 0 : ParagraphSelection.InArea(texts, sample.Min(o => o.Left) - 3, sample.Min(o => o.Bottom) - 3, sample.Max(o => o.Right) + 3, sample.Max(o => o.Top) + 3).Length;
        Console.WriteLine($"Page={i + 1} TextObjects={texts.Length} FragmentedBands={bands.Length} LargestBand={bands.Select(g => g.Count()).DefaultIfEmpty().Max()} SampleSelection={selected} Fonts={texts.Select(o => o.Font).Distinct().Count()}");
    }
    return;
}
if (args.FirstOrDefault() == "--diagnose-import")
{
    using var first = new PdfDocument(LocalFiles.Read(args[1]));
    using var second = new PdfDocument(LocalFiles.Read(args[2]));
    try
    {
        first.Import(second, Enumerable.Range(0, second.Count).ToArray(), first.Count);
        if (args.Length > 3)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(args[3])!);
            first.SaveNew(args[3]);
        }
        Console.WriteLine("PASS union de los documentos indicados");
    }
    catch (Exception ex) { Console.WriteLine(ex.Message); Environment.ExitCode = 1; }
    return;
}
if (args.FirstOrDefault() == "--diagnose-recovery")
{
    using var restored = new Session();
    restored.Restore(args[1]);
    using var second = new PdfDocument(LocalFiles.Read(args[2]));
    try
    {
        restored.Document.Import(second, Enumerable.Range(0, second.Count).ToArray(), restored.Document.Count);
        Console.WriteLine("PASS union desde copia de recuperación en memoria");
    }
    catch (Exception ex) { Console.WriteLine(ex.Message); Environment.ExitCode = 1; }
    return;
}
if (args.FirstOrDefault() == "--diagnose-edited")
{
    using var restored = new Session();
    restored.Open(args[1]);
    restored.Execute(d => d.AddText(0, "Prueba local sin datos", 14, 50, 50));
    restored.Document.Render(0, 1200);
    try
    {
        restored.Import(args[2], null, restored.Document.Count);
        Console.WriteLine("PASS union tras edición, render y deshacer/rehacer en memoria");
    }
    catch (Exception ex) { Console.WriteLine(ex.Message); Environment.ExitCode = 1; }
    return;
}
using var network = new NetworkAudit();
var output = Path.GetFullPath(args.FirstOrDefault() ?? "artifacts/tests");
Directory.CreateDirectory(output);
void Assert(bool b, string message) { if (!b) throw new Exception(message); Console.WriteLine("PASS " + message); }
using (var seed = new PdfDocument()) { seed.Blank(0); seed.AddText(0, "Texto original editable 123", 18, 60, 750); seed.AddText(0, "Contenido conservado", 12, 60, 700); seed.SaveNew(Path.Combine(output, "original.pdf")); }
var original = File.ReadAllBytes(Path.Combine(output, "original.pdf"));
var hash = SHA256.HashData(original);
using (var layers = new Session())
{
    layers.Execute(d =>
    {
        d.Blank(0);
        d.AddImage(0, 4, 4, 16, Enumerable.Range(0, 64).Select(i => (byte)(i % 4 is 2 or 3 ? 255 : 0)).ToArray(), 100, 100, 80, 60);
        d.AddImage(0, 4, 4, 16, Enumerable.Range(0, 64).Select(i => (byte)(i % 4 is 0 or 3 ? 255 : 0)).ToArray(), 100, 100, 80, 60);
    });
    var front = SHA256.HashData(layers.Document.Render(0, 600).Pixels);
    layers.Execute(d => d.OrderObject(0, 1, -1, true));
    Assert(!front.SequenceEqual(SHA256.HashData(layers.Document.Render(0, 600).Pixels)) && layers.Document.Objects(0).Count == 2, "orden de objetos cambia solapamiento sin perder imágenes");
    layers.SaveNew(Path.Combine(output, "orden-imagenes.pdf"));
    layers.Undo();
    Assert(front.SequenceEqual(SHA256.HashData(layers.Document.Render(0, 600).Pixels)), "deshacer orden restaura imagen visible");
    layers.Redo();
    layers.Execute(d => d.OrderObject(0, 0, 1, true));
    Assert(front.SequenceEqual(SHA256.HashData(layers.Document.Render(0, 600).Pixels)), "traer al frente conserva objetos y posición");
}
foreach (int rotation in Enumerable.Range(0, 4))
{
    using var rotatedText = new PdfDocument();
    rotatedText.Blank(0);
    for (int n = 0; n < rotation; n++)
        rotatedText.Rotate(0);
    rotatedText.AddText(0, "Texto horizontal", 14, 200, 300, rotation);
    var obj = rotatedText.Objects(0).Single();
    var a = rotatedText.ToDevice(0, 600, 600, obj.Left, obj.Bottom);
    var b = rotatedText.ToDevice(0, 600, 600, obj.Right, obj.Top);
    Assert(Math.Abs(a.X - b.X) > Math.Abs(a.Y - b.Y), "texto de página horizontal con rotación " + rotation);
}
using (var pdf = new PdfDocument(original))
{
    var objects = pdf.Objects(0);
    Assert(objects.Count == 2, "objetos de texto originales reabiertos");
    var font = objects[0].Font;
    pdf.ReplaceText(0, objects[0].Index, "Texto editado real 456");
    pdf.SaveNew(Path.Combine(output, "editado.pdf"));
    using var saved = new PdfDocument(File.ReadAllBytes(Path.Combine(output, "editado.pdf")));
    Assert(saved.Text(0).Contains("Texto editado real 456") && !saved.Text(0).Contains("original"), "edición original y guardado sin texto anterior");
    Assert(saved.Objects(0).Count == 2 && saved.Objects(0)[0].Font == font, "conserva fuente y número de objetos sin rectángulos");
    Assert(saved.Text(0).Contains("Contenido conservado"), "contenido ajeno conservado");
    pdf.Transform(0, objects[0].Index, 20, -30);
    pdf.RemoveObject(0, objects[1].Index);
    pdf.SaveNew(Path.Combine(output, "movido.pdf"));
    pdf.Blank(1);
    pdf.Duplicate(0);
    Assert(pdf.Count == 3, "duplicar y página en blanco");
    pdf.Move(0, 2);
    Assert(pdf.Text(2).Contains("editado"), "reordenar hacia abajo");
    pdf.Move(2, 0);
    Assert(pdf.Text(0).Contains("editado"), "reordenar hacia arriba");
    pdf.Rotate(0);
    pdf.Delete(1);
    Assert(pdf.Count == 2 && pdf.Info(0).Rotation == 1, "rotar y eliminar");
    using var joined = new PdfDocument(original);
    pdf.Import(joined, [0], pdf.Count);
    Assert(pdf.Count == 3, "unir otro PDF");
    var pixels = Enumerable.Repeat((byte)100, 32 * 24 * 4).ToArray();
    pdf.AddImage(0, 32, 24, 128, pixels, 100, 200, 80, 60);
    var image = pdf.Objects(0).Last(x => x.Type == 3);
    pdf.Transform(0, image.Index, 12, 15, 1.3f, 1.3f);
    pdf.SaveNew(Path.Combine(output, "organizado.pdf"));
    bool collision = false;
    try
    {
        pdf.SaveNew(Path.Combine(output, "original.pdf"));
    }
    catch (IOException) { collision = true; }
    Assert(collision, "no sobrescribe archivos existentes");
}
Assert(hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(Path.Combine(output, "original.pdf")))), "original intacto SHA256");
var fixture = Path.Combine(output, "fixtures", "externo.pdf");
if (File.Exists(fixture))
{
    using (var s = new Session())
    {
        s.Open(fixture);
        var first = s.Document.Objects(0).Single(o => o.Text == "Texto original compatible 123");
        var font = first.Font;
        s.Execute(d => d.ReplaceText(0, first.Index, "Edicion real 456"));
        Assert(s.Document.Objects(0).Single(o => o.Text == "Edicion real 456").Font == font, "fuente original Helvetica conservada en PDF externo");
        s.SaveNew(Path.Combine(output, "externo-editado.pdf"));
        s.Undo();
        Assert(s.Document.Text(0).Contains("Texto original compatible 123"), "deshacer restaura texto original");
        s.Redo();
        Assert(s.Document.Text(0).Contains("Edicion real 456"), "rehacer restaura edicion");
        var before = s.Document;
        var beforeRaster = SHA256.HashData(s.Document.Render(0, 600).Pixels);
        bool failed = false;
        try
        {
            s.Execute(d => { d.Delete(1); throw new InvalidOperationException("fallo inyectado"); });
        }
        catch (InvalidOperationException) { failed = true; }
        Assert(failed && ReferenceEquals(s.Document, before) && beforeRaster.SequenceEqual(SHA256.HashData(s.Document.Render(0, 600).Pixels)), "fallo intermedio revierte toda la operacion");
        failed = false;
        try
        {
            s.Execute(d => d.ReplaceText(0, first.Index, "漢字"));
        }
        catch (InvalidOperationException) { failed = true; }
        Assert(failed && s.Document.Text(0).Contains("Edicion real 456"), "fuente incompatible rechazada sin perder trabajo");
        s.Execute(d => d.ReplaceText(0, first.Index, "Texto con sustitucion ñáé", true));
        Assert(s.Document.Objects(0).Any(o => o.Font.Contains("NotoSans") && o.Text.Contains("sustitucion")), "sustitucion explicita con fuente incluida");
        string recovery = Path.Combine(output, "recovery");
        s.SaveRecovery(recovery);
        using var recovered = new Session();
        recovered.Restore(recovery);
        Assert(recovered.Document.Text(0) == s.Document.Text(0) && recovered.Dirty, "recuperacion local conserva cambios");
        var kept = s.Document;
        bool openFailed = false;
        try
        {
            s.OpenMany([fixture, Path.Combine(output, "fixtures", "protegido.pdf")]);
        }
        catch (InvalidOperationException) { openFailed = true; }
        Assert(openFailed && ReferenceEquals(kept, s.Document), "abrir varios documentos falla sin perder la sesion anterior");
        using var extracted = new PdfDocument();
        extracted.Import(s.Document, [1, 0], 0);
        extracted.SaveNew(Path.Combine(output, "extraido.pdf"));
        Assert(extracted.Count == 2 && extracted.Text(0).Contains("Segunda"), "extraer rangos en orden solicitado");
    }
    foreach (var name in new[] { "protegido", "firma-sintetica" })
    {
        using var s = new Session();
        s.Open(Path.Combine(output, "fixtures", name + ".pdf"));
        Assert(s.Document.EditBlock != null, name + " identificado como solo lectura");
        bool blocked = false;
        try
        {
            s.Execute(d => d.AddText(0, "No permitido", 12, 50, 50));
        }
        catch (InvalidOperationException) { blocked = true; }
        Assert(blocked, name + " bloquea cambios");
        var recovery = Path.Combine(output, "recovery-" + name);
        s.SaveRecovery(recovery);
        var record = System.Text.Json.JsonSerializer.Deserialize<Session.Recovery>(File.ReadAllBytes(Path.Combine(recovery, "session.json")))!;
        Assert(record.Pdf.SequenceEqual(File.ReadAllBytes(Path.Combine(output, "fixtures", name + ".pdf"))), name + " recuperacion conserva los bytes originales");
        using var editable = new Session();
        editable.OpenEditableCopy(Path.Combine(output, "fixtures", name + ".pdf"));
        Assert(editable.Document.EditBlock == null && !editable.Document.Signed && !editable.Document.Protected && editable.Dirty, name + " copia realmente editable sin cifrado ni firmas");
        editable.Execute(d => d.AddText(0, "Copia editable comprobada", 12, 50, 100));
        editable.SaveNew(Path.Combine(output, name + "-editable.pdf"));
        Assert(record.Pdf.SequenceEqual(File.ReadAllBytes(Path.Combine(output, "fixtures", name + ".pdf"))), name + " original intacto tras conversion");
    }
    using (var s = new Session())
    {
        string protectedPath = Path.Combine(output, "fixtures", "con-contrasena.pdf");
        s.Open(fixture);
        var kept = s.Document;
        bool rejected = false;
        try
        {
            s.OpenEditableCopy(protectedPath, "incorrecta");
        }
        catch (PdfPasswordException) { rejected = true; }
        Assert(rejected && ReferenceEquals(kept, s.Document), "contraseña incorrecta rechazada sin perder la sesión");
        s.OpenEditableCopy(protectedPath, "abrir-local");
        s.SaveNew(Path.Combine(output, "con-contrasena-editable.pdf"));
        s.SaveRecovery(Path.Combine(output, "recovery-copia-descifrada"));
        Assert(s.Document.EditBlock == null && s.Document.Bookmarks && s.Document.Attachments == 1, "contraseña valida: copia descifrada conserva estructuras y no guarda contraseña");
        Assert(!File.ReadAllText(Path.Combine(output, "recovery-copia-descifrada", "session.json")).Contains("abrir-local"), "recuperacion no almacena la contraseña proporcionada");
    }
    {
        string signedPath = Path.Combine(output, "firma-real-prueba.pdf");
        SignatureFixture.Create(fixture, signedPath);
        Assert(SignatureFixture.Verify(signedPath), "firma de prueba criptograficamente integra antes de convertir");
        byte[] signedHash = SHA256.HashData(File.ReadAllBytes(signedPath));
        using var copy = new Session();
        copy.OpenEditableCopy(signedPath);
        copy.Execute(d => d.Move(0, 1));
        copy.SaveNew(Path.Combine(output, "firma-real-editable.pdf"));
        Assert(!copy.Document.Signed && signedHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(signedPath))) && SignatureFixture.Verify(signedPath), "quitar firma en copia permite editar y conserva la firma integra del original");
    }
    using (var s = new Session())
    {
        s.Open(fixture);
        var text = s.Document.Objects(0).Single(o => o.Text == "Texto original compatible 123");
        s.Execute(d => d.SetTextStyle(0, text.Index, 24, 0xB42318));
        var changed = s.Document.Objects(0).Single(o => o.Index == text.Index);
        Assert(changed.Font == text.Font && changed.Text == text.Text && Math.Abs(changed.Size - 24) < .01 && changed.FillRgb == 0xB42318, "tamaño y color sobre objeto original conservan texto y fuente");
        s.SaveNew(Path.Combine(output, "estilo-original.pdf"));
        s.Undo();
        Assert(s.Document.Objects(0).Single(o => o.Index == text.Index).Size == text.Size, "deshacer estilo restaura objeto");
        s.Redo();
        bool rejected = false;
        try
        {
            s.Execute(d => d.SetTextStyle(0, text.Index, float.NaN, 0));
        }
        catch (ArgumentException) { rejected = true; }
        Assert(rejected && s.Document.Objects(0).Single(o => o.Index == text.Index).FillRgb == 0xB42318, "estilo invalido no pierde cambios");
    }
    using (var s = new Session())
    {
        s.Open(Path.Combine(output, "fixtures", "estructuras.pdf"));
        var o = s.Document.Objects(0).Single(o => o.Text == "Texto original compatible 123");
        s.Execute(d => d.ReplaceText(0, o.Index, "Edicion real 456"));
        s.SaveNew(Path.Combine(output, "estructuras-editado.pdf"));
        Assert(s.Document.Bookmarks && s.Document.Attachments == 1 && s.Document.Info(0).Annotations == 1, "edicion conserva marcadores, adjunto y anotacion");
        s.Execute(d => d.Move(0, 1));
        Assert(s.Document.Text(1).Contains("Edicion real 456"), "reordenar con referencias conserva paginas");
        s.SaveNew(Path.Combine(output, "reordenado-estructuras.pdf"));
    }
    using (var s = new Session())
    {
        s.Execute(d => d.Blank(0));
        Assert(s.Document.Count == 1, "sesion nueva sin PDF previo");
    }
    using (var deletion = new Session())
    {
        deletion.Open(Path.Combine(output, "fixtures", "pagina-blanca-con-destinos.pdf"));
        var review = deletion.Document.ReviewPageDeletion(1);
        Assert(review.Annotations == 0 && review.Links == 1 && review.Bookmarks == 1 &&
            review.NamedDestinations == 1 && review.ReplacementPage == 3,
            "borrado explica qué referencias apuntan a la página en blanco");
        deletion.Execute(d => d.Delete(1));
        Assert(deletion.Document.Count == 2 && deletion.Document.NamedDestinations == 2 && deletion.Document.Attachments == 1,
            "borrado de página en blanco conserva referencias y adjunto");
        deletion.SaveNew(Path.Combine(output, "borrado-pagina-blanca.pdf"));
        deletion.Undo();
        Assert(deletion.Document.Count == 3, "deshacer restaura la página borrada");
    }
    using (var deletion = new Session())
    {
        deletion.Open(Path.Combine(output, "fixtures", "pagina-blanca-sin-destinos.pdf"));
        var review = deletion.Document.ReviewPageDeletion(2);
        Assert(review.Annotations + review.Links + review.Bookmarks + review.NamedDestinations + review.OtherDestinations == 0,
            "marcadores y anotaciones ajenos no impiden borrar página");
        deletion.Execute(d => d.Delete(2));
        Assert(deletion.Document.Count == 2 && deletion.Document.Bookmarks, "borrado conserva marcador de otra página");
    }
    foreach (var name in new[] { "estructuras", "anotaciones", "destinos" })
    {
        var path = Path.Combine(output, "fixtures", name + ".pdf");
        var originalHash = SHA256.HashData(File.ReadAllBytes(path));
        using var s = new Session();
        s.Open(path);
        int count = s.Document.Count;
        var pagesBefore = Enumerable.Range(0, count).Select(i => SHA256.HashData(s.Document.Render(i, 600).Pixels)).ToArray();
        var annotationsBefore = Enumerable.Range(0, count).Select(i => s.Document.Info(i).Annotations).ToArray();
        bool bookmarks = s.Document.Bookmarks;
        int attachments = s.Document.Attachments;
        uint destinations = s.Document.NamedDestinations;
        s.Import(fixture, null, count);
        Assert(s.Document.Count == count + 2, "añadir al final con " + name + " sin bloqueo general");
        Assert(s.Document.Bookmarks == bookmarks && s.Document.Attachments == attachments && s.Document.NamedDestinations == destinations &&
            Enumerable.Range(0, count).All(i => s.Document.Info(i).Annotations == annotationsBefore[i] && SHA256.HashData(s.Document.Render(i, 600).Pixels).SequenceEqual(pagesBefore[i])),
            "conserva paginas existentes y estructuras con " + name);
        s.Undo();
        Assert(s.Document.Count == count, "deshacer union con " + name);
        s.Redo();
        s.SaveNew(Path.Combine(output, "unido-" + name + ".pdf"));
        Assert(originalHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "original con " + name + " intacto");
    }
    using (var s = new Session())
    {
        s.Open(fixture);
        var path = Path.Combine(output, "fixtures", "anotaciones.pdf");
        s.Import(path, null, s.Document.Count);
        Assert(s.Document.Count == 4 && s.Document.Info(2).Annotations == 3, "importa nota, sello visual y enlace web sin perder anotaciones");
        s.SaveNew(Path.Combine(output, "importado-anotaciones.pdf"));
    }
    using (var rendered = new PdfDocument(File.ReadAllBytes(Path.Combine(output, "fixtures", "anotaciones.pdf"))))
    {
        rendered.Render(0, 600);
        rendered.Render(0, 900);
        rendered.SaveNew(Path.Combine(output, "visualizado-anotaciones.pdf"));
        Assert(rendered.Info(0).Annotations == 3, "visualizacion aislada no altera el documento editable");
    }
    foreach (var name in new[] { "estructuras", "enlace-interno", "destinos", "formulario", "etiquetado" })
    {
        using var s = new Session();
        s.Open(fixture);
        s.Import(Path.Combine(output, "fixtures", name + ".pdf"), null, s.Document.Count);
        Assert(s.Document.Count == 4, "importar estructuras de origen: " + name);
        s.Execute(d => d.Move(2, 0));
        s.SaveNew(Path.Combine(output, "mezcla-" + name + ".pdf"));
    }
    using (var s = new Session())
    {
        var form = Path.Combine(output, "fixtures", "formulario.pdf");
        s.Open(form);
        s.Import(form, null, 2);
        s.Execute(d => d.Move(2, 0));
        s.SaveNew(Path.Combine(output, "formularios-dobles.pdf"));
    }
    using (var s = new Session())
    {
        var named = Path.Combine(output, "fixtures", "destinos.pdf");
        s.Open(named);
        s.Import(named, null, 2);
        s.SaveNew(Path.Combine(output, "destinos-dobles.pdf"));
    }
    using (var s = new Session())
    {
        s.Open(Path.Combine(output, "fixtures", "destinos.pdf"));
        s.Import(Path.Combine(output, "fixtures", "destinos-enlazados.pdf"), null, 2);
        s.SaveNew(Path.Combine(output, "destinos-enlazados-dobles.pdf"));
    }
    using (var s = new Session())
    {
        s.Open(Path.Combine(output, "fixtures", "estructuras.pdf"));
        s.Import(Path.Combine(output, "fixtures", "estructuras-otro.pdf"), null, 2);
        s.SaveNew(Path.Combine(output, "adjuntos-dobles.pdf"));
    }
    Assert(Session.ParseRanges("1-2,1", 2).SequenceEqual(new[] { 0, 1, 0 }), "analizador de rangos");
    bool remote = false;
    try
    {
        LocalFiles.Validate(@"\\localhost\synthetic\file.pdf");
    }
    catch (IOException) { remote = true; }
    Assert(remote, "rutas UNC rechazadas antes de acceder");
    using (var coordinates = new PdfDocument(original))
    {
        for (int r = 0; r < 4; r++)
        {
            var info = coordinates.Info(0);
            var point = coordinates.ToDevice(0, 600, 850, 100, 300);
            var restored = coordinates.ToPage(0, 600, 850, point.X, point.Y);
            Assert(Math.Abs(restored.X - 100) < 2 && Math.Abs(restored.Y - 300) < 2, $"coordenadas de objetos, rotacion {r * 90}");
            coordinates.Rotate(0);
        }
    }
}
Assert(network.Count == 0, "sin eventos de red .NET durante los flujos probados");
using (var paragraph = new PdfDocument())
{
    paragraph.Blank(0);
    paragraph.AddText(0, "Primera linea", 14, 40, 700);
    paragraph.AddText(0, "Segunda linea", 14, 40, 680);
    paragraph.AddText(0, "Tercera linea", 14, 40, 660);
    paragraph.AddText(0, "Contenido aparte", 14, 370, 700);
    var beforeCollision = paragraph.Objects(0).Select(o => (o.Text, o.Left, o.Bottom, o.Right, o.Top)).ToArray();
    var beforeRaster = SHA256.HashData(paragraph.Render(0, 600).Pixels);
    bool collision = false;
    try { paragraph.ReplaceParagraph(0, [0, 1, 2], string.Concat(Enumerable.Repeat("palabra ", 120)), 40); }
    catch (ArgumentException) { collision = true; }
    catch (InvalidOperationException) { collision = true; }
    Assert(collision && beforeCollision.SequenceEqual(paragraph.Objects(0).Select(o => (o.Text, o.Left, o.Bottom, o.Right, o.Top))) && beforeRaster.SequenceEqual(SHA256.HashData(paragraph.Render(0, 600).Pixels)), "rechazo de párrafo incompatible sin modificar el PDF");
    paragraph.ReplaceParagraph(0, [0, 1, 2], "Primera linea con varias palabras añadidas para cambiar la longitud del párrafo y desplazar el resto de sus líneas", 180);
    var copy = Path.Combine(output, "parrafo-real.pdf");
    paragraph.SaveNew(copy);
    using var reopened = new PdfDocument(File.ReadAllBytes(copy));
    var textObjects = reopened.Objects(0);
    Assert(textObjects.Any(o => o.Text == "Contenido aparte") && textObjects.Count(o => o.Type == 1) > 3 && string.Join(" ", textObjects.Where(o => o.Text != "Contenido aparte").Select(o => o.Text)).Contains("palabras añadidas para cambiar la longitud del párrafo"), "párrafo refluido y contenido ajeno conservado al reabrir");
}
using (var fragments = new PdfDocument())
{
    fragments.Blank(0);
    fragments.AddText(0, "Primera", 14, 40, 700);
    fragments.AddText(0, " linea", 14, 90, 700);
    fragments.AddText(0, "Segunda", 14, 40, 680, family: "Liberation Serif");
    fragments.AddText(0, " linea", 14, 101, 680);
    fragments.AddText(0, "Fuera", 14, 400, 700);
    var group = ParagraphSelection.InArea(fragments.Objects(0), 35, 665, 180, 720);
    Assert(group.Length == 4 && ParagraphSelection.Compose(group) == "Primera linea\nSegunda linea", "selección de párrafo fragmentado en orden de lectura");
    fragments.ReplaceParagraph(0, group.Select(o => o.Index).ToArray(), "Primera linea modificada y más larga\nSegunda linea también modificada", 170, "Noto Sans");
    string path = Path.Combine(output, "parrafo-fragmentado.pdf");
    fragments.SaveNew(path);
    using var reopened = new PdfDocument(File.ReadAllBytes(path));
    Assert(reopened.Objects(0).Any(o => o.Text == "Fuera") && reopened.Text(0).Contains("modificada") && !reopened.Text(0).Contains("Primera linea\nSegunda linea"), "reflujo de párrafo fragmentado con estilos distintos y texto ajeno conservado");
}
using (var flowing = new PdfDocument())
{
    flowing.Blank(0);
    flowing.AddText(0, "Uno dos", 14, 40, 700);
    flowing.AddText(0, "tres cuatro", 14, 40, 680);
    flowing.AddText(0, "Otro bloque", 14, 40, 625);
    var selected = ParagraphSelection.InArea(flowing.Objects(0), 35, 605, 180, 720);
    Assert(ParagraphSelection.Compose(selected) == "Uno dos\ntres cuatro\n\nOtro bloque", "saltos entre párrafos conservados en la selección");
    flowing.ReplaceParagraph(0, selected.Select(o => o.Index).ToArray(), "Uno dos\ntres cuatro cinco\n\nOtro bloque ampliado", 150);
    Assert(flowing.Text(0).Contains("cinco") && flowing.Text(0).Contains("ampliado") && flowing.Objects(0).Where(o => o.Type == 1).All(o => o.Right <= 192), "reflujo dentro del ancho con separación entre párrafos");
}
for(int alignment=0;alignment<4;alignment++)
{
    using var aligned=new PdfDocument(); aligned.Blank(0);
    aligned.AddText(0,"Texto inicial del bloque",14,40,700);
    aligned.ReplaceParagraph(0,[0],"Una frase con suficientes palabras para varias líneas y probar el ajuste del texto al ancho elegido",180,alignment:alignment);
    var objects=aligned.Objects(0).Where(o=>o.Type==1).ToArray();
    Assert(objects.All(o=>o.Left>=39 && o.Right<=222),$"alineación {alignment}: conserva márgenes");
    string path=Path.Combine(output,$"alineacion-{alignment}.pdf"); aligned.SaveNew(path);
    using var reopened=new PdfDocument(File.ReadAllBytes(path));
    Assert(reopened.Text(0).Contains("elegido"),$"alineación {alignment}: guardado y reapertura");
}
using(var duplicate=new PdfDocument())
{
    duplicate.Blank(0); duplicate.AddImage(0,2,2,8,Enumerable.Repeat((byte)255,16).ToArray(),40,500,60,60);
    duplicate.DuplicateImage(0,0);
    Assert(duplicate.Objects(0).Count(o=>o.Type==3)==2,"duplicar imagen crea objeto independiente");
    duplicate.RemoveObject(0,1); Assert(duplicate.Objects(0).Count(o=>o.Type==3)==1,"borrar copia conserva imagen original");
}
File.WriteAllText(Path.Combine(output, "result.txt"), "PASS: PDFium original text edit, same font, save/reopen, preserve independent text, move/remove, pages, images, no overwrite.\n");
sealed class NetworkAudit : EventListener
{
    public int Count;
    protected override void OnEventSourceCreated(EventSource source)
    {
        if (source.Name.StartsWith("System.Net.", StringComparison.Ordinal))
            EnableEvents(source, EventLevel.Verbose, EventKeywords.All);
    }
    protected override void OnEventWritten(EventWrittenEventArgs e)
    {
        if (e.EventId > 0)
            Interlocked.Increment(ref Count);
    }
}
