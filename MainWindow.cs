using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using MessageBox = System.Windows.MessageBox;

namespace SlimClip;

internal sealed class Job : INotifyPropertyChanged
{
    private string _state = "排队中";
    private double _progress;
    private bool _indeterminate;
    public string File { get; init; } = "";
    public string Level { get; init; } = "";
    public string State { get => _state; set { _state = value; Changed(); } }
    public double Progress { get => _progress; set { _progress = value; Changed(); } }
    public bool IsIndeterminate { get => _indeterminate; set { _indeterminate = value; Changed(); } }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

internal sealed class MainWindow : Window
{
    private readonly ObservableCollection<Job> _jobs = new();
    private readonly Channel<Job> _queue = Channel.CreateUnbounded<Job>();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly System.Windows.Forms.NotifyIcon _tray;
    private CancellationTokenSource? _current;
    private bool _exitRequested;
    private bool _warnedBackups;
    private int _pending;
    private readonly TextBlock _message = new() { Text = "就绪。右键视频文件，选择压缩档位或预估。", Margin = new Thickness(8) };

    public MainWindow()
    {
        Title = "SlimClip · 视频压缩";
        Width = 900; Height = 500; MinWidth = 650; MinHeight = 350;
        _tray = new System.Windows.Forms.NotifyIcon
        {
            Text = "SlimClip 视频压缩", Icon = System.Drawing.SystemIcons.Application,
            Visible = true, ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip()
        };
        _tray.ContextMenuStrip.Items.Add("打开 SlimClip", null, (_, _) => Dispatcher.BeginInvoke((Action)ShowWindow));
        _tray.ContextMenuStrip.Items.Add("退出", null, (_, _) => Dispatcher.BeginInvoke((Action)ExitApplication));
        _tray.DoubleClick += (_, _) => Dispatcher.BeginInvoke((Action)ShowWindow);

        var root = new DockPanel(); Content = root;
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8) };
        DockPanel.SetDock(toolbar, Dock.Top); root.Children.Add(toolbar);
        AddButton(toolbar, "设置", () => new SettingsWindow { Owner = this }.ShowDialog());
        AddButton(toolbar, "取消当前任务", () => _current?.Cancel());
        AddButton(toolbar, "退出", ExitApplication);
        DockPanel.SetDock(_message, Dock.Bottom); root.Children.Add(_message);
        var grid = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, ItemsSource = _jobs, Margin = new Thickness(8) };
        grid.Columns.Add(new DataGridTextColumn { Header = "视频文件", Binding = new Binding(nameof(Job.File)), Width = new DataGridLength(3, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "档位", Binding = new Binding(nameof(Job.Level)), Width = 65 });
        var progress = new FrameworkElementFactory(typeof(ProgressBar));
        progress.SetValue(ProgressBar.MinimumProperty, 0.0);
        progress.SetValue(ProgressBar.MaximumProperty, 100.0);
        progress.SetValue(FrameworkElement.HeightProperty, 17.0);
        progress.SetBinding(ProgressBar.ValueProperty, new Binding(nameof(Job.Progress)));
        progress.SetBinding(ProgressBar.IsIndeterminateProperty, new Binding(nameof(Job.IsIndeterminate)));
        grid.Columns.Add(new DataGridTemplateColumn { Header = "进度", CellTemplate = new DataTemplate { VisualTree = progress }, Width = 140 });
        grid.Columns.Add(new DataGridTextColumn { Header = "状态 / 结果", Binding = new Binding(nameof(Job.State)), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
        root.Children.Add(grid);
        Loaded += (_, _) => WarnAboutBackups();
        Closing += (_, args) =>
        {
            if (_exitRequested) return;
            args.Cancel = true;
            Hide(); // Keep the queue and tray alive when the title-bar close button is clicked.
        };
        Closed += (_, _) =>
        {
            _current?.Cancel(); _lifetime.Cancel(); _queue.Writer.TryComplete();
            _tray.Visible = false; _tray.ContextMenuStrip?.Dispose(); _tray.Dispose();
        };
        // Queue worker starts inside a Dispatcher operation after the message loop is running.
        // Its await continuations must stay on the WPF UI thread when updating bound jobs.
        Dispatcher.BeginInvoke((Action)(() => { _ = WorkAsync(); }));
    }

    private void WarnAboutBackups()
    {
        if (_warnedBackups) return;
        _warnedBackups = true;
        foreach (var item in RecoveryJournal.FindBackups())
        {
            if (File.Exists(item.Source))
            {
                MessageBox.Show(this, $"检测到压缩后仍保留的原片备份，请确认文件无误后手动处理：\n{item.Backup}",
                    "备份待处理", MessageBoxButton.OK, MessageBoxImage.Warning);
                continue;
            }
            var choice = MessageBox.Show(this, $"原文件不见了，但发现备份：\n{item.Backup}\n\n是否将它恢复到：\n{item.Source}\n\n已有转换结果不会被删除。",
                "恢复原文件", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (choice != MessageBoxResult.Yes) continue;
            try { File.Move(item.Backup, item.Source); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message + "\n备份仍在：" + item.Backup, "恢复失败", MessageBoxButton.OK, MessageBoxImage.Error); }
        }
    }

    private void ExitApplication()
    {
        if (_current is not null || _pending > 0)
        {
            var choice = MessageBox.Show(this, "退出会取消当前任务，并丢弃未开始的队列。确定退出吗？",
                "退出 SlimClip", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (choice != MessageBoxResult.Yes) return;
        }
        _exitRequested = true;
        Close();
    }

    private void ShowWindow()
    {
        if (!IsVisible) Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }

    private static void AddButton(Panel panel, string text, Action click)
    {
        var button = new Button { Content = text, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(8, 4, 8, 4) };
        button.Click += (_, _) => click(); panel.Children.Add(button);
    }

    public void Accept(Command command)
    {
        ShowWindow();
        if (command.Level == "settings") { new SettingsWindow { Owner = this }.ShowDialog(); return; }
        if (command.Level is not ("low" or "medium" or "high" or "estimate") || command.File is null) return;
        string file;
        try { file = Path.GetFullPath(command.File); }
        catch (Exception ex) { _message.Text = "文件路径无效：" + ex.Message; return; }
        if (!Compression.IsSupported(file)) { _message.Text = "不是支持的视频文件：" + file; return; }
        var job = new Job { File = file, Level = command.Level == "estimate" ? "estimate" : command.Level };
        _jobs.Add(job);
        _pending++;
        _queue.Writer.TryWrite(job);
    }

    private async Task WorkAsync()
    {
        try
        {
            await foreach (var job in _queue.Reader.ReadAllAsync(_lifetime.Token))
            {
                _pending--;
                _current = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
                job.IsIndeterminate = true;
                job.State = job.Level == "estimate" ? "探测视频，准备评估…" : "探测视频，准备转换…";
                try
                {
                    var settings = Settings.Load();
                    if (job.Level == "estimate")
                    {
                        var results = await Estimator.RunAsync(job.File, settings, (stage, value) =>
                        {
                            Dispatcher.VerifyAccess(); // WorkAsync and its awaits run on the UI dispatcher.
                            job.State = stage;
                            job.IsIndeterminate = value is null;
                            if (value is not null) job.Progress = value.Value * 100;
                        }, _current.Token);
                        var labels = new Dictionary<string, string> { ["low"] = "低", ["medium"] = "中", ["high"] = "高" };
                        string detail = string.Join("\n", results.Select(x => $"{labels[x.Level]}：约 {x.Bytes / 1048576.0:F1} MiB"));
                        job.IsIndeterminate = false; job.Progress = 100;
                        job.State = detail.Replace('\n', ' ');
                        _message.Text = "预估已完成：" + Path.GetFileName(job.File);
                        ShowWindow(); // A dialog owned by a hidden tray window may be invisible.
                        MessageBox.Show(this, $"源文件：{new FileInfo(job.File).Length / 1048576.0:F1} MiB\n{detail}\n\n根据中间片段取样推算，场景变化可能使误差很大；不是大小保证。", "视频大小预估");
                    }
                    else
                    {
                        bool allowOmission = false;
                        var source = await MediaTools.ProbeAsync(job.File, _current.Token);
                        if (source.SubtitleCount + source.AncillaryCount > 0 && settings.SaveMode == SaveMode.Copy)
                        {
                            ShowWindow(); // Ask for explicit data-loss consent in a visible window.
                            var choice = MessageBox.Show(this,
                                $"{Path.GetFileName(job.File)} 包含 {source.SubtitleCount} 条字幕和 {source.AncillaryCount} 条额外视频/数据轨。首版输出只保留主视频与全部音轨；原文件将保留。是否继续另存？",
                                "轨道提示", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                            if (choice != MessageBoxResult.Yes) { job.State = "用户取消：输出会省略额外轨道"; continue; }
                            allowOmission = true;
                        }
                        job.State = "转换中";
                        var result = await Compression.RunAsync(new CompressionRequest(job.File, job.Level),
                            settings.Presets[job.Level], settings, allowOmission, value =>
                            {
                                Dispatcher.VerifyAccess();
                                job.IsIndeterminate = false; job.Progress = value * 100;
                            }, _current.Token);
                        job.State = result.Message;
                        job.IsIndeterminate = false;
                    }
                }
                catch (OperationCanceledException) { job.State = "已取消"; job.IsIndeterminate = false; }
                catch (Exception ex) { job.State = "失败：" + ex.Message; job.IsIndeterminate = false; }
                finally { _current.Dispose(); _current = null; }
            }
        }
        catch (OperationCanceledException) { }
    }
}
