using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EditorPdf.Core;
namespace EditorPdf.App;

public partial class MainWindow
{
    int[] SelectedPageIndices() => PagesList.SelectedItems.Cast<Thumb>().Select(t=>t.Index).Order().ToArray();
    void SetPageSelection(int[] indices)
    {
        bool wasRefreshing=refreshing;
        refreshing=true;
        try
        {
            PagesList.SelectedItems.Clear();
            foreach(var item in PagesList.Items.Cast<Thumb>().Where(t=>indices.Contains(t.Index))) PagesList.SelectedItems.Add(item);
        }
        finally { refreshing=wasRefreshing; }
    }
    void PagesRightClick(object sender,MouseButtonEventArgs e)
    {
        var item=ItemsControl.ContainerFromElement(PagesList,e.OriginalSource as DependencyObject) as ListBoxItem;
        if(item?.DataContext is Thumb thumb && !PagesList.SelectedItems.Contains(thumb)) PagesList.SelectedItem=thumb;
    }
    void PagesContextOpening(object sender,ContextMenuEventArgs e)
    {
        var menu=PagesList.ContextMenu!; menu.Items.Clear();
        var selected=SelectedPageIndices();
        var add=new MenuItem { Header=Ui.T("Añadir a otro documento"),Icon=ToolButton.MakeIcon("Insert"),IsEnabled=selected.Length>0 && Pdf.EditBlock==null };
        string? sourceId=activeDocument?.Id;
        foreach(var target in documents.Where(d=>d!=activeDocument))
        {
            var item=new MenuItem { Header=target.Header,IsEnabled=target.Session.Document.EditBlock==null };
            item.Click+=(_,_)=>Run(()=>CopyPagesToDocument(sourceId!,target.Id,selected));
            add.Items.Add(item);
        }
        if(add.Items.Count==0) add.IsEnabled=false;
        menu.Items.Add(add);
    }
    public void CopyPagesToDocument(string sourceId,string targetId,int[] indices)
    {
        FinishInlineText();
        var source=documents.Single(d=>d.Id==sourceId);
        var target=documents.Single(d=>d.Id==targetId);
        if(source==target) throw new ArgumentException(Ui.T("Elige otro documento."));
        var selected=indices.Distinct().Order().ToArray();
        if(selected.Length==0 || selected.Any(i=>i<0 || i>=source.Session.Document.Count)) throw new ArgumentException(Ui.T("Rango no válido."));
        if(source.Session.Document.EditBlock is { } reason) throw new InvalidOperationException(Ui.T(reason));
        using var snapshot=new PdfDocument(source.Session.Document.SaveBytes());
        int first=target.Session.Document.Count;
        target.Session.Execute(d=>d.Import(snapshot,selected,first));
        foreach(var path in source.Session.Originals)
            if(!target.Session.Originals.Contains(path,StringComparer.OrdinalIgnoreCase)) target.Session.Originals.Add(path);
        target.View.Page=first;
        target.Notify();
        if(ReferenceEquals(target,activeDocument)) { page=first; Refresh(); }
        else SwitchDocument(target);
        SetPageSelection(Enumerable.Range(first,selected.Length).ToArray());
    }
    WorkspaceDocument? DropDocument(DragEventArgs e) =>
        (ItemsControl.ContainerFromElement(DocumentTabs,e.OriginalSource as DependencyObject) as TabItem)?.DataContext as WorkspaceDocument;
    void DocumentTabDragOver(object sender,DragEventArgs e)
    {
        if(ReadPageDrag(e.Data) is not { } drag) return;
        e.Handled=true;
        var source=documents.FirstOrDefault(d=>d.Id==drag.Document);
        var target=DropDocument(e);
        e.Effects=source!=null && target!=null && source!=target && source.Session.Document.EditBlock==null && target.Session.Document.EditBlock==null && drag.Pages.All(i=>i<source.Session.Document.Count)
            ? DragDropEffects.Copy : DragDropEffects.None;
    }
    void DocumentTabDrop(object sender,DragEventArgs e)
    {
        if(ReadPageDrag(e.Data) is not { } drag) return;
        e.Handled=true;
        var target=DropDocument(e);
        if(target==null || target.Id==drag.Document) return;
        Run(()=>CopyPagesToDocument(drag.Document,target.Id,drag.Pages));
    }
    public string SuggestedSigningFileName()
    {
        string? source=activeDocument?.SourcePath ?? (session.Originals.Count==1 ? session.Originals[0] : null);
        string name=Path.GetFileNameWithoutExtension(source ?? activeDocument?.Title ?? "documento");
        foreach(char c in Path.GetInvalidFileNameChars()) name=name.Replace(c,'_');
        if(string.IsNullOrWhiteSpace(name)) name="documento";
        return name+"_para_firmar.pdf";
    }
}
