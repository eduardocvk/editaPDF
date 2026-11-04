using EditorPdf.Core;
using System.Security.Cryptography;
using System.Diagnostics.Tracing;
using System.Text;
using var network = new NetworkAudit();
var output = Path.GetFullPath(args.FirstOrDefault() ?? "artifacts/tests");
Directory.CreateDirectory(output);
void Assert(bool b, string message) { if (!b) throw new Exception(message); Console.WriteLine("PASS " + message); }
using (var seed = new PdfDocument()) { seed.Blank(0); seed.AddText(0, "Texto original editable 123", 18, 60, 750); seed.AddText(0, "Contenido conservado", 12, 60, 700); seed.SaveNew(Path.Combine(output, "original.pdf")); }
var original = File.ReadAllBytes(Path.Combine(output, "original.pdf"));
var hash = SHA256.HashData(original);
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
    }
    using (var s = new Session())
    {
        s.Open(Path.Combine(output, "fixtures", "estructuras.pdf"));
        var o = s.Document.Objects(0).Single(o => o.Text == "Texto original compatible 123");
        s.Execute(d => d.ReplaceText(0, o.Index, "Edicion real 456"));
        s.SaveNew(Path.Combine(output, "estructuras-editado.pdf"));
        Assert(s.Document.Bookmarks && s.Document.Attachments == 1 && s.Document.Info(0).Annotations == 1, "edicion conserva marcadores, adjunto y anotacion");
        bool blocked = false;
        try
        {
            s.Execute(d => d.Move(0, 1));
        }
        catch (InvalidOperationException) { blocked = true; }
        Assert(blocked, "reorganizacion con referencias bloqueada");
    }
    using (var s = new Session())
    {
        s.Execute(d => d.Blank(0));
        Assert(s.Document.Count == 1, "sesion nueva sin PDF previo");
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
    foreach (var name in new[] { "estructuras", "enlace-interno", "destinos" })
    {
        using var s = new Session();
        s.Open(fixture);
        var before = s.Document;
        bool blocked = false;
        try
        {
            s.Import(Path.Combine(output, "fixtures", name + ".pdf"), null, s.Document.Count);
        }
        catch (InvalidOperationException error) { blocked = error.Message.Contains("documento principal"); }
        Assert(blocked && ReferenceEquals(s.Document, before), "importacion no compatible de " + name + " informa alternativa sin cambiar sesion");
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
