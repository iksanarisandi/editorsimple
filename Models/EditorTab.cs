using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;

namespace EditorSimple.Models;

/// <summary>
/// One open document: a title, an optional on-disk path, dirty tracking and
/// the underlying AvalonEdit editor instance displayed inside its tab.
/// </summary>
public sealed class EditorTab : INotifyPropertyChanged
{
    private static readonly Brush EditorBackground = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
    private static readonly Brush EditorForeground = new SolidColorBrush(Color.FromRgb(0xD4, 0xD4, 0xD4));
    private static readonly Brush LineNumberBrush  = new SolidColorBrush(Color.FromRgb(0x85, 0x85, 0x85));

    private string _title = "Untitled";
    private bool _isDirty;
    private string _format = "Plain";
    private bool _loading; // suppress dirty while loading text programmatically

    public TextEditor Editor { get; }
    public string? FilePath { get; private set; }

    public string Title
    {
        get => _title;
        set { _title = value; Raise(nameof(Title)); Raise(nameof(Header)); }
    }

    public bool IsDirty
    {
        get => _isDirty;
        set { _isDirty = value; Raise(nameof(IsDirty)); Raise(nameof(Header)); }
    }

    public string Format
    {
        get => _format;
        set { _format = value; Raise(nameof(Format)); }
    }

    /// <summary>What the tab header renders (adds a dot when dirty).</summary>
    public string Header => _isDirty ? "● " + _title : _title;

    public EditorTab()
    {
        Editor = new TextEditor
        {
            ShowLineNumbers = true,
            WordWrap = false,
            FontFamily = new FontFamily("Cascadia Code, Consolas, Courier New"),
            FontSize = 14,
            Background = EditorBackground,
            Foreground = EditorForeground,
            LineNumbersForeground = LineNumberBrush,
            Padding = new Thickness(8, 6, 8, 6),
            HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
        };
        Editor.Options.ConvertTabsToSpaces = true;
        Editor.Options.IndentationSize = 4;
        Editor.Options.EnableRectangularSelection = true;
        Editor.TextChanged += (_, _) =>
        {
            if (!_loading)
                IsDirty = true;
        };
    }

    public void LoadFile(string path)
    {
        _loading = true;
        try
        {
            FilePath = path;
            Title = Path.GetFileName(path);
            Format = FormatFromExtension(path);
            Editor.Text = File.ReadAllText(path);
            ApplyHighlighting(path);
            Editor.ScrollToHome();
        }
        finally
        {
            _loading = false;
            IsDirty = false;
        }
    }

    public bool Save()
    {
        if (FilePath == null)
            return false;
        File.WriteAllText(FilePath, Editor.Text);
        IsDirty = false;
        return true;
    }

    public bool SaveAs(string path)
    {
        FilePath = path;
        Title = Path.GetFileName(path);
        Format = FormatFromExtension(path);
        ApplyHighlighting(path);
        File.WriteAllText(path, Editor.Text);
        IsDirty = false;
        return true;
    }

    private void ApplyHighlighting(string path)
    {
        var ext = Path.GetExtension(path);
        Editor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinitionByExtension(ext);
    }

    private static string FormatFromExtension(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".json" or ".jsonc" => "JSON",
            ".yaml" or ".yml" => "YAML",
            ".toml" => "TOML",
            ".md" or ".markdown" or ".mkd" => "Markdown",
            ".xml" => "XML",
            ".html" or ".htm" => "HTML",
            ".css" => "CSS",
            ".js" => "JavaScript",
            ".ts" => "TypeScript",
            ".cs" => "C#",
            ".sql" => "SQL",
            _ => ext.Length > 0 ? ext.TrimStart('.').ToUpperInvariant() : "Plain",
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise([CallerMemberName] string? prop = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
}
