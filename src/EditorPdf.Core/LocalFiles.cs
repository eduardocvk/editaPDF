namespace EditorPdf.Core;

public static class LocalFiles
{
    public static string Validate(string path)
    {
        var full = Path.GetFullPath(path);
        if (full.StartsWith(@"\\", StringComparison.Ordinal) || new DriveInfo(Path.GetPathRoot(full)!).DriveType == DriveType.Network)
            throw new IOException("Solo se permiten archivos en unidades locales.");
        var current = full;
        while (!string.IsNullOrEmpty(current))
        {
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Esta versión no abre rutas con enlaces o redirecciones. Usa una carpeta local directa.");
            current = Path.GetDirectoryName(current);
        }
        return full;
    }
    public static byte[] Read(string path)
    {
        path = Validate(path);
        var length = new FileInfo(path).Length;
        if (length > 256L * 1024 * 1024)
            throw new IOException("Límite de esta versión: 256 MiB por archivo.");
        return File.ReadAllBytes(path);
    }
}
