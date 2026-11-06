using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Xobject;
namespace EditorPdf.Core;

public sealed record ImageInput(byte[] Bytes, int Width, int Height);
public sealed record ImageOutput(byte[] Jpeg, int Width, int Height);
public sealed record OptimizationResult(byte[] Bytes, int Images, int Reduced, int Skipped);

// Rewrite image streams only: page content, OCR, links and document structures stay intact.
public static class PdfImageOptimizer
{
    public static OptimizationResult Optimize(byte[] bytes, Func<ImageInput, ImageOutput?> encode, CancellationToken cancellation = default)
    {
        int images=0, reduced=0, skipped=0;
        var result=PdfStructure.Rewrite(bytes, document =>
        {
            for(int i=1; i<document.GetNumberOfPdfObjects(); i++)
            {
                cancellation.ThrowIfCancellationRequested();
                var item=document.GetPdfObject(i);
                if(item is PdfDictionary signature && (PdfName.Sig.Equals(signature.GetAsName(PdfName.Type)) || signature.ContainsKey(PdfName.ByteRange)))
                    throw new InvalidOperationException("Crea primero una copia editable del PDF firmado.");
                if(item is not PdfStream stream || !PdfName.Image.Equals(stream.GetAsName(PdfName.Subtype))) continue;
                images++;
                var color=stream.GetAsName(PdfName.ColorSpace);
                int width=stream.GetAsNumber(PdfName.Width)?.IntValue()??0, height=stream.GetAsNumber(PdfName.Height)?.IntValue()??0;
                if(width<=0 || height<=0 || (long)width*height>50000000 || stream.GetAsNumber(PdfName.BitsPerComponent)?.IntValue()!=8 ||
                    (!PdfName.DeviceRGB.Equals(color) && !PdfName.DeviceGray.Equals(color)) ||
                    new[]{PdfName.SMask,PdfName.Mask,PdfName.ImageMask,PdfName.Decode}.Any(stream.ContainsKey)) { skipped++; continue; }
                var original=stream.GetBytes(false);
                ImageOutput? replacement;
                try { replacement=encode(new(new PdfImageXObject(stream).GetImageBytes(true),width,height)); }
                catch(NotSupportedException) { skipped++; continue; }
                cancellation.ThrowIfCancellationRequested();
                if(replacement==null) { skipped++; continue; }
                if(replacement.Jpeg.Length>=original.Length) continue;
                stream.SetData(replacement.Jpeg);
                stream.SetCompressionLevel(CompressionConstants.NO_COMPRESSION);
                stream.Put(PdfName.Filter,PdfName.DCTDecode); stream.Remove(PdfName.DecodeParms);
                stream.Put(PdfName.Width,new PdfNumber(replacement.Width)); stream.Put(PdfName.Height,new PdfNumber(replacement.Height));
                stream.Put(PdfName.BitsPerComponent,new PdfNumber(8)); stream.Put(PdfName.ColorSpace,PdfName.DeviceRGB);
                reduced++;
            }
        });
        cancellation.ThrowIfCancellationRequested();
        return result.Length<bytes.Length && reduced>0 ? new(result,images,reduced,skipped) : new(bytes,images,0,skipped);
    }
}
