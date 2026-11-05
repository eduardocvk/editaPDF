using System.Diagnostics;
using System.IO;
namespace EditorPdf.App;

public static class FirmaPdfBridge
{
    public static string? FindExecutable()
    {
        string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","FirmaPDF","versions");
        if(!Directory.Exists(root)) return null;
        return Directory.EnumerateDirectories(root).Select(d=>Path.Combine(d,"FirmaPDF.exe")).Where(File.Exists).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
    }
    public static void Open(string executable,string path)
    {
        var start=new ProcessStartInfo(executable) { UseShellExecute=false };
        start.ArgumentList.Add("--open"); start.ArgumentList.Add(path);
        _ = Process.Start(start) ?? throw new IOException(Ui.T("No se pudo abrir FirmaPDF."));
    }
}
