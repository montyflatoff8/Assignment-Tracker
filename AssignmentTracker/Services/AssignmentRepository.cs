using AssignmentTracker.Models;
using Npgsql;
using NpgsqlTypes;

namespace AssignmentTracker.Services;

/// <summary>
/// PostgreSQL data access for the assignments table.
/// Uses the shared NpgsqlDataSource (a connection pool registered in Program.cs), so every
/// command rents a pooled connection and returns it when disposed.
/// All values are sent as parameters ($1, $2...), never concatenated into the SQL.
/// </summary>
public class AssignmentRepository
{
    private const string Columns = "id, title, course, due_date, status";

    private readonly NpgsqlDataSource _dataSource;

    public AssignmentRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<List<Assignment>> GetAllAsync(CancellationToken ct = default)
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
    }

    public async Task<Assignment?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var cmd = _dataSource.CreateCommand(
            $"SELECT {Columns} FROM assignments WHERE id = $1;");
        cmd.Parameters.AddWithValue(id);

        return await ReadSingleAsync(cmd, ct);
    }

    public async Task<Assignment> AddAsync(AssignmentInput input, CancellationToken ct = default)
    {
        await using var cmd = _dataSource.CreateCommand(
            "INSERT INTO assignments (title, course, due_date, status) " +
            $"VALUES ($1, $2, $3, $4) RETURNING {Columns};");
        cmd.Parameters.AddWithValue(input.Title!.Trim());
        cmd.Parameters.AddWithValue(input.Course!.Trim());
        cmd.Parameters.AddWithValue(input.DueDate!.Value);
        cmd.Parameters.AddWithValue(input.Status ?? "todo");

        return (await ReadSingleAsync(cmd, ct))!;
    }

    /// <summary>Replaces an existing assignment's fields. Returns null if the id doesn't exist.</summary>
    public async Task<Assignment?> UpdateAsync(int id, AssignmentInput input, CancellationToken ct = default)
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
    }

    /// <summary>Removes an assignment. Returns false if the id doesn't exist.</summary>
    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var cmd = _dataSource.CreateCommand("DELETE FROM assignments WHERE id = $1;");
        cmd.Parameters.AddWithValue(id);

        return await cmd.ExecuteNonQueryAsync(ct) > 0;
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
