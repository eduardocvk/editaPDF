using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows.Interop;
using EditorPdf.Core;
namespace EditorPdf.App;

public sealed record ActivationRequest(bool Merge, string[] Paths)
{
    public static ActivationRequest Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        bool merge = args.FirstOrDefault() == "--merge";
        var paths = args.FirstOrDefault() is "--merge" or "--open" ? args.Skip(1).ToArray() : args;
        if (paths.Length > 1000 || paths.Any(p => string.IsNullOrWhiteSpace(p) || p.StartsWith("--", StringComparison.Ordinal)))
            throw new ArgumentException("Selección de PDF no válida.");
        if (merge && paths.Any(p => !Path.GetExtension(p).Equals(".pdf", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Selecciona únicamente archivos PDF para unir.");
        return new(merge, paths.Select(p => LocalFiles.Validate(p)).ToArray());
    }
}

// Local window messages only: no socket, pipe listener, shared document or path log.
public sealed class Activation : IDisposable
{
    const string Property = "EditorPDF.Activation.v1";
    const int CopyData = 0x004A;
    readonly HwndSource source;
    readonly Action<ActivationRequest> receive;
    public Activation(IntPtr hwnd, Action<ActivationRequest> receive)
    {
        this.receive = receive;
        source = HwndSource.FromHwnd(hwnd) ?? throw new InvalidOperationException("Ventana no disponible.");
        source.AddHook(Hook);
        if (!SetProp(hwnd, Property, new IntPtr(1)))
            throw new System.ComponentModel.Win32Exception();
    }
    IntPtr Hook(IntPtr hwnd, int msg, IntPtr w, IntPtr l, ref bool handled)
    {
        if (msg != CopyData)
            return IntPtr.Zero;
        handled = true;
        try
        {
            var data = Marshal.PtrToStructure<Copy>(l);
            if (data.Tag != new IntPtr(0x45504446) || data.Length <= 0 || data.Length > 1024 * 1024)
                return IntPtr.Zero;
            byte[] bytes = new byte[data.Length];
            Marshal.Copy(data.Data, bytes, 0, bytes.Length);
            var request = JsonSerializer.Deserialize<ActivationRequest>(bytes) ?? throw new InvalidDataException();
            if (request.Paths == null)
                return IntPtr.Zero;
            var validated = ActivationRequest.Parse([request.Merge ? "--merge" : "--open", .. request.Paths]);
            source.Dispatcher.BeginInvoke(() => receive(validated));
            return new IntPtr(1);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or JsonException) { return IntPtr.Zero; }
    }
    public static bool Send(ActivationRequest request)
    {
        bool sent = false;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(request);
        if (bytes.Length > 1024 * 1024)
            throw new ArgumentException("La selección es demasiado grande.");
        var pin = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            var data = new Copy { Tag = new IntPtr(0x45504446), Length = bytes.Length, Data = pin.AddrOfPinnedObject() };
            EnumWindows((hwnd, _) =>
            {
                if (GetProp(hwnd, Property) == IntPtr.Zero)
                    return true;
                if (SendMessageTimeout(hwnd, CopyData, IntPtr.Zero, ref data, 2, 2000, out var result) != IntPtr.Zero && result == new IntPtr(1))
                {
                    sent = true;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
        }
        finally { pin.Free(); }
        return sent;
    }
    public void Dispose()
    {
        RemoveProp(source.Handle, Property);
        source.RemoveHook(Hook);
    }
    [StructLayout(LayoutKind.Sequential)]
    struct Copy
    {
        public IntPtr Tag; public int Length; public IntPtr Data;
    }
    delegate bool EnumCallback(IntPtr hwnd, IntPtr data);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumCallback callback, IntPtr data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool SetProp(IntPtr hwnd, string name, IntPtr value);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr GetProp(IntPtr hwnd, string name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr RemoveProp(IntPtr hwnd, string name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessageTimeout(IntPtr hwnd, int msg, IntPtr w, ref Copy l, uint flags, uint timeout, out IntPtr result);
}
