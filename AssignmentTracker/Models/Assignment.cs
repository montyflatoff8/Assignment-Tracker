namespace AssignmentTracker.Models;

public class Assignment
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Course { get; set; } = "";
    public DateOnly DueDate { get; set; }
    public string Status { get; set; } = "todo";   // todo, in-progress, done
}