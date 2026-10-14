using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Windows;
using MessageBox = System.Windows.MessageBox;
using Application = System.Windows.Application;

namespace SlimClip;

internal sealed record Command(string Level, string? File);

internal static class Program
{
    private const string PipeName = "SlimClip-SingleInstance-v1";
    private const string MutexName = "Local\\SlimClip-SingleInstance-v1";

    [STAThread]
    private static void Main(string[] args)
    {
        var command = Parse(args);
        using var mutex = new Mutex(true, MutexName, out bool first);
        if (!first)
        {
            // The owning process may still be opening its pipe.
            for (int attempt = 0; attempt < 30; attempt++)
            {
                try
                {
                    using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
                    client.Connect(300);
                    using var writer = new StreamWriter(client) { AutoFlush = true };
                    writer.WriteLine(JsonSerializer.Serialize(command));
                    return;
                }
                catch (IOException) { Thread.Sleep(100); }
                catch (TimeoutException) { Thread.Sleep(100); }
            }
            MessageBox.Show("无法连接现有 SlimClip 窗口，请稍后重试。", "SlimClip");
            return;
        }
        var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        var window = new MainWindow();
        app.MainWindow = window;
        _ = ListenAsync(window);
        window.Show();
        window.Accept(command);
        app.Run();
    }

    private static Command Parse(string[] args)
    {
        if (args.Length >= 2 && args[0] is "--low" or "--medium" or "--high" or "--estimate")
            return new Command(args[0][2..], args[1]);
        if (args.Length >= 1 && args[0] == "--settings") return new Command("settings", null);
        return new Command("show", null);
    }

    private static async Task ListenAsync(MainWindow window)
    {
        while (!window.Dispatcher.HasShutdownStarted)
        {
            try
            {
                using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync();
                using var reader = new StreamReader(server);
                var line = await reader.ReadLineAsync();
                if (line is not null)
                {
                    var command = JsonSerializer.Deserialize<Command>(line);
                    if (command is not null && !window.Dispatcher.HasShutdownStarted)
                        window.Dispatcher.BeginInvoke(() => window.Accept(command));
                }
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or JsonException or System.Threading.Tasks.TaskCanceledException)
            { if (window.Dispatcher.HasShutdownStarted) break; }
        }
    }
}
