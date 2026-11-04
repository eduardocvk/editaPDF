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
        using var server = new LocalPrintServer();
        var queues = server.GetPrintQueues([EnumeratedPrintQueueTypes.Local]).ToArray();
        try
        {
            var local = queues.Where(q => IsLocalPort(q.QueuePort.Name)).ToArray();
            if (local.Length == 0)
                throw new InvalidOperationException("No hay impresoras con puerto local compatible. Usa USB o Microsoft Print to PDF.");
            var select = new ComboBox { ItemsSource = local, DisplayMemberPath = "Name", SelectedIndex = 0, MinWidth = 300, Margin = new Thickness(0, 8, 0, 12) };
            var panel = new StackPanel { Margin = new Thickness(18) };
            panel.Children.Add(new Label { Content = "Impresora local", Target = select });
            panel.Children.Add(select);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var window = new Window { Title = "Imprimir", Owner = owner, Content = panel, SizeToContent = SizeToContent.WidthAndHeight, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            var accept = new Button { Content = "Imprimir", IsDefault = true };
            accept.Click += (_, _) => window.DialogResult = true;
            buttons.Children.Add(new Button { Content = "Cancelar", IsCancel = true });
            buttons.Children.Add(accept);
            panel.Children.Add(buttons);
            if (window.ShowDialog() != true)
                return false;
            var queue = (PrintQueue)select.SelectedItem;
            var capabilities = queue.GetPrintCapabilities();
            var area = capabilities.PageImageableArea;
            var size = area == null ? new Size(793, 1122) : new Size(area.ExtentWidth, area.ExtentHeight);
            using var snapshot = new PdfDocument(document.SaveBytes());
            PrintQueue.CreateXpsDocumentWriter(queue).Write(new PdfPaginator(snapshot, size));
            return true;
        }
        finally { foreach (var queue in queues) queue.Dispose(); }
    }
}
