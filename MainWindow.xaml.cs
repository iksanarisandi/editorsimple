using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EditorSimple.Models;

namespace EditorSimple;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<EditorTab> _tabs;
    private readonly HashSet<string> _recentlyOpened = new(StringComparer.OrdinalIgnoreCase);

    private EditorTab? _activeTab;
    private int _untitledCount;

    private static readonly HashSet<string> ExcludedDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", ".git", ".vs", "node_modules", ".idea", "dist", "target", "__pycache__"
    };

    private const string DummyMarker = "◆"; // placeholder child used for lazy loading

    public MainWindow()
    {
        InitializeComponent();
        _tabs = new ObservableCollection<EditorTab>();
        _tabs.CollectionChanged += (_, _) => UpdateEmptyHint();
        TabHost.ItemsSource = _tabs;
        UpdateEmptyHint();
    }

    // ---------------- File commands ----------------

    private void New_Click(object sender, RoutedEventArgs e) => NewFile();

    private void NewFile()
    {
        var tab = new EditorTab();
        _untitledCount++;
        tab.Title = _untitledCount == 1 ? "Untitled" : $"Untitled {_untitledCount}";
        _tabs.Add(tab);
        TabHost.SelectedItem = tab;
    }

    private void OpenFile_Click(object sender, RoutedEventArgs e) => OpenFile();

    private void OpenFile()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Open File",
            CheckFileExists = true,
            Multiselect = true,
            Filter = "All files (*.*)|*.*|Config (*.json;*.yaml;*.yml;*.toml)|*.json;*.yaml;*.yml;*.toml|Markdown (*.md)|*.md"
        };
        if (dlg.ShowDialog(this) != true) return;
        foreach (var path in dlg.FileNames)
            OpenPath(path);
    }

    private void OpenPath(string path)
    {
        string full;
        try { full = Path.GetFullPath(path); }
        catch { full = path; }

        var existing = _tabs.FirstOrDefault(t => t.FilePath != null
            && string.Equals(t.FilePath, full, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            TabHost.SelectedItem = existing;
            return;
        }

        try
        {
            var tab = new EditorTab();
            tab.LoadFile(full);
            _tabs.Add(tab);
            TabHost.SelectedItem = tab;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Could not open file:\n" + ex.Message, "Open file",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e) => SaveActive();

    private bool SaveActive()
    {
        if (TabHost.SelectedItem is not EditorTab tab) return false;
        if (tab.FilePath == null) return SaveActiveAs();
        return SaveTab(tab);
    }

    private void SaveAs_Click(object sender, RoutedEventArgs e) => SaveActiveAs();

    private bool SaveActiveAs()
    {
        if (TabHost.SelectedItem is not EditorTab tab) return false;
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save As",
            FileName = tab.Title,
            OverwritePrompt = true,
            Filter = "All files (*.*)|*.*"
        };
        if (dlg.ShowDialog(this) != true) return false;
        try
        {
            tab.SaveAs(dlg.FileName);
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Could not save file:\n" + ex.Message, "Save",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    private bool SaveTab(EditorTab tab)
    {
        try { tab.Save(); return true; }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Could not save file:\n" + ex.Message, "Save",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    // ---------------- Close tab ----------------

    private void CloseTab_Click(object sender, RoutedEventArgs e) => CloseActiveTab();

    private void CloseTabButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.DataContext is EditorTab tab)
            CloseTab(tab);
    }

    private void CloseActiveTab()
    {
        if (TabHost.SelectedItem is EditorTab tab)
            CloseTab(tab);
    }

    private void CloseTab(EditorTab tab)
    {
        if (!ConfirmSaveIfDirty(tab)) return;
        _tabs.Remove(tab);
    }

    private bool ConfirmSaveIfDirty(EditorTab tab)
    {
        if (!tab.IsDirty) return true;
        TabHost.SelectedItem = tab;
        var r = MessageBox.Show(this,
            $"Save changes to \"{tab.Title}\" before closing?",
            "Unsaved changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        if (r == MessageBoxResult.Cancel) return false;
        if (r == MessageBoxResult.Yes) return SaveTabWithDialogIfNeeded(tab);
        return true; // No
    }

    private bool SaveTabWithDialogIfNeeded(EditorTab tab)
    {
        if (tab.FilePath != null) return SaveTab(tab);
        var dlg = new Microsoft.Win32.SaveFileDialog { Title = "Save As", FileName = tab.Title, Filter = "All files (*.*)|*.*" };
        if (dlg.ShowDialog(this) != true) return false;
        try { tab.SaveAs(dlg.FileName); return true; }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Could not save file:\n" + ex.Message, "Save",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    // ---------------- Folder explorer ----------------

    private void OpenFolder_Click(object sender, RoutedEventArgs e) => OpenFolder();

    private void OpenFolder()
    {
        var dlg = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select a folder to open",
            ShowNewFolderButton = false,
            UseDescriptionForTitle = true
        };
        if (dlg.ShowDialog(new Win32Window(this)) != System.Windows.Forms.DialogResult.OK) return;
        LoadFolder(dlg.SelectedPath);
    }

    private void LoadFolder(string root)
    {
        FolderTree.Items.Clear();
        DirectoryInfo di;
        try { di = new DirectoryInfo(root); }
        catch { return; }
        if (!di.Exists) return;

        var node = MakeDirNode(di, isRoot: true);
        FolderTree.Items.Add(node);
        node.IsExpanded = true;
        node.IsSelected = true;
        TryExpandLoad(node);
    }

    private static TreeViewItem MakeDirNode(DirectoryInfo di, bool isRoot = false) => new()
    {
        Header = isRoot ? di.FullName : di.Name,
        Tag = new FsNode { Path = di.FullName, IsDir = true },
        FontWeight = isRoot ? FontWeights.SemiBold : FontWeights.Normal,
        ToolTip = di.FullName,
        Items = { new TreeViewItem { Header = DummyMarker, Tag = null } } // lazy-load placeholder
    };

    private static TreeViewItem MakeFileNode(FileInfo fi) => new()
    {
        Header = fi.Name,
        Tag = new FsNode { Path = fi.FullName, IsDir = false },
        ToolTip = fi.FullName,
    };

    private void FolderTree_Expanded(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is TreeViewItem node)
            TryExpandLoad(node);
    }

    private void TryExpandLoad(TreeViewItem node)
    {
        if (node.Items.Count == 1
            && node.Items[0] is TreeViewItem child
            && child.Header is string h && h == DummyMarker)
        {
            node.Items.Clear();
            if (node.Tag is FsNode fs)
                PopulateChildren(node, fs.Path);
        }
    }

    private static void PopulateChildren(TreeViewItem parent, string dirPath)
    {
        DirectoryInfo di;
        try { di = new DirectoryInfo(dirPath); }
        catch { return; }

        DirectoryInfo[] dirs;
        FileInfo[] files;
        try
        {
            dirs = di.GetDirectories();
            files = di.GetFiles();
        }
        catch (UnauthorizedAccessException) { return; }
        catch (IOException) { return; }

        foreach (var d in dirs.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
        {
            if ((d.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0) continue;
            if (ExcludedDirs.Contains(d.Name)) continue;
            parent.Items.Add(MakeDirNode(d));
        }
        foreach (var f in files.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
        {
            if ((f.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0) continue;
            parent.Items.Add(MakeFileNode(f));
        }
    }

    private void FolderTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (FolderTree.SelectedItem is TreeViewItem node && node.Tag is FsNode fs && !fs.IsDir)
            OpenPath(fs.Path);
    }

    // ---------------- Tabs / status ----------------

    private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_activeTab != null)
        {
            _activeTab.Editor.TextArea.Caret.PositionChanged -= Caret_PositionChanged;
            _activeTab.PropertyChanged -= ActiveTab_PropertyChanged;
        }

        _activeTab = TabHost.SelectedItem as EditorTab;

        if (_activeTab != null)
        {
            _activeTab.PropertyChanged += ActiveTab_PropertyChanged;
            _activeTab.Editor.TextArea.Caret.PositionChanged += Caret_PositionChanged;
            _activeTab.Editor.Focus();
        }

        UpdateStatusBar();
    }

    private void ActiveTab_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(EditorTab.IsDirty) or nameof(EditorTab.Format))
            UpdateStatusBar();
    }

    private void Caret_PositionChanged(object? sender, EventArgs e)
    {
        if (_activeTab == null) return;
        var caret = _activeTab.Editor.TextArea.Caret;
        PositionStatus.Text = $"Ln {caret.Line}, Col {caret.Column}";
    }

    private void UpdateStatusBar()
    {
        if (_activeTab == null)
        {
            FormatStatus.Text = "No file open";
            PositionStatus.Text = "";
            DirtyStatus.Text = "";
            return;
        }
        FormatStatus.Text = _activeTab.Format;
        DirtyStatus.Text = _activeTab.IsDirty ? "● Unsaved" : "";
        var caret = _activeTab.Editor.TextArea.Caret;
        PositionStatus.Text = $"Ln {caret.Line}, Col {caret.Column}";
    }

    private void UpdateEmptyHint()
        => EmptyHint.Visibility = _tabs.Count > 0 ? Visibility.Collapsed : Visibility.Visible;

    // ---------------- View ----------------

    private void ToggleWordWrap_Click(object sender, RoutedEventArgs e)
    {
        foreach (var tab in _tabs)
            tab.Editor.WordWrap = !tab.Editor.WordWrap;
    }

    // ---------------- Shortcuts & lifecycle ----------------

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var mod = Keyboard.Modifiers;
        bool ctrl = (mod & ModifierKeys.Control) != 0;
        bool shift = (mod & ModifierKeys.Shift) != 0;
        if (!ctrl) return;

        switch (e.Key)
        {
            case Key.O when shift: OpenFolder(); e.Handled = true; break;
            case Key.O: OpenFile(); e.Handled = true; break;
            case Key.N: NewFile(); e.Handled = true; break;
            case Key.S when shift: SaveActiveAs(); e.Handled = true; break;
            case Key.S: SaveActive(); e.Handled = true; break;
            case Key.W: CloseActiveTab(); e.Handled = true; break;
            case Key.Tab when !shift: SwitchTab(+1); e.Handled = true; break;
            case Key.Tab when shift: SwitchTab(-1); e.Handled = true; break;
        }
    }

    private void SwitchTab(int delta)
    {
        if (_tabs.Count == 0) return;
        int n = _tabs.Count;
        TabHost.SelectedIndex = ((TabHost.SelectedIndex + delta) % n + n) % n;
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        foreach (var tab in _tabs.ToList())
        {
            if (!tab.IsDirty) continue;
            TabHost.SelectedItem = tab;
            var r = MessageBox.Show(this,
                $"Save changes to \"{tab.Title}\" before closing?",
                "Unsaved changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (r == MessageBoxResult.Cancel) { e.Cancel = true; return; }
            if (r == MessageBoxResult.Yes && !SaveTabWithDialogIfNeeded(tab)) { e.Cancel = true; return; }
        }
    }

    // ---------------- Helpers ----------------

    private sealed class FsNode
    {
        public string Path { get; init; } = "";
        public bool IsDir { get; init; }
    }

    private sealed class Win32Window : System.Windows.Forms.IWin32Window
    {
        private readonly Window _window;
        public Win32Window(Window window) => _window = window;
        public IntPtr Handle => new System.Windows.Interop.WindowInteropHelper(_window).Handle;
    }
}
