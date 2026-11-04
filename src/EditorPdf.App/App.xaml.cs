using System.Windows;
namespace EditorPdf.App;

public partial class App : Application
{
    FileStream? sessionLock;
    Activation? activation;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            if (e.Args.FirstOrDefault() is "--register-explorer" or "--unregister-explorer")
            {
                if (e.Args[0] == "--register-explorer")
                    ExplorerIntegration.Install(Environment.ProcessPath!);
                else
                    ExplorerIntegration.Uninstall();
                Shutdown();
                return;
            }
            OpenRequest(ActivationRequest.Parse(e.Args));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { MessageBox.Show(ex.Message, "Editor PDF", MessageBoxButton.OK, MessageBoxImage.Warning); Shutdown(); }
    }
    void OpenRequest(ActivationRequest request)
    {
        try
        {
            if (MainWindow is MainWindow current)
            {
                current.Receive(request);
                return;
            }
            Directory.CreateDirectory(EditorPdf.Core.LocalFiles.Validate(EditorPdf.App.MainWindow.RecoveryFolder));
            try
            {
                sessionLock = new FileStream(Path.Combine(EditorPdf.App.MainWindow.RecoveryFolder, "instance.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException)
            {
                bool sent = false;
                for (int i = 0; i < 20 && !sent; i++)
                {
                    sent = Activation.Send(request);
                    if (!sent)
                        Thread.Sleep(75);
                }
                if (!sent)
                    throw new IOException("No se pudo entregar la selección a Editor PDF. Vuelve a intentarlo cuando termine de abrirse.");
                Shutdown();
                return;
            }
            var window = new MainWindow(restore: request.Paths.Length == 0);
            MainWindow = window;
            window.SourceInitialized += (_, _) => activation = new Activation(new System.Windows.Interop.WindowInteropHelper(window).Handle, window.Receive);
            window.Closed += (_, _) => { activation?.Dispose(); activation = null; Shutdown(); };
            window.Show();
            if (request.Paths.Length > 0)
                window.Receive(request);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { MessageBox.Show(ex.Message, "Editor PDF", MessageBoxButton.OK, MessageBoxImage.Warning); if (MainWindow == null) Shutdown(); }
    }
    protected override void OnExit(ExitEventArgs e)
    {
        sessionLock?.Dispose();
        base.OnExit(e);
    }
}
