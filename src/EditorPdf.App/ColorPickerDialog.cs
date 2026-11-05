using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
namespace EditorPdf.App;

public sealed class ColorPickerDialog : Window
{
    public uint SelectedColor { get; private set; }
    readonly UniformGrid palette = new() { Columns = 16 };
    readonly Border sample = new() { Height = 36, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1) };
    readonly Slider brightness = new() { Minimum = .2, Maximum = 1, Value = 1, TickFrequency = .1, Margin = new Thickness(4, 8, 4, 12) };

    public ColorPickerDialog(Window owner, uint original)
    {
        if (owner.IsVisible) Owner = owner;
        Title = "Elegir color";
        Width = 475;
        Height = 440;
        MinWidth = 410;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SelectedColor = original;
        sample.Background = Brush(original);
        var root = new DockPanel { Margin = new Thickness(18), Background = Brushes.White };
        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "Cancelar", IsCancel = true };
        var accept = new Button { Content = "Elegir color", IsDefault = true };
        accept.Click += (_, _) => DialogResult = true;
        footer.Children.Add(cancel);
        footer.Children.Add(accept);
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);
        DockPanel.SetDock(sample, Dock.Bottom);
        root.Children.Add(sample);
        DockPanel.SetDock(brightness, Dock.Bottom);
        root.Children.Add(brightness);
        var area = new StackPanel();
        area.Children.Add(new TextBlock { Text = "Color", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) });
        area.Children.Add(palette);
        root.Children.Add(area);
        Content = root;
        brightness.ValueChanged += (_, _) => BuildPalette();
        BuildPalette();
    }
    static SolidColorBrush Brush(uint rgb) => new(Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb));
    static uint Hsv(double hue, double saturation, double value)
    {
        double c = value * saturation, x = c * (1 - Math.Abs((hue / 60) % 2 - 1)), m = value - c;
        (double r, double g, double b) = hue switch
        {
            < 60 => (c, x, 0d), < 120 => (x, c, 0d), < 180 => (0d, c, x),
            < 240 => (0d, x, c), < 300 => (x, 0d, c), _ => (c, 0d, x)
        };
        return (uint)Math.Round((r + m) * 255) << 16 | (uint)Math.Round((g + m) * 255) << 8 | (uint)Math.Round((b + m) * 255);
    }
    void BuildPalette()
    {
        palette.Children.Clear();
        for (int row = 0; row < 9; row++)
            for (int column = 0; column < 16; column++)
            {
                uint color = Hsv(column * 360.0 / 16, row / 8.0, brightness.Value);
                var swatch = new Button { Width = 25, Height = 25, MinHeight = 25, Padding = new Thickness(0), Margin = new Thickness(1), Background = Brush(color), BorderBrush = Brushes.LightGray, ToolTip = $"Color {row * 16 + column + 1}" };
                swatch.Click += (_, _) => { SelectedColor = color; sample.Background = Brush(color); };
                palette.Children.Add(swatch);
            }
    }
}
