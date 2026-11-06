using System.Reflection;
using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EditorPdf.App;
using EditorPdf.Core;

static class PageTransferChecks
{
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static object? Call(object target,string name,params object?[] args) => target.GetType().GetMethod(name,Hidden)!.Invoke(target,args);
    static T Field<T>(object target,string name) => (T)target.GetType().GetField(name,Hidden)!.GetValue(target)!;
    static void Assert(bool condition,string message) { if(!condition) throw new Exception(message); Console.WriteLine("PASS "+message); }
    static DragEventArgs Drop(IDataObject data,DependencyObject target,Point point,RoutedEvent routed,DragDropEffects effects=DragDropEffects.Copy)

    {
        var args=(DragEventArgs)Activator.CreateInstance(typeof(DragEventArgs),BindingFlags.Instance|BindingFlags.NonPublic,null,[data,DragDropKeyStates.LeftMouseButton,effects,target,point],System.Globalization.CultureInfo.InvariantCulture)!;
        args.RoutedEvent=routed; return args;
    }
    static void Layout(FrameworkElement root,int width=1280,int height=780) { root.Measure(new Size(width,height)); root.Arrange(new Rect(0,0,width,height)); root.UpdateLayout(); }
    static void Capture(FrameworkElement root,string path,int width,int height)
    {
        var host=new Border { Background=new SolidColorBrush(Color.FromRgb(243,245,248)),Child=root };
        Layout(host,width,height); var image=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32); image.Render(host);
        var png=new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(image)); using var output=File.Create(path); png.Save(output); host.Child=null;
    }
    public static void Run(string folder,string fixtures)
    {
        string a=Path.Combine(folder,"transfer-source-a.pdf"),b=Path.Combine(folder,"transfer-source-b.pdf"),c=Path.Combine(folder,"transfer-source-c.pdf");
        void Fixture(string path,string label,int count)
        {
            using var document=new PdfDocument();
            for(int i=0;i<count;i++) { document.Blank(i); document.AddText(i,label+(i+1),16,40,700); }
            document.SaveNew(path);
        }
        Fixture(a,"A",4); Fixture(b,"B",2); Fixture(c,"C",1);
        var originalA=SHA256.HashData(File.ReadAllBytes(a));
        var window=new MainWindow(Path.Combine(folder,"unused-recovery"),false,_=>{},ex=>throw ex);
        try
        {
            window.OpenDocuments([a,b]);
            var tabs=(TabControl)window.FindName("DocumentTabs");
            var source=(WorkspaceDocument)tabs.Items[0]; var target=(WorkspaceDocument)tabs.Items[1];
            var root=(FrameworkElement)window.Content; window.Content=null; Layout(root);
            var pages=(ListBox)window.FindName("PagesList");
            Assert(pages.SelectionMode==SelectionMode.Extended,"miniaturas permiten Ctrl/Mayús con selección extendida");
            pages.SelectedItems.Clear(); pages.SelectedItems.Add(pages.Items[1]); pages.SelectedItems.Add(pages.Items[3]);
            Assert(pages.SelectedItems.Count==2,"seleccionar varias páginas conserva el grupo al mostrar la última");
            Call(window,"PagesContextOpening",pages,null);
            var submenu=pages.ContextMenu!.Items.OfType<MenuItem>().Single();
            submenu.Items.OfType<MenuItem>().Single().RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert(target.Session.Document.Count==4 && target.Session.Document.Text(2).Contains("A2") && target.Session.Document.Text(3).Contains("A4"),"menú contextual copia las páginas seleccionadas a otra pestaña");
            Assert(source.Session.Document.Count==4 && !source.Session.CanUndo && pages.SelectedItems.Count==2,"copia entre pestañas conserva origen y selecciona las páginas añadidas");
            ((Button)window.FindName("UndoButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert(target.Session.Document.Count==2 && !target.Session.CanUndo,"deshacer transferencia revierte el grupo en una operación");
            ((Button)window.FindName("RedoButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            tabs.SelectedItem=source; Layout(root);
            Assert(pages.SelectedItems.Cast<MainWindow.Thumb>().Select(t=>t.Index).Order().SequenceEqual([1,3]),"la selección de páginas se conserva por documento");
            pages.SelectedItems.Clear(); pages.SelectedItems.Add(pages.Items[0]); pages.SelectedItems.Add(pages.Items[2]);
            var header=(TabItem)tabs.ItemContainerGenerator.ContainerFromItem(target); Layout(root);
            var data=new DataObject("EditorPDF.Page",source.Id+":0,2");
            var drop=Drop(data,header,new Point(10,10),DragDrop.PreviewDropEvent);
            header.RaiseEvent(drop);
            Assert(drop.Handled && target.Session.Document.Count==6 && target.Session.Document.Text(4).Contains("A1") && target.Session.Document.Text(5).Contains("A3"),"soltar varias páginas sobre otra pestaña las añade en orden");
            tabs.SelectedItem=source;
            window.MovePagesToInsertion([0,2],4);
            Assert(source.Session.Document.Text(0).Contains("A2") && source.Session.Document.Text(1).Contains("A4") && source.Session.Document.Text(2).Contains("A1") && source.Session.Document.Text(3).Contains("A3") && pages.SelectedItems.Count==2,"reordenación de un grupo conserva el orden interno");
            ((Button)window.FindName("UndoButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert(source.Session.Document.Text(0).Contains("A1") && source.Session.Document.Text(3).Contains("A4") && !source.Session.CanUndo,"deshacer reordenación múltiple recupera todo el orden original");
            Assert(window.SuggestedSigningFileName()=="transfer-source-a_para_firmar.pdf","nombre para firmar usa el original y sufijo para_firmar");
            Capture(root,Path.Combine(folder,"panel-compacto.png"),1280,780);
            using var sessionA=new Session(); sessionA.Open(Path.Combine(fixtures,"destinos-enlazados.pdf"));
            using var sessionB=new Session(); sessionB.Open(Path.Combine(fixtures,"anotaciones.pdf"));
            using var composer=new MergeComposerDialog(window,[new WorkspaceDocument(sessionA,"Destinations.pdf"),new WorkspaceDocument(sessionB,"Annotations.pdf")]);
            var composerRoot=(FrameworkElement)composer.Content; composer.Content=null; Layout(composerRoot,1000,760);
            var output=Field<ListBox>(composer,"output");
            var token=Field<string>(composer,"dragToken");
            var dragType=typeof(MergeComposerDialog).GetNestedType("DragSelection",BindingFlags.NonPublic)!;
            object Drag(bool result,int[] indices) => Activator.CreateInstance(dragType,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance,null,[token,result,indices],null)!;
            var addData=new DataObject("EditorPDF.MergePages",Drag(false,[0,1]));
            var addDrop=Drop(addData,output,new Point(10,10),DragDrop.DropEvent);
            output.RaiseEvent(addDrop);
            Assert(addDrop.Handled && composer.Entries.Count==2,"compositor recibe varias miniaturas de origen por arrastre");
            composer.AddPages(1,[0,1],1); Layout(composerRoot,1000,760);
            var first=(ListBoxItem)output.ItemContainerGenerator.ContainerFromIndex(0);
            var reorderData=new DataObject("EditorPDF.MergePages",Drag(true,[2]));
            var reorderDrop=Drop(reorderData,first,new Point(5,1),DragDrop.DropEvent,DragDropEffects.Move);
            first.RaiseEvent(reorderDrop);
            Assert(reorderDrop.Handled && composer.Entries[0].Page.Source==composer.Sources[1] && composer.Entries[0].Page.Index==1,"compositor reordena miniaturas de resultado al soltarlas");
            composer.AddExternal(c); composer.AddPages(2,[0],2);
            Capture(composerRoot,Path.Combine(folder,"compositor-union.png"),1000,760);
            var expected=composer.Entries.Select(e=>(Text:e.Page.Source.Document.Text(e.Page.Index),Info:e.Page.Source.Document.Info(e.Page.Index))).ToArray();
            var merged=window.OpenComposition(composer);
            Assert(merged.Session.Document.Count==5 && Enumerable.Range(0,5).All(i=>merged.Session.Document.Text(i)==expected[i].Text && merged.Session.Document.Info(i).Annotations==expected[i].Info.Annotations),"compositor crea otra pestaña con el orden y anotaciones previstos");
            Assert(merged.Session.Document.NamedDestinations>0 && merged.Session.Document.Bookmarks,"compositor conserva destinos y marcadores al intercalar documentos");
            string saved=Path.Combine(folder,"ui-merge-composer.pdf"); merged.Session.SaveNew(saved);
            using(var reopened=new PdfDocument(File.ReadAllBytes(saved))) Assert(reopened.Count==5 && Enumerable.Range(0,5).All(i=>reopened.Text(i)==expected[i].Text),"resultado del compositor se guarda y reabre con todo el contenido");
            ((Button)window.FindName("UndoButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert(merged.Session.Document.Count==0 && !merged.Session.CanUndo,"crear unión es una sola operación de historial");
            ((Button)window.FindName("RedoButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert(merged.Session.Document.Count==5 && sessionA.Document.Count==2 && sessionB.Document.Count==2 && SHA256.HashData(File.ReadAllBytes(a)).SequenceEqual(originalA),"rehacer composición conserva los documentos de origen");
            window.OpenDocuments([Path.Combine(fixtures,"firma-sintetica.pdf")]);
            var signed=(WorkspaceDocument)tabs.SelectedItem;
            int targetCount=target.Session.Document.Count; bool refused=false;
            try { window.CopyPagesToDocument(signed.Id,target.Id,[0]); } catch(InvalidOperationException) { refused=true; }
            Assert(refused && target.Session.Document.Count==targetCount,"transferencia de PDF firmado se rechaza sin cambiar el destino");
        }
        finally { Call(window,"DisposeWorkspace"); }
    }
}
