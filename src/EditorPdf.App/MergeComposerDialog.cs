using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EditorPdf.Core;
using Microsoft.Win32;
namespace EditorPdf.App;

public sealed class MergeComposerDialog : Window, IDisposable
{
    public sealed class Source : IDisposable
    {
        public string Title { get; }
        public PdfDocument Document { get; }
        public string[] OriginalPaths { get; }
        public string? BlockReason { get; }
        public IReadOnlyList<Page> Pages { get; }
        public Source(string title,PdfDocument document,string[] originalPaths,string? blockReason=null)
        {
            Title=title; Document=document; OriginalPaths=originalPaths; BlockReason=blockReason ?? document.EditBlock;
            Pages=Enumerable.Range(0,document.Count).Select(i=>new Page(this,i)).ToArray();
        }
        public void Dispose() => Document.Dispose();
    }
    public sealed class Page(Source source,int index)
    {
        public Source Source { get; }=source;
        public int Index { get; }=index;
        public string Label => $"{Ui.T("Página")} {Index+1}";
        BitmapSource? thumbnail;
        public BitmapSource Image
        {
            get
            {
                if(thumbnail!=null) return thumbnail;
                var raster=Source.Document.Render(Index,140);
                thumbnail=BitmapSource.Create(raster.Width,raster.Height,96,96,PixelFormats.Bgra32,null,raster.Pixels,raster.Stride);
                thumbnail.Freeze(); return thumbnail;
            }
        }
    }
    public sealed class Entry(Page page) : INotifyPropertyChanged
    {
        public Page Page { get; }=page;
        public BitmapSource Image => Page.Image;
        public int Position { get; private set; }
        public string Label => $"{Position}. {Page.Source.Title} · {Page.Label}";
        internal void SetPosition(int position) { Position=position; PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(nameof(Label))); }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    readonly ObservableCollection<Source> sources=[];
    readonly ObservableCollection<Entry> result=[];
    readonly ComboBox sourceChoice=new() { DisplayMemberPath="Title",Margin=new Thickness(0,5,0,8) };
    readonly ListBox sourcePages=new() { SelectionMode=SelectionMode.Extended,HorizontalContentAlignment=HorizontalAlignment.Stretch };
    readonly ListBox output=new() { SelectionMode=SelectionMode.Extended,HorizontalContentAlignment=HorizontalAlignment.Stretch,AllowDrop=true };
    readonly Button create=new() { Content=Ui.T("Crear PDF"),IsDefault=true };
    readonly TextBlock error=new() { Foreground=Brushes.Firebrick,TextWrapping=TextWrapping.Wrap };
    readonly TextBlock count=new() { Margin=new Thickness(8,0,8,0) };
    readonly Button add=new() { Content=Ui.T("Añadir selección") };
    readonly string dragToken=Guid.NewGuid().ToString("N");
    const string DragFormat="EditorPDF.MergePages";
    sealed record DragSelection(string Owner,bool Result,int[] Indices);
    Point? dragStart;
    ListBox? dragList;
    bool deferSelection;
    object? pressedItem;
    AdornerLayer? markerLayer;
    DropLine? marker;
    long lastScroll;
    Session? prepared;
    public IReadOnlyList<Source> Sources => sources;
    public IReadOnlyList<Entry> Entries => result;

    public MergeComposerDialog(Window owner,IEnumerable<WorkspaceDocument> documents)
    {
        if(owner.IsVisible) Owner=owner;
        Title=Ui.T("Unir documentos"); Width=1000; Height=760; MinWidth=740; MinHeight=540;
        WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var root=new DockPanel { Margin=new Thickness(16),Background=Brushes.White };
        var footer=new DockPanel { Margin=new Thickness(0,10,0,0) };
        var buttons=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right };
        var cancel=new Button { Content=Ui.T("Cancelar"),IsCancel=true };
        buttons.Children.Add(cancel); buttons.Children.Add(create); DockPanel.SetDock(buttons,Dock.Right); footer.Children.Add(buttons); footer.Children.Add(error);
        DockPanel.SetDock(footer,Dock.Bottom); root.Children.Add(footer);
        var top=new StackPanel { Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,8) };
        var open=new ToolButton { Icon="Insert",Content="Abrir otro PDF…" }; open.Click+=(_,_)=>OpenOther(); top.Children.Add(open);
        DockPanel.SetDock(top,Dock.Top); root.Children.Add(top);
        var grid=new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(12) }); grid.ColumnDefinitions.Add(new ColumnDefinition()); root.Children.Add(grid);
        var left=new DockPanel(); Grid.SetColumn(left,0); grid.Children.Add(left);
        var sourceHeader=new StackPanel(); sourceHeader.Children.Add(new TextBlock { Text=Ui.T("Documentos de origen"),FontWeight=FontWeights.SemiBold }); sourceHeader.Children.Add(sourceChoice);
        var sourceButtons=new StackPanel { Orientation=Orientation.Horizontal };
        var all=new Button { Content=Ui.T("Añadir todas") };
        sourceButtons.Children.Add(add); sourceButtons.Children.Add(all); sourceHeader.Children.Add(sourceButtons); DockPanel.SetDock(sourceHeader,Dock.Top); left.Children.Add(sourceHeader);
        left.Children.Add(sourcePages);
        var right=new DockPanel(); Grid.SetColumn(right,2); grid.Children.Add(right);
        var resultHeader=new DockPanel { Margin=new Thickness(0,0,0,8) };
        var resultTools=new StackPanel { Orientation=Orientation.Horizontal };
        void ActionButton(string text,string icon,Action action)
        {
            var button=new ToolButton { Icon=icon,IconOnly=true,Content=text,ToolTip=Ui.T(text),Padding=new Thickness(5) };
            button.Click+=(_,_)=>Try(action); resultTools.Children.Add(button);
        }
        ActionButton("Subir","Up",()=>Nudge(-1)); ActionButton("Bajar","Down",()=>Nudge(1));
        ActionButton("Quitar selección","Delete",RemoveSelected);
        DockPanel.SetDock(resultTools,Dock.Right); resultHeader.Children.Add(resultTools);
        resultHeader.Children.Add(new TextBlock { Text=Ui.T("PDF nuevo"),FontWeight=FontWeights.SemiBold }); resultHeader.Children.Add(count);
        DockPanel.SetDock(resultHeader,Dock.Top); right.Children.Add(resultHeader); right.Children.Add(output);
        sourcePages.ItemTemplate=ThumbnailTemplate(); output.ItemTemplate=ThumbnailTemplate();
        System.Windows.Automation.AutomationProperties.SetName(sourceChoice,Ui.T("Documentos de origen"));
        System.Windows.Automation.AutomationProperties.SetName(sourcePages,Ui.T("Páginas de origen"));
        System.Windows.Automation.AutomationProperties.SetName(output,Ui.T("Páginas del PDF nuevo"));
        foreach(var list in new[] { sourcePages,output })
        {
            VirtualizingPanel.SetIsVirtualizing(list,true); VirtualizingPanel.SetVirtualizationMode(list,VirtualizationMode.Recycling);
            ScrollViewer.SetHorizontalScrollBarVisibility(list,ScrollBarVisibility.Disabled);
            list.PreviewMouseLeftButtonDown+=DragDown; list.PreviewMouseMove+=DragMove; list.PreviewMouseLeftButtonUp+=DragUp;
        }
        sourceChoice.ItemsSource=sources; output.ItemsSource=result;
        sourceChoice.SelectionChanged+=(_,_)=> { sourcePages.ItemsSource=(sourceChoice.SelectedItem as Source)?.Pages; error.Text=(sourceChoice.SelectedItem as Source)?.BlockReason ?? ""; UpdateControls(); };
        sourcePages.SelectionChanged+=(_,_)=>UpdateControls();
        add.Click+=(_,_)=>Try(()=>AddCurrent(false)); all.Click+=(_,_)=>Try(()=>AddCurrent(true));
        sourcePages.MouseDoubleClick+=(_,_)=>Try(()=>AddCurrent(false));
        output.PreviewKeyDown+=(_,e)=> { if(e.Key==Key.Delete) { Try(RemoveSelected); e.Handled=true; } };
        output.DragOver+=DragOverResult; output.Drop+=DropResult; output.DragLeave+=(_,_)=>ClearMarker();
        result.CollectionChanged+=(_,_)=>UpdateControls();
        create.Click+=(_,_)=>Try(()=> { if(result.Count==0) return; prepared?.Dispose(); prepared=null; prepared=CreateDocument(); DialogResult=true; });
        Content=root;
        try
        {
            foreach(var document in documents.Where(d=>d.Session.Document.Count>0))
                sources.Add(new Source(document.Title,new PdfDocument(document.Session.Document.SaveBytes()),document.Session.Originals.ToArray(),document.Session.Document.EditBlock));
            if(sources.Count>0) sourceChoice.SelectedIndex=0;
            UpdateControls();
        }
        catch { Dispose(); throw; }
    }
    static DataTemplate ThumbnailTemplate()
    {
        var row=new FrameworkElementFactory(typeof(StackPanel)); row.SetValue(StackPanel.OrientationProperty,Orientation.Horizontal); row.SetValue(FrameworkElement.MarginProperty,new Thickness(8));
        var border=new FrameworkElementFactory(typeof(Border)); border.SetValue(Border.BackgroundProperty,Brushes.White); border.SetValue(Border.BorderBrushProperty,Brushes.LightSlateGray); border.SetValue(Border.BorderThicknessProperty,new Thickness(1));
        var image=new FrameworkElementFactory(typeof(Image)); image.SetBinding(Image.SourceProperty,new Binding("Image")); image.SetValue(FrameworkElement.WidthProperty,100d); image.SetValue(FrameworkElement.MaxHeightProperty,145d); image.SetValue(Image.StretchProperty,Stretch.Uniform); border.AppendChild(image); row.AppendChild(border);
        var label=new FrameworkElementFactory(typeof(TextBlock)); label.SetBinding(TextBlock.TextProperty,new Binding("Label")); label.SetValue(TextBlock.TextWrappingProperty,TextWrapping.Wrap); label.SetValue(FrameworkElement.MaxWidthProperty,180d); label.SetValue(FrameworkElement.MarginProperty,new Thickness(12,0,0,0)); row.AppendChild(label);
        return new DataTemplate { VisualTree=row };
    }
    void Try(Action action)
    {
        try { Mouse.OverrideCursor=Cursors.Wait; error.Text=""; action(); }
        catch(Exception ex) when(ex is not OutOfMemoryException) { error.Text=Ui.T(ex.Message); }
        finally { Mouse.OverrideCursor=null; }
    }
    public void AddExternal(string path)
    {
        var document=new PdfDocument(LocalFiles.Read(path));
        if(document.Count==0) { document.Dispose(); throw new InvalidDataException(Ui.T("El PDF no contiene páginas.")); }
        var source=new Source(Path.GetFileName(path),document,[Path.GetFullPath(path)]);
        sources.Add(source); sourceChoice.SelectedItem=source;
    }
    void OpenOther()
    {
        var dialog=new OpenFileDialog { Filter="PDF (*.pdf)|*.pdf",Multiselect=true,CheckFileExists=true };
        if(dialog.ShowDialog(this)!=true) return;
        Try(()=> { foreach(string path in dialog.FileNames) AddExternal(path); });
    }
    void AddCurrent(bool all)
    {
        if(sourceChoice.SelectedItem is not Source source) return;
        var indices=all ? Enumerable.Range(0,source.Pages.Count).ToArray() : sourcePages.SelectedItems.Cast<Page>().Select(p=>p.Index).Order().ToArray();
        if(indices.Length>0) AddPages(sources.IndexOf(source),indices,result.Count);
    }
    public void AddPages(int sourceIndex,int[] indices,int insertion)
    {
        var source=sources[sourceIndex];
        if(source.BlockReason is { } reason) throw new InvalidOperationException(reason);
        if(indices.Length==0 || indices.Any(i=>i<0 || i>=source.Pages.Count) || insertion<0 || insertion>result.Count) throw new ArgumentException(Ui.T("Rango no válido."));
        var added=new List<Entry>();
        foreach(int index in indices) { var entry=new Entry(source.Pages[index]); result.Insert(insertion++,entry); added.Add(entry); }
        Reindex();
        output.SelectedItems.Clear(); foreach(var entry in added) output.SelectedItems.Add(entry);
    }
    public void ReorderEntries(int[] indices,int insertion)
    {
        var selected=indices.Distinct().Order().ToArray();
        if(selected.Length==0 || selected.Any(i=>i<0 || i>=result.Count) || insertion<0 || insertion>result.Count) throw new ArgumentException(Ui.T("Rango no válido."));
        var moving=selected.Select(i=>result[i]).ToArray();
        var order=result.Except(moving).ToList();
        order.InsertRange(insertion-selected.Count(i=>i<insertion),moving);
        result.Clear(); foreach(var entry in order) result.Add(entry);
        Reindex(); foreach(var entry in moving) output.SelectedItems.Add(entry);
    }
    void Reindex() { for(int i=0;i<result.Count;i++) result[i].SetPosition(i+1); UpdateControls(); }
    void UpdateControls()
    {
        create.IsEnabled=result.Count>0;
        add.IsEnabled=sourcePages.SelectedItems.Count>0 && (sourceChoice.SelectedItem as Source)?.BlockReason==null;
        count.Text=$"{result.Count} {Ui.T("Páginas")}";
    }
    void RemoveSelected() { foreach(var entry in output.SelectedItems.Cast<Entry>().ToArray()) result.Remove(entry); Reindex(); }
    void Nudge(int direction)
    {
        var indices=output.SelectedItems.Cast<Entry>().Select(e=>result.IndexOf(e)).Order().ToArray();
        if(indices.Length==0) return;
        int insertion=direction<0 ? Math.Max(0,indices[0]-1) : Math.Min(result.Count,indices[^1]+2);
        ReorderEntries(indices,insertion);
    }
    public Session CreateDocument()
    {
        if(result.Count==0) throw new InvalidOperationException(Ui.T("Añade páginas al PDF nuevo."));
        var next=new Session();
        try
        {
            next.Execute(document=>
            {
                var grouped=new List<Entry>();
                // Copy each source in one operation so its internal destinations can be mapped together.
                foreach(var group in result.GroupBy(e=>e.Page.Source))
                {
                    var entries=group.ToArray();
                    document.Import(group.Key.Document,entries.Select(e=>e.Page.Index).ToArray(),document.Count);
                    grouped.AddRange(entries);
                }
                document.Reorder(result.Select(e=>grouped.IndexOf(e)).ToArray());
            });
            next.Originals.AddRange(result.SelectMany(e=>e.Page.Source.OriginalPaths).Distinct(StringComparer.OrdinalIgnoreCase));
            return next;
        }
        catch { next.Dispose(); throw; }
    }
    public Session TakeDocument()
    {
        var document=prepared ?? CreateDocument(); prepared=null; return document;
    }
    void DragDown(object sender,MouseButtonEventArgs e)
    {
        dragList=(ListBox)sender;
        var item=ItemsControl.ContainerFromElement(dragList,e.OriginalSource as DependencyObject) as ListBoxItem;
        if(item==null) { dragStart=null; return; }
        pressedItem=item.DataContext;
        deferSelection=Keyboard.Modifiers==ModifierKeys.None && dragList.SelectedItems.Count>1 && dragList.SelectedItems.Contains(pressedItem);
        if(deferSelection) { e.Handled=true; dragList.Focus(); }
        dragStart=e.GetPosition(dragList);
    }
    void DragUp(object sender,MouseButtonEventArgs e)
    {
        if(deferSelection && dragStart!=null && dragList!=null) { dragList.SelectedItem=pressedItem; e.Handled=true; }
        deferSelection=false; dragStart=null;
    }
    void DragMove(object sender,MouseEventArgs e)
    {
        if(e.LeftButton!=MouseButtonState.Pressed) { dragStart=null; return; }
        if(dragStart is not { } start || dragList==null) return;
        var point=e.GetPosition(dragList);
        if(Math.Abs(point.X-start.X)<SystemParameters.MinimumHorizontalDragDistance && Math.Abs(point.Y-start.Y)<SystemParameters.MinimumVerticalDragDistance) return;
        var list=dragList; dragStart=null; deferSelection=false;
        int[] indices=list==output ? list.SelectedItems.Cast<Entry>().Select(e=>result.IndexOf(e)).Order().ToArray() : list.SelectedItems.Cast<Page>().Select(p=>p.Index).Order().ToArray();
        if(indices.Length==0) return;
        try { DragDrop.DoDragDrop(list,new DataObject(DragFormat,new DragSelection(dragToken,list==output,indices)),list==output ? DragDropEffects.Move : DragDropEffects.Copy); }
        finally { ClearMarker(); }
    }
    DragSelection? ReadDrag(IDataObject data) => data.GetData(DragFormat) is DragSelection selection && selection.Owner==dragToken ? selection : null;
    int Insertion(DragEventArgs e,bool show)
    {
        var item=ItemsControl.ContainerFromElement(output,e.OriginalSource as DependencyObject) as ListBoxItem;
        if(item?.DataContext is not Entry entry) { if(show) ClearMarker(); return result.Count; }
        bool after=e.GetPosition(item).Y>=item.ActualHeight/2;
        if(show)
        {
            ClearMarker(); markerLayer=AdornerLayer.GetAdornerLayer(item);
            if(markerLayer!=null) { marker=new DropLine(item,after); markerLayer.Add(marker); }
        }
        return result.IndexOf(entry)+(after ? 1 : 0);
    }
    void DragOverResult(object sender,DragEventArgs e)
    {
        if(ReadDrag(e.Data) is not { } selection) return;
        e.Handled=true;
        if(!selection.Result && (sourceChoice.SelectedItem as Source)?.BlockReason!=null) { e.Effects=DragDropEffects.None; return; }
        e.Effects=selection.Result ? DragDropEffects.Move : DragDropEffects.Copy;
        Insertion(e,true);
        if(Environment.TickCount64-lastScroll>80)
        {
            lastScroll=Environment.TickCount64;
            var viewer=FindScroll(output); double y=e.GetPosition(output).Y;
            if(y<25) viewer?.ScrollToVerticalOffset(viewer.VerticalOffset-25);
            else if(y>output.ActualHeight-25) viewer?.ScrollToVerticalOffset(viewer.VerticalOffset+25);
        }
    }
    void DropResult(object sender,DragEventArgs e)
    {
        if(ReadDrag(e.Data) is not { } selection) return;
        e.Handled=true; ClearMarker(); int insertion=Insertion(e,false);
        Try(()=> { if(selection.Result) ReorderEntries(selection.Indices,insertion); else AddPages(sourceChoice.SelectedIndex,selection.Indices,insertion); });
    }
    static ScrollViewer? FindScroll(DependencyObject parent)
    {
        if(parent is ScrollViewer viewer) return viewer;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++) if(FindScroll(VisualTreeHelper.GetChild(parent,i)) is { } child) return child;
        return null;
    }
    void ClearMarker() { if(marker!=null) markerLayer?.Remove(marker); marker=null; markerLayer=null; }
    sealed class DropLine(UIElement target,bool after) : Adorner(target)
    {
        protected override void OnRender(DrawingContext drawing) { IsHitTestVisible=false; double y=after ? AdornedElement.RenderSize.Height-1 : 1; drawing.DrawLine(new Pen(Brushes.RoyalBlue,3),new Point(0,y),new Point(AdornedElement.RenderSize.Width,y)); }
    }
    public void Dispose() { ClearMarker(); prepared?.Dispose(); prepared=null; foreach(var source in sources) source.Dispose(); sources.Clear(); }
}
