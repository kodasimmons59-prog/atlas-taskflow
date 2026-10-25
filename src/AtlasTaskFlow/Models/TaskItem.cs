namespace AtlasTaskFlow.Models;

/// <summary>
/// A unit of work. Named TaskItem rather than Task so it does not clash with System.Threading.Tasks.Task.
/// Stored in the Tasks table.
/// </summary>
public class TaskItem
{
    public int TaskId { get; set; }
    public int ProjectId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public TaskItemStatus Status { get; set; } = TaskItemStatus.NotAssigned;
    public DateTime DueDate { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    /// <summary>Rows in the TaskAssignees junction table: zero, one or many assignees (FR-2.3).</summary>
    public List<TaskAssignee> Assignees { get; set; } = new();

    /// <summary>The assigned users' ids, for the workflow rules and views.</summary>
    public IReadOnlyList<int> AssigneeIds => Assignees.Select(a => a.UserId).ToList();

    /// <summary>
    /// Makes the assignee rows match <paramref name="userIds"/>. Keeps rows that are still wanted and only adds or
    /// removes the difference, so Entity Framework doesn't try to insert a row that already exists.
    /// </summary>
    public void SetAssignees(IEnumerable<int> userIds)
    {
        var wanted = userIds.Distinct().ToList();
        Assignees.RemoveAll(a => !wanted.Contains(a.UserId));
        foreach (var userId in wanted.Where(id => Assignees.All(a => a.UserId != id)))
            Assignees.Add(new TaskAssignee { TaskId = TaskId, UserId = userId });
    }

    public bool IsOverdue(DateTime today) => Status != TaskItemStatus.Completed && DueDate.Date < today.Date;
}

/// <summary>Junction between a task and a user assigned to it. The key is (TaskId, UserId), so nobody is assigned twice.</summary>
public class TaskAssignee
{
    public int TaskId { get; set; }
    public int UserId { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}
