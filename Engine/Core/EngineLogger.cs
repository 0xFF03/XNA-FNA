using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace MyGame.Engine.Core;

public static class EngineLogger
{
    private struct LogMessage
    {
        public string FormattedText;
        public string Category;
    }

    private static Channel<LogMessage>? _logChannel;
    private static Task? _writerTask;

    private static StreamWriter? _mainWriter;
    private static StreamWriter? _networkWriter;
    private static StreamWriter? _errorWriter;
    private static StreamWriter? _perfWriter;

    public static readonly List<string> LiveConsole = new();
    private const int MaxConsoleLines = 150;
    public static readonly object ConsoleLock = new object();

    public static void Initialize()
    {
        string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MyGame", "Logs");
        Directory.CreateDirectory(logDir);

        string timeStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        int pid = Environment.ProcessId;

        _mainWriter = new StreamWriter(Path.Combine(logDir, $"main_{timeStamp}_p{pid}.log"), append: true) { AutoFlush = true };
        _networkWriter = new StreamWriter(Path.Combine(logDir, $"network_{timeStamp}_p{pid}.log"), append: true) { AutoFlush = true };
        _errorWriter = new StreamWriter(Path.Combine(logDir, $"error_{timeStamp}_p{pid}.log"), append: true) { AutoFlush = true };
        _perfWriter = new StreamWriter(Path.Combine(logDir, $"perf_{timeStamp}_p{pid}.log"), append: true) { AutoFlush = true };

        _logChannel = Channel.CreateUnbounded<LogMessage>(new UnboundedChannelOptions { SingleReader = true });

        _writerTask = Task.Run(async () =>
        {
            await foreach (var msg in _logChannel.Reader.ReadAllAsync())
            {
                await _mainWriter!.WriteLineAsync(msg.FormattedText);

                if (msg.Category == "NETWORK" || msg.Category == "STEAM")
                    await _networkWriter!.WriteLineAsync(msg.FormattedText);
                else if (msg.Category == "ERROR" || msg.Category == "FATAL")
                    await _errorWriter!.WriteLineAsync(msg.FormattedText);
                else if (msg.Category == "PERF" || msg.Category == "LAG")
                    await _perfWriter!.WriteLineAsync(msg.FormattedText);
            }
        });

        Log("Advanced Diagnostic Logger Initialized.", "SYSTEM");
    }

    public static void Log(string message, string category = "INFO")
    {
        string formatted = $"[{DateTime.Now:HH:mm:ss.fff}] [{category}] {message}";

        _logChannel?.Writer.TryWrite(new LogMessage { FormattedText = formatted, Category = category });
        Console.WriteLine(formatted);

        lock (ConsoleLock)
        {
            LiveConsole.Add(formatted);
            if (LiveConsole.Count > MaxConsoleLines) LiveConsole.RemoveAt(0);
        }
    }

    public static void LogError(string message, Exception ex)
    {
        Log($"{message} | Exception: {ex.Message}\n{ex.StackTrace}", "ERROR");
    }

    // ARCHITECTURE FIX: Absolute guarantee that a hard-crash is documented to the hard drive instantly.
    public static void LogFatalSync(string message, Exception ex)
    {
        string formatted = $"[{DateTime.Now:HH:mm:ss.fff}] [FATAL] {message} | Exception: {ex.Message}\n{ex.StackTrace}";
        Console.WriteLine(formatted);

        _errorWriter?.WriteLine(formatted);
        _errorWriter?.Flush();
        _mainWriter?.WriteLine(formatted);
        _mainWriter?.Flush();
    }

    public static void LogPerformance(string systemName, double elapsedMilliseconds)
    {
        float heapMb = GC.GetTotalMemory(false) / (1024f * 1024f);
        Log($"LAG SPIKE DETECTED in [{systemName}]: {elapsedMilliseconds:0.00}ms. Managed Heap: {heapMb:0.00}MB", "PERF");
    }

    public static void Shutdown()
    {
        if (_logChannel != null)
        {
            _logChannel.Writer.Complete();
            _writerTask?.Wait(2000); // Give it exactly 2 seconds to flush before dying

            _mainWriter?.Dispose();
            _networkWriter?.Dispose();
            _errorWriter?.Dispose();
            _perfWriter?.Dispose();
        }
    }
}
