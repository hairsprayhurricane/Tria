using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Data;
using Tria.Data;

namespace Tria.Pages.Admin;

[Authorize(Roles = "Admin")]
public class SqlConsoleModel : PageModel
{
    private readonly ApplicationDbContext _db;

    [BindProperty]
    public string? Query { get; set; }

    public List<string> Columns { get; set; } = new();
    public List<List<string?>> Rows { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public string? InfoMessage { get; set; }
    public bool HasResult { get; set; }
    public long ElapsedMs { get; set; }

    public SqlConsoleModel(ApplicationDbContext db) => _db = db;

    public void OnGet() { }

    public async Task OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Query)) return;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var conn = _db.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = Query.Trim();
            cmd.CommandTimeout = 30;

            using var reader = await cmd.ExecuteReaderAsync();

            if (reader.FieldCount > 0)
            {
                for (int i = 0; i < reader.FieldCount; i++)
                    Columns.Add(reader.GetName(i));

                while (await reader.ReadAsync())
                {
                    var row = new List<string?>();
                    for (int i = 0; i < reader.FieldCount; i++)
                        row.Add(reader.IsDBNull(i) ? null : reader.GetValue(i)?.ToString());
                    Rows.Add(row);
                }
                HasResult = true;
            }

            sw.Stop();
            ElapsedMs = sw.ElapsedMilliseconds;
            InfoMessage = HasResult
                ? $"Возвращено строк: {Rows.Count} ({ElapsedMs} мс)"
                : $"Запрос выполнен успешно ({ElapsedMs} мс)";
        }
        catch (Exception ex)
        {
            sw.Stop();
            ElapsedMs = sw.ElapsedMilliseconds;
            ErrorMessage = ex.Message;
        }
    }
}
