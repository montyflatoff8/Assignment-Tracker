namespace AssignmentTracker.Models;

public class Assignment
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Course { get; set; } = "";
    public DateOnly DueDate { get; set; }
    public string Status { get; set; } = "todo";   // todo, in-progress, done
}

/// <summary>
/// The body a client sends for POST and PUT. The client never chooses the Id;
/// the server assigns it on create and takes it from the route on update.
/// </summary>
public record AssignmentInput(string? Title, string? Course, DateOnly? DueDate, string? Status)
{
    public static readonly string[] AllowedStatuses = ["todo", "in-progress", "done"];

    /// <summary>Returns field errors keyed by property name; empty when the input is valid.</summary>
    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(Title))
            errors["title"] = ["Title is required."];
        else if (Title.Trim().Length > 100)
            errors["title"] = ["Title must be 100 characters or fewer."];

        if (string.IsNullOrWhiteSpace(Course))
            errors["course"] = ["Course is required."];
        else if (Course.Trim().Length > 60)
            errors["course"] = ["Course must be 60 characters or fewer."];

        if (DueDate is null)
            errors["dueDate"] = ["Due date is required (format YYYY-MM-DD)."];

        if (Status is not null && !AllowedStatuses.Contains(Status))
            errors["status"] = [$"Status must be one of: {string.Join(", ", AllowedStatuses)}."];

        return errors;
    }
}
