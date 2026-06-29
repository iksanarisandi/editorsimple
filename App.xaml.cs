using System.Reflection;
using System.Windows;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace EditorSimple;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Register the four dark-themed syntax definitions (embedded resources).
        RegisterHighlighting("JSON",     "EditorSimple.Syntax.json.xshd",     new[] { ".json", ".jsonc" });
        RegisterHighlighting("YAML",     "EditorSimple.Syntax.yaml.xshd",     new[] { ".yaml", ".yml" });
        RegisterHighlighting("TOML",     "EditorSimple.Syntax.toml.xshd",     new[] { ".toml" });
        RegisterHighlighting("Markdown", "EditorSimple.Syntax.markdown.xshd", new[] { ".md", ".markdown", ".mkd" });
    }

    private static void RegisterHighlighting(string name, string resourceName, string[] extensions)
    {
        var asm = Assembly.GetExecutingAssembly();
        using var stream = asm.GetManifestResourceStream(resourceName);
        if (stream == null)
            return;

        using var reader = XmlReader.Create(stream);
        var definition = HighlightingLoader.Load(reader, HighlightingManager.Instance);
        HighlightingManager.Instance.RegisterHighlighting(name, extensions, definition);
    }
}
