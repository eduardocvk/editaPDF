using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace EditorPdf.App;

public partial class MainWindow
{
    int adjacentIndex = -1;
    object? adjacentDocument;
    int adjacentWidth;

    void UpdateAdjacentPage()
    {
        int target = page % 2 == 0 ? page + 1 : page - 1;
        if (target < 0 || target >= Pdf.Count)
        {
            HideAdjacentPage();
            return;
        }

        var info = Pdf.Info(target);
        int width = Math.Max(1, (int)(info.Width * zoom));
        if (PageImage.Width + width + 62 > DocumentScroll.ActualWidth)
        {
            HideAdjacentPage();
            return;
        }

        if (!ReferenceEquals(adjacentDocument, Pdf) || target != adjacentIndex || width != adjacentWidth)
        {
            var raster = Pdf.Render(target, width);
            AdjacentImage.Source = Bitmap(raster);
            AdjacentImage.Width = raster.Width;
            AdjacentImage.Height = raster.Height;
            adjacentDocument = Pdf;
            adjacentIndex = target;
            adjacentWidth = width;
        }
        Grid.SetColumn(Paper, page % 2);
        Grid.SetColumn(AdjacentPage, 1 - page % 2);
        AdjacentPage.Visibility = Visibility.Visible;
        AdjacentPage.ToolTip = $"{Ui.T("Página")} {target + 1}";
    }

    void HideAdjacentPage()
    {
        AdjacentPage.Visibility = Visibility.Collapsed;
        AdjacentImage.Source = null;
        adjacentDocument = null;
        adjacentIndex = -1;
        Grid.SetColumn(Paper, 0);
    }

    void ActivateAdjacentPage(object sender, RoutedEventArgs e)
    {
        if (adjacentIndex < 0) return;
        Run(() =>
        {
            FinishInlineText();
            page = adjacentIndex;
            refreshing = true;
            PagesList.SelectedIndex = page;
            refreshing = false;
            ShowPage();
            PageImage.Focus();
        });
    }

    void ChooseObjectDestination()
    {
        var dialog = new Window { Title = Ui.T("Mover a página…"), Owner = IsVisible ? this : null,
            SizeToContent = SizeToContent.WidthAndHeight, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize };
        var content = new StackPanel { Margin = new Thickness(16), MinWidth = 240 };
        var pages = new ComboBox { ItemsSource = Enumerable.Range(1, Pdf.Count), SelectedIndex = page, MaxDropDownHeight = 260 };
        content.Children.Add(new Label { Content = Ui.T("Página de destino"), Target = pages });
        content.Children.Add(pages);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        actions.Children.Add(new Button { Content = Ui.T("Cancelar"), IsCancel = true });
        var move = new Button { Content = Ui.T("Mover"), IsDefault = true };
        move.Click += (_, _) => dialog.DialogResult = true;
        actions.Children.Add(move);
        content.Children.Add(actions);
        dialog.Content = content;
        Mouse.OverrideCursor = null;
        if (dialog.ShowDialog() == true) MoveSelectionToPage(pages.SelectedIndex);
    }
}
