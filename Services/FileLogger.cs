using System.Collections.Concurrent;
using System.Text;

namespace PraffLeather.Services;

/// <summary>
/// Uyarı ve hataları App_Data/logs/yyyy-MM-dd.log dosyasına yazar (paylaşımlı hostingde konsol görünmediği için).
/// 30 günden eski günlükler silinir. Bağımlılık gerektirmez.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _dir;
    private readonly LogLevel _min;
    private readonly object _lock = new();
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();
    private DateOnly _cleaned;

    public FileLoggerProvider(string dir, LogLevel min)
    {
        _dir = dir;
        _min = min;
        Directory.CreateDirectory(dir);
    }

    public ILogger CreateLogger(string categoryName) => _loggers.GetOrAdd(categoryName, n => new FileLogger(this, n));

    internal void Write(LogLevel level, string category, string message, Exception? ex)
    {
        if (level < _min) return;
        var now = DateTime.Now;
        var sb = new StringBuilder();
        sb.Append(now.ToString("yyyy-MM-dd HH:mm:ss")).Append(" [").Append(level).Append("] ").Append(category).Append(": ").AppendLine(message);
        if (ex != null) sb.AppendLine(ex.ToString());
        lock (_lock)
        {
            try
            {
                File.AppendAllText(Path.Combine(_dir, $"{now:yyyy-MM-dd}.log"), sb.ToString(), Encoding.UTF8);
                var today = DateOnly.FromDateTime(now);
                if (_cleaned != today)
                {
                    _cleaned = today;
                    foreach (var f in Directory.EnumerateFiles(_dir, "*.log"))
                        if (File.GetLastWriteTime(f) < now.AddDays(-30)) File.Delete(f);
                }
            }
            catch { /* günlük yazılamazsa uygulama çalışmaya devam eder */ }
        }
    }

    public void Dispose() { }

    private sealed class FileLogger(FileLoggerProvider owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= owner._min && logLevel != LogLevel.None;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel)) owner.Write(logLevel, category, formatter(state, exception), exception);
        }
    }
}
