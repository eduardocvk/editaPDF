using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EditorPdf.Core;
namespace EditorPdf.App;

public partial class MainWindow
{
    readonly ObservableCollection<WorkspaceDocument> documents = [];
    WorkspaceDocument? activeDocument;
    bool switchingDocuments;
    void InitializeWorkspace()
    {
        switchingDocuments = true;
        activeDocument = new WorkspaceDocument(session, Ui.T("Sin título"));
        documents.Add(activeDocument);
        DocumentTabs.ItemsSource = documents;
        DocumentTabs.SelectedItem = activeDocument;
        switchingDocuments = false;
    }
    void StoreView()
    {
        if (activeDocument == null)
            return;
        activeDocument.View = new DocumentView
        {
            Page = page,
            Zoom = zoom,
            Mode = 1,
            Search = SearchBox.Text,
            Ranges = RangeBox.Text,
            Object = Selected?.Value.Index,
            Draft = [TextValue.Text, XValue.Text, YValue.Text, WValue.Text, HValue.Text],
            Substitute = FontChoice.SelectedIndex > 0,
            FontChoiceName = FontChoice.SelectedIndex > 0 ? FontChoice.SelectedItem as string : null,
            StyleDraft = [FontSizeValue.Text, ColorValue.Text],
            Inline = inlineDraft
        };
    }
    void UpdateTabHeaders()
    {
        activeDocument?.Notify();
        Title = "Editor PDF" + (activeDocument == null ? "" : " — " + activeDocument.Header);
    }
    void SwitchDocument(WorkspaceDocument target)
    {
        if (ReferenceEquals(target, activeDocument))
            return;
        FinishInlineText();
        CancelCanvasGesture(false);
        if (paragraphArmed) CancelParagraphSelection();
        StoreView();
        addTextArmed = false;
        PageImage.Cursor = Cursors.Arrow;
        switchingDocuments = true;
        try
        {
            // Equal object records in two PDFs must not retain the previous selection/draft.
            ObjectsList.SelectedItem = null;
            activeDocument = target;
            session = target.Session;
            page = target.View.Page;
            zoom = Math.Clamp(target.View.Zoom, 0.25, 4);
            SearchBox.Text = target.View.Search;
            RangeBox.Text = target.View.Ranges;
            ModeTabs.SelectedIndex = 0;
            matchQuery = "";
            matchIndex = -1;
            SearchCount.Text = "";
            CancelCanvasGesture(false);
            DocumentTabs.SelectedItem = target;
            Refresh();
            if (target.View.Inline is { } draft)
            {
                page = Math.Clamp(draft.Page, 0, Math.Max(0, Pdf.Count - 1));
                FontChoice.SelectedItem = draft.OriginalIndex == null || draft.Substitute ? draft.Family : Ui.T("Conservar original");
                BoldChoice.IsChecked = draft.Bold;
                ItalicChoice.IsChecked = draft.Italic;
                var position = Device(draft.X, draft.Y);
                BeginInlineText(new System.Windows.Point(position.X, position.Y - draft.Size * PageImage.Width / Pdf.Info(page).Width * 1.15), draft.Text, draft.OriginalIndex);
                inlineDraft = draft;
            }
            if (target.View.Object is { } index)
            {
                ObjectsList.SelectedItem = ObjectsList.Items.Cast<ObjectRow>().FirstOrDefault(r => r.Value.Index == index);
                if (Selected != null && target.View.Draft.Length == 5)
                {
                    TextValue.Text = target.View.Draft[0];
                    XValue.Text = target.View.Draft[1];
                    YValue.Text = target.View.Draft[2];
                    WValue.Text = target.View.Draft[3];
                    HValue.Text = target.View.Draft[4];
                    FontChoice.SelectedItem = target.View.FontChoiceName ?? (target.View.Substitute ? "Noto Sans" : Ui.T("Conservar original"));
                    if (target.View.StyleDraft.Length == 2)
                    {
                        FontSizeValue.Text = target.View.StyleDraft[0];
                        ColorValue.Text = target.View.StyleDraft[1];
                    }
                }
            }
            (DocumentTabs.ItemContainerGenerator.ContainerFromItem(target) as FrameworkElement)?.BringIntoView();
        }
        finally { switchingDocuments = false; }
    }
    void DocumentSelected(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized || switchingDocuments || e.Source != DocumentTabs || DocumentTabs.SelectedItem is not WorkspaceDocument target)
            return;
        Run(() => { SwitchDocument(target); Recover(); });
    }
    public void OpenDocuments(string[] paths)
    {
        FinishInlineText();
        CancelCanvasGesture(false);
        var created = new List<WorkspaceDocument>();
        try
        {
            foreach (string path in paths)
            {
                string full = LocalFiles.Validate(path);
                var existing = documents.FirstOrDefault(d => string.Equals(d.SourcePath, full, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    SwitchDocument(existing);
                    continue;
                }
                if (documents.Count + created.Count >= 32)
                    throw new InvalidOperationException(Ui.T("Límite de esta versión: 32 documentos abiertos."));
                var next = new Session();
                try
                {
                    bool copy = false;
                    try
                    {
                        next.Open(full);
                    }
                    catch (PdfPasswordException)
                    {
                        if (!ConfirmEditableCopy())
                        {
                            next.Dispose();
                            continue;
                        }
                        next.Dispose();
                        next = CreateEditableSession(full)!;
                        if (next == null)
                            continue;
                        copy = true;
                    }
                    created.Add(new WorkspaceDocument(next, Path.GetFileName(full) + (copy ? " — copia editable" : "")) { SourcePath = copy ? null : full });
                }
                catch { next.Dispose(); throw; }
            }
            if (created.Count == 0)
            {
                Recover();
                return;
            }
            foreach (var document in created)
                documents.Add(document);
            var empty = documents.Where(d => d.Session.Document.Count == 0 && !d.Session.Dirty).ToArray();
            SwitchDocument(created[0]);
            foreach (var document in empty)
            {
                documents.Remove(document);
                document.Dispose();
            }
            created.Clear();
            Recover();
            if (Pdf.EditBlock is { } reason)
                Status.Text = reason;
        }
        finally { foreach (var document in created) { if (!documents.Contains(document)) document.Dispose(); } }
    }
    public void AppendPaths(string[] paths)
    {
        FinishInlineText();
        CancelCanvasGesture(false);
        if (paths.Length == 0)
            return;
        int first = Pdf.Count;
        if (first == 0)
        {
            session.OpenMany(paths);
            activeDocument!.Title = Path.GetFileName(paths[0]);
            activeDocument.SourcePath = Path.GetFullPath(paths[0]);
        }
        else
            session.ImportMany(paths, first);
        page = Math.Min(first, Pdf.Count - 1);
        Refresh();
        Recover();
    }
    void CloseDocumentClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is not Button { DataContext: WorkspaceDocument document })
            return;
        Run(() => CloseDocument(document));
    }
    void CloseDocument(WorkspaceDocument document)
    {
        SwitchDocument(document);
        FinishInlineText();
        CancelCanvasGesture(false);
        if (HasUnappliedProperties() && MessageBox.Show(this, Ui.T("Hay cambios en las propiedades que aún no has aplicado al PDF. ¿Descartar ese borrador y cerrar la pestaña?"), Ui.T("Borrador sin aplicar"), MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes)
            return;
        if (!LeaveCurrent())
            return;
        int index = documents.IndexOf(document);
        // Clear the active reference before selection changes so no disposed session is read.
        StoreView();
        switchingDocuments = true;
        activeDocument = null;
        documents.Remove(document);
        if (documents.Count == 0)
            documents.Add(new WorkspaceDocument(new Session(), Ui.T("Sin título")));
        switchingDocuments = false;
        SwitchDocument(documents[Math.Clamp(index, 0, documents.Count - 1)]);
        document.Dispose();
    }
    bool HasUnappliedProperties()
    {
        if (Selected is not { } row)
            return false;
        var value = row.Value;
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        return TextValue.Text != value.Text || FontChoice.SelectedIndex > 0 || XValue.Text != value.Left.ToString("0.##", culture) || YValue.Text != value.Bottom.ToString("0.##", culture) || WValue.Text != (value.Right - value.Left).ToString("0.##", culture) || HValue.Text != (value.Top - value.Bottom).ToString("0.##", culture) || value.Type == 1 && (FontSizeValue.Text != value.Size.ToString("0.##", culture) || !string.Equals(ColorValue.Text, $"#{value.FillRgb:X6}", StringComparison.OrdinalIgnoreCase));
    }
    void DisposeWorkspace()
    {
        DisposeCanvas();
        foreach (var document in documents)
            document.Dispose();
    }
    bool CanCloseWorkspace()
    {
        FinishInlineText();
        CancelCanvasGesture(false);
        foreach (var document in documents.Where(d => d.Session.Dirty).ToArray())
        {
            SwitchDocument(document);
            if (!LeaveCurrent())
                return false;
        }
        if (!Recover())
            return false;
        DisposeWorkspace();
        return true;
    }
}
