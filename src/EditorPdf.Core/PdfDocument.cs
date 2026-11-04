using System.Runtime.InteropServices;
using System.Text;
using System.Security.Cryptography;
namespace EditorPdf.Core;

public sealed record PageInfo(int Index, float Width, float Height, int Rotation, int Annotations);
public sealed record PdfObject(int Index, int Type, string Text, string Font, float Size, float Left, float Bottom, float Right, float Top);
public sealed record Raster(int Width, int Height, int Stride, byte[] Pixels);
public sealed class PdfDocument : IDisposable
{
    static PdfDocument()
    {
        N.FPDF_InitLibrary();
    }
    IntPtr handle; GCHandle pin; readonly List<IntPtr> fonts = [];
    public int Count => N.FPDF_GetPageCount(handle);
    public bool Signed => N.FPDF_GetSignatureCount(handle) > 0;
    public bool Protected => N.FPDF_GetSecurityHandlerRevision(handle) >= 0;
    public bool Forms => N.FPDF_GetFormType(handle) > 0;
    public bool Tagged => N.FPDFCatalog_IsTagged(handle) != 0;
    public bool Bookmarks => N.FPDFBookmark_GetFirstChild(handle, IntPtr.Zero) != IntPtr.Zero;
    public uint NamedDestinations => N.FPDF_CountNamedDests(handle);
    public int Attachments => N.FPDFDoc_GetAttachmentCount(handle);
    public string? EditBlock => Signed ? "El documento contiene firmas digitales. Su edición invalidaría las firmas; abre una copia sin firmar." : Protected ? "El documento está cifrado o protegido. Esta versión no modifica documentos protegidos." : Forms ? "El documento contiene formularios. Esta versión permite visualizarlo, pero bloquea la edición para conservar su estructura." : Tagged ? "El PDF está etiquetado. Se abre en modo de solo lectura para conservar su estructura de accesibilidad." : null;
    public PdfDocument(byte[] data)
    {
        if (data.Length > 256 * 1024 * 1024)
            throw new InvalidOperationException("Límite de esta versión: 256 MiB por PDF.");
        pin = GCHandle.Alloc(data, GCHandleType.Pinned);
        handle = N.FPDF_LoadMemDocument64(pin.AddrOfPinnedObject(), (nuint)data.Length, IntPtr.Zero);
        if (handle == IntPtr.Zero)
        {
            pin.Free();
            throw new InvalidDataException($"No se pudo abrir el PDF (PDFium {N.FPDF_GetLastError()}). Puede estar dañado o requerir contraseña.");
        }
    }
    public PdfDocument()
    {
        handle = N.FPDF_CreateNewDocument();
        if (handle == IntPtr.Zero)
            throw new InvalidOperationException("No se pudo crear el documento.");
    }
    void Editable(bool structure = false)
    {
        if (EditBlock is { } reason)
            throw new InvalidOperationException(reason);
        if (structure && (Tagged || Bookmarks || NamedDestinations > 0 || Enumerable.Range(0, Count).Any(i => Info(i).Annotations > 0)))
            throw new InvalidOperationException("La reorganización está bloqueada en PDF con etiquetas, marcadores, anotaciones o destinos con nombre para no romper referencias. La edición de objetos sigue disponible.");
    }
    sealed class Page : IDisposable
    {
        public IntPtr H; public Page(IntPtr d, int i)
        {
            H = N.FPDF_LoadPage(d, i);
            if (H == IntPtr.Zero)
                throw new InvalidOperationException("No se pudo cargar la página.");
        }
        public void Dispose() => N.FPDF_ClosePage(H);
    }
    Page Open(int i)
    {
        if (i < 0 || i >= Count)
            throw new ArgumentOutOfRangeException(nameof(i));
        return new(handle, i);
    }
    static void Check(int ok, string message)
    {
        if (ok == 0)
            throw new InvalidOperationException(message);
    }
    static void Generate(IntPtr p) => Check(N.FPDFPage_GenerateContent(p), "PDFium no pudo regenerar el contenido.");
    public PageInfo Info(int i)
    {
        using var p = Open(i);
        return new(i, N.FPDF_GetPageWidthF(p.H), N.FPDF_GetPageHeightF(p.H), N.FPDFPage_GetRotation(p.H), N.FPDFPage_GetAnnotCount(p.H));
    }
    public string Text(int i)
    {
        using var p = Open(i);
        var t = N.FPDFText_LoadPage(p.H);
        if (t == IntPtr.Zero)
            throw new InvalidOperationException("No se pudo leer el texto.");
        try
        {
            var n = N.FPDFText_CountChars(t);
            var b = new byte[(n + 1) * 2];
            N.FPDFText_GetText(t, 0, n, b);
            return Encoding.Unicode.GetString(b).TrimEnd('\0');
        }
        finally { N.FPDFText_ClosePage(t); }
    }
    static string ObjectText(IntPtr o, IntPtr t)
    {
        var n = N.FPDFTextObj_GetText(o, t, null, 0);
        if (n == 0)
            return "";
        var b = new byte[n];
        N.FPDFTextObj_GetText(o, t, b, n);
        return Encoding.Unicode.GetString(b).TrimEnd('\0');
    }
    public List<PdfObject> Objects(int i)
    {
        using var p = Open(i);
        var t = N.FPDFText_LoadPage(p.H);
        try
        {
            var result = new List<PdfObject>();
            for (int j = 0; j < N.FPDFPage_CountObjects(p.H); j++)
            {
                var o = N.FPDFPage_GetObject(p.H, j);
                var type = N.FPDFPageObj_GetType(o);
                if (type != 1 && type != 3)
                    continue;
                N.FPDFPageObj_GetBounds(o, out var l, out var b, out var r, out var top);
                string text = "", font = "";
                float size = 0;
                if (type == 1)
                {
                    text = ObjectText(o, t);
                    N.FPDFTextObj_GetFontSize(o, out size);
                    var f = N.FPDFTextObj_GetFont(o);
                    var bytes = new byte[(int)N.FPDFFont_GetBaseFontName(f, null, 0)];
                    if (bytes.Length > 0)
                    {
                        N.FPDFFont_GetBaseFontName(f, bytes, (nuint)bytes.Length);
                        font = Encoding.UTF8.GetString(bytes).TrimEnd('\0');
                    }
                }
                result.Add(new(j, type, text, font, size, l, b, r, top));
            }
            return result;
        }
        finally { if (t != IntPtr.Zero) N.FPDFText_ClosePage(t); }
    }
    public void Blank(int at)
    {
        Editable(true);
        var p = N.FPDFPage_New(handle, at, 595.276, 841.89);
        if (p == IntPtr.Zero)
            throw new InvalidOperationException("No se pudo crear la página.");
        try
        {
            Generate(p);
        }
        finally { N.FPDF_ClosePage(p); }
    }
    public void Import(PdfDocument source, int[] pages, int at)
    {
        Editable(true);
        source.Editable(true);
        if (source.Attachments > 0 || Enumerable.Range(0, source.Count).Any(i => source.Info(i).Annotations > 0))
            throw new InvalidOperationException("Esta versión no importa páginas de PDF con adjuntos o anotaciones, para conservar sus referencias. Puedes editar sus objetos al abrirlos directamente.");
        if (pages.Length == 0 || pages.Any(i => i < 0 || i >= source.Count))
            throw new ArgumentException("Rango no válido.");
        Check(N.FPDF_ImportPagesByIndex(handle, source.handle, pages, (uint)pages.Length, at), "No se pudieron importar las páginas.");
    }
    public void Delete(int i)
    {
        Editable(true);
        if (Count <= 1)
            throw new InvalidOperationException("Debe quedar al menos una página.");
        using (var p = Open(i))
        {
        }
        N.FPDFPage_Delete(handle, i);
    }
    public void Duplicate(int i)
    {
        Editable(true);
        using var copy = new PdfDocument(SaveBytes());
        Import(copy, [i], i + 1);
    }
    public void Move(int from, int to)
    {
        Editable(true);
        if (to < 0 || to >= Count)
            throw new ArgumentOutOfRangeException(nameof(to));
        Check(N.FPDF_MovePages(handle, [from], 1, to), "No se pudo mover la página.");
    }
    public void Rotate(int i)
    {
        Editable(true);
        using var p = Open(i);
        N.FPDFPage_SetRotation(p.H, (N.FPDFPage_GetRotation(p.H) + 1) % 4);
    }
    static void CheckText(string text)
    {
        if (string.IsNullOrEmpty(text) || text.Any(c => char.IsControl(c) || char.IsSurrogate(c) || c >= 0x0590 && c <= 0x109F))
            throw new ArgumentException("Esta versión edita texto en una sola línea, sin escritura compleja ni caracteres fuera del plano básico Unicode.");
    }
    IntPtr NewText(string text, float size)
    {
        CheckText(text);
        if (string.IsNullOrWhiteSpace(text) || text.Length > 10000 || size < 1 || size > 500)
            throw new ArgumentException("Texto o tamaño no válido.");
        var bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "assets", "NotoSans-Regular.ttf"));
        var f = N.FPDFText_LoadFont(handle, bytes, (uint)bytes.Length, 2, 1);
        if (f == IntPtr.Zero)
            throw new InvalidOperationException("No se pudo cargar la fuente incluida.");
        fonts.Add(f);
        foreach (char c in text.Distinct())
        {
            if (!char.IsWhiteSpace(c) && N.FPDFFont_GetGlyphPath(f, c, size) == IntPtr.Zero)
                throw new InvalidOperationException("La fuente incluida no contiene alguno de los caracteres.");
        }
        var o = N.FPDFPageObj_CreateTextObj(handle, f, size);
        if (o == IntPtr.Zero)
            throw new InvalidOperationException("No se pudo crear texto.");
        if (N.FPDFText_SetText(o, text) == 0)
        {
            N.FPDFPageObj_Destroy(o);
            throw new InvalidOperationException("La fuente no admite este texto.");
        }
        return o;
    }
    public void AddText(int page, string text, float size, float x, float y)
    {
        Editable();
        using var p = Open(page);
        var o = NewText(text, size);
        N.FPDFPageObj_Transform(o, 1, 0, 0, 1, x, y);
        N.FPDFPage_InsertObject(p.H, o);
        Generate(p.H);
    }
    public void ReplaceText(int page, int index, string text, bool substitute = false)
    {
        Editable();
        CheckText(text);
        if (string.IsNullOrEmpty(text))
            throw new ArgumentException("Para quitar texto, usa Eliminar objeto.");
        using var p = Open(page);
        var o = N.FPDFPage_GetObject(p.H, index);
        if (N.FPDFPageObj_GetType(o) != 1)
            throw new InvalidOperationException("Selecciona un objeto de texto.");
        if (N.FPDFTextObj_GetTextRenderMode(o) != 0)
            throw new InvalidOperationException("Este objeto usa un modo de texto especial (contorno, máscara o invisible) y no es compatible con la sustitución de texto.");
        if (substitute)
        {
            N.FPDFPageObj_GetMatrix(o, out var m);
            N.FPDFTextObj_GetFontSize(o, out var size);
            var replacement = NewText(text, size);
            N.FPDFPageObj_SetMatrix(replacement, ref m);
            if (N.FPDFPageObj_GetFillColor(o, out var r, out var g, out var b, out var a) != 0)
                N.FPDFPageObj_SetFillColor(replacement, r, g, b, a);
            Check(N.FPDFPage_RemoveObject(p.H, o), "No se pudo quitar el objeto.");
            N.FPDFPageObj_Destroy(o);
            N.FPDFPage_InsertObject(p.H, replacement);
        }
        else
        {
            Check(N.FPDFText_SetText(o, text), "La fuente original no permite este texto. Puedes elegir sustituir por Noto Sans.");
        }
        Generate(p.H);
        var t = N.FPDFText_LoadPage(p.H);
        try
        {
            var obj = substitute ? N.FPDFPage_GetObject(p.H, N.FPDFPage_CountObjects(p.H) - 1) : o;
            if (ObjectText(obj, t) != text)
                throw new InvalidOperationException("La fuente original no conserva todos los caracteres. Deshaz esta operación o elige sustituir por Noto Sans.");
        }
        finally { N.FPDFText_ClosePage(t); }
    }
    public void RemoveObject(int page, int index)
    {
        Editable();
        using var p = Open(page);
        var o = N.FPDFPage_GetObject(p.H, index);
        var type = N.FPDFPageObj_GetType(o);
        if (type != 1 && type != 3)
            throw new InvalidOperationException("Objeto no editable.");
        Check(N.FPDFPage_RemoveObject(p.H, o), "No se pudo eliminar el objeto.");
        N.FPDFPageObj_Destroy(o);
        Generate(p.H);
    }
    public void Transform(int page, int index, float dx, float dy, float sx = 1, float sy = 1)
    {
        Editable();
        if (!float.IsFinite(dx) || !float.IsFinite(dy) || !float.IsFinite(sx) || !float.IsFinite(sy) || sx <= 0 || sy <= 0)
            throw new ArgumentException("Medidas no válidas.");
        using var p = Open(page);
        var o = N.FPDFPage_GetObject(p.H, index);
        if (N.FPDFPageObj_GetType(o) != 1 && N.FPDFPageObj_GetType(o) != 3)
            throw new InvalidOperationException("Objeto no editable.");
        Check(N.FPDFPageObj_GetBounds(o, out var l, out var b, out _, out _), "No se pudieron leer las medidas.");
        N.FPDFPageObj_Transform(o, sx, 0, 0, sy, dx + l * (1 - sx), dy + b * (1 - sy));
        Generate(p.H);
    }
    public void AddImage(int page, int width, int height, int stride, byte[] bgra, float x, float y, float w, float h)
    {
        Editable();
        using var p = Open(page);
        var bm = N.FPDFBitmap_Create(width, height, 1);
        if (bm == IntPtr.Zero)
            throw new InvalidOperationException("No se pudo crear la imagen.");
        IntPtr o = IntPtr.Zero;
        try
        {
            var dest = N.FPDFBitmap_GetBuffer(bm);
            int ds = N.FPDFBitmap_GetStride(bm);
            for (int row = 0; row < height; row++)
                Marshal.Copy(bgra, row * stride, IntPtr.Add(dest, row * ds), width * 4);
            o = N.FPDFPageObj_NewImageObj(handle);
            Check(N.FPDFImageObj_SetBitmap(IntPtr.Zero, 0, o, bm), "No se pudo insertar la imagen.");
            var m = new Matrix(w, 0, 0, h, x, y);
            Check(N.FPDFPageObj_SetMatrix(o, ref m), "No se pudo colocar la imagen.");
            N.FPDFPage_InsertObject(p.H, o);
            o = IntPtr.Zero;
            Generate(p.H);
        }
        finally { if (o != IntPtr.Zero) N.FPDFPageObj_Destroy(o); N.FPDFBitmap_Destroy(bm); }
    }
    public Raster Render(int i, int width)
    {
        using var p = Open(i);
        width = Math.Clamp(width, 50, 4096);
        int height = Math.Max(1, (int)Math.Round(width * N.FPDF_GetPageHeightF(p.H) / N.FPDF_GetPageWidthF(p.H)));
        if ((long)height * width > 32_000_000)
            throw new InvalidOperationException("Página demasiado grande para mostrar.");
        var b = N.FPDFBitmap_Create(width, height, 1);
        if (b == IntPtr.Zero)
            throw new InvalidOperationException("No hay memoria para representar la página.");
        try
        {
            N.FPDFBitmap_FillRect(b, 0, 0, width, height, 0xffffffff);
            N.FPDF_RenderPageBitmap(b, p.H, 0, 0, width, height, 0, 1);
            int stride = N.FPDFBitmap_GetStride(b);
            var bytes = new byte[stride * height];
            Marshal.Copy(N.FPDFBitmap_GetBuffer(b), bytes, 0, bytes.Length);
            return new(width, height, stride, bytes);
        }
        finally { N.FPDFBitmap_Destroy(b); }
    }
    public (double X, double Y) ToPage(int i, int width, int height, int x, int y)
    {
        using var p = Open(i);
        Check(N.FPDF_DeviceToPage(p.H, 0, 0, width, height, 0, x, y, out var px, out var py), "No se pudo convertir la posición.");
        return (px, py);
    }
    public (int X, int Y) ToDevice(int i, int width, int height, double x, double y)
    {
        using var p = Open(i);
        N.FPDF_PageToDevice(p.H, 0, 0, width, height, 0, x, y, out int dx, out int dy);
        return (dx, dy);
    }
    public byte[] SaveBytes()
    {
        using var stream = new MemoryStream();
        Exception? failure = null;
        N.WriteBlock write = (_, data, size) => { try { var b = new byte[checked((int)size)]; Marshal.Copy(data, b, 0, b.Length); stream.Write(b); return 1; } catch (Exception ex) { failure = ex; return 0; } };
        var writer = new N.Writer { Version = 1, Write = Marshal.GetFunctionPointerForDelegate(write) };
        var ok = N.FPDF_SaveAsCopy(handle, ref writer, 2);
        GC.KeepAlive(write);
        if (failure != null)
            throw new IOException("Error al guardar el PDF.", failure);
        Check(ok, "PDFium no pudo guardar el PDF.");
        return stream.ToArray();
    }
    public void SaveNew(string path)
    {
        Editable();
        path = LocalFiles.Validate(path);
        var bytes = SaveBytes();
        using (var check = new PdfDocument(bytes))
        {
            if (check.Count != Count || check.Attachments != Attachments || check.Tagged != Tagged || check.Bookmarks != Bookmarks || check.NamedDestinations != NamedDestinations)
                throw new InvalidDataException("La comprobación de estructura de la copia ha fallado.");
            for (int i = 0; i < Count; i++)
            {
                if (check.Info(i) != Info(i) || check.Text(i) != Text(i))
                    throw new InvalidDataException("La copia no conserva texto, geometría o anotaciones.");
                var before = Render(i, 600);
                var after = check.Render(i, 600);
                if (!SHA256.HashData(before.Pixels).SequenceEqual(SHA256.HashData(after.Pixels)))
                    throw new InvalidDataException("La copia no conserva la representación de la página.");
            }
        }
        var full = Path.GetFullPath(path);
        var temp = Path.Combine(Path.GetDirectoryName(full)!, ".editorpdf-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var f = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                f.Write(bytes);
                f.Flush(true);
            }
            File.Move(temp, full, false);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public void Dispose()
    {
        if (handle != IntPtr.Zero)
        {
            foreach (var f in fonts)
                N.FPDFFont_Close(f);
            N.FPDF_CloseDocument(handle);
            handle = IntPtr.Zero;
            if (pin.IsAllocated)
                pin.Free();
        }
    }
}
