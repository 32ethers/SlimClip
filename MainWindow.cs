using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using MessageBox = System.Windows.MessageBox;

namespace SlimClip;

internal sealed class Job : INotifyPropertyChanged
{
    private string _state = Loc.T("queued");
    private double _progress;
    private bool _indeterminate;
    private string _remaining = Loc.T("queued");
    public string File { get; init; } = "";
    public string Level { get; init; } = "";
    public string DisplayLevel => Level == "estimate" ? Loc.T("estimate.title") : Loc.T("level." + Level);
    public string State { get => _state; set { _state = value; Changed(); } }
    public double Progress { get => _progress; set { _progress = value; Changed(); } }
    public bool IsIndeterminate { get => _indeterminate; set { _indeterminate = value; Changed(); } }
    public string Remaining { get => _remaining; set { _remaining = value; Changed(); } }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

internal sealed class MainWindow : Window
{
    private readonly ObservableCollection<Job> _jobs = new();
    private readonly Channel<Job> _queue = Channel.CreateUnbounded<Job>();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly System.Windows.Forms.NotifyIcon _tray;
    private readonly System.Drawing.Icon? _ownedTrayIcon;
    private CancellationTokenSource? _current;
    private Task? _worker;
    private bool _exitRequested;
    private bool _exitReady;
    private bool _warnedBackups;
    private int _pending;
    private readonly TextBlock _message = new() { Text = Loc.T("ready"), Margin = new Thickness(8) };

    public MainWindow()
    {
        Title = Loc.T("main.title");
        Icon = BitmapFrame.Create(new Uri("pack://application:,,,/assets/SlimClip.ico", UriKind.Absolute));
        Width = 900; Height = 500; MinWidth = 650; MinHeight = 350;
        _ownedTrayIcon = Environment.ProcessPath is string executable
            ? System.Drawing.Icon.ExtractAssociatedIcon(executable) : null;
        _tray = new System.Windows.Forms.NotifyIcon
        {
            Text = Loc.T("main.title"), Icon = _ownedTrayIcon ?? System.Drawing.SystemIcons.Application,
            Visible = true, ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip()
        };
        _tray.ContextMenuStrip.Items.Add(Loc.T("tray.open"), null, (_, _) => Dispatcher.BeginInvoke((Action)ShowWindow));
        _tray.ContextMenuStrip.Items.Add(Loc.T("exit"), null, (_, _) => Dispatcher.BeginInvoke((Action)ExitApplication));
        _tray.DoubleClick += (_, _) => Dispatcher.BeginInvoke((Action)ShowWindow);

        var root = new DockPanel(); Content = root;
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8) };
        DockPanel.SetDock(toolbar, Dock.Top); root.Children.Add(toolbar);
        AddButton(toolbar, Loc.T("settings"), () => new SettingsWindow { Owner = this }.ShowDialog());
        AddButton(toolbar, Loc.T("cancel.current"), () => _current?.Cancel());
        AddButton(toolbar, Loc.T("exit"), ExitApplication);
        DockPanel.SetDock(_message, Dock.Bottom); root.Children.Add(_message);
        var grid = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, ItemsSource = _jobs, Margin = new Thickness(8) };
        grid.Columns.Add(new DataGridTextColumn { Header = Loc.T("file.column"), Binding = new Binding(nameof(Job.File)), Width = new DataGridLength(3, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = Loc.T("preset.column"), Binding = new Binding(nameof(Job.DisplayLevel)), Width = 65 });
        var progress = new FrameworkElementFactory(typeof(ProgressBar));
        progress.SetValue(ProgressBar.MinimumProperty, 0.0);
        progress.SetValue(ProgressBar.MaximumProperty, 100.0);
        progress.SetValue(FrameworkElement.HeightProperty, 17.0);
        progress.SetBinding(ProgressBar.ValueProperty, new Binding(nameof(Job.Progress)));
        progress.SetBinding(ProgressBar.IsIndeterminateProperty, new Binding(nameof(Job.IsIndeterminate)));
        grid.Columns.Add(new DataGridTemplateColumn { Header = Loc.T("progress.column"), CellTemplate = new DataTemplate { VisualTree = progress }, Width = 130 });
        grid.Columns.Add(new DataGridTextColumn { Header = Loc.T("remaining.column"), Binding = new Binding(nameof(Job.Remaining)), Width = 130 });
        grid.Columns.Add(new DataGridTextColumn { Header = Loc.T("status.column"), Binding = new Binding(nameof(Job.State)), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
        root.Children.Add(grid);
        Loaded += (_, _) => WarnAboutBackups();
        Closing += (_, args) =>
        {
            if (_exitReady) return;
            args.Cancel = true;
            if (!_exitRequested) Hide(); // Never close before cancelled work cleans up.
        };
        Closed += (_, _) =>
        {
            _current?.Cancel(); _lifetime.Cancel(); _queue.Writer.TryComplete();
            _tray.Visible = false; _tray.ContextMenuStrip?.Dispose(); _tray.Dispose();
            _ownedTrayIcon?.Dispose();
        };
        // Queue worker starts inside a Dispatcher operation after the message loop is running.
        // Its await continuations must stay on the WPF UI thread when updating bound jobs.
        Dispatcher.BeginInvoke((Action)(() => { _worker = WorkAsync(); }));
    }

    private void WarnAboutBackups()
    {
        if (_warnedBackups) return;
        _warnedBackups = true;
        foreach (var item in RecoveryJournal.FindBackups())
        {
            if (File.Exists(item.Source))
            {
                MessageBox.Show(this, Loc.F("backup.found", item.Backup),
                    Loc.T("backup.pending"), MessageBoxButton.OK, MessageBoxImage.Warning);
                continue;
            }
            var choice = MessageBox.Show(this, Loc.F("backup.restore", item.Backup, item.Source),
                Loc.T("backup.restore.title"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (choice != MessageBoxResult.Yes) continue;
            try { File.Move(item.Backup, item.Source); }
            catch (Exception ex) { MessageBox.Show(this, Loc.F("backup.restore.error", ex.Message, item.Backup), Loc.T("backup.restore.failed"), MessageBoxButton.OK, MessageBoxImage.Error); }
        }
    }

    private async void ExitApplication()
    {
        if (_exitRequested) return;
        if (_current is not null || _pending > 0)
        {
            var choice = MessageBox.Show(this, Loc.T("exit.confirm"),
                Loc.T("exit.title"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (choice != MessageBoxResult.Yes) return;
        }
        _exitRequested = true;
        _message.Text = Loc.T("exit.cleaning");
        _current?.Cancel(); _lifetime.Cancel(); _queue.Writer.TryComplete();
        try { if (_worker is not null) await _worker; }
        catch (Exception ex)
        {
            MessageBox.Show(this, Loc.F("exit.error", ex.Message),
                "SlimClip", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { _exitReady = true; Close(); }
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
        catch (Exception ex) { _message.Text = Loc.F("file.invalid", ex.Message); return; }
        if (!Compression.IsSupported(file)) { _message.Text = Loc.F("file.unsupported", file); return; }
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
                job.Remaining = Loc.T("preparing");
                job.State = job.Level == "estimate" ? Loc.T("probe.estimate") : Loc.T("probe.encode");
                try
                {
                    var settings = Settings.Load();
                    if (string.IsNullOrWhiteSpace(settings.FfmpegDirectory) && !FfmpegSetup.HasTools(FfmpegSetup.ToolsDirectory) &&
                        FfmpegSetup.FindOnPath(Environment.GetEnvironmentVariable("PATH")) is null)
                    {
                        ShowWindow();
                        var consent = MessageBox.Show(this,
                            Loc.F("download.consent", FfmpegSetup.DownloadUrl),
                            Loc.T("download.title"), MessageBoxButton.YesNo, MessageBoxImage.Question);
                        if (consent != MessageBoxResult.Yes)
                        {
                            job.State = Loc.T("download.declined");
                            job.IsIndeterminate = false;
                            job.Remaining = "—";
                            continue;
                        }
                        var setupProgress = new Progress<string>(text => { job.State = text; _message.Text = text; });
                        await FfmpegSetup.InstallAsync(setupProgress, _current.Token);
                    }
                    _current.Token.ThrowIfCancellationRequested();
                    if (job.Level == "estimate")
                    {
                        var eta = new RemainingTime();
                        job.Remaining = Loc.T("eta.calculating");
                        var results = await Estimator.RunAsync(job.File, settings, (stage, value) =>
                        {
                            Dispatcher.VerifyAccess(); // WorkAsync and its awaits run on the UI dispatcher.
                            job.State = stage;
                            job.IsIndeterminate = value is null;
                            if (value is not null)
                            {
                                job.Progress = value.Value * 100;
                                job.Remaining = eta.Update(value.Value);
                            }
                        }, _current.Token);
                        string detail = string.Join("\n", results.Select(x =>
                            Loc.F("estimate.size", Loc.T("level." + x.Level), x.Bytes / 1048576.0)));
                        job.IsIndeterminate = false; job.Progress = 100; job.Remaining = Loc.T("eta.done");
                        job.State = detail.Replace('\n', ' ');
                        _message.Text = Loc.F("estimate.done", Path.GetFileName(job.File));
                        ShowWindow(); // A dialog owned by a hidden tray window may be invisible.
                        MessageBox.Show(this, Loc.F("estimate.result", new FileInfo(job.File).Length / 1048576.0, detail), Loc.T("estimate.title"));
                    }
                    else
                    {
                        bool allowOmission = false;
                        var source = await MediaTools.ProbeAsync(job.File, _current.Token);
                        if (source.SubtitleCount + source.AncillaryCount > 0 && settings.SaveMode == SaveMode.Copy)
                        {
                            ShowWindow(); // Ask for explicit data-loss consent in a visible window.
                            var choice = MessageBox.Show(this,
                                Loc.F("tracks.consent", Path.GetFileName(job.File), source.SubtitleCount, source.AncillaryCount),
                                Loc.T("tracks.title"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
                            if (choice != MessageBoxResult.Yes) { job.State = Loc.T("tracks.declined"); job.IsIndeterminate = false; job.Remaining = "—"; continue; }
                            allowOmission = true;
                        }
                        var eta = new RemainingTime();
                        job.Remaining = Loc.T("eta.calculating");
                        job.State = Loc.T("encoding");
                        var result = await Compression.RunAsync(new CompressionRequest(job.File, job.Level),
                            settings.Presets[job.Level], settings, allowOmission, value =>
                            {
                                Dispatcher.VerifyAccess();
                                job.IsIndeterminate = false; job.Progress = value * 100;
                                job.Remaining = eta.Update(value);
                            }, _current.Token);
                        job.State = result.Message;
                        job.IsIndeterminate = false;
                        job.Remaining = result.Success ? Loc.T("eta.done") : Loc.T("finished");
                    }
                }
                catch (OperationCanceledException) { job.State = Loc.T("cancelled"); job.IsIndeterminate = false; job.Remaining = "—"; }
                catch (Exception ex) { job.State = Loc.F("failed", ex.Message); job.IsIndeterminate = false; job.Remaining = "—"; }
                finally { _current.Dispose(); _current = null; }
            }
        }
        catch (OperationCanceledException) { }
    }
}
