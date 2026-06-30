using System.Text;

namespace Tria.Services;

public enum SentinelCheckStatus { Idle, Running, Done, Error }

public sealed record SentinelCheckRow(
    int Id,
    string Question,
    string StudentAnswer,
    string ExpectedLabel,
    bool? AiIsCorrect,
    string AiComment,
    bool IsError);

public sealed record SentinelCheckSnapshot(
    string Status,
    int Total,
    int Processed,
    string? ErrorMessage,
    string? OutputFile,
    List<SentinelCheckRow> Results);

public sealed record SentinelCheckRunSummary(
    int Number,
    string FileName,
    int RowCount,
    DateTime ModifiedAtUtc);

/// <summary>
/// Runs wwwroot/Resources/sentinel_test_pairs.csv (read-only, never modified) through
/// IOllamaGradingService row by row in a background task. Each run is persisted as its
/// own immutable file SentinelCheck/checked-{n}.csv (input columns + result + comment),
/// so past runs stay browsable after the live run finishes.
/// </summary>
public class SentinelCsvCheckRunner
{
    private readonly IWebHostEnvironment _env;
    private readonly IOllamaGradingService _grading;
    private readonly ILogger<SentinelCsvCheckRunner> _logger;

    private readonly object _lock = new();
    private List<SentinelCheckRow> _results = new();
    private SentinelCheckStatus _status = SentinelCheckStatus.Idle;
    private int _total;
    private string? _errorMessage;
    private string? _outputFileName;
    private int? _currentRunNumber;

    public SentinelCsvCheckRunner(IWebHostEnvironment env, IOllamaGradingService grading, ILogger<SentinelCsvCheckRunner> logger)
    {
        _env = env;
        _grading = grading;
        _logger = logger;
    }

    private string OutputDir => Path.Combine(_env.ContentRootPath, "SentinelCheck");

    public bool TryStart()
    {
        lock (_lock)
        {
            if (_status == SentinelCheckStatus.Running) return false;
            _results = new List<SentinelCheckRow>();
            _status = SentinelCheckStatus.Running;
            _total = 0;
            _errorMessage = null;
            _outputFileName = null;
            _currentRunNumber = null;
        }

        _ = Task.Run(RunAsync);
        return true;
    }

    public SentinelCheckSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            return new SentinelCheckSnapshot(
                _status.ToString().ToLowerInvariant(),
                _total,
                _results.Count,
                _errorMessage,
                _outputFileName,
                new List<SentinelCheckRow>(_results));
        }
    }

    public List<SentinelCheckRunSummary> GetPastRuns()
    {
        var dir = OutputDir;
        if (!Directory.Exists(dir)) return new List<SentinelCheckRunSummary>();

        int? exclude;
        lock (_lock) exclude = _status == SentinelCheckStatus.Running ? _currentRunNumber : null;

        var result = new List<SentinelCheckRunSummary>();
        foreach (var file in Directory.EnumerateFiles(dir, "checked-*.csv"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var dashIndex = name.LastIndexOf('-');
            if (dashIndex < 0 || !int.TryParse(name[(dashIndex + 1)..], out var number)) continue;
            if (exclude.HasValue && number == exclude.Value) continue;

            var rowCount = File.ReadLines(file).Count(l => !string.IsNullOrWhiteSpace(l)) - 1;
            result.Add(new SentinelCheckRunSummary(number, Path.GetFileName(file), Math.Max(0, rowCount), File.GetLastWriteTimeUtc(file)));
        }

        return result.OrderByDescending(r => r.Number).ToList();
    }

    public List<SentinelCheckRow>? GetRunRows(int number)
    {
        var path = Path.Combine(OutputDir, $"checked-{number}.csv");
        if (!File.Exists(path)) return null;
        return ParseRunFile(path);
    }

    private async Task RunAsync()
    {
        try
        {
            var inputPath = Path.Combine(_env.WebRootPath, "Resources", "sentinel_test_pairs.csv");
            var rows = ParseInputCsv(await File.ReadAllTextAsync(inputPath));

            Directory.CreateDirectory(OutputDir);
            var (outputPath, runNumber) = ComputeOutputPath();

            lock (_lock)
            {
                _total = rows.Count;
                _outputFileName = Path.GetFileName(outputPath);
                _currentRunNumber = runNumber;
            }

            foreach (var (id, question, studentAnswer, expectedLabel) in rows)
            {
                bool? isCorrect = null;
                string comment;
                bool isError = false;

                try
                {
                    var (graded, aiComment) = await _grading.GradeAnswerAsync(question, studentAnswer);
                    isCorrect = graded;
                    comment = aiComment;
                }
                catch (Exception ex)
                {
                    isError = true;
                    comment = "Ошибка обращения к Ollama: " + ex.Message;
                    _logger.LogError(ex, "Sentinel CSV check failed for row {Id}", id);
                }

                var row = new SentinelCheckRow(id, question, studentAnswer, expectedLabel, isCorrect, comment, isError);
                List<SentinelCheckRow> snapshot;
                lock (_lock)
                {
                    _results.Add(row);
                    snapshot = new List<SentinelCheckRow>(_results);
                }

                // This file belongs solely to the current run (created fresh above) — rewriting it
                // as rows come in is how progress is persisted live. The source CSV is never touched.
                await File.WriteAllTextAsync(outputPath, BuildCsv(snapshot), Encoding.UTF8);
            }

            lock (_lock) _status = SentinelCheckStatus.Done;
        }
        catch (Exception ex)
        {
            lock (_lock)
            {
                _status = SentinelCheckStatus.Error;
                _errorMessage = ex.Message;
            }
            _logger.LogError(ex, "Sentinel CSV check run failed");
        }
    }

    private (string Path, int Number) ComputeOutputPath()
    {
        int n = 1;
        string candidate;
        while (File.Exists(candidate = Path.Combine(OutputDir, $"checked-{n}.csv")))
            n++;

        return (candidate, n);
    }

    private static string BuildCsv(IReadOnlyList<SentinelCheckRow> rows)
    {
        var sb = new StringBuilder();
        sb.Append("id,question,student_answer,expected_label,result,comment\n");
        foreach (var r in rows)
        {
            sb.Append(r.Id).Append(',')
              .Append(CsvField(r.Question)).Append(',')
              .Append(CsvField(r.StudentAnswer)).Append(',')
              .Append(CsvField(r.ExpectedLabel)).Append(',')
              .Append(r.IsError ? "" : (r.AiIsCorrect == true ? "true" : "false")).Append(',')
              .Append(CsvField(r.AiComment))
              .Append('\n');
        }
        return sb.ToString();
    }

    private static string CsvField(string? value)
    {
        value ??= "";
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static List<(int Id, string Question, string StudentAnswer, string ExpectedLabel)> ParseInputCsv(string content)
    {
        var lines = SplitLines(content);
        var result = new List<(int, string, string, string)>();
        for (int i = 1; i < lines.Count; i++) // skip header
        {
            var fields = ParseCsvLine(lines[i]);
            if (fields.Count < 4) continue;
            if (!int.TryParse(fields[0], out var id)) continue;
            result.Add((id, fields[1], fields[2], fields[3]));
        }
        return result;
    }

    private static List<SentinelCheckRow> ParseRunFile(string path)
    {
        var lines = SplitLines(File.ReadAllText(path));
        var rows = new List<SentinelCheckRow>();
        for (int i = 1; i < lines.Count; i++) // skip header
        {
            var f = ParseCsvLine(lines[i]);
            if (f.Count < 6) continue;
            if (!int.TryParse(f[0], out var id)) continue;

            var isError = string.IsNullOrEmpty(f[4]);
            bool? isCorrect = isError ? null : f[4] == "true";
            rows.Add(new SentinelCheckRow(id, f[1], f[2], f[3], isCorrect, f[5], isError));
        }
        return rows;
    }

    private static List<string> SplitLines(string content) =>
        content.Replace("\r\n", "\n").Split('\n')
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else inQuotes = false;
                }
                else sb.Append(c);
            }
            else
            {
                if (c == '"') inQuotes = true;
                else if (c == ',') { fields.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(c);
            }
        }
        fields.Add(sb.ToString());
        return fields;
    }
}
