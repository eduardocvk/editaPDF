using System.Runtime.InteropServices;
namespace EditorPdf.Core;
// ABI from vendor/include, PDFium chromium/8138. Windows unsigned long is 32 bits.
internal static class N
{
    const string D = "pdfium"; const CallingConvention C = CallingConvention.Cdecl;
    [DllImport(D, CallingConvention = C)] internal static extern void FPDF_InitLibrary();
    [DllImport(D, CallingConvention = C)] internal static extern IntPtr FPDF_CreateNewDocument();
    [DllImport(D, CallingConvention = C)] internal static extern IntPtr FPDF_LoadMemDocument64(IntPtr data, nuint size, IntPtr password);
    [DllImport(D, CallingConvention = C)] internal static extern uint FPDF_GetLastError();
    [DllImport(D, CallingConvention = C)] internal static extern void FPDF_CloseDocument(IntPtr d);
    [DllImport(D, CallingConvention = C)] internal static extern int FPDF_GetPageCount(IntPtr d);
    [DllImport(D, CallingConvention = C)] internal static extern IntPtr FPDF_LoadPage(IntPtr d, int i);
    [DllImport(D, CallingConvention = C)] internal static extern void FPDF_ClosePage(IntPtr p);
    [DllImport(D, CallingConvention = C)] internal static extern float FPDF_GetPageWidthF(IntPtr p);
    [DllImport(D, CallingConvention = C)] internal static extern float FPDF_GetPageHeightF(IntPtr p);
    [DllImport(D, CallingConvention = C)] internal static extern int FPDFPage_GetRotation(IntPtr p);
    [DllImport(D, CallingConvention = C)] internal static extern void FPDFPage_SetRotation(IntPtr p, int r);
    [DllImport(D, CallingConvention = C)] internal static extern IntPtr FPDFPage_New(IntPtr d, int i, double w, double h);
    [DllImport(D, CallingConvention = C)] internal static extern void FPDFPage_Delete(IntPtr d, int i);
    [DllImport(D, CallingConvention = C)] internal static extern int FPDF_ImportPagesByIndex(IntPtr d, IntPtr source, int[] pages, uint count, int at);
    [DllImport(D, CallingConvention = C)] internal static extern int FPDF_MovePages(IntPtr d, int[] pages, uint count, int at);
    [DllImport(D, CallingConvention = C)] internal static extern int FPDF_GetSecurityHandlerRevision(IntPtr d);
    [DllImport(D, CallingConvention = C)] internal static extern int FPDF_GetSignatureCount(IntPtr d);
    [DllImport(D, CallingConvention = C)] internal static extern int FPDF_GetFormType(IntPtr d);
    [DllImport(D, CallingConvention = C)] internal static extern int FPDFCatalog_IsTagged(IntPtr d);
    [DllImport(D, CallingConvention = C)] internal static extern uint FPDF_CountNamedDests(IntPtr d);
    [DllImport(D, CallingConvention = C)] internal static extern int FPDFTextObj_GetTextRenderMode(IntPtr o);
    [DllImport(D, CallingConvention = C)] internal static extern IntPtr FPDFFont_GetGlyphPath(IntPtr font, uint glyph, float size);
    [DllImport(D, CallingConvention = C)] internal static extern IntPtr FPDFBookmark_GetFirstChild(IntPtr d, IntPtr b);
    [DllImport(D, CallingConvention = C)] internal static extern int FPDFDoc_GetAttachmentCount(IntPtr d);
    [DllImport(D, CallingConvention = C)] internal static extern int FPDFPage_GetAnnotCount(IntPtr p);
    [DllImport(D, CallingConvention = C)] internal static extern IntPtr FPDFPage_GetAnnot(IntPtr p, int index);
    [DllImport(D, CallingConvention = C)] internal static extern void FPDFPage_CloseAnnot(IntPtr annotation);
    [DllImport(D, CallingConvention = C)] internal static extern int FPDFAnnot_GetSubtype(IntPtr annotation);
    [DllImport(D, CallingConvention = C)] internal static extern int FPDFAnnot_GetFlags(IntPtr annotation);
    [DllImport(D, CallingConvention = C)] internal static extern int FPDFAnnot_GetRect(IntPtr annotation, out NativeRect rect);
    [DllImport(D, CallingConvention = C, CharSet = CharSet.Ansi)] internal static extern int FPDFAnnot_HasKey(IntPtr annotation, string key);
    [DllImport(D, CallingConvention = C, CharSet = CharSet.Ansi)] internal static extern uint FPDFAnnot_GetStringValue(IntPtr annotation, string key, byte[]? buffer, uint bytes);
    [DllImport(D, CallingConvention = C)] internal static extern IntPtr FPDFAnnot_GetLink(IntPtr annotation);
    [DllImport(D, CallingConvention = C)] internal static extern IntPtr FPDFLink_GetAction(IntPtr link);
    [DllImport(D, CallingConvention = C)] internal static extern uint FPDFAction_GetType(IntPtr action);
    [DllImport(D, CallingConvention = C)] internal static extern uint FPDFAction_GetURIPath(IntPtr document, IntPtr action, byte[]? buffer, uint bytes);
    [DllImport(D, CallingConvention = C)] internal static extern int FPDFPage_CountObjects(IntPtr p);
    [DllImport(D, CallingConvention = C)] internal static extern IntPtr FPDFPage_GetObject(IntPtr p, int i);
    [DllImport(D, CallingConvention = C)] internal static extern int FPDFPageObj_GetType(IntPtr o);
    [DllImport(D, CallingConvention = C)] internal static extern int FPDFPageObj_GetBounds(IntPtr o, out float l, out float b, out float r, out float t);
    [DllImport(D, CallingConvention = C)] internal static extern int FPDFPageObj_GetMatrix(IntPtr o, out Matrix m);
    [DllImport(D, CallingConvention = C)] internal static extern int FPDFPageObj_SetMatrix(IntPtr o, ref Matrix m);
    [DllImport(D, CallingConvention = C)] internal static extern void FPDFPageObj_Transform(IntPtr o, double a, double b, double c, double d, double e, double f);
    [DllImport(D, CallingConvention = C)] internal static extern int FPDFPage_RemoveObject(IntPtr p, IntPtr o);
    [DllImport(D, CallingConvention = C)] internal static extern void FPDFPageObj_Destroy(IntPtr o);
    [DllImport(D, CallingConvention = C)] internal static extern void FPDFPage_InsertObject(IntPtr p, IntPtr o);
    [DllImport(D, CallingConvention = C)] internal static extern int FPDFPage_GenerateContent(IntPtr p);
    [DllImport(D, CallingConvention = C)] internal static extern IntPtr FPDFText_LoadPage(IntPtr p);
    [DllImport(D, CallingConvention = C)] internal static extern void FPDFText_ClosePage(IntPtr t);
    [DllImport(D, CallingConvention = C)] internal static extern int FPDFText_CountChars(IntPtr t);
    [DllImport(D, CallingConvention = C)] internal static extern int FPDFText_GetText(IntPtr t, int start, int count, byte[] buffer);
    [DllImport(D, CallingConvention = C)] internal static extern uint FPDFTextObj_GetText(IntPtr o, IntPtr t, byte[]? buffer, uint bytes);
    [DllImport(D, CallingConvention = C)] internal static extern int FPDFTextObj_GetFontSize(IntPtr o, out float size);
    [DllImport(D, CallingConvention = C)] internal static extern IntPtr FPDFTextObj_GetFont(IntPtr o);
    [DllImport(D, CallingConvention = C)] internal static extern nuint FPDFFont_GetBaseFontName(IntPtr f, byte[]? b, nuint length);
    [DllImport(D, CallingConvention = C)] internal static extern IntPtr FPDFText_LoadFont(IntPtr d, byte[] bytes, uint length, int type, int cid);
    [DllImport(D, CallingConvention = C)] internal static extern void FPDFFont_Close(IntPtr f);
    [DllImport(D, CallingConvention = C)] internal static extern IntPtr FPDFPageObj_CreateTextObj(IntPtr d, IntPtr font, float size);
    [DllImport(D, CallingConvention = C, CharSet = CharSet.Ansi)] internal static extern IntPtr FPDFPageObj_NewTextObj(IntPtr d, string font, float size);
    [DllImport(D, CallingConvention = C, CharSet = CharSet.Unicode)] internal static extern int FPDFText_SetText(IntPtr o, string text);
    [DllImport(D, CallingConvention = C)] internal static extern int FPDFPageObj_GetFillColor(IntPtr o, out uint r, out uint g, out uint b, out uint a);
    [DllImport(D, CallingConvention = C)] internal static extern int FPDFPageObj_SetFillColor(IntPtr o, uint r, uint g, uint b, uint a);
    [DllImport(D, CallingConvention = C)] internal static extern IntPtr FPDFPageObj_NewImageObj(IntPtr d);
    [DllImport(D, CallingConvention = C)] internal static extern int FPDFImageObj_SetBitmap(IntPtr pages, int count, IntPtr o, IntPtr bitmap);
    [DllImport(D, CallingConvention = C)] internal static extern IntPtr FPDFBitmap_Create(int w, int h, int alpha);
    [DllImport(D, CallingConvention = C)] internal static extern IntPtr FPDFBitmap_GetBuffer(IntPtr b);
    [DllImport(D, CallingConvention = C)] internal static extern int FPDFBitmap_GetStride(IntPtr b);
    [DllImport(D, CallingConvention = C)] internal static extern void FPDFBitmap_FillRect(IntPtr b, int l, int t, int w, int h, uint color);
    [DllImport(D, CallingConvention = C)] internal static extern void FPDF_RenderPageBitmap(IntPtr bitmap, IntPtr page, int x, int y, int w, int h, int rotation, int flags);
    [DllImport(D, CallingConvention = C)] internal static extern void FPDFBitmap_Destroy(IntPtr b);
    [UnmanagedFunctionPointer(C)] internal delegate int WriteBlock(IntPtr self, IntPtr data, uint size);
    [StructLayout(LayoutKind.Sequential)]
    internal struct Writer
    {
        public int Version; public IntPtr Write;
    }
    [DllImport(D, CallingConvention = C)] internal static extern int FPDF_SaveAsCopy(IntPtr d, ref Writer w, uint flags);
    [DllImport(D, CallingConvention = C)] internal static extern void FPDF_PageToDevice(IntPtr p, int x, int y, int w, int h, int rotation, double px, double py, out int dx, out int dy);
    [DllImport(D, CallingConvention = C)] internal static extern int FPDF_DeviceToPage(IntPtr p, int x, int y, int w, int h, int rotation, int dx, int dy, out double px, out double py);
}
[StructLayout(LayoutKind.Sequential)]
internal struct NativeRect
{
    public float Left, Top, Right, Bottom;
}
[StructLayout(LayoutKind.Sequential)]
public struct Matrix
{
    public float A, B, C, D, E, F; public Matrix(float a, float b, float c, float d, float e, float f)
    {
        A = a;
        B = b;
        C = c;
        D = d;
        E = e;
        F = f;
    }
}
