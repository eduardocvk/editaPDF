using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using EditorPdf.App;
using EditorPdf.Core;

static class ReductionChecks
{
    static void Assert(bool ok,string text) { if(!ok) throw new Exception(text); Console.WriteLine("PASS "+text); }
    static void Wait(Task task)
    {
        var frame=new DispatcherFrame();
        task.ContinueWith(_=>Application.Current.Dispatcher.BeginInvoke(new Action(()=>frame.Continue=false)));
        Dispatcher.PushFrame(frame); task.GetAwaiter().GetResult();
    }
    public static void Run(string output,string fixtures)
    {
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        var bytes=File.ReadAllBytes(Path.Combine(fixtures,"escaneo.pdf"));
        using var original=new PdfDocument(bytes);
        var reduced=PdfImageOptimizer.Optimize(bytes,image=>ReduceSizeDialog.Encode(image,30));
        Assert(reduced.Reduced>0 && reduced.Bytes.Length<bytes.Length*.8,"reducción real de imágenes escaneadas sin rasterizar el documento");
        using var check=new PdfDocument(reduced.Bytes); ReduceSizeDialog.Validate(original,check);
        Assert(check.Text(0).Contains("Hidden OCR text preserved 123") && check.Attachments==1 && check.Bookmarks && check.Info(0).Annotations==1,"compresión conserva OCR, adjunto, marcador y enlace");
        check.SaveNew(Path.Combine(output,"ui-reduced.pdf"));
        using var reopened=new PdfDocument(File.ReadAllBytes(Path.Combine(output,"ui-reduced.pdf"))); ReduceSizeDialog.Validate(original,reopened);
        using var dialog=new ReduceSizeDialog(new Window(),bytes,"escaneo.pdf");
        Wait(dialog.RecalculateAsync(90)); var high=dialog.Result!.Bytes.Length;
        var obsolete=dialog.RecalculateAsync(90,true); ((Slider)typeof(ReduceSizeDialog).GetField("quality",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(dialog)!).Value=25; var latest=dialog.RecalculateAsync(25); Wait(Task.WhenAll(obsolete,latest));
        Assert(dialog.Result is { Reduced:>0 } && dialog.Result.Bytes.Length<high,"barra de calidad recalcula tamaño y descarta resultados obsoletos");
        var root=(FrameworkElement)dialog.Content; root.Measure(new Size(960,700)); root.Arrange(new Rect(0,0,960,700)); root.UpdateLayout();
        var bitmap=new RenderTargetBitmap(960,700,96,96,PixelFormats.Pbgra32); bitmap.Render(root); var png=new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using(var stream=File.Create(Path.Combine(output,"reducir-tamano.png"))) png.Save(stream);
        using(var cancel=new CancellationTokenSource()) { cancel.Cancel(); bool stopped=false; try { PdfImageOptimizer.Optimize(bytes,i=>ReduceSizeDialog.Encode(i,30),cancel.Token); } catch(OperationCanceledException) { stopped=true; } Assert(stopped,"compresión cancelable"); }
        var masked=File.ReadAllBytes(Path.Combine(fixtures,"mascara-transparente.pdf"));
        var maskResult=PdfImageOptimizer.Optimize(masked,i=>throw new Exception("No se debe recomprimir una máscara ni su imagen"));
        Assert(maskResult.Reduced==0 && maskResult.Bytes.SequenceEqual(masked),"máscaras de transparencia conservan gris y dimensiones");
        using(var links=new PdfDocument(File.ReadAllBytes(Path.Combine(fixtures,"destinos-enlazados.pdf"))))
        using(var partial=new PdfDocument())
        {
            partial.Import(links,[0],0);
            Assert(partial.Info(0).Annotations==links.Info(0).Annotations,"importar página con destino omitido conserva anotaciones");
            partial.SaveNew(Path.Combine(output,"partial-links.pdf"));
        }
        using(var input=new MemoryStream(masked))
        using(var outputBytes=new MemoryStream())
        {
            using(var doc=new iText.Kernel.Pdf.PdfDocument(new iText.Kernel.Pdf.PdfReader(input),new iText.Kernel.Pdf.PdfWriter(outputBytes)))
            {
                for(int i=1;i<doc.GetNumberOfPdfObjects();i++)
                    if(doc.GetPdfObject(i) is iText.Kernel.Pdf.PdfStream image && image.GetAsStream(iText.Kernel.Pdf.PdfName.SMask) is {} mask)
                    {
                        var raw=mask.GetBytes(true); mask.SetData(raw.SelectMany(v=>new[]{v,v,v}).ToArray());
                        mask.Put(iText.Kernel.Pdf.PdfName.ColorSpace,iText.Kernel.Pdf.PdfName.DeviceRGB); mask.SetModified();
                    }
            }
            using var broken=new PdfDocument(outputBytes.ToArray());
            string fixedPath=Path.Combine(output,"auto-repaired-mask.pdf"); broken.SaveNew(fixedPath);
            PdfImageChecks.Validate(File.ReadAllBytes(fixedPath));
            Assert(true,"guardar repara automáticamente máscaras RGB sin pedir decisiones técnicas");
        }
        PdfImageChecks.Validate(masked);
        Assert(true,"validación acepta máscaras grises originales");
        var noImages=File.ReadAllBytes(Path.Combine(fixtures,"externo.pdf")); var unchanged=PdfImageOptimizer.Optimize(noImages,i=>null); Assert(unchanged.Bytes.SequenceEqual(noImages),"sin imágenes reducibles no aumenta el tamaño");
        bool blocked=false; try { using var signed=new ReduceSizeDialog(new Window(),File.ReadAllBytes(Path.Combine(fixtures,"firma-sintetica.pdf")),"firmado.pdf"); } catch(InvalidOperationException) { blocked=true; } Assert(blocked,"compresión exige copia editable en documentos firmados");
        using(var composer=new MergeComposerDialog(new Window(),Array.Empty<WorkspaceDocument>()))
        {
            foreach(var name in new[]{"firma-sintetica.pdf","protegido.pdf"})
            {
                composer.AddExternal(Path.Combine(fixtures,name)); var index=composer.Sources.Count-1;
                Assert(composer.Sources[index].BlockReason==null,"unión prepara copia editable automática de "+name);
                composer.AddPages(index,[0],composer.Entries.Count);
            }
            using var merged=composer.CreateDocument(); Assert(merged.Document.Count==2,"unión acepta páginas de las copias automáticas");
        }
        Assert(File.ReadAllBytes(Path.Combine(fixtures,"escaneo.pdf")).SequenceEqual(bytes),"original de compresión intacto");
    }
}
