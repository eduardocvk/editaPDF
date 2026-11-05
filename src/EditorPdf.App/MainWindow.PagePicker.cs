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
        var files = PickPdfs(false);
        if (files.Length == 0)
            return;
        using var source = new PdfDocument(LocalFiles.Read(files[0]));
        var picker = Picker(source, "Añadir páginas", insertion: Pdf.Count);
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
        var picker = Picker(Pdf, "Extraer páginas", page);
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
        Status.Text = "Páginas extraídas; original intacto.";
    }
    void SplitVisualClick(object sender, RoutedEventArgs e) => Run(() =>
    {
        FinishInlineText();
        RequireDocument();
        var picker = Picker(Pdf, "Dividir PDF", split: true);
        if (picker.ShowDialog() != true)
            return;
        var folder = new OpenFolderDialog { Title = "Carpeta para los PDF divididos" };
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
