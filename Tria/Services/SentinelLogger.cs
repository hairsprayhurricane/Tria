namespace Tria.Services;

public class SentinelLogger
{
    private readonly string _path;
    private readonly object _lock = new();

    public SentinelLogger(string path) => _path = path;

    public void Log(string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
        lock (_lock)
            File.AppendAllText(_path, line + Environment.NewLine);
    }

    public void LogError(string context, Exception ex)
        => Log($"ОШИБКА [{context}]: {ex.GetType().Name}: {ex.Message}");
}
