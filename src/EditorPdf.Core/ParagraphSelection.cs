namespace EditorPdf.Core;

public static class ParagraphSelection
{
    public static PdfObject[] InArea(IEnumerable<PdfObject> objects, float left, float bottom, float right, float top) =>
        objects.Where(o => o.Type == 1 && !string.IsNullOrWhiteSpace(o.Text) && o.HorizontalText &&
            o.Left >= left - 2 && o.Right <= right + 2 && o.Bottom >= bottom - 2 && o.Top <= top + 2)
        .OrderByDescending(o => o.Top).ThenBy(o => o.Left).ToArray();

    public static string Compose(IEnumerable<PdfObject> objects)
    {
        var sorted = objects.OrderByDescending(o => o.Top).ThenBy(o => o.Left).ToArray();
        if (sorted.Length == 0) return "";
        var lines = new List<List<PdfObject>>();
        foreach (var obj in sorted)
        {
            var line = lines.FirstOrDefault(items => Math.Abs(items[0].Bottom - obj.Bottom) <= Math.Max(2, Math.Min(items[0].Size, obj.Size) * .35f));
            if (line == null) lines.Add([obj]);
            else line.Add(obj);
        }
        var composed = lines.Select(line =>
        {
            var parts = line.OrderBy(o => o.Left).ToArray();
            string result = parts[0].Text;
            for (int i = 1; i < parts.Length; i++)
            {
                var previous = parts[i - 1];
                var next = parts[i];
                bool gap = next.Left - previous.Right > Math.Max(1, Math.Min(previous.Size, next.Size) * .15f);
                if (gap && !char.IsWhiteSpace(result[^1]) && !char.IsWhiteSpace(next.Text[0])) result += " ";
                result += next.Text;
            }
            return string.Join(' ', result.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        }).ToArray();
        string result = composed[0];
        for (int i = 1; i < composed.Length; i++)
        {
            float gap = Math.Abs(lines[i - 1][0].Bottom - lines[i][0].Bottom);
            result += gap > Math.Max(lines[i - 1][0].Size, lines[i][0].Size) * 1.85f ? "\n\n" : "\n";
            result += composed[i];
        }
        return result;
    }
}
