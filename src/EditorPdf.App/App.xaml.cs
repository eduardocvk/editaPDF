using System.Windows;
namespace EditorPdf.App;

public partial class App : Application
{
    FileStream? sessionLock;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            Directory.CreateDirectory(EditorPdf.Core.LocalFiles.Validate(EditorPdf.App.MainWindow.RecoveryFolder));
            sessionLock = new FileStream(Path.Combine(EditorPdf.App.MainWindow.RecoveryFolder, "instance.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            var window = new MainWindow(restore: e.Args.Length == 0);
            MainWindow = window;
            window.Show();
            if (e.Args.Length > 0)
                window.OpenPaths(e.Args);
        }
        catch (IOException) { MessageBox.Show("Editor PDF ya está abierto o no puede acceder a su carpeta local.", "Editor PDF"); Shutdown(); }
    }
    protected override void OnExit(ExitEventArgs e)
    {
        sessionLock?.Dispose();
        base.OnExit(e);
    }
}
