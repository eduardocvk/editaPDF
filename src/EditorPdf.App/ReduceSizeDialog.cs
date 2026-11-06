using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EditorPdf.Core;
using Microsoft.Win32;
namespace EditorPdf.App;

public sealed class ReduceSizeDialog : Window, IDisposable
{
    readonly byte[] original;
    readonly PdfDocument source;
    readonly string suggestedName;
    readonly Slider quality = new() { Minimum=20, Maximum=95, Value=70, TickFrequency=5, IsSnapToTickEnabled=true, Margin=new Thickness(8) };
    readonly TextBlock sizes = new() { Margin=new Thickness(8), TextWrapping=TextWrapping.Wrap };
    readonly TextBlock details = new() { Margin=new Thickness(8), TextWrapping=TextWrapping.Wrap };
    readonly TextBlock pageLabel = new() { Margin=new Thickness(8) };
    readonly Image preview = new() { Stretch=Stretch.Uniform, Margin=new Thickness(12) };
    readonly CheckBox showOriginal = new() { Content=Ui.T("Ver original"), Margin=new Thickness(8) };
    readonly Button save = new() { Content=Ui.T("Guardar copia reducida"), IsEnabled=false };
    readonly SemaphoreSlim worker = new(1);
    CancellationTokenSource? pending;
    PdfDocument? candidate;
    OptimizationResult? result;
    bool disposed;
    int page;
    double previewZoom=1;
    public OptimizationResult? Result => result;

    public ReduceSizeDialog(Window owner, byte[] bytes, string name)
    {
        source=new(bytes);
        if(source.EditBlock is {} reason) { source.Dispose(); throw new InvalidOperationException(reason); }
        original=bytes; suggestedName=Path.GetFileNameWithoutExtension(name)+"_reducido.pdf";
        if(owner.IsVisible) Owner=owner;
        Title=Ui.T("Reducir tamaño del PDF"); Width=1000; Height=760; MinWidth=720; MinHeight=560;
        Background=Brushes.White; WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var root=new DockPanel { Margin=new Thickness(16), Background=Brushes.White };
        var controls=new StackPanel(); DockPanel.SetDock(controls,Dock.Top); root.Children.Add(controls);
        controls.Children.Add(new TextBlock { Text=Ui.T("Calidad de las imágenes"),FontWeight=FontWeights.SemiBold,Margin=new Thickness(8) });
        controls.Children.Add(quality);
        var labels=new DockPanel { Margin=new Thickness(8,0,8,0) };
        var high=new TextBlock { Text=Ui.T("Más calidad"), HorizontalAlignment=HorizontalAlignment.Right }; DockPanel.SetDock(high,Dock.Right); labels.Children.Add(high);
        labels.Children.Add(new TextBlock { Text=Ui.T("Menos tamaño") }); controls.Children.Add(labels); controls.Children.Add(sizes); controls.Children.Add(details);
        var footer=new StackPanel { Orientation=Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Right };
        var close=new Button { Content=Ui.T("Cerrar"),IsCancel=true }; footer.Children.Add(save); footer.Children.Add(close); DockPanel.SetDock(footer,Dock.Bottom); root.Children.Add(footer);
        var navigation=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center };
        void Nav(string text,Action action) { var b=new Button { Content=text,MinWidth=32 }; b.Click+=(_,_)=>action(); navigation.Children.Add(b); }
        Nav("‹",()=> { if(page>0) { page--; Render(); } }); navigation.Children.Add(pageLabel); Nav("›",()=> { if(page<source.Count-1) { page++; Render(); } });
        navigation.Children.Add(showOriginal); Nav("−",()=> { previewZoom=Math.Max(1,previewZoom/1.5); Render(); }); Nav("+",()=> { previewZoom=Math.Min(3,previewZoom*1.5); Render(); });
        DockPanel.SetDock(navigation,Dock.Top); root.Children.Add(navigation);
        root.Children.Add(new ScrollViewer { Content=preview,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Background=new SolidColorBrush(Color.FromRgb(235,239,245)) }); Content=root;
        System.Windows.Automation.AutomationProperties.SetName(quality,Ui.T("Calidad de las imágenes"));
        quality.ValueChanged+=async (_,_)=>await RecalculateAsync((int)quality.Value,true);
        showOriginal.Checked+=(_,_)=>Render(); showOriginal.Unchecked+=(_,_)=>Render();
        save.Click+=(_,_)=>SaveCopy(); Loaded+=async (_,_)=>await RecalculateAsync((int)quality.Value);
        Closed+=(_,_)=>Dispose();
    }

    public static ImageOutput? Encode(ImageInput input,int quality)
    {
        try
        {
            using var stream=new MemoryStream(input.Bytes,false);
            int maxEdge=1200+(quality-20)*32;
            var bitmap=new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption=BitmapCacheOption.OnLoad; bitmap.StreamSource=stream;
            if(Math.Max(input.Width,input.Height)>maxEdge) { if(input.Width>=input.Height) bitmap.DecodePixelWidth=maxEdge; else bitmap.DecodePixelHeight=maxEdge; }
            bitmap.EndInit(); bitmap.Freeze();
            var rgb=new FormatConvertedBitmap(bitmap,PixelFormats.Bgr24,null,0); rgb.Freeze();
            var encoder=new JpegBitmapEncoder { QualityLevel=quality }; encoder.Frames.Add(BitmapFrame.Create(rgb));
            using var output=new MemoryStream(); encoder.Save(output); return new(output.ToArray(),rgb.PixelWidth,rgb.PixelHeight);
        }
        catch(Exception ex) when(ex is NotSupportedException or FileFormatException or ArgumentException) { return null; }
    }

    public async Task RecalculateAsync(int value,bool debounce=false)
    {
        pending?.Cancel(); var cancellation=new CancellationTokenSource(); pending=cancellation; var token=cancellation.Token;
        save.IsEnabled=false; result=null; preview.Opacity=.5; sizes.Text=Ui.T("Calculando tamaño y vista previa…");
        try
        {
            if(debounce) await Task.Delay(350,token);
            await worker.WaitAsync(token);
            OptimizationResult optimized;
            try { optimized=await Task.Run(()=>PdfImageOptimizer.Optimize(original,image=>Encode(image,value),token),token); }
            finally { worker.Release(); }
            token.ThrowIfCancellationRequested(); if(disposed) return;
            var check=new PdfDocument(optimized.Bytes);
            try { Validate(source,check); } catch { check.Dispose(); throw; }
            candidate?.Dispose(); candidate=check; result=optimized;
            sizes.Text=$"{Ui.T("Original")}: {Mb(original.Length)}  →  {Ui.T("Estimado")}: {Mb(optimized.Bytes.Length)}  (−{100.0*(original.Length-optimized.Bytes.Length)/original.Length:0.#} %) · {Ui.T("Calidad")}: {value}";
            details.Text=optimized.Reduced==0 ? Ui.T("No se consigue una copia menor con estas imágenes y calidad.") : $"{Ui.T("Imágenes reducidas")}: {optimized.Reduced}/{optimized.Images}. {Ui.T("Imágenes no compatibles")}: {optimized.Skipped}.";
            save.IsEnabled=optimized.Reduced>0; preview.Opacity=1; Render();
        }
        catch(OperationCanceledException) { }
        catch(Exception ex) { if(!disposed && !token.IsCancellationRequested) { sizes.Text=Ui.T("No se pudo reducir el PDF."); details.Text=ex.Message; preview.Opacity=1; } }
        finally { if(ReferenceEquals(pending,cancellation)) pending=null; cancellation.Dispose(); }
    }
    static string Mb(int bytes)=>$"{bytes/1048576.0:0.00} MiB";
    public static void Validate(PdfDocument before,PdfDocument after)
    {
        if(before.Count!=after.Count || before.Attachments!=after.Attachments || before.Bookmarks!=after.Bookmarks || before.NamedDestinations!=after.NamedDestinations || before.Forms!=after.Forms || before.Tagged!=after.Tagged)
            throw new InvalidDataException(Ui.T("No se pudo conservar la estructura del PDF."));
        for(int i=0;i<before.Count;i++) if(before.Info(i)!=after.Info(i) || before.Text(i)!=after.Text(i)) throw new InvalidDataException(Ui.T("No se pudo conservar el texto o las páginas del PDF."));
    }
    void Render()
    {
        if(disposed) return;
        var document=showOriginal.IsChecked==true ? source : candidate??source;
        var raster=document.Render(page,(int)(720*previewZoom));
        var bitmap=BitmapSource.Create(raster.Width,raster.Height,96,96,PixelFormats.Bgra32,null,raster.Pixels,raster.Stride); bitmap.Freeze(); preview.Source=bitmap;
        preview.Width=raster.Width; preview.Height=raster.Height;
        pageLabel.Text=$"{page+1} / {document.Count}";
    }
    void SaveCopy()
    {
        if(candidate==null || !save.IsEnabled) return;
        var dialog=new SaveFileDialog { Filter="PDF (*.pdf)|*.pdf",FileName=suggestedName,Title=Ui.T("Guardar copia reducida") };
        if(dialog.ShowDialog(this)!=true) return;
        try { candidate.SaveNew(LocalFiles.Validate(dialog.FileName)); }
        catch(Exception ex) { MessageBox.Show(this,ex.Message,Title,MessageBoxButton.OK,MessageBoxImage.Warning); }
    }
    public void Dispose() { if(disposed) return; disposed=true; pending?.Cancel(); candidate?.Dispose(); source.Dispose(); }
}
