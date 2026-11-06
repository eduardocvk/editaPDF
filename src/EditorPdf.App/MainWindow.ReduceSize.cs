using System.Windows;
using System.Windows.Input;
namespace EditorPdf.App;
public partial class MainWindow
{
    void ReduceSizeClick(object sender,RoutedEventArgs e)=>Run(()=>
    {
        FinishInlineText(); RequireDocument();
        using var dialog=new ReduceSizeDialog(this,Pdf.SaveBytes(),activeDocument?.SourcePath??activeDocument?.Title??"documento.pdf");
        Mouse.OverrideCursor=null;
        dialog.ShowDialog();
    });
}
