using System.Windows;
using EditorPdf.Core;
using Microsoft.Win32;
namespace EditorPdf.App;

public partial class MainWindow
{
    PageSelectionDialog Picker(PdfDocument source, string title, int selected = -1, int? insertion = null, bool split = false) => new(source, title, selected, insertion, page, split) { Owner = this, FontFamily = FontFamily };
    void InsertVisualClick(object sender, RoutedEventArgs e) => Run(() =>
    {
        FinishInlineText();
        var available = documents.Where(d => d != activeDocument && d.Session.Document.Count > 0).ToArray();
        if (available.Length > 0)
        {
            var choice = new System.Windows.Controls.ListBox { ItemsSource = available, DisplayMemberPath = "Header", SelectedIndex = 0, MinHeight = 150 };
            var panel = new System.Windows.Controls.StackPanel { Margin = new Thickness(16) };
            var dialog = new Window { Owner = this, Title = Ui.T("Insertar páginas"), Width = 470, SizeToContent = SizeToContent.Height, Content = panel, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            panel.Children.Add(choice);
            bool browse = false;
            var use = new System.Windows.Controls.Button { Content = Ui.T("Elegir páginas del documento"), IsDefault = true };
            var external = new System.Windows.Controls.Button { Content = Ui.T("Abrir otro PDF…") };
            var cancel = new System.Windows.Controls.Button { Content = Ui.T("Cancelar"), IsCancel = true };
            use.Click += (_,_) => dialog.DialogResult = true;
            external.Click += (_,_) => { browse=true; dialog.DialogResult=true; };
            panel.Children.Add(use); panel.Children.Add(external); panel.Children.Add(cancel);
            if(dialog.ShowDialog()!=true) return;
            if(!browse && choice.SelectedItem is WorkspaceDocument selected)
            {
                using var snapshot = new PdfDocument(selected.Session.Document.SaveBytes());
                var pages = Picker(snapshot,Ui.T("Añadir páginas"), insertion: Pdf.Count);
                if(pages.ShowDialog()==true) Change(d=>d.Import(snapshot,pages.SelectedPages,pages.InsertionIndex),pages.InsertionIndex);
                return;
            }
        }
        var files = PickPdfs(false);
        if (files.Length == 0)
            return;
        using var source = new PdfDocument(LocalFiles.Read(files[0]));
        var picker = Picker(source, Ui.T("Añadir páginas"), insertion: Pdf.Count);
        if (picker.ShowDialog() != true)
            return;
        InsertSelectedPages(files[0], picker.SelectedPages, picker.InsertionIndex);
    });
    public void InsertSelectedPages(string path, int[] pages, int at)
    {
        FinishInlineText();
        session.Import(path, pages, at);
        page = at;
        Refresh();
        Recover();
    }
    void ExtractVisualClick(object sender, RoutedEventArgs e) => Run(() =>
    {
        FinishInlineText();
        RequireDocument();
        var picker = Picker(Pdf, Ui.T("Extraer páginas"), page);
        if (picker.ShowDialog() != true)
            return;
        var save = new SaveFileDialog { Filter = "PDF (*.pdf)|*.pdf", FileName = "paginas.pdf" };
        if (save.ShowDialog(this) == true)
            ExtractSelectedPages(picker.SelectedPages, save.FileName);
    });
    public void ExtractSelectedPages(int[] pages, string path)
    {
        FinishInlineText();
        using var extracted = new PdfDocument();
        extracted.Import(Pdf, pages, 0);
        extracted.SaveNew(path);
        Status.Text = Ui.T("Páginas extraídas; original intacto.");
    }
    void SplitVisualClick(object sender, RoutedEventArgs e) => Run(() =>
    {
        FinishInlineText();
        RequireDocument();
        var picker = Picker(Pdf, Ui.T("Dividir PDF"), split: true);
        if (picker.ShowDialog() != true)
            return;
        var folder = new OpenFolderDialog { Title = Ui.T("Carpeta para los PDF divididos") };
        if (folder.ShowDialog(this) != true)
            return;
        int number = 0;
        string prefix = "division_" + Guid.NewGuid().ToString("N")[..8];
        foreach (var chunk in picker.SelectedPages.Chunk(picker.PagesPerFile))
        {
            using var part = new PdfDocument();
            part.Import(Pdf, chunk, 0);
            part.SaveNew(Path.Combine(folder.FolderName, prefix + "_" + (++number) + ".pdf"));
        }
        Status.Text = $"Guardados {number} PDF; original intacto.";
    });
}
