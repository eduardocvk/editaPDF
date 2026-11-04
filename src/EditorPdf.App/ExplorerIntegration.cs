using Microsoft.Win32;
using System.Runtime.InteropServices;
namespace EditorPdf.App;

public static class ExplorerIntegration
{
    const string Prefix = @"Software\Classes\";
    const string ShortcutName = "Unir PDF con Editor PDF.lnk";
    static string SendTo(string? folder) => EditorPdf.Core.LocalFiles.Validate(folder ?? Environment.GetFolderPath(Environment.SpecialFolder.SendTo));
    public static void Install(string executable, RegistryKey? root = null, string? sendTo = null)
    {
        root ??= Registry.CurrentUser;
        executable = EditorPdf.Core.LocalFiles.Validate(executable);
        if (!File.Exists(executable))
            throw new FileNotFoundException("No se encuentra el ejecutable.");
        void Set(string path, string name, string value)
        {
            using var key = root.CreateSubKey(Prefix + path);
            key.SetValue(name, value);
        }
        string folder = SendTo(sendTo);
        Directory.CreateDirectory(folder);
        ShellShortcut.Write(Path.Combine(folder, ShortcutName), executable, "--merge");
        string open = @"SystemFileAssociations\.pdf\shell\EditorPDF.Open";
        Set(open, "", "Abrir con Editor PDF");
        Set(open, "Icon", executable + ",0");
        Set(open, "MultiSelectModel", "Player");
        Set(open + @"\command", "", '\"' + executable + "\" --open \"%1\"");
        Set(@"Applications\EditorPDF.exe", "FriendlyAppName", "Editor PDF");
        Set(@"Applications\EditorPDF.exe\SupportedTypes", ".pdf", "");
        Set(@"Applications\EditorPDF.exe\shell\open\command", "", '"' + executable + "\" --open \"%1\"");
        Set("EditorPDF.Document", "", "Documento PDF");
        Set(@"EditorPDF.Document\DefaultIcon", "", executable + ",0");
        Set(@"EditorPDF.Document\shell\open\command", "", '"' + executable + "\" --open \"%1\"");
        Set(@".pdf\OpenWithProgids", "EditorPDF.Document", "");
        Notify();
    }
    public static void Uninstall(RegistryKey? root = null, string? sendTo = null)
    {
        root ??= Registry.CurrentUser;
        // Only our named keys and OpenWith value; preserve defaults and other PDF apps.
        foreach (var path in new[] { @"SystemFileAssociations\.pdf\shell\EditorPDF.Open", @"Applications\EditorPDF.exe", "EditorPDF.Document" })
            root.DeleteSubKeyTree(Prefix + path, false);
        using var key = root.OpenSubKey(Prefix + @".pdf\OpenWithProgids", true);
        key?.DeleteValue("EditorPDF.Document", false);
        string link = Path.Combine(SendTo(sendTo), ShortcutName);
        for (int attempt = 0; File.Exists(link); attempt++)
        {
            try { File.Delete(link); break; }
            catch (IOException) when (attempt < 5) { Thread.Sleep(80); }
        }
        Notify();
    }
    static void Notify() => SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
    [DllImport("shell32.dll")] static extern void SHChangeNotify(uint change, uint flags, IntPtr a, IntPtr b);
}
