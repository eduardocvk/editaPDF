using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
namespace EditorPdf.App;

public sealed class ToolButton : Button
{
    public string Icon { get; set; } = "PagePlus";
    public bool IconOnly { get; set; }
    static readonly Dictionary<string, string> Shapes = new()
    {
        ["PagePlus"] = "M3,2 L11,2 16,7 16,18 3,18 Z M11,2 V7 H16 M9,9 V15 M6,12 H12",
        ["Duplicate"] = "M7,6 H17 V18 H7 Z M3,14 V2 H13",
        ["Rotate"] = "M4,7 A7,7 0 1 1 4,15 M4,2 V7 H9",
        ["Delete"] = "M3,5 H17 M7,5 V2 H13 V5 M5,5 L6,18 H14 L15,5 M8,8 V15 M12,8 V15",
        ["Up"] = "M10,17 V3 M4,9 L10,3 16,9",
        ["Down"] = "M10,3 V17 M4,11 L10,17 16,11",
        ["Extract"] = "M11,3 H3 V17 H11 M8,10 H18 M14,6 L18,10 14,14",
        ["Insert"] = "M9,3 H17 V17 H9 M2,10 H12 M8,6 L12,10 8,14",
        ["Split"] = "M2,3 H8 V17 H2 Z M12,3 H18 V17 H12 Z M10,1 V19",
        ["Text"] = "M3,4 H17 M10,4 V17 M6,17 H14",
        ["Paragraph"] = "M2,4 H18 M2,8 H16 M2,12 H18 M2,16 H12",
        ["Image"] = "M2,3 H18 V17 H2 Z M3,15 L8,9 12,13 15,10 18,14 M12,6 A1.5,1.5 0 1 0 15,6 A1.5,1.5 0 1 0 12,6",
        ["Check"] = "M3,10 L8,15 17,5",
        ["Position"] = "M10,1 V19 M1,10 H19 M7,4 L10,1 13,4 M7,16 L10,19 13,16 M4,7 L1,10 4,13 M16,7 L19,10 16,13"
    };
    public static Viewbox MakeIcon(string icon)
    {
        var path = new System.Windows.Shapes.Path { Data = Geometry.Parse(Shapes.GetValueOrDefault(icon, Shapes["Check"])), Stroke = Brushes.RoyalBlue, StrokeThickness = 1.6, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round };
        var canvas = new Canvas { Width = 20, Height = 20 };
        canvas.Children.Add(path);
        return new Viewbox { Width = 16, Height = 16, Child = canvas };
    }
    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        if (Content is not string label)
            return;
        var path = new System.Windows.Shapes.Path { Data = Geometry.Parse(Shapes.GetValueOrDefault(Icon, Shapes["PagePlus"])), StrokeThickness = 1.6, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round };
        path.SetBinding(System.Windows.Shapes.Path.StrokeProperty, new Binding(nameof(Foreground)) { Source = this });
        var canvas = new Canvas { Width = 20, Height = 20 };
        canvas.Children.Add(path);
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(new Viewbox { Width = 16, Height = 16, Child = canvas, Margin = IconOnly ? new Thickness(0) : new Thickness(0, 0, 6, 0) });
        if (!IconOnly) panel.Children.Add(new AccessText { Text = label, VerticalAlignment = VerticalAlignment.Center });
        AutomationProperties.SetName(this, label.Replace("_", ""));
        Content = panel;
    }
}
