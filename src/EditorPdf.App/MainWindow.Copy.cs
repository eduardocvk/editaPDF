using System.Windows;
using System.Windows.Controls;
using EditorPdf.Core;
namespace EditorPdf.App;

public partial class MainWindow
{
    public void Receive(ActivationRequest request)
    {
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Activate();
        Run(() => { if (request.Paths.Length == 0) return; if (request.Merge) MergeDocuments(request.Paths); else OpenDocuments(request.Paths); });
    }
    public void MergeDocuments(string[] paths)
    {
        FinishInlineText();
        CancelCanvasGesture(false);
        CheckDocumentCapacity();
        var next = new Session();
        try
        {
            next.OpenMany(paths);
            var document = new WorkspaceDocument(next, "Unión de PDF");
            documents.Add(document);
            SwitchDocument(document);
            next = null!;
            Recover();
        }
        finally { next?.Dispose(); }
    }
    bool ConfirmEditableCopy() => MessageBox.Show(this,
        "Se abrirá una copia sin cifrado ni restricciones. Se eliminarán las firmas digitales, sus campos y su apariencia visible; la copia ya no acreditará la firma del original.\n\nEl original no se modificará. La copia y su recuperación se almacenarán sin contraseña solo en este equipo. ¿Continuar?",
        "Crear copia editable", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;
    string? AskPassword()
    {
        var input = new PasswordBox { Margin = new Thickness(0, 10, 0, 12), MinWidth = 320 };
        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock { Text = "Contraseña para abrir el PDF" });
        panel.Children.Add(input);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var accept = new Button { Content = "Continuar", IsDefault = true };
        var cancel = new Button { Content = "Cancelar", IsCancel = true };
        buttons.Children.Add(accept);
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);
        var dialog = new Window { Owner = this, Title = "PDF protegido", Content = panel, SizeToContent = SizeToContent.WidthAndHeight, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        accept.Click += (_, _) => dialog.DialogResult = true;
        dialog.Loaded += (_, _) => input.Focus();
        bool ok = dialog.ShowDialog() == true;
        string password = input.Password;
        input.Clear();
        return ok ? password : null;
    }
    Session? CreateEditableSession(string path, byte[]? source = null)
    {
        var next = new Session();
        try
        {
            source ??= LocalFiles.Read(path);
            string? password = null;
            while (true)
            {
                try
                {
                    next.OpenEditableCopy(source, path, password);
                    return next;
                }
                catch (PdfPasswordException)
                {
                    if (password != null)
                        MessageBox.Show(this, "La contraseña no es válida.", "PDF protegido", MessageBoxButton.OK, MessageBoxImage.Warning);
                    password = AskPassword();
                    if (password == null)
                    {
                        next.Dispose();
                        return null;
                    }
                }
            }
        }
        catch { next.Dispose(); throw; }
    }
    public void OpenEditableCopy(string path, string? password = null)
    {
        CheckDocumentCapacity();
        var next = new Session();
        try
        {
            next.OpenEditableCopy(path, password);
            var document = new WorkspaceDocument(next, Path.GetFileNameWithoutExtension(path) + " — copia editable") { SourcePath = null };
            documents.Add(document);
            SwitchDocument(document);
            next = null!;
            Recover();
        }
        finally { next?.Dispose(); }
    }
    void EditableCopyClick(object sender, RoutedEventArgs e) => Run(() =>
    {
        CheckDocumentCapacity();
        if (session.Originals.Count != 1 || !ConfirmEditableCopy())
            return;
        string path = session.Originals[0];
        var next = CreateEditableSession(path, session.EditableCopySource());
        if (next == null)
            return;
        var document = new WorkspaceDocument(next, Path.GetFileNameWithoutExtension(path) + " — copia editable");
        documents.Add(document);
        SwitchDocument(document);
        Recover();
    });
    void CheckDocumentCapacity()
    {
        if (documents.Count >= 32)
            throw new InvalidOperationException("Límite de esta versión: 32 documentos abiertos.");
    }
    void RegisterExplorerClick(object sender, RoutedEventArgs e) => Run(() =>
    {
        ExplorerIntegration.Install(Environment.ProcessPath!);
        MessageBox.Show(this, "Menú activado para tu usuario. En Mostrar más opciones encontrarás Abrir con Editor PDF y Enviar a → Unir PDF con Editor PDF. Conserva la carpeta de esta aplicación en su ubicación.", "Explorador de Windows");
    });
    void UnregisterExplorerClick(object sender, RoutedEventArgs e) => Run(() => { ExplorerIntegration.Uninstall(); Status.Text = "Opciones de Editor PDF eliminadas del Explorador."; });
}
