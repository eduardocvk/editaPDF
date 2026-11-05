using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using EditorPdf.Core;
namespace EditorPdf.App;

public sealed class ParagraphEditorDialog : Window
{
    readonly TextBox editor = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MinHeight = 220, MaxLength = 10000 };
    readonly TextBox widthBox = new() { Width = 80, Margin = new Thickness(8, 0, 0, 0) };
    readonly ComboBox fonts = new() { MinWidth = 180, Margin = new Thickness(8, 0, 0, 0) };
    readonly float maxWidth;
    public string ParagraphText => editor.Text;
    public float ParagraphWidth => float.Parse(widthBox.Text, CultureInfo.CurrentCulture);
    public string? FontChoice => fonts.SelectedIndex == 0 ? null : fonts.SelectedItem as string;
    public ParagraphEditorDialog(Window owner, string text, float width, int objects, bool mixedStyles = false, float maxWidth = 2000)
    {
        this.maxWidth = maxWidth;
        if (owner.IsVisible) Owner = owner;
        Title = "Editar párrafo";
        Width = 650;
        Height = 480;
        MinWidth = 480;
        MinHeight = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        editor.Text = text;
        widthBox.Text = width.ToString("0.##", CultureInfo.CurrentCulture);
        fonts.ItemsSource = new[] { "Conservar original" }.Concat(PdfDocument.IncludedFontFamilies).ToArray();
        fonts.SelectedIndex = 0;
        if (mixedStyles) fonts.SelectedIndex = 1;
        var root = new DockPanel { Margin = new Thickness(18), Background = System.Windows.Media.Brushes.White };
        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "Cancelar", IsCancel = true };
        var apply = new Button { Content = "Aplicar párrafo", IsDefault = true };
        apply.Click += (_, _) =>
        {
            if (!float.TryParse(widthBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value) || value < 30 || value > this.maxWidth + .1f || string.IsNullOrWhiteSpace(editor.Text))
            { MessageBox.Show(this, $"Introduce texto y un ancho entre 30 y {this.maxWidth:0.##} pt. El párrafo no puede superar su margen derecho actual.", "Editar párrafo"); return; }
            DialogResult = true;
        };
        footer.Children.Add(cancel);
        footer.Children.Add(apply);
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);
        var controls = new WrapPanel { Margin = new Thickness(0, 12, 0, 12) };
        controls.Children.Add(new TextBlock { Text = "Ancho (pt)", VerticalAlignment = VerticalAlignment.Center });
        controls.Children.Add(widthBox);
        controls.Children.Add(new TextBlock { Text = "Fuente", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(18, 0, 0, 0) });
        controls.Children.Add(fonts);
        DockPanel.SetDock(controls, Dock.Bottom);
        root.Children.Add(controls);
        var body = new DockPanel();
        var count = new TextBlock { Text = mixedStyles ? $"{objects} fragmentos · Los estilos se unificarán con la fuente elegida." : $"{objects} fragmentos seleccionados", Margin = new Thickness(0, 0, 0, 8) };
        DockPanel.SetDock(count, Dock.Top);
        body.Children.Add(count);
        body.Children.Add(editor);
        root.Children.Add(body);
        Content = root;
        Loaded += (_, _) => editor.Focus();
    }
}
