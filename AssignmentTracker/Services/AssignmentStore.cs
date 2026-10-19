using AssignmentTracker.Models;

namespace AssignmentTracker.Services;

public class AssignmentStore
{
    private readonly List<Assignment> _assignments = new()
    {
        new Assignment { Id = 1, Title = "Midterm project", Course = "Web Programming 1",
                         DueDate = new DateOnly(2026, 10, 25), Status = "in-progress" },
        new Assignment { Id = 2, Title = "Chapter 6 reading", Course = "Databases",
                         DueDate = new DateOnly(2026, 10, 22), Status = "todo" }
    };

    private int _nextId = 3;

    public List<Assignment> GetAll() => _assignments;

    public Assignment? GetById(int id) => _assignments.FirstOrDefault(a => a.Id == id);
}