using System.Printing;
using System.Windows;
using System.Windows.Controls;
using EditorPdf.Core;
namespace EditorPdf.App;

public static class LocalPrinting
{
    public static bool IsLocalPort(string port) => port.Equals("PORTPROMPT:", StringComparison.OrdinalIgnoreCase) || port.Equals("FILE:", StringComparison.OrdinalIgnoreCase) || port.Equals("XPSPort:", StringComparison.OrdinalIgnoreCase) || System.Text.RegularExpressions.Regex.IsMatch(port, @"^(USB\d+|LPT\d+:?|COM\d+:?)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    public static bool Print(Window owner, PdfDocument document)
    {
        var dialog = new PrintDialog { UserPageRangeEnabled = true, PageRange = new PageRange(1, document.Count), MinPage = 1, MaxPage = (uint)document.Count };
        if (dialog.ShowDialog() != true)
            return false;
        var queue = dialog.PrintQueue ?? throw new InvalidOperationException("Selecciona una impresora.");
        string port = queue.QueuePort?.Name ?? "";
        if (!IsLocalPort(port) && MessageBox.Show(owner, "Esta impresora puede enviar el documento por la red. ¿Imprimir en " + queue.Name + "?", "Impresora de red", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return false;
        var selected = dialog.PageRangeSelection == PageRangeSelection.UserPages
            ? Enumerable.Range(dialog.PageRange.PageFrom - 1, dialog.PageRange.PageTo - dialog.PageRange.PageFrom + 1).ToArray()
            : null;
        if (selected != null && (selected.Length == 0 || selected[0] < 0 || selected[^1] >= document.Count))
            throw new ArgumentException("El rango de impresión está fuera del documento.");
        using var snapshot = new PdfDocument(document.SaveBytes());
        var paginator = new PdfPaginator(snapshot, new Size(dialog.PrintableAreaWidth, dialog.PrintableAreaHeight), selected);
        dialog.PrintDocument(paginator, "Editor PDF");
        return true;
    }
}
