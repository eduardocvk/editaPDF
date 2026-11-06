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
        using var snapshot = new PdfDocument(document.SaveBytes());
        System.Windows.Input.Mouse.OverrideCursor=null;
        return new PrintPreviewDialog(owner, snapshot).ShowDialog() == true;
    }
}
