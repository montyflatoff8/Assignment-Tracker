using System.Diagnostics;
using AssignmentTracker.Models;
using Npgsql;
using NpgsqlTypes;

namespace AssignmentTracker.Services;

/// <summary>
/// PostgreSQL data access for the assignments table.
/// Uses the shared NpgsqlDataSource (a connection pool registered in Program.cs), so every
/// command rents a pooled connection and returns it when disposed.
/// All values are sent as parameters ($1, $2...), never concatenated into the SQL.
///
/// Every query goes through RunAsync, which writes structured log events:
///   Debug       - renting a pooled connection (diagnostics; off in production)
///   Information - successful changes (create / update / delete)
///   Warning     - slow queries (over SlowQueryThresholdMs)
///   Error       - database failures, with the exception and stack trace
/// Log messages use named placeholders ({DbHost}, {AssignmentId}...), never string
/// interpolation, and never include the connection string or password.
/// </summary>
public class AssignmentRepository
{
    private const string Columns = "id, title, course, due_date, status";
    private const long SlowQueryThresholdMs = 150;

    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<AssignmentRepository> _logger;
    private readonly string _dbHost;
    private readonly string _dbName;

    public AssignmentRepository(NpgsqlDataSource dataSource, ILogger<AssignmentRepository> logger)
    {
        _dataSource = dataSource;
        _logger = logger;

        // Only the host and database name are logged - never the full connection string.
        var settings = new NpgsqlConnectionStringBuilder(dataSource.ConnectionString);
        _dbHost = settings.Host ?? "unknown";
        _dbName = settings.Database ?? "unknown";
    }

    public Task<List<Assignment>> GetAllAsync(CancellationToken ct = default) =>
        RunAsync("GET /api/assignments", null, async () =>
        {
            await using var cmd = _dataSource.CreateCommand(
                $"SELECT {Columns} FROM assignments ORDER BY due_date, id;");

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            var results = new List<Assignment>();
            while (await reader.ReadAsync(ct))
            {
                results.Add(Map(reader));
            }
            return results;
        });

    public Task<Assignment?> GetByIdAsync(int id, CancellationToken ct = default) =>
        RunAsync("GET /api/assignments/{id}", null, async () =>
        {
            await using var cmd = _dataSource.CreateCommand(
                $"SELECT {Columns} FROM assignments WHERE id = $1;");
            cmd.Parameters.AddWithValue(id);

            return await ReadSingleAsync(cmd, ct);
        });

    public async Task<Assignment> AddAsync(AssignmentInput input, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var created = await RunAsync("POST /api/assignments", input.Title, async () =>
        {
            await using var cmd = _dataSource.CreateCommand(
                "INSERT INTO assignments (title, course, due_date, status) " +
                $"VALUES ($1, $2, $3, $4) RETURNING {Columns};");
            cmd.Parameters.AddWithValue(input.Title!.Trim());
            cmd.Parameters.AddWithValue(input.Course!.Trim());
            cmd.Parameters.AddWithValue(input.DueDate!.Value);
            cmd.Parameters.AddWithValue(input.Status ?? "todo");

            return (await ReadSingleAsync(cmd, ct))!;
        });

        _logger.LogInformation(
            "Successfully created assignment {AssignmentTitle} with generated ID {AssignmentId} in {ElapsedMs}ms",
            created.Title, created.Id, sw.ElapsedMilliseconds);
        return created;
    }

    /// <summary>Replaces an existing assignment's fields. Returns null if the id doesn't exist.</summary>
    public async Task<Assignment?> UpdateAsync(int id, AssignmentInput input, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var updated = await RunAsync("PUT /api/assignments/{id}", input.Title, async () =>
        {
            // COALESCE keeps the current status when the client leaves it out
            await using var cmd = _dataSource.CreateCommand(
                "UPDATE assignments SET title = $1, course = $2, due_date = $3, status = COALESCE($4, status) " +
                $"WHERE id = $5 RETURNING {Columns};");
            cmd.Parameters.AddWithValue(input.Title!.Trim());
            cmd.Parameters.AddWithValue(input.Course!.Trim());
            cmd.Parameters.AddWithValue(input.DueDate!.Value);
            cmd.Parameters.Add(new NpgsqlParameter
            {
                NpgsqlDbType = NpgsqlDbType.Varchar,
                Value = (object?)input.Status ?? DBNull.Value
            });
            cmd.Parameters.AddWithValue(id);

            return await ReadSingleAsync(cmd, ct);
        });

        if (updated is not null)
        {
            _logger.LogInformation(
                "Successfully updated assignment {AssignmentId} ({AssignmentTitle}) to status {Status} in {ElapsedMs}ms",
                updated.Id, updated.Title, updated.Status, sw.ElapsedMilliseconds);
        }
        return updated;
    }

    /// <summary>Removes an assignment. Returns false if the id doesn't exist.</summary>
    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var deleted = await RunAsync("DELETE /api/assignments/{id}", null, async () =>
        {
            await using var cmd = _dataSource.CreateCommand("DELETE FROM assignments WHERE id = $1;");
            cmd.Parameters.AddWithValue(id);

            return await cmd.ExecuteNonQueryAsync(ct) > 0;
        });

        if (deleted)
        {
            _logger.LogInformation("Successfully deleted assignment {AssignmentId} in {ElapsedMs}ms",
                id, sw.ElapsedMilliseconds);
        }
        return deleted;
    }

    /// <summary>
    /// Runs one database operation with Debug / Warning / Error telemetry around it.
    /// Database exceptions are logged here and re-thrown so the controller can return a 503.
    /// </summary>
    private async Task<T> RunAsync<T>(string endpoint, string? assignmentTitle, Func<Task<T>> operation)
    {
        _logger.LogDebug("Renting connection from pool for host {DbHost}. Querying database: {DbName} for {Endpoint}",
            _dbHost, _dbName, endpoint);

        var sw = Stopwatch.StartNew();
        try
        {
            var result = await operation();
            sw.Stop();

            if (sw.ElapsedMilliseconds > SlowQueryThresholdMs)
            {
                _logger.LogWarning("Slow query detected on {Endpoint}: execution took {ElapsedMs}ms (threshold {ThresholdMs}ms)",
                    endpoint, sw.ElapsedMilliseconds, SlowQueryThresholdMs);
            }

            return result;
        }
        catch (NpgsqlException ex)
        {
            sw.Stop();
            // PostgresException carries the server's SQLSTATE code (e.g. 28P01, 3D000); network faults have none.
            var sqlState = (ex as PostgresException)?.SqlState ?? "none";

            _logger.LogError(ex,
                "Database operation failed against {DbHost} on {Endpoint} for assignment {AssignmentTitle}: {ErrorMessage} (SqlState {SqlState}, after {ElapsedMs}ms)",
                _dbHost, endpoint, assignmentTitle ?? "(n/a)", ex.Message, sqlState, sw.ElapsedMilliseconds);
            throw;
        }
    }

    private static async Task<Assignment?> ReadSingleAsync(NpgsqlCommand cmd, CancellationToken ct)
    {
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Map(reader) : null;
    }

    private static Assignment Map(NpgsqlDataReader r) => new()
    {
        Id = r.GetInt32(0),
        Title = r.GetString(1),
        Course = r.GetString(2),
        DueDate = r.GetFieldValue<DateOnly>(3),
        Status = r.GetString(4)
    };
}
