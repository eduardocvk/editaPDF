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
            // A soft mask is itself an Image XObject, but must stay grayscale and match its parent.
            var masks=new HashSet<PdfStream>();
            for(int index=1;index<document.GetNumberOfPdfObjects();index++)
                if(document.GetPdfObject(index) is PdfDictionary owner)
                    foreach(var key in new[]{PdfName.SMask,PdfName.Mask})
                        if(owner.GetAsStream(key) is {} mask) masks.Add(mask);
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
                if(masks.Contains(stream) || width<=0 || height<=0 || (long)width*height>50000000 || stream.GetAsNumber(PdfName.BitsPerComponent)?.IntValue()!=8 ||
                    (!PdfName.DeviceRGB.Equals(color) && !PdfName.DeviceGray.Equals(color)) ||
                    new[]{PdfName.SMask,PdfName.Mask,PdfName.ImageMask,PdfName.Decode}.Any(stream.ContainsKey)) { skipped++; continue; }
                var original=stream.GetBytes(false);
                ImageOutput? replacement;
                try { replacement=encode(new(new PdfImageXObject(stream).GetImageBytes(true),width,height)); }
                catch(NotSupportedException) { skipped++; continue; }
                cancellation.ThrowIfCancellationRequested();
                if(replacement==null) { skipped++; continue; }
                if(replacement.Jpeg.Length>=original.Length) continue;
                // Let iText derive the JPEG dictionary from its actual encoded components.
                var encoded=new PdfImageXObject(iText.IO.Image.ImageDataFactory.Create(replacement.Jpeg)).GetPdfObject();
                stream.SetData(replacement.Jpeg);
                stream.SetCompressionLevel(CompressionConstants.NO_COMPRESSION);
                foreach(var key in new[]{PdfName.Filter,PdfName.DecodeParms,PdfName.ColorSpace,PdfName.BitsPerComponent,PdfName.Width,PdfName.Height})
                { stream.Remove(key); if(encoded.ContainsKey(key)) stream.Put(key,encoded.Get(key)); }
                stream.Remove(PdfName.Length);
                stream.Remove(new PdfName("DL"));
                stream.SetModified();
                stream.Put(PdfName.Width,new PdfNumber(replacement.Width)); stream.Put(PdfName.Height,new PdfNumber(replacement.Height));

                reduced++;
            }
        });
        cancellation.ThrowIfCancellationRequested();
        return result.Length<bytes.Length && reduced>0 ? new(result,images,reduced,skipped) : new(bytes,images,0,skipped);
    }
}
