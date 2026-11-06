using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Xobject;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace EditorPdf.Core;
public static class PdfImageChecks
{
    static List<PdfStream> InvalidMasks(iText.Kernel.Pdf.PdfDocument document)
    {
        var masks=new HashSet<PdfStream>();
        for(int i=1;i<document.GetNumberOfPdfObjects();i++)
            if(document.GetPdfObject(i) is PdfStream image && PdfName.Image.Equals(image.GetAsName(PdfName.Subtype)) && image.GetAsStream(PdfName.SMask) is {} mask && !PdfName.DeviceGray.Equals(mask.GetAsName(PdfName.ColorSpace))) masks.Add(mask);
        return masks.ToList();
    }
    public static void Validate(byte[] bytes)
    {
        using var document=new iText.Kernel.Pdf.PdfDocument(new PdfReader(new MemoryStream(bytes,false)));
        if(InvalidMasks(document).Count>0) throw new InvalidDataException("No se pudo comprobar la transparencia de las imágenes.");
    }
    public static byte[] Repair(byte[] bytes)
    {
        using(var document=new iText.Kernel.Pdf.PdfDocument(new PdfReader(new MemoryStream(bytes,false))))
            if(InvalidMasks(document).Count==0) return bytes;
        var repaired=PdfStructure.Rewrite(bytes,document=>
        {
            foreach(var mask in InvalidMasks(document))
            {
                using var input=new MemoryStream(new PdfImageXObject(mask).GetImageBytes(true),false);
                var bitmap=BitmapDecoder.Create(input,BitmapCreateOptions.IgnoreImageCache,BitmapCacheOption.OnLoad).Frames[0];
                if((long)bitmap.PixelWidth*bitmap.PixelHeight>50000000) throw new InvalidDataException("Esta imagen es demasiado grande para repararla.");
                var gray=new FormatConvertedBitmap(bitmap,PixelFormats.Gray8,null,0);
                var pixels=new byte[checked(gray.PixelWidth*gray.PixelHeight)]; gray.CopyPixels(pixels,gray.PixelWidth,0);
                mask.SetData(pixels); mask.SetCompressionLevel(CompressionConstants.DEFAULT_COMPRESSION);
                mask.Put(PdfName.ColorSpace,PdfName.DeviceGray); mask.Put(PdfName.BitsPerComponent,new PdfNumber(8));
                mask.Put(PdfName.Width,new PdfNumber(gray.PixelWidth)); mask.Put(PdfName.Height,new PdfNumber(gray.PixelHeight));
                mask.Remove(PdfName.Length); mask.Remove(PdfName.Decode); mask.SetModified();
            }
        });
        Validate(repaired); return repaired;
    }
}
