using System.Runtime.InteropServices;
using System.Text;
using System.Security.Cryptography;
namespace EditorPdf.Core;

public sealed record PageInfo(int Index, float Width, float Height, int Rotation, int Annotations);
public sealed record PdfObject(int Index, int Type, string Text, string Font, float Size, float Left, float Bottom, float Right, float Top)
{
    public uint FillRgb
    {
        get; init;
    }
    public float VisualSize { get; init; }
    public bool HorizontalText { get; init; } = true;
}
public sealed record Raster(int Width, int Height, int Stride, byte[] Pixels);
public sealed class PdfDocument : IDisposable
{
    static PdfDocument()
    {
        N.FPDF_InitLibrary();
    }
    IntPtr handle; GCHandle pin; readonly List<IntPtr> fonts = [];
    PdfDocument? renderingCopy;
    bool modified;
    public int Count => N.FPDF_GetPageCount(handle);
    public bool Signed => N.FPDF_GetSignatureCount(handle) > 0;
    public bool Protected => N.FPDF_GetSecurityHandlerRevision(handle) >= 0;
    public bool Xfa => N.FPDF_GetFormType(handle) >= 2;
    public bool Forms => N.FPDF_GetFormType(handle) > 0;
    public bool Tagged => N.FPDFCatalog_IsTagged(handle) != 0;
    public bool Bookmarks => N.FPDFBookmark_GetFirstChild(handle, IntPtr.Zero) != IntPtr.Zero;
    public uint NamedDestinations => N.FPDF_CountNamedDests(handle);
    public int Attachments => N.FPDFDoc_GetAttachmentCount(handle);
    public string? EditBlock => Signed ? "El original contiene firmas digitales. Crea una copia editable para quitar las firmas sin alterar el original." : Protected ? "El original está cifrado o protegido. Puedes crear una copia editable sin restricciones." : Xfa ? "Los formularios XFA no están admitidos; el documento se abre en solo lectura." : null;
    public string? ObjectEditBlock => EditBlock ?? (Forms || Tagged ? "Puedes organizar las páginas de este PDF. La edición de objetos está limitada para conservar sus formularios o etiquetas de accesibilidad." : null);
    public PdfDocument(byte[] data)
    {
        if (data.Length > 256 * 1024 * 1024)
            throw new InvalidOperationException("Límite de esta versión: 256 MiB por PDF.");
        pin = GCHandle.Alloc(data, GCHandleType.Pinned);
        handle = N.FPDF_LoadMemDocument64(pin.AddrOfPinnedObject(), (nuint)data.Length, IntPtr.Zero);
        if (handle == IntPtr.Zero)
        {
            pin.Free();
            if (N.FPDF_GetLastError() == 4)
                throw new PdfPasswordException();
            throw new InvalidDataException($"No se pudo abrir el PDF (PDFium {N.FPDF_GetLastError()}). Puede estar dañado.");
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
        if ((structure ? EditBlock : ObjectEditBlock) is { } reason)
            throw new InvalidOperationException(reason);
        if (structure && (Forms || Tagged || Bookmarks || NamedDestinations > 0 || Enumerable.Range(0, Count).Any(i => Info(i).Annotations > 0)))
            throw new InvalidOperationException("Este PDF tiene marcadores, anotaciones o enlaces entre páginas. Esta operación aún está limitada para conservar sus referencias. Puedes añadir PDF al final y editar sus objetos.");
    }
    void Mutating(bool structure = false)
    {
        Editable(structure);
        renderingCopy?.Dispose();
        renderingCopy = null;
        modified = true;
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
                float visualSize = 0;
                bool horizontalText = true;
                if (type == 1)
                {
                    text = ObjectText(o, t);
                    N.FPDFTextObj_GetFontSize(o, out size);
                    N.FPDFPageObj_GetMatrix(o, out var textMatrix);
                    visualSize = size * MathF.Sqrt(textMatrix.A * textMatrix.A + textMatrix.B * textMatrix.B);
                    horizontalText = Math.Abs(textMatrix.B) < .01 && Math.Abs(textMatrix.C) < .01;
                    var f = N.FPDFTextObj_GetFont(o);
                    var bytes = new byte[(int)N.FPDFFont_GetBaseFontName(f, null, 0)];
                    if (bytes.Length > 0)
                    {
                        N.FPDFFont_GetBaseFontName(f, bytes, (nuint)bytes.Length);
                        font = Encoding.UTF8.GetString(bytes).TrimEnd('\0');
                    }
                }
                N.FPDFPageObj_GetFillColor(o, out var red, out var green, out var blue, out _);
                result.Add(new(j, type, text, font, size, l, b, r, top)
                {
                    FillRgb = red << 16 | green << 8 | blue,
                    VisualSize = visualSize,
                    HorizontalText = horizontalText
                });
            }
            return result;
        }
        finally { if (t != IntPtr.Zero) N.FPDFText_ClosePage(t); }
    }
    public void Blank(int at)
    {
        Mutating(true);
        var p = N.FPDFPage_New(handle, at, 595.276, 841.89);
        if (p == IntPtr.Zero)
            throw new InvalidOperationException("No se pudo crear la página.");
        try
        {
            Generate(p);
        }
        finally { N.FPDF_ClosePage(p); }
    }
    void StructureAllowed()
    {
        if (EditBlock is { } reason)
            throw new InvalidOperationException(reason);
    }
    void ReplaceBytes(byte[] bytes)
    {
        using var replacement = new PdfDocument(bytes);
        renderingCopy?.Dispose();
        renderingCopy = null;
        foreach (var font in fonts)
            N.FPDFFont_Close(font);
        fonts.Clear();
        N.FPDF_CloseDocument(handle);
        if (pin.IsAllocated)
            pin.Free();
        handle = replacement.handle;
        pin = replacement.pin;
        replacement.handle = IntPtr.Zero;
        replacement.pin = default;
        modified = false;
    }
    public void Import(PdfDocument source, int[] pages, int at)
    {
        if (ReferenceEquals(this, source) || at < 0 || at > Count || pages.Length == 0 || pages.Any(i => i < 0 || i >= source.Count))
            throw new ArgumentException("Rango no válido.");
        StructureAllowed();
        source.StructureAllowed();
        var bytes = PdfStructure.Import(SaveBytes(), source.SaveBytes(), pages, at);
        using var check = new PdfDocument(bytes);
        if (check.Count != Count + pages.Length)
            throw new InvalidDataException("La unión no conserva el número de páginas.");
        for (int i = 0; i < check.Count; i++)
        {
            bool imported = i >= at && i < at + pages.Length;
            var origin = imported ? source : this;
            int index = imported ? pages[i - at] : i < at ? i : i - pages.Length;
            var expected = origin.Info(index) with
            {
                Index = i
            };
            string? difference = check.Info(i) != expected ? "tamaño, rotación o anotaciones" : check.Text(i) != origin.Text(index) ? "texto" :
                !SHA256.HashData(check.Render(i, 600).Pixels).SequenceEqual(SHA256.HashData(origin.Render(index, 600).Pixels)) ? "apariencia" : null;
            if (difference != null)
                throw new InvalidDataException($"No se pudo conservar la página {index + 1} del {(imported ? "PDF añadido" : "documento actual")}: {difference}. La sesión no se ha cambiado.");
        }
        ReplaceBytes(bytes);
    }
    public void Delete(int i)
    {
        Mutating(true);
        if (Count <= 1)
            throw new InvalidOperationException("Debe quedar al menos una página.");
        using (var p = Open(i))
        {
        }
        N.FPDFPage_Delete(handle, i);
    }
    public void Duplicate(int i)
    {
        Mutating(true);
        using var copy = new PdfDocument(SaveBytes());
        Import(copy, [i], i + 1);
    }
    public void Move(int from, int to)
    {
        if (from < 0 || from >= Count || to < 0 || to >= Count)
            throw new ArgumentOutOfRangeException(nameof(to));
        if (from == to)
            return;
        var order = Enumerable.Range(0, Count).ToList();
        order.RemoveAt(from);
        order.Insert(to, from);
        Reorder(order.ToArray());
    }
    public void Reorder(int[] order)
    {
        StructureAllowed();
        if(order.Length != Count || order.Distinct().Count() != Count || order.Any(i => i < 0 || i >= Count))
            throw new ArgumentException("Rango no válido.");
        if(order.SequenceEqual(Enumerable.Range(0,Count))) return;
        var bytes = PdfStructure.Reorder(SaveBytes(), order);
        using var check = new PdfDocument(bytes);
        if (check.Count != Count || check.Attachments != Attachments || check.Bookmarks != Bookmarks || check.Tagged != Tagged || check.NamedDestinations != NamedDestinations)
            throw new InvalidDataException("La reorganización no conserva la estructura del PDF.");
        for (int i = 0; i < Count; i++)
            if (check.Info(i) != (Info(order[i]) with
            {
                Index = i
            }) || check.Text(i) != Text(order[i]) ||
                !SHA256.HashData(check.Render(i, 600).Pixels).SequenceEqual(SHA256.HashData(Render(order[i], 600).Pixels)))
                throw new InvalidDataException("La reorganización no conserva el contenido de las páginas.");
        ReplaceBytes(bytes);
    }
    public void Rotate(int i)
    {
        Mutating(true);
        using var p = Open(i);
        N.FPDFPage_SetRotation(p.H, (N.FPDFPage_GetRotation(p.H) + 1) % 4);
    }
    static void CheckText(string text)
    {
        if (string.IsNullOrEmpty(text) || text.Any(c => char.IsControl(c) || char.IsSurrogate(c) || c >= 0x0590 && c <= 0x109F))
            throw new ArgumentException("Esta versión edita texto en una sola línea, sin escritura compleja ni caracteres fuera del plano básico Unicode.");
    }
    public static readonly string[] IncludedFontFamilies = ["Noto Sans", "Liberation Sans", "Liberation Serif", "Liberation Mono", "Liberation Sans Narrow", "Carlito", "DejaVu Sans", "DejaVu Serif", "DejaVu Sans Mono", "DejaVu Sans Condensed"];
    static string FontFile(string family, bool bold, bool italic)
    {
        if (!IncludedFontFamilies.Contains(family)) throw new ArgumentException("Selecciona una fuente incluida.");
        if (family == "Noto Sans")
        {
            if (bold || italic) throw new InvalidOperationException("Noto Sans solo incluye estilo normal; elige otra fuente para negrita o cursiva.");
            return "NotoSans-Regular.ttf";
        }
        var stem = family.Replace(" ", "");
        if (family.StartsWith("DejaVu", StringComparison.Ordinal))
            return Path.Combine("fonts", stem + (bold && italic ? family == "DejaVu Serif" ? "-BoldItalic" : "-BoldOblique" : bold ? "-Bold" : italic ? family == "DejaVu Serif" ? "-Italic" : "-Oblique" : "") + ".ttf");
        return Path.Combine("fonts", stem + "-" + (bold && italic ? "BoldItalic" : bold ? "Bold" : italic ? "Italic" : "Regular") + ".ttf");
    }
    IntPtr NewText(string text, float size, string family = "Noto Sans", bool bold = false, bool italic = false)
    {
        CheckText(text);
        if (string.IsNullOrWhiteSpace(text) || text.Length > 10000 || size < 1 || size > 500)
            throw new ArgumentException("Texto o tamaño no válido.");
        var bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "assets", FontFile(family, bold, italic)));
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
    public void AddText(int page, string text, float size, float x, float y, int orientation = 0, string family = "Noto Sans", bool bold = false, bool italic = false)
    {
        Mutating();
        using var p = Open(page);
        var o = NewText(text, size, family, bold, italic);
        var matrix = orientation switch
        {
            0 => new Matrix(1, 0, 0, 1, x, y),
            1 => new Matrix(0, 1, -1, 0, x, y),
            2 => new Matrix(-1, 0, 0, -1, x, y),
            3 => new Matrix(0, -1, 1, 0, x, y),
            _ => throw new ArgumentOutOfRangeException(nameof(orientation))
        };
        N.FPDFPageObj_SetMatrix(o, ref matrix);
        N.FPDFPage_InsertObject(p.H, o);
        Generate(p.H);
    }
    public void ReplaceText(int page, int index, string text, bool substitute = false, string family = "Noto Sans", bool bold = false, bool italic = false)
    {
        Mutating();
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
            var replacement = NewText(text, size, family, bold, italic);
            N.FPDFPageObj_SetMatrix(replacement, ref m);
            if (N.FPDFPageObj_GetFillColor(o, out var r, out var g, out var b, out var a) != 0)
                N.FPDFPageObj_SetFillColor(replacement, r, g, b, a);
            Check(N.FPDFPage_RemoveObject(p.H, o), "No se pudo quitar el objeto.");
            N.FPDFPageObj_Destroy(o);
            N.FPDFPage_InsertObject(p.H, replacement);
        }
        else
        {
            Check(N.FPDFText_SetText(o, text), "La fuente original no permite este texto. Elige una fuente en el desplegable para sustituirla en este objeto.");
        }
        Generate(p.H);
        var t = N.FPDFText_LoadPage(p.H);
        try
        {
            var obj = substitute ? N.FPDFPage_GetObject(p.H, N.FPDFPage_CountObjects(p.H) - 1) : o;
            if (ObjectText(obj, t) != text)
                throw new InvalidOperationException("La fuente original no conserva todos los caracteres. Elige una fuente en el desplegable para sustituirla en este objeto. El PDF no se ha cambiado.");
        }
        finally { N.FPDFText_ClosePage(t); }
    }
    // Rebuilds a simple horizontal paragraph as real text objects, inside Session.Execute's transaction.
    public void ReplaceParagraph(int page, int[] indices, string text, float width, string? family = null, bool bold = false, bool italic = false, int alignment = 0)
    {
        Mutating();
        if (alignment < 0 || alignment > 3) throw new ArgumentOutOfRangeException(nameof(alignment));
        if (indices.Length == 0 || indices.Distinct().Count() != indices.Length || !float.IsFinite(width) || width < 30 || width > 2000)
            throw new ArgumentException("Selecciona líneas de texto y un ancho válido.");
        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        if (normalized.Length > 10000 || string.IsNullOrWhiteSpace(normalized))
            throw new ArgumentException("Introduce texto para el párrafo.");
        var paragraphs = normalized.Split("\n\n", StringSplitOptions.None);
        if (paragraphs.Any(p => p.Split('\n').Any(string.IsNullOrWhiteSpace)) || normalized.Contains("  ") || normalized.Contains('\t'))
            throw new ArgumentException("Usa espacios simples y una sola línea vacía entre párrafos, sin tabulaciones.");
        using var p = Open(page);
        int count = N.FPDFPage_CountObjects(p.H);
        if (indices.Any(i => i < 0 || i >= count)) throw new ArgumentOutOfRangeException(nameof(indices));
        var chosen = indices.Select(i => N.FPDFPage_GetObject(p.H, i)).ToArray();
        foreach (var obj in chosen)
            if (N.FPDFPageObj_GetType(obj) != 1 || N.FPDFTextObj_GetTextRenderMode(obj) != 0)
                throw new InvalidOperationException("El párrafo contiene un objeto de texto no compatible.");
        var first = chosen.OrderByDescending(o => { N.FPDFPageObj_GetBounds(o, out _, out _, out _, out float top); return top; }).First();
        N.FPDFTextObj_GetFontSize(first, out float size);
        N.FPDFPageObj_GetMatrix(first, out var baseMatrix);
        if (Math.Abs(baseMatrix.B) > .001 || Math.Abs(baseMatrix.C) > .001 || baseMatrix.A <= 0 || baseMatrix.D <= 0)
            throw new InvalidOperationException("La edición de párrafos admite texto horizontal sin inclinación.");
        N.FPDFPageObj_GetFillColor(first, out uint red, out uint green, out uint blue, out uint alpha);
        var originalFont = N.FPDFTextObj_GetFont(first);
        var fontName = new byte[(int)N.FPDFFont_GetBaseFontName(originalFont, null, 0)];
        N.FPDFFont_GetBaseFontName(originalFont, fontName, (nuint)fontName.Length);
        string baseFont = Encoding.UTF8.GetString(fontName).TrimEnd('\0');
        foreach (var obj in chosen)
        {
            N.FPDFTextObj_GetFontSize(obj, out float itemSize);
            N.FPDFPageObj_GetMatrix(obj, out var matrix);
            N.FPDFPageObj_GetFillColor(obj, out uint itemRed, out uint itemGreen, out uint itemBlue, out uint itemAlpha);
            var font = N.FPDFTextObj_GetFont(obj);
            var bytes = new byte[(int)N.FPDFFont_GetBaseFontName(font, null, 0)];
            N.FPDFFont_GetBaseFontName(font, bytes, (nuint)bytes.Length);
            if (Math.Abs(matrix.B) > .001 || Math.Abs(matrix.C) > .001 || matrix.A <= 0 || matrix.D <= 0 || (family == null && (Math.Abs(itemSize - size) > .1 || Encoding.UTF8.GetString(bytes).TrimEnd('\0') != baseFont || Math.Abs(matrix.A - baseMatrix.A) > .001 || Math.Abs(matrix.D - baseMatrix.D) > .001 || (itemRed, itemGreen, itemBlue, itemAlpha) != (red, green, blue, alpha))))
                throw new InvalidOperationException("El párrafo contiene estilos u orientaciones distintos. Selecciona un grupo uniforme.");
        }
        var ordered = chosen.Select(o => { N.FPDFPageObj_GetBounds(o, out float l, out _, out _, out float t); N.FPDFPageObj_GetMatrix(o, out var m); return (Left: l, Top: t, Base: m.F); }).OrderByDescending(x => x.Top).ToArray();
        float spacing = ordered.Skip(1).Select(x => Math.Abs(ordered[0].Base - x.Base)).FirstOrDefault(x => x > size * .4f);
        if (spacing < size * .7f || spacing > size * 2.2f) spacing = size * 1.3f;
        var originalBounds = chosen.Select(o => { N.FPDFPageObj_GetBounds(o, out float l, out float b, out float r, out float t); return (l, b, r, t); }).ToArray();
        float startX = originalBounds.Min(x => x.l);
        float startY = ordered[0].Base;
        var prepared = new List<(IntPtr Object, string Text)>();
        int attached = 0;
        IntPtr Create(string line, float x, float y)
        {
            CheckText(line);
            var obj = family == null ? N.FPDFPageObj_CreateTextObj(handle, originalFont, size) : NewText(line, size, family, bold, italic);
            if (obj == IntPtr.Zero) throw new InvalidOperationException("No se pudo crear la línea del párrafo.");
            if (family == null && N.FPDFText_SetText(obj, line) == 0) { N.FPDFPageObj_Destroy(obj); throw new InvalidOperationException("La fuente original no admite el párrafo. Elige una fuente incluida."); }
            var matrix = baseMatrix;
            matrix.E = x;
            matrix.F = y;
            N.FPDFPageObj_SetMatrix(obj, ref matrix);
            N.FPDFPageObj_SetFillColor(obj, red, green, blue, alpha);
            return obj;
        }
        try
        {
            var lines = new List<string?>();
            foreach (string paragraph in paragraphs)
            {
                if (lines.Count > 0) lines.Add(null);
                string current = "";
                foreach (string word in paragraph.Replace('\n', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    string candidate = current.Length == 0 ? word : current + " " + word;
                    var probe = Create(candidate, startX, startY);
                    N.FPDFPageObj_GetBounds(probe, out float l, out _, out float r, out _);
                    N.FPDFPageObj_Destroy(probe);
                    if (r - l > width && current.Length > 0) { lines.Add(current); current = word; }
                    else current = candidate;
                }
                if (current.Length > 0) lines.Add(current);
            }
            if (lines.Count == 0 || lines.Count > 100) throw new ArgumentException("El párrafo contiene demasiadas líneas.");
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i] is not { } line) continue;
                float Measure(string value) { var probe=Create(value,startX,startY); N.FPDFPageObj_GetBounds(probe,out float ml,out _,out float mr,out _); N.FPDFPageObj_Destroy(probe); return mr-ml; }
                float lineWidth=Measure(line);
                var parts=new List<(string Text,float X)>();
                var words=line.Split(' ');
                if(alignment==3 && words.Length>1 && i+1<lines.Count && lines[i+1]!=null)
                {
                    float gap=Math.Max(0,(width-words.Sum(Measure))/(words.Length-1));
                    float offset=0;
                    foreach(var word in words) { parts.Add((word,startX+offset)); offset+=Measure(word)+gap; }
                }
                else parts.Add((line,startX+(alignment==1 ? width-lineWidth : alignment==2 ? (width-lineWidth)/2 : 0)));
                foreach(var part in parts)
                {
                var obj = Create(part.Text, part.X, startY - i * spacing);
                prepared.Add((obj, part.Text));
                N.FPDFPageObj_GetBounds(obj, out float l, out float b, out float r, out float t);
                if (l < 0 || b < 0 || r > Info(page).Width + 1 || t > Info(page).Height + 1 || r - l > width + 2)
                    throw new InvalidOperationException("El párrafo no cabe en la página o en el ancho elegido.");
                for (int j = 0; j < count; j++)
                {
                    if (indices.Contains(j)) continue;
                    var other = N.FPDFPage_GetObject(p.H, j);
                    if (N.FPDFPageObj_GetType(other) is not (1 or 3)) continue;
                    N.FPDFPageObj_GetBounds(other, out float ol, out float ob, out float oright, out float ot);
                    if (Math.Min(r, oright) - Math.Max(l, ol) > 2 && Math.Min(t, ot) - Math.Max(b, ob) > 2)
                        throw new InvalidOperationException("El párrafo ampliado se solaparía con otro objeto. Reduce el texto o cambia el ancho.");
                }
            }
            }
            foreach (int index in indices.OrderDescending())
            {
                var obj = N.FPDFPage_GetObject(p.H, index);
                Check(N.FPDFPage_RemoveObject(p.H, obj), "No se pudo retirar una línea original.");
                N.FPDFPageObj_Destroy(obj);
            }
            int at = indices.Min();
            foreach (var item in prepared)
            {
                if (N.FPDFPage_InsertObjectAtIndex(p.H, item.Object, (nuint)at++) == 0)
                    throw new InvalidOperationException("No se pudo insertar una línea del párrafo.");
                attached++;
            }
            Generate(p.H);
            var textPage = N.FPDFText_LoadPage(p.H);
            try
            {
                foreach (var item in prepared)
                    if (ObjectText(item.Object, textPage).Trim() != item.Text)
                        throw new InvalidOperationException("La fuente no conserva todos los caracteres del párrafo. Elige otra fuente incluida.");
            }
            finally { N.FPDFText_ClosePage(textPage); }
        }
        finally
        {
            foreach (var item in prepared.Skip(attached)) N.FPDFPageObj_Destroy(item.Object);
        }
    }
    public void RemoveObject(int page, int index)
    {
        Mutating();
        using var p = Open(page);
        var o = N.FPDFPage_GetObject(p.H, index);
        var type = N.FPDFPageObj_GetType(o);
        if (type != 1 && type != 3)
            throw new InvalidOperationException("Objeto no editable.");
        Check(N.FPDFPage_RemoveObject(p.H, o), "No se pudo eliminar el objeto.");
        N.FPDFPageObj_Destroy(o);
        Generate(p.H);
    }
    public void SetTextStyle(int page, int index, float size, uint rgb)
    {
        if (!float.IsFinite(size) || size < 1 || size > 512 || rgb > 0xFFFFFF)
            throw new ArgumentException("Usa un tamaño entre 1 y 512 pt y un color #RRGGBB.");
        Mutating();
        using var p = Open(page);
        var o = N.FPDFPage_GetObject(p.H, index);
        if (N.FPDFPageObj_GetType(o) != 1 || N.FPDFTextObj_GetTextRenderMode(o) != 0)
            throw new InvalidOperationException("Selecciona texto compatible con relleno normal.");
        Check(N.FPDFPageObj_GetFillColor(o, out _, out _, out _, out uint alpha), "No se pudo leer el color original.");
        Check(N.FPDFTextObj_SetFontSize(o, size), "PDFium no pudo cambiar el tamaño.");
        Check(N.FPDFPageObj_SetFillColor(o, rgb >> 16, rgb >> 8 & 255, rgb & 255, alpha), "No se pudo cambiar el color.");
        Generate(p.H);
    }
    public void Transform(int page, int index, float dx, float dy, float sx = 1, float sy = 1)
    {
        Mutating();
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
    // Paint order, including intervening paths/forms; this does not create PDF optional-content layers.
    public int OrderObject(int page, int index, int direction, bool extreme = false)
    {
        Editable();
        // PDFium generates each existing content stream separately. Consolidate the
        // streams first so changing the object list also changes the saved paint order.
        var consolidated = PdfStructure.ConsolidatePageContent(SaveBytes(), page);
        using (var check = new PdfDocument(consolidated))
            if (check.Info(page) != Info(page) || check.Text(page) != Text(page) || check.Objects(page).Count != Objects(page).Count ||
                !SHA256.HashData(check.Render(page, 600).Pixels).SequenceEqual(SHA256.HashData(Render(page, 600).Pixels)))
                throw new InvalidOperationException("Este PDF no permite cambiar el orden sin alterar su contenido.");
        ReplaceBytes(consolidated);
        Mutating();
        using var p = Open(page);
        int count = N.FPDFPage_CountObjects(p.H);
        if (index < 0 || index >= count || direction is not (-1 or 1))
            throw new ArgumentOutOfRangeException(nameof(index));
        var obj = N.FPDFPage_GetObject(p.H, index);
        if (N.FPDFPageObj_GetType(obj) is not (1 or 3))
            throw new InvalidOperationException("Selecciona texto o imagen.");
        int target = extreme ? direction > 0 ? count - 1 : 0 : Math.Clamp(index + direction, 0, count - 1);
        if (target == index)
            return index;
        Check(N.FPDFPage_RemoveObject(p.H, obj), "No se pudo cambiar el orden del objeto.");
        if (N.FPDFPage_InsertObjectAtIndex(p.H, obj, (nuint)target) == 0)
        {
            // The page no longer owns this object after removal.
            N.FPDFPageObj_Destroy(obj);
            throw new InvalidOperationException("PDFium no pudo colocar el objeto en ese orden.");
        }
        for (int n = 0; n < count; n++)
            N.FPDFPageObj_Transform(N.FPDFPage_GetObject(p.H, n), 1, 0, 0, 1, 0, 0);
        Generate(p.H);
        return target;
    }
    public void DuplicateImage(int page, int index)
    {
        Mutating(); using var p = Open(page);
        var original = N.FPDFPage_GetObject(p.H,index);
        if (N.FPDFPageObj_GetType(original) != 3) throw new InvalidOperationException("Selecciona una imagen.");
        var bitmap = N.FPDFImageObj_GetBitmap(original);
        if (bitmap == IntPtr.Zero) throw new InvalidOperationException("Esta imagen no se puede duplicar.");
        IntPtr copy = IntPtr.Zero;
        try { copy=N.FPDFPageObj_NewImageObj(handle); Check(N.FPDFImageObj_SetBitmap(IntPtr.Zero,0,copy,bitmap),"No se pudo duplicar la imagen."); N.FPDFPageObj_GetMatrix(original,out var matrix); matrix.E+=12; matrix.F-=12; Check(N.FPDFPageObj_SetMatrix(copy,ref matrix),"No se pudo colocar la imagen."); N.FPDFPage_InsertObject(p.H,copy); copy=IntPtr.Zero; Generate(p.H); }
        finally { if(copy!=IntPtr.Zero) N.FPDFPageObj_Destroy(copy); N.FPDFBitmap_Destroy(bitmap); }
    }
    public void AddImage(int page, int width, int height, int stride, byte[] bgra, float x, float y, float w, float h)
    {
        Mutating();
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
        // PDFium can generate annotation appearances and adjust note rectangles while
        // rendering. Keep those mutations out of the editable/saved document.
        if (i < 0 || i >= Count)
            throw new ArgumentOutOfRangeException(nameof(i));
        renderingCopy ??= new PdfDocument(!modified && pin.IsAllocated ? (byte[])pin.Target! : SaveBytes());
        return renderingCopy.RenderNative(i, width);
    }
    Raster RenderNative(int i, int width)
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
    // For disposable preview documents only. Rendering may generate annotation
    // appearances; a preview must never be used as the committed session document.
    public Raster RenderPreview(int page, int width) => RenderNative(page, width);
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
    public void SaveNew(string path, bool overwrite = false)
    {
        StructureAllowed();
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
        bytes=PdfImageChecks.Repair(bytes);
        using(var repaired=new PdfDocument(bytes))
        {
            if(repaired.Count!=Count) throw new InvalidDataException("No se pudieron conservar todas las páginas.");
            for(int i=0;i<Count;i++) if(repaired.Text(i)!=Text(i) || repaired.Info(i)!=Info(i)) throw new InvalidDataException("No se pudo conservar el contenido de la copia.");
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
            File.Move(temp, full, overwrite);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public void Dispose()
    {
        renderingCopy?.Dispose();
        renderingCopy = null;
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
