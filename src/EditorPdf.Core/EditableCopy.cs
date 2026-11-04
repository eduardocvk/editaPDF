using System.Security.Cryptography;
using System.Text;
using iText.Forms;
using iText.Kernel.Pdf;
using Document = iText.Kernel.Pdf.PdfDocument;
namespace EditorPdf.Core;

public sealed class PdfPasswordException : IOException
{
    public PdfPasswordException() : base("El PDF requiere una contraseña válida para abrirlo.") { }
}

public static class EditableCopy
{
    // An explicit user action creates a new unsigned/decrypted document. Never writes the source.
    public static byte[] Create(byte[] original, string? password = null)
    {
        if (original.Length > 256L * 1024 * 1024)
            throw new IOException("Límite de esta versión: 256 MiB por PDF.");
        byte[] secret = Encoding.UTF8.GetBytes(password ?? "");
        try
        {
            using var input = new MemoryStream(original, false);
            using var output = new MemoryStream();
            try
            {
                using var reader = new PdfReader(input, new ReaderProperties().SetPassword(secret));
                // Only this explicitly requested conversion permits lifting owner restrictions.
                reader.SetUnethicalReading(true);
                using (var document = new Document(reader, new PdfWriter(output)))
                {
                    var form = PdfAcroForm.GetAcroForm(document, false);
                    if (form?.HasXfaForm() == true)
                        throw new InvalidOperationException("No se convierte XFA: esta versión no puede garantizar su conservación.");
                    if (form != null)
                    {
                        foreach (var entry in form.GetAllFormFields().ToArray())
                            if (PdfName.Sig.Equals(entry.Value.GetFormType()))
                                form.RemoveField(entry.Key);
                        form.GetPdfObject().Remove(PdfName.SigFlags);
                        if (form.GetAllFormFields().Count == 0)
                            document.GetCatalog().GetPdfObject().Remove(PdfName.AcroForm);
                    }
                    // Signature certification and revocation data have no validity in this copy.
                    document.GetCatalog().GetPdfObject().Remove(PdfName.Perms);
                    document.GetCatalog().GetPdfObject().Remove(PdfName.DSS);
                    document.GetDocumentInfo().SetMoreInfo("EditorPDF-License", "AGPL-3.0-or-later; iText Community copyright (c) 1998-2026 Apryse Group NV");
                    document.GetDocumentInfo().SetMoreInfo("EditorPDF-Copy", "Unsigned editable copy; original signatures and encryption removed by user request");
                }
            }
            catch (iText.Kernel.Exceptions.BadPasswordException) { throw new PdfPasswordException(); }
            var result = output.ToArray();
            using var check = new PdfDocument(result);
            if (check.Count == 0 || check.Signed || check.Protected)
                throw new InvalidDataException("No se pudo crear una copia editable sin firmas ni cifrado.");
            // Validate against the decrypted original before committing any session or file.
            using var baselineInput = new MemoryStream(original, false);
            using var baselineOutput = new MemoryStream();
            using (var baseline = new Document(new PdfReader(baselineInput, new ReaderProperties().SetPassword(secret)).SetUnethicalReading(true), new PdfWriter(baselineOutput)))
            {
            }
            using var before = new PdfDocument(baselineOutput.ToArray());
            if (before.Count != check.Count || before.Bookmarks != check.Bookmarks || before.Attachments != check.Attachments || before.NamedDestinations != check.NamedDestinations || before.Tagged != check.Tagged)
                throw new InvalidDataException("La copia no conserva las estructuras del PDF.");
            for (int i = 0; i < check.Count; i++)
            {
                if ((before.Info(i) with
                {
                    Annotations = check.Info(i).Annotations
                }) != check.Info(i) || before.Text(i) != check.Text(i) ||
                    !SHA256.HashData(before.Render(i, 600).Pixels).SequenceEqual(SHA256.HashData(check.Render(i, 600).Pixels)))
                    throw new InvalidDataException("La copia no conserva el contenido de una página. No se ha cambiado el original.");
            }
            return result;
        }
        finally { CryptographicOperations.ZeroMemory(secret); }
    }
}
