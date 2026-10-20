using AssignmentTracker.Models;

namespace AssignmentTracker.Services;

/// <summary>
/// In-memory data store. Registered as a singleton, so the data lives as long as
/// the API process is running and resets to the seed data on restart.
/// A lock keeps it safe when several requests arrive at once.
/// </summary>
public class AssignmentStore
{
    private readonly object _lock = new();

    private readonly List<Assignment> _assignments = new()
    {
        new Assignment { Id = 1, Title = "Midterm project", Course = "Web Programming 1",
                         DueDate = new DateOnly(2026, 10, 25), Status = "in-progress" },
        new Assignment { Id = 2, Title = "Chapter 6 reading", Course = "Databases",
                         DueDate = new DateOnly(2026, 10, 22), Status = "todo" }
    };

    private int _nextId = 3;

    public List<Assignment> GetAll()
    {
        lock (_lock)
        {
            // Return copies so callers can't change the stored objects without going through the store.
            return _assignments.OrderBy(a => a.DueDate).ThenBy(a => a.Id).Select(Copy).ToList();
        }
    }

    public Assignment? GetById(int id)
    {
        lock (_lock)
        {
            var found = _assignments.FirstOrDefault(a => a.Id == id);
            return found is null ? null : Copy(found);
        }
    }

    public Assignment Add(AssignmentInput input)
    {
        lock (_lock)
        {
            var assignment = new Assignment
            {
                Id = _nextId++,
                Title = input.Title!.Trim(),
                Course = input.Course!.Trim(),
                DueDate = input.DueDate!.Value,
                Status = input.Status ?? "todo"
            };
            _assignments.Add(assignment);
            return Copy(assignment);
        }
    }

    /// <summary>Replaces an existing assignment's fields. Returns null if the id doesn't exist.</summary>
    public Assignment? Update(int id, AssignmentInput input)
    {
        lock (_lock)
        {
            var existing = _assignments.FirstOrDefault(a => a.Id == id);
            if (existing is null) return null;

            existing.Title = input.Title!.Trim();
            existing.Course = input.Course!.Trim();
            existing.DueDate = input.DueDate!.Value;
            existing.Status = input.Status ?? existing.Status;
            return Copy(existing);
        }
    }

    /// <summary>Removes an assignment. Returns false if the id doesn't exist.</summary>
    public bool Delete(int id)
    {
        lock (_lock)
        {
            return _assignments.RemoveAll(a => a.Id == id) > 0;
        }
    }

    private static Assignment Copy(Assignment a) => new()
    {
        Id = a.Id, Title = a.Title, Course = a.Course, DueDate = a.DueDate, Status = a.Status
    };
}
