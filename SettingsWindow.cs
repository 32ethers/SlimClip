using System.Windows;
using System.Windows.Controls;
using MessageBox = System.Windows.MessageBox;

namespace SlimClip;

internal sealed class SettingsWindow : Window
{
    private readonly Settings _settings = Settings.Load();
    private readonly ComboBox _saveMode = new();
    private readonly TextBox _ffmpegDirectory = new();
    private readonly CheckBox _skipLarger = new() { Content = "结果不小于原文件时，不保存压缩结果", Margin = new Thickness(0, 8, 0, 8) };
    private readonly Dictionary<string, (TextBox Crf, TextBox Height)> _controls = new();

    public SettingsWindow()
    {
        Title = "SlimClip · 压缩设置"; Width = 580; Height = 500;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new StackPanel { Margin = new Thickness(16) };
        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        panel.Children.Add(new TextBlock { Text = "输出格式固定为 H.265 视频 + MP4 容器。", Margin = new Thickness(0, 0, 0, 12) });
        panel.Children.Add(new TextBlock { Text = "压缩后文件：", Margin = new Thickness(0, 0, 0, 5) });
        _saveMode.Items.Add("同目录生成新文件（推荐）");
        _saveMode.Items.Add("成功后替换原文件（有字幕时禁止）");
        _saveMode.SelectedIndex = _settings.SaveMode == SaveMode.Copy ? 0 : 1;
        panel.Children.Add(_saveMode);
        _skipLarger.IsChecked = _settings.SkipLargerResult; panel.Children.Add(_skipLarger);
        panel.Children.Add(new TextBlock { Text = "CRF 越大文件通常越小、画质越低；高度 0 表示保持原分辨率。不放大原片。", Margin = new Thickness(0, 4, 0, 10) });
        var table = new Grid(); panel.Children.Add(table);
        foreach (var width in new[] { 90.0, 150.0, 180.0 }) table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width) });
        for (int i = 0; i < 4; i++) table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        AddCell(table, new TextBlock { Text = "档位" }, 0, 0);
        AddCell(table, new TextBlock { Text = "H.265 CRF (0–51)" }, 0, 1);
        AddCell(table, new TextBlock { Text = "最大高度 (像素)" }, 0, 2);
        int row = 1;
        foreach (var (key, name) in new[] { ("low", "低"), ("medium", "中"), ("high", "高") })
        {
            var preset = _settings.Presets[key];
            AddCell(table, new TextBlock { Text = name }, row, 0);
            var crf = new TextBox { Text = preset.Crf.ToString(), Margin = new Thickness(0, 2, 10, 2) };
            var height = new TextBox { Text = preset.MaxHeight.ToString(), Margin = new Thickness(0, 2, 10, 2) };
            AddCell(table, crf, row, 1); AddCell(table, height, row, 2);
            _controls.Add(key, (crf, height)); row++;
        }
        panel.Children.Add(new TextBlock { Text = "FFmpeg 工具目录（已有 ffmpeg.exe 和 ffprobe.exe）：", Margin = new Thickness(0, 16, 0, 4) });
        var toolsRow = new DockPanel(); panel.Children.Add(toolsRow);
        var browse = new Button { Content = "浏览…", Padding = new Thickness(8, 4, 8, 4) };
        DockPanel.SetDock(browse, Dock.Right); toolsRow.Children.Add(browse);
        _ffmpegDirectory.Text = _settings.FfmpegDirectory ?? "";
        _ffmpegDirectory.ToolTip = "留空使用经过授权后下载到当前用户目录的 FFmpeg；不搜索 PATH。";
        toolsRow.Children.Add(_ffmpegDirectory);
        browse.Click += (_, _) =>
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog { Description = "选择包含 ffmpeg.exe 和 ffprobe.exe 的目录" };
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) _ffmpegDirectory.Text = dialog.SelectedPath;
        };
        panel.Children.Add(new TextBlock { Text = "留空使用当前用户的 tools 目录；SlimClip 不从 PATH 查找工具，也不在安装包中附带 GPLv3 FFmpeg。",
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 0) });
        var save = new Button { Content = "保存设置", Width = 120, Margin = new Thickness(0, 20, 0, 0), Padding = new Thickness(8) };
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
                    throw new ArgumentException($"{key} 档参数无效：CRF 应为 0–51，高度为 0 或 2–16384 的偶数。");
                _settings.Presets[key] = new Preset { Crf = crf, MaxHeight = height };
            }
            _settings.SaveMode = _saveMode.SelectedIndex == 0 ? SaveMode.Copy : SaveMode.Replace;
            _settings.SkipLargerResult = _skipLarger.IsChecked == true;
            string folder = _ffmpegDirectory.Text.Trim();
            if (folder.Length > 0 && (!System.IO.Path.IsPathFullyQualified(folder) || !FfmpegSetup.HasTools(folder)))
                throw new ArgumentException("请选择同时包含 ffmpeg.exe 和 ffprobe.exe 的绝对目录；留空则使用用户目录中的工具。");
            _settings.FfmpegDirectory = folder.Length == 0 ? null : System.IO.Path.GetFullPath(folder);
            _settings.Save(); DialogResult = true;
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "无法保存设置", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
}
