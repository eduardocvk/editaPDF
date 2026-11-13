using System.Windows;
using System.Windows.Media;

namespace EditorPdf.App;

public partial class MainWindow
{
    ObjectRow? HitObject(Point pointer)
    {
        var position = Pdf.ToPage(page, (int)PageImage.Width, (int)PageImage.Height,
            (int)Math.Round(pointer.X), (int)Math.Round(pointer.Y));
        var point = new Point(position.X, position.Y);
        double tolerance = 4 / zoom;
        foreach (var row in ObjectsList.Items.Cast<ObjectRow>().Reverse())
        {
            var obj = row.Value;
            if (point.X < obj.Left - tolerance || point.X > obj.Right + tolerance ||
                point.Y < obj.Bottom - tolerance || point.Y > obj.Top + tolerance)
                continue;
            if (obj.Type != 2 || HitPath(obj.Index, point, tolerance)) return row;
        }
        return null;
    }

    bool HitPath(int index, Point pointer, double tolerance)
    {
        var path = Pdf.GetPathOutline(page, index);
        if (path is null || path.Points.Count == 0) return false;
        var geometry = new PathGeometry { FillRule = path.FillRule == 1 ? FillRule.EvenOdd : FillRule.Nonzero };
        PathFigure? figure = null;
        var points = path.Points;
        Point Position(int i) => new(points[i].X, points[i].Y);
        for (int i = 0; i < points.Count; i++)
        {
            if (points[i].Kind == 2)
            {
                figure = new PathFigure { StartPoint = Position(i), IsFilled = path.FillRule != 0 };
                geometry.Figures.Add(figure);
            }
            else if (figure != null && points[i].Kind == 0)
                figure.Segments.Add(new LineSegment(Position(i), true));
            else if (figure != null && i + 2 < points.Count &&
                points[i].Kind == 1 && points[i + 1].Kind == 1 && points[i + 2].Kind == 1)
            {
                figure.Segments.Add(new BezierSegment(Position(i), Position(i + 1), Position(i + 2), true));
                i += 2;
            }
            if (figure != null && points[i].Closes) figure.IsClosed = true;
        }
        if (path.FillRule != 0 && geometry.FillContains(pointer)) return true;
        return path.Stroked && geometry.StrokeContains(new Pen(Brushes.Black, Math.Max(2 * tolerance, path.StrokeWidth + 1.5 * tolerance)), pointer);
    }
}
