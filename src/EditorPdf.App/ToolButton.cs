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
    public double IconSize { get; set; } = 16;
    static readonly Dictionary<string, string> Shapes = new()
    {
        ["FolderOpen"] = "M2,16 V5 Q2,3 4,3 H8 L10,5 H17 Q18,5 18,7 M2,16 Q2,18 4,18 H16 Q17,18 18,16 L20,9 H5 Z",
        ["Save"] = "M3,2 H15 L18,5 V17 Q18,18 17,18 H3 Q2,18 2,17 V3 Q2,2 3,2 Z M6,2 V7 H14 V2 M6,18 V11 H14 V18",
        ["Print"] = "M5,7 V2 H15 V7 M5,14 H2 V8 H18 V14 H15 M5,11 H15 V18 H5 Z M14,9.5 H15",
        ["Merge"] = "M2,2 H8 V7 H2 Z M2,12 H8 V18 H2 Z M8,5 H11 Q13,5 13,8 V10 M8,15 H11 Q13,15 13,12 V10 H19 M16,7 L19,10 16,13",
        ["Compress"] = "M2,2 L7,7 M2,7 H7 V2 M18,2 L13,7 M13,2 V7 H18 M2,18 L7,13 M2,13 H7 V18 M18,18 L13,13 M13,18 V13 H18",
        ["Undo"] = "M8,3 L3,8 8,13 M3,8 H12 A5,5 0 0 1 12,18",
        ["Redo"] = "M12,3 L17,8 12,13 M17,8 H8 A5,5 0 0 0 8,18",
        ["Front"] = "M3,2 H17 M10,18 V6 M5,11 L10,6 15,11",
        ["Back"] = "M3,18 H17 M10,2 V14 M5,9 L10,14 15,9",
        ["Style"] = "M3,16 L6,7 H14 L17,16 M5,12 H15 M3,19 H17 M15,2 L17,4 19,2",
        ["PositionCheck"] = "M8,1 V13 M2,7 H14 M5,4 L8,1 11,4 M3,16 L7,19 17,10",
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
        panel.Children.Add(new Viewbox { Width = IconSize, Height = IconSize, Child = canvas, Margin = IconOnly ? new Thickness(0) : new Thickness(0, 0, 6, 0) });
        if (!IconOnly) { var text=new AccessText { VerticalAlignment=VerticalAlignment.Center }; text.SetBinding(AccessText.TextProperty,new Binding("["+label+"]") { Source=Ui.Current }); panel.Children.Add(text); }
        AutomationProperties.SetName(this, label.Replace("_", ""));
        Content = panel;
    }
}
