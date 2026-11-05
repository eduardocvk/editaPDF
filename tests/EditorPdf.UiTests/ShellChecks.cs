using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using EditorPdf.App;
using Microsoft.Win32;

static class ShellChecks
{
    public static void Run(string fixture, string folder)
    {
        string scratch = @"Software\EditorPDF\Tests\" + Guid.NewGuid().ToString("N");
        string sendTo = Path.Combine(folder, "SendTo");
        try
        {
            using var root = Registry.CurrentUser.CreateSubKey(scratch);
            using (var preserved = root.CreateSubKey(@"Software\Classes\.pdf"))
                preserved.SetValue("", "Other.PdfApp");
            ExplorerIntegration.Install(Environment.ProcessPath!, root, sendTo);
            using (var open = root.OpenSubKey(@"Software\Classes\SystemFileAssociations\.pdf\shell\EditorPDF.Open\command"))
                if (!(open?.GetValue("") as string)?.Contains("--open") ?? true)
                    throw new Exception("Falta comando Abrir con");
            var link = ShellShortcut.Read(Path.Combine(sendTo, "Unir PDF con Editor PDF.lnk"));
            if (link.Target != Environment.ProcessPath || link.Arguments != "--merge")
                throw new Exception("Acceso Enviar a incorrecto");
            ExplorerIntegration.Uninstall(root, sendTo);
            using var other = root.OpenSubKey(@"Software\Classes\.pdf");
            if ((string?)other?.GetValue("") != "Other.PdfApp" || root.OpenSubKey(@"Software\Classes\EditorPDF.Document") != null || Directory.GetFiles(sendTo).Length != 0)
                throw new Exception("La desinstalación debe conservar otras asociaciones y retirar solo nuestro acceso");
        }
        finally { Registry.CurrentUser.DeleteSubKeyTree(scratch, false); }
        ActivationRequest? received = null;
        using (var source = new HwndSource(new HwndSourceParameters("EditorPDF activation test") { Width = 1, Height = 1, WindowStyle = 0 }))
        using (var activation = new Activation(source.Handle, r => received = r))
        {
            if (!Activation.Send(new ActivationRequest(true, [fixture, fixture]), source.Handle))
                throw new Exception("No se entregó WM_COPYDATA");
            Pump();
            if (received is not { Merge: true, Paths.Length: 2 })
                throw new Exception("La instancia pierde los PDF seleccionados");
        }
        Console.WriteLine("PASS Explorador: Abrir con, registro reversible aislado, Enviar a multiselección y activación local WM_COPYDATA");
    }
    static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () => frame.Continue = false);
        Dispatcher.PushFrame(frame);
    }
}
