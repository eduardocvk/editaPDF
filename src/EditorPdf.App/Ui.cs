using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows.Data;
using System.Windows.Markup;
namespace EditorPdf.App;

public sealed class Ui : INotifyPropertyChanged
{
    public static Ui Current { get; } = new();
    readonly Dictionary<string,string> english;
    static string Settings => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"EditorPDF","language.txt");
    public string Language { get; private set; } = "es";
    Ui()
    {
        using var stream=typeof(Ui).Assembly.GetManifestResourceStream("EditorPdf.App.translations.en.json")!;
        english=JsonSerializer.Deserialize<Dictionary<string,string>>(stream)!;
        if(File.Exists(Settings)) Language=File.ReadAllText(Settings).Trim()=="en" ? "en" : "es";
    }
    public string this[string text] => Language=="en" && english.TryGetValue(text,out var translated) ? translated : text;
    public static string T(string text) => Current[text];
    public void SetLanguage(string language, bool persist = true)
    {
        if(persist) { Directory.CreateDirectory(Path.GetDirectoryName(Settings)!); File.WriteAllText(Settings,language=="en" ? "en" : "es"); }
        Language=language=="en" ? "en" : "es";
        PropertyChanged?.Invoke(this,new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(nameof(Language)));
    }
    public event PropertyChangedEventHandler? PropertyChanged;
}
public sealed class LocExtension(string text) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) => new Binding(nameof(Ui.Language)) { Source=Ui.Current,Mode=BindingMode.OneWay,Converter=new TranslateConverter(),ConverterParameter=text }.ProvideValue(serviceProvider);
}
public sealed class TranslateConverter : IValueConverter
{
    public object Convert(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture) => Ui.T((string)parameter);
    public object ConvertBack(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture) => throw new NotSupportedException();
}
