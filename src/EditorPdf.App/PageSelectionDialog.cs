using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using EditorPdf.Core;
namespace EditorPdf.App;

public sealed class PageSelectionDialog : Window
{
    public sealed class Choice(PdfDocument document, int index, bool selected) : INotifyPropertyChanged
    {
        readonly MainWindow.Thumb thumbnail = new(document, index);
        bool chosen = selected;
        public int Index => index;
        public string Label => "Página " + (index + 1);
        public System.Windows.Media.Imaging.BitmapSource Thumbnail => thumbnail.Image;
        public bool Chosen
        {
            get => chosen; set
            {
                chosen = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Chosen)));
            }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    readonly PdfDocument pdf;
    readonly ListBox pages;
    readonly Image preview = new() { Stretch = Stretch.Uniform, Margin = new Thickness(12) };
    readonly TextBlock count = new();
    readonly Button accept = new() { Content = "Continuar", IsDefault = true };
    readonly ComboBox position = new() { MinWidth = 210, Margin = new Thickness(8, 0, 0, 0) };
    readonly TextBox blockSize = new() { Text = "1", Width = 60, Margin = new Thickness(8, 0, 0, 0) };
    readonly int destinationCount, currentPage;
    bool selectingAll;
    public ObservableCollection<Choice> Choices
    {
        get;
    }
    public int[] SelectedPages => Choices.Where(c => c.Chosen).Select(c => c.Index).ToArray();
    public int InsertionIndex => position.SelectedIndex switch { 0 => Math.Clamp(currentPage, 0, destinationCount), 1 => Math.Min(currentPage + 1, destinationCount), _ => destinationCount };
    public int PagesPerFile => int.TryParse(blockSize.Text, out int size) && size > 0 && size <= 10000 ? size : throw new ArgumentException("Usa entre 1 y 10000 páginas por archivo.");
    public PageSelectionDialog(PdfDocument pdf, string title, int selectedPage = -1, int? insertIntoCount = null, int currentPage = 0, bool split = false)
    {
        this.pdf = pdf;
        this.currentPage = currentPage;
        destinationCount = insertIntoCount ?? 0;
        Title = title;
        Width = 920;
        Height = 700;
        MinWidth = 680;
        MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(243, 245, 248));
        Choices = new ObservableCollection<Choice>(Enumerable.Range(0, pdf.Count).Select(i => new Choice(pdf, i, selectedPage < 0 || i == selectedPage)));
        var root = new DockPanel { Margin = new Thickness(14), Background = Background };
        Content = root;
        var top = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);
        var select = new StackPanel { Orientation = Orientation.Horizontal };
        top.Children.Add(select);
        var all = new Button { Content = "Todas" };
        var none = new Button { Content = "Ninguna" };
        all.Click += (_, _) => SetSelection(true);
        none.Click += (_, _) => SetSelection(false);
        select.Children.Add(all);
        select.Children.Add(none);
        select.Children.Add(count);
        var bottom = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(bottom);
        var cancel = new Button { Content = "Cancelar", IsCancel = true };
        bottom.Children.Add(cancel);
        bottom.Children.Add(accept);
        if (insertIntoCount != null)
        {
            var placement = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            DockPanel.SetDock(placement, Dock.Top);
            root.Children.Add(placement);
            placement.Children.Add(new TextBlock { Text = "Insertar" });
            position.Items.Add("Antes de la página " + (currentPage + 1));
            position.Items.Add("Después de la página " + (currentPage + 1));
            position.Items.Add("Al final");
            position.SelectedIndex = destinationCount == 0 ? 2 : 1;
            placement.Children.Add(position);
        }
        if (split)
        {
            var grouping = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            DockPanel.SetDock(grouping, Dock.Top);
            root.Children.Add(grouping);
            grouping.Children.Add(new TextBlock { Text = "Páginas por archivo" });
            grouping.Children.Add(blockSize);
        }
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        root.Children.Add(grid);
        pages = new ListBox { ItemsSource = Choices, HorizontalContentAlignment = HorizontalAlignment.Stretch, BorderThickness = new Thickness(0) };
        VirtualizingPanel.SetIsVirtualizing(pages, true);
        VirtualizingPanel.SetVirtualizationMode(pages, VirtualizationMode.Recycling);
        ScrollViewer.SetCanContentScroll(pages, true);
        ScrollViewer.SetHorizontalScrollBarVisibility(pages, ScrollBarVisibility.Disabled);
        var panel = new FrameworkElementFactory(typeof(StackPanel));
        panel.SetValue(FrameworkElement.MarginProperty, new Thickness(8));
        var check = new FrameworkElementFactory(typeof(CheckBox));
        check.SetBinding(ToggleButtonCheckedProperty(), new Binding(nameof(Choice.Chosen)) { Mode = BindingMode.TwoWay });
        check.SetBinding(ContentControl.ContentProperty, new Binding(nameof(Choice.Label)));
        panel.AppendChild(check);
        var image = new FrameworkElementFactory(typeof(Image));
        image.SetValue(FrameworkElement.HeightProperty, 170d);
        image.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 8, 0, 4));
        image.SetBinding(Image.SourceProperty, new Binding(nameof(Choice.Thumbnail)));
        panel.AppendChild(image);
        pages.ItemTemplate = new DataTemplate { VisualTree = panel };
        grid.Children.Add(pages);
        var paper = new Border { Background = Brushes.White, Child = preview, Margin = new Thickness(12, 0, 0, 0) };
        Grid.SetColumn(paper, 1);
        grid.Children.Add(paper);
        pages.SelectionChanged += (_, _) => { if (pages.SelectedItem is Choice choice) preview.Source = RasterBitmap(pdf.Render(choice.Index, 650)); };
        pages.PreviewKeyDown += (_, e) => { if (e.Key == Key.Space && Keyboard.FocusedElement is not CheckBox && pages.SelectedItem is Choice c) { c.Chosen = !c.Chosen; e.Handled = true; } };
        foreach (var choice in Choices)
            choice.PropertyChanged += (sender, _) => { if (!selectingAll) { UpdateCount(); pages.SelectedItem = sender; } };
        accept.Click += (_, _) => { if (SelectedPages.Length == 0) return; if (split && (!int.TryParse(blockSize.Text, out int n) || n < 1 || n > 10000)) { MessageBox.Show(this, "Indica un número entre 1 y 10000.", "Dividir PDF"); return; } DialogResult = true; };
        UpdateCount();
        pages.SelectedIndex = Math.Clamp(selectedPage, 0, Math.Max(0, pdf.Count - 1));
        Closed += (_, _) => { pages.ItemsSource = null; preview.Source = null; };
    }
    static DependencyProperty ToggleButtonCheckedProperty() => System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty;
    static System.Windows.Media.Imaging.BitmapSource RasterBitmap(Raster raster)
    {
        var image = System.Windows.Media.Imaging.BitmapSource.Create(raster.Width, raster.Height, 96, 96, PixelFormats.Bgra32, null, raster.Pixels, raster.Stride);
        image.Freeze();
        return image;
    }
    void UpdateCount()
    {
        count.Text = $"  {Choices.Count(c => c.Chosen)} de {Choices.Count}";
        accept.IsEnabled = Choices.Any(c => c.Chosen);
    }
    public void SetSelection(bool selected)
    {
        selectingAll = true;
        try
        {
            foreach (var choice in Choices)
                choice.Chosen = selected;
        }
        finally { selectingAll = false; UpdateCount(); }
    }
}
