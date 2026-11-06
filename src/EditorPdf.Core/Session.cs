using System.Text.Json;
namespace EditorPdf.Core;

public sealed class Session : IDisposable
{
    public PdfDocument Document { get; private set; } = new();
    readonly LinkedList<byte[]> undo = [], redo = [];
    byte[]? readOnlyOriginal;
    public bool CanUndo => undo.Count > 0; public bool CanRedo => redo.Count > 0;
    public bool Dirty
    {
        get; private set;
    }
    public List<string> Originals { get; private set; } = [];
    public const long HistoryLimit = 128L * 1024 * 1024;
    static void Push(LinkedList<byte[]> stack, byte[] data)
    {
        stack.AddLast(data);
        long size = stack.Sum(x => (long)x.Length);
        while (stack.Count > 30 || size > HistoryLimit)
        {
            size -= stack.First!.Value.Length;
            stack.RemoveFirst();
        }
    }
    public void Open(string path) => OpenMany([path]);
    public void OpenEditableCopy(string path, string? password = null)
        => OpenEditableCopy(LocalFiles.Read(path), path, password);
    public byte[] EditableCopySource() => (readOnlyOriginal ?? Document.SaveBytes()).ToArray();
    public void OpenEditableCopy(byte[] original, string path, string? password = null)
    {
        var bytes = EditableCopy.Create(original, password);
        var next = new PdfDocument(bytes);
        Document.Dispose();
        Document = next;
        readOnlyOriginal = null;
        Originals = [Path.GetFullPath(path)];
        undo.Clear();
        redo.Clear();
        Dirty = true;
    }
    public void OpenMany(string[] paths)
    {
        if (paths.Length == 0)
            return;
        PdfDocument? pdf = null;
        try
        {
            var firstBytes = LocalFiles.Read(paths[0]);
            pdf = new PdfDocument(firstBytes);
            if (pdf.Count == 0)
                throw new InvalidDataException("El PDF no contiene páginas.");
            foreach (var path in paths.Skip(1))
            {
                using var source = new PdfDocument(LocalFiles.Read(path));
                pdf.Import(source, Enumerable.Range(0, source.Count).ToArray(), pdf.Count);
            }
            Document.Dispose();
            Document = pdf;
            readOnlyOriginal = paths.Length == 1 && pdf.EditBlock != null ? firstBytes : null;
            pdf = null;
            Originals = paths.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            undo.Clear();
            redo.Clear();
            Dirty = paths.Length > 1;
        }
        finally { pdf?.Dispose(); }
    }
    public void Execute(Action<PdfDocument> action)
    {
        if (Document.EditBlock is { } reason)
            throw new InvalidOperationException(reason);
        var before = Document.SaveBytes();
        using var candidate = new PdfDocument(before);
        action(candidate);
        var after = candidate.SaveBytes();
        var committed = new PdfDocument(after);
        Push(undo, before);
        redo.Clear();
        Document.Dispose();
        Document = committed;
        Dirty = true;
    }
    public void Import(string path, int[]? pages, int at)
    {
        using var source = new PdfDocument(LocalFiles.Read(path));
        Execute(d => d.Import(source, pages ?? Enumerable.Range(0, source.Count).ToArray(), at));
        var full = Path.GetFullPath(path);
        if (!Originals.Contains(full, StringComparer.OrdinalIgnoreCase))
            Originals.Add(full);
    }
    public void ImportMany(string[] paths, int at)
    {
        var sources = new List<PdfDocument>();
        try
        {
            foreach (var path in paths)
                sources.Add(new PdfDocument(LocalFiles.Read(path)));
            Execute(d => { int position = at; foreach (var source in sources) { d.Import(source, Enumerable.Range(0, source.Count).ToArray(), position); position += source.Count; } });
            foreach (var path in paths)
            {
                string full = Path.GetFullPath(path);
                if (!Originals.Contains(full, StringComparer.OrdinalIgnoreCase))
                    Originals.Add(full);
            }
        }
        finally { foreach (var source in sources) source.Dispose(); }
    }
    public void Undo()
    {
        if (!CanUndo)
            return;
        var data = undo.Last!.Value;
        var restored = new PdfDocument(data);
        Push(redo, Document.SaveBytes());
        undo.RemoveLast();
        Document.Dispose();
        Document = restored;
        Dirty = true;
    }
    public void Redo()
    {
        if (!CanRedo)
            return;
        var data = redo.Last!.Value;
        var restored = new PdfDocument(data);
        Push(undo, Document.SaveBytes());
        redo.RemoveLast();
        Document.Dispose();
        Document = restored;
        Dirty = true;
    }
    public void SaveNew(string path, bool overwrite = false)
    {
        if (Originals.Contains(Path.GetFullPath(path), StringComparer.OrdinalIgnoreCase))
            throw new IOException("No se puede sobrescribir un original.");
        Document.SaveNew(path, overwrite);
        Dirty = false;
    }
    public void SaveRecovery(string folder)
    {
        if (Document.Count == 0)
            return;
        folder = LocalFiles.Validate(folder);
        Directory.CreateDirectory(folder);
        var record = new Recovery(1, readOnlyOriginal ?? Document.SaveBytes(), Originals.ToArray(), Dirty);
        var data = JsonSerializer.SerializeToUtf8Bytes(record);
        string target = Path.Combine(folder, "session.json"), temp = Path.Combine(folder, "session.new");
        using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            file.Write(data);
            file.Flush(true);
        }
        for (int attempt = 0; ; attempt++)
        {
            try { File.Move(temp, target, true); break; }
            catch (IOException) when (attempt < 3) { Thread.Sleep(100); }
            catch (UnauthorizedAccessException) when (attempt < 3) { Thread.Sleep(100); }
        }
    }
    public void Restore(string folder)
    {
        folder = LocalFiles.Validate(folder);
        var path = Path.Combine(folder, "session.json");
        if (new FileInfo(path).Length > 350L * 1024 * 1024)
            throw new InvalidDataException("La sesión supera el límite.");
        var record = JsonSerializer.Deserialize<Recovery>(File.ReadAllBytes(path)) ?? throw new InvalidDataException("Sesión ilegible.");
        if (record.Version != 1)
            throw new InvalidDataException("Versión de sesión no compatible.");
        var restored = new PdfDocument(record.Pdf);
        Document.Dispose();
        Document = restored;
        readOnlyOriginal = restored.EditBlock != null ? record.Pdf : null;
        Originals = record.Originals.ToList();
        Dirty = record.Dirty;
        undo.Clear();
        redo.Clear();
    }
    public static int[] ParseRanges(string value, int count)
    {
        var result = new List<int>();
        foreach (var part in value.Split(',', StringSplitOptions.TrimEntries))
        {
            var bounds = part.Split('-', StringSplitOptions.TrimEntries);
            if (bounds.Length > 2 || !int.TryParse(bounds[0], out int a))
                throw new ArgumentException("Usa rangos como 1-3,5.");
            int b = a;
            if (bounds.Length == 2 && !int.TryParse(bounds[1], out b))
                throw new ArgumentException("Rango no válido.");
            if (a < 1 || b < a || b > count)
                throw new ArgumentException($"Rangos entre 1 y {count}.");
            for (int i = a; i <= b; i++)
                result.Add(i - 1);
            if (result.Count > 10000)
                throw new ArgumentException("Demasiadas páginas.");
        }
        return result.ToArray();
    }
    public void Dispose() => Document.Dispose();
    public sealed record Recovery(int Version, byte[] Pdf, string[] Originals, bool Dirty);
}
