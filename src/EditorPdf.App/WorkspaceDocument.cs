using System.ComponentModel;
using System.Runtime.CompilerServices;
using EditorPdf.Core;
namespace EditorPdf.App;

public sealed class WorkspaceDocument(Session session, string title) : INotifyPropertyChanged, IDisposable
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public Session Session { get; } = session;
    public string Title { get; set; } = title;
    public string? SourcePath
    {
        get; set;
    }
    public string Header => Title + (Session.Dirty ? " •" : "");
    public override string ToString() => Header;
    public DocumentView View { get; set; } = new();
    public event PropertyChangedEventHandler? PropertyChanged;
    public void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Header)));
    public void Dispose() => Session.Dispose();
}
public sealed class DocumentView
{
    public int Page
    {
        get; set;
    }
    public double Zoom { get; set; } = 1;
    public int Mode
    {
        get; set;
    }
    public string Search { get; set; } = "";
    public string Ranges { get; set; } = "1";
    public int? Object
    {
        get; set;
    }
    public string[] Draft { get; set; } = [];
    public bool Substitute
    {
        get; set;
    }
    public string[] StyleDraft { get; set; } = [];
    public InlineTextDraft? Inline
    {
        get; set;
    }
}
