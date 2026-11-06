using iText.Forms;
using iText.Kernel.Pdf;
using Document = iText.Kernel.Pdf.PdfDocument;
namespace EditorPdf.Core;

// This layer only handles PDF structure. Rendering and original text editing stay in PDFium.
// Never enable unethical reading or open any URL from a document.
internal static class PdfStructure
{
    internal static byte[] Rewrite(byte[] bytes, Action<Document> operation)
    {
        using var input = new MemoryStream(bytes, false);
        using var output = new MemoryStream();
        using (var document = new Document(new PdfReader(input), new PdfWriter(output)))
        {
            if (document.GetReader().IsEncrypted())
                throw new InvalidOperationException("No se modifican documentos protegidos.");
            if (PdfAcroForm.GetAcroForm(document, false)?.HasXfaForm() == true)
                throw new InvalidOperationException("Los formularios XFA no están admitidos.");
            operation(document);
            document.GetDocumentInfo().SetMoreInfo("EditorPDF-License", "AGPL-3.0-or-later; iText Community copyright (c) 1998-2026 Apryse Group NV");
        }
        return output.ToArray();
    }

    internal static byte[] Move(byte[] bytes, int from, int to) => Rewrite(bytes, document => document.MovePage(from + 1, to > from ? to + 2 : to + 1));
    internal static byte[] Reorder(byte[] bytes, int[] order) => Rewrite(bytes, document =>
    {
        var current = Enumerable.Range(0, order.Length).ToList();
        for (int at = 0; at < order.Length; at++)
        {
            int from = current.IndexOf(order[at]);
            if (from == at) continue;
            document.MovePage(from + 1, at + 1);
            current.RemoveAt(from);
            current.Insert(at, order[at]);
        }
    });
    internal static byte[] ConsolidatePageContent(byte[] bytes, int page) => Rewrite(bytes, document =>
    {
        var target = document.GetPage(page + 1);
        target.GetPdfObject().Put(PdfName.Contents, new PdfStream(target.GetContentBytes()));
    });

    internal static byte[] Import(byte[] destination, byte[] source, int[] pages, int at) => Rewrite(destination, document =>
    {
        using var input = new Document(new PdfReader(new MemoryStream(source, false)));
        if (input.GetReader().IsEncrypted())
            throw new InvalidOperationException("No se importan documentos protegidos.");
        if (PdfAcroForm.GetAcroForm(input, false)?.HasXfaForm() == true)
            throw new InvalidOperationException("Los formularios XFA no están admitidos.");
        document.InitializeOutlines();
        if (input.IsTagged() && !document.IsTagged())
            document.SetTagged();
        ResolveNameCollisions(input, document);
        ResolveFormCollisions(input, document);
        input.InitializeOutlines();
        var copied = input.CopyPagesTo(pages.Select(i => i + 1).ToList(), document, at + 1, PdfAcroForm.GetAcroForm(input, false)?.GetAllFormFields().Count > 0 ? new PdfPageFormCopier() : null);
        CopyNamedDestinations(input, document, pages, copied);
        CopyAttachments(input, document);
    });

    static void ResolveFormCollisions(Document source, Document destination)
    {
        var form = PdfAcroForm.GetAcroForm(source, false);
        var existing = PdfAcroForm.GetAcroForm(destination, false);
        if (form == null || existing == null)
            return;
        var used = existing.GetAllFormFields().Keys.ToHashSet(StringComparer.Ordinal);
        foreach (var name in form.GetAllFormFields().Keys.ToArray())
        {
            string unique = name;
            for (int n = 2; used.Contains(unique); n++)
                unique = name + "_" + n;
            if (unique != name)
                form.RenameField(name, unique);
            used.Add(unique);
        }
    }

    static void ResolveNameCollisions(Document source, Document destination)
    {
        var tree = source.GetCatalog().GetNameTree(PdfName.Dests);
        var entries = tree.GetNames();
        var used = destination.GetCatalog().GetNameTree(PdfName.Dests).GetNames().Keys.Select(k => k.ToUnicodeString()).ToHashSet(StringComparer.Ordinal);
        var renames = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in entries.ToArray())
        {
            string name = item.Key.ToUnicodeString(), unique = name;
            for (int n = 2; used.Contains(unique); n++)
                unique = name + "_" + n;
            if (unique != name)
            {
                entries.Remove(item.Key);
                tree.AddEntry(new PdfString(unique), item.Value);
                renames[name] = unique;
            }
            used.Add(unique);
        }
        if (renames.Count == 0)
            return;
        var visited = new HashSet<iText.Kernel.Pdf.PdfObject>(ReferenceEqualityComparer.Instance);
        void Visit(iText.Kernel.Pdf.PdfObject? obj)
        {
            if (obj is PdfIndirectReference reference)
                obj = reference.GetRefersTo();
            if (obj == null || !visited.Add(obj))
                return;
            if (obj is PdfDictionary dictionary)
            {
                foreach (var key in dictionary.KeySet().ToArray())
                {
                    var value = dictionary.Get(key);
                    bool isDestination = key.Equals(PdfName.Dest) || key.Equals(PdfName.D) && PdfName.GoTo.Equals(dictionary.GetAsName(PdfName.S));
                    string? name = value is PdfString text ? text.ToUnicodeString() : value is PdfName named ? named.GetValue() : null;
                    if (isDestination && name != null && renames.TryGetValue(name, out var renamed))
                        dictionary.Put(key, new PdfString(renamed));
                    else
                        Visit(value);
                }
            }
            else if (obj is PdfArray array)
                for (int i = 0; i < array.Size(); i++)
                    Visit(array.Get(i));
        }
        Visit(source.GetCatalog().GetPdfObject());
    }

    static void CopyAttachments(Document source, Document destination)
    {
        var sourceTree = source.GetCatalog().GetNameTree(PdfName.EmbeddedFiles).GetNames();
        var destinationTree = destination.GetCatalog().GetNameTree(PdfName.EmbeddedFiles);
        var used = destinationTree.GetNames().Keys.Select(k => k.ToUnicodeString()).ToHashSet(StringComparer.Ordinal);
        foreach (var item in sourceTree)
        {
            string name = item.Key.ToUnicodeString(), unique = name;
            for (int n = 2; used.Contains(unique); n++)
                unique = Path.GetFileNameWithoutExtension(name) + " (" + n + ")" + Path.GetExtension(name);
            var copied = item.Value.CopyTo(destination);
            if (unique != name && copied is PdfDictionary fileSpec)
            {
                fileSpec.Put(PdfName.F, new PdfString(unique));
                fileSpec.Put(PdfName.UF, new PdfString(unique));
            }
            destinationTree.AddEntry(new PdfString(unique), copied);
            used.Add(unique);
        }
        var associated = source.GetCatalog().GetPdfObject().GetAsArray(PdfName.AF);
        if (associated == null)
            return;
        var target = destination.GetCatalog().GetPdfObject().GetAsArray(PdfName.AF) ?? new PdfArray();
        for (int i = 0; i < associated.Size(); i++)
            target.Add(associated.Get(i).CopyTo(destination));
        destination.GetCatalog().GetPdfObject().Put(PdfName.AF, target);
    }

    static void CopyNamedDestinations(Document source, Document destination, int[] pages, IList<PdfPage> copied)
    {
        var target = destination.GetCatalog().GetNameTree(PdfName.Dests);
        foreach (var item in source.GetCatalog().GetNameTree(PdfName.Dests).GetNames())
        {
            if (target.GetNames().ContainsKey(item.Key))
                continue;
            var array = item.Value as PdfArray ?? (item.Value as PdfDictionary)?.GetAsArray(PdfName.D);
            if (array == null || array.Size() == 0)
                continue;
            int page = source.GetPageNumber(array.GetAsDictionary(0)) - 1;
            int index = Array.IndexOf(pages, page);
            if (index < 0)
                continue;
            var cloned = (PdfArray)array.CopyTo(destination);
            cloned.Set(0, copied[index].GetPdfObject());
            target.AddEntry(item.Key, cloned);
        }
    }
}
