using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using MessageBox = System.Windows.MessageBox;

namespace SlimClip;

internal sealed class SettingsWindow : Window
{
    private readonly Settings _settings = Settings.Load();
    private readonly ComboBox _saveMode = new();
    private readonly TextBox _ffmpegDirectory = new();
    private readonly CheckBox _skipLarger = new() { Content = Loc.T("settings.skip"), Margin = new Thickness(0, 8, 0, 8) };
    private readonly ComboBox _language = new();
    private readonly Dictionary<string, (TextBox Crf, TextBox Height)> _controls = new();

    public SettingsWindow()
    {
        Title = Loc.T("settings.title");
        Icon = BitmapFrame.Create(new Uri("pack://application:,,,/assets/SlimClip.ico", UriKind.Absolute));
        Width = 580; Height = 540;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new StackPanel { Margin = new Thickness(16) };
        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        panel.Children.Add(new TextBlock { Text = Loc.T("settings.format"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) });
        panel.Children.Add(new TextBlock { Text = Loc.T("settings.output"), Margin = new Thickness(0, 0, 0, 5) });
        _saveMode.Items.Add(Loc.T("settings.copy"));
        _saveMode.Items.Add(Loc.T("settings.replace"));
        _saveMode.SelectedIndex = _settings.SaveMode == SaveMode.Copy ? 0 : 1;
        panel.Children.Add(_saveMode);
        _skipLarger.IsChecked = _settings.SkipLargerResult; panel.Children.Add(_skipLarger);
        panel.Children.Add(new TextBlock { Text = Loc.T("settings.crf.help"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 10) });
        var table = new Grid(); panel.Children.Add(table);
        foreach (var width in new[] { 90.0, 150.0, 180.0 }) table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width) });
        for (int i = 0; i < 4; i++) table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        AddCell(table, new TextBlock { Text = Loc.T("preset.column") }, 0, 0);
        AddCell(table, new TextBlock { Text = "H.265 CRF (0–51)" }, 0, 1);
        AddCell(table, new TextBlock { Text = Loc.T("settings.height") }, 0, 2);
        int row = 1;
        foreach (var (key, name) in new[] { ("low", Loc.T("level.low")), ("medium", Loc.T("level.medium")), ("high", Loc.T("level.high")) })
        {
            var preset = _settings.Presets[key];
            AddCell(table, new TextBlock { Text = name }, row, 0);
            var crf = new TextBox { Text = preset.Crf.ToString(), Margin = new Thickness(0, 2, 10, 2) };
            var height = new TextBox { Text = preset.MaxHeight.ToString(), Margin = new Thickness(0, 2, 10, 2) };
            AddCell(table, crf, row, 1); AddCell(table, height, row, 2);
            _controls.Add(key, (crf, height)); row++;
        }
        panel.Children.Add(new TextBlock { Text = Loc.T("settings.tools"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 16, 0, 4) });
        var toolsRow = new DockPanel(); panel.Children.Add(toolsRow);
        var browse = new Button { Content = Loc.T("settings.browse"), Padding = new Thickness(8, 4, 8, 4) };
        DockPanel.SetDock(browse, Dock.Right); toolsRow.Children.Add(browse);
        _ffmpegDirectory.Text = _settings.FfmpegDirectory ?? "";
        _ffmpegDirectory.ToolTip = Loc.T("settings.tools.tip");
        toolsRow.Children.Add(_ffmpegDirectory);
        browse.Click += (_, _) =>
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog { Description = Loc.T("settings.folder.select") };
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) _ffmpegDirectory.Text = dialog.SelectedPath;
        };
        panel.Children.Add(new TextBlock { Text = Loc.T("settings.tools.help"),
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 0) });
        panel.Children.Add(new TextBlock { Text = Loc.T("settings.language"), Margin = new Thickness(0, 14, 0, 4) });
        _language.Items.Add(Loc.T("language.auto"));
        _language.Items.Add(Loc.T("language.english"));
        _language.Items.Add(Loc.T("language.chinese"));
        _language.SelectedIndex = _settings.Language switch { "en" => 1, "zh-Hans" => 2, _ => 0 };
        panel.Children.Add(_language);
        var save = new Button { Content = Loc.T("settings.save"), Width = 120, Margin = new Thickness(0, 20, 0, 0), Padding = new Thickness(8) };
        save.Click += (_, _) => Save(); panel.Children.Add(save);
    }

    private static void AddCell(Grid grid, UIElement element, int row, int col)
    { Grid.SetRow(element, row); Grid.SetColumn(element, col); grid.Children.Add(element); }

    private void Save()
    {
        try
        {
            foreach (var (key, controls) in _controls)
            {
                if (!int.TryParse(controls.Crf.Text, out int crf) || crf is < 0 or > 51 ||
                    !int.TryParse(controls.Height.Text, out int height) || height < 0 || height > 16384 ||
                    (height != 0 && height % 2 != 0))
                    throw new ArgumentException(Loc.F("settings.invalid.preset", Loc.T("level." + key)));
                _settings.Presets[key] = new Preset { Crf = crf, MaxHeight = height };
            }
            _settings.SaveMode = _saveMode.SelectedIndex == 0 ? SaveMode.Copy : SaveMode.Replace;
            _settings.SkipLargerResult = _skipLarger.IsChecked == true;
            string folder = _ffmpegDirectory.Text.Trim();
            if (folder.Length > 0 && (!System.IO.Path.IsPathFullyQualified(folder) || !FfmpegSetup.HasTools(folder)))
                throw new ArgumentException(Loc.T("settings.invalid.tools"));
            _settings.FfmpegDirectory = folder.Length == 0 ? null : System.IO.Path.GetFullPath(folder);
            string selectedLanguage = _language.SelectedIndex switch { 1 => "en", 2 => "zh-Hans", _ => "auto" };
            bool languageChanged = _settings.Language != selectedLanguage;
            _settings.Language = selectedLanguage;
            _settings.Save();
            if (languageChanged)
                MessageBox.Show(this, Loc.T("settings.restart"), "SlimClip", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, Loc.T("settings.save.failed"), MessageBoxButton.OK, MessageBoxImage.Error); }
    }
}
