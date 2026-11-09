using AtlasTaskFlow.Models;

namespace AtlasTaskFlow.Services;

public record WorkflowResult(bool Succeeded, string? Error = null)
{
    public static WorkflowResult Ok() => new(true);
    public static WorkflowResult Fail(string error) => new(false, error);
}

public record UploadedFile(string FileName, string ContentType, byte[] Content);

/// <summary>
/// The task rules from the Sprint 3 design: who may create and change tasks, the five-status workflow,
/// the leader-only completion gate, and the notifications each step sends.
/// Each public method is one user action and ends with a single save, so its changes are stored together or not at all.
/// </summary>
public class TaskWorkflowService
{
    public const string LeaderOnlyCompleteMessage = "Only Team Leaders can mark tasks as Completed.";
    public const long MaxFileBytes = 5 * 1024 * 1024;
    public const int MaxFilesPerUpload = 5;

    private readonly AtlasStore _store;

    public TaskWorkflowService(AtlasStore store) => _store = store;

    /// <summary>
    /// Decides whether <paramref name="actor"/> may move <paramref name="task"/> to <paramref name="newStatus"/>.
    /// Returns null when allowed, or the message to show the user.
    /// </summary>
    public static string? CheckStatusChange(TeamMember actor, TaskItem task, TaskItemStatus newStatus)
    {
        if (task.Status == newStatus)
            return $"This task is already {newStatus.DisplayName()}.";

        if (!actor.CanManage)
        {
            // FR-3.3: the completion gate.
            if (newStatus == TaskItemStatus.Completed)
                return LeaderOnlyCompleteMessage;
            if (task.Status == TaskItemStatus.Completed)
                return "This task is completed. Only Team Leaders can reopen it.";
            if (newStatus == TaskItemStatus.NotAssigned)
                return "Only Team Leaders can unassign a task.";
            if (!task.AssigneeIds.Contains(actor.UserId))
                return "You can only update tasks assigned to you. Pick it up from the Not Assigned pool first.";
        }

        if (newStatus != TaskItemStatus.NotAssigned && task.AssigneeIds.Count == 0)
            return $"Assign someone to this task before moving it to {newStatus.DisplayName()}.";

        return null;
    }

    public WorkflowResult CreateTask(User actor, TeamMember membership, int projectId, string title, string? description,
        DateTime dueDate, IEnumerable<int> assigneeIds, out TaskItem? created)
    {
        created = null;
        if (!membership.CanManage)
            return WorkflowResult.Fail("Only Team Leaders can create tasks.");

        var project = _store.GetProject(projectId);
        if (project is null || project.TeamId != membership.TeamId)
            return WorkflowResult.Fail("Choose a project from your team.");

        var assignees = ValidAssignees(membership.TeamId, assigneeIds);
        var task = new TaskItem
        {
            ProjectId = projectId,
            Title = title.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            DueDate = dueDate.Date,
            CreatedByUserId = actor.UserId,
            Status = assignees.Count > 0 ? TaskItemStatus.Assigned : TaskItemStatus.NotAssigned
        };
        task.SetAssignees(assignees);
        created = _store.AddTask(task);

        _store.AddActivity(membership.TeamId, created.TaskId, $"{actor.FullName} created \"{created.Title}\".");
        NotifyAssigned(actor, membership.TeamId, created, assignees);
        _store.SaveChanges();
        return WorkflowResult.Ok();
    }

    public WorkflowResult UpdateTask(User actor, TeamMember membership, int taskId, int projectId, string title,
        string? description, DateTime dueDate, IEnumerable<int> assigneeIds)
    {
        if (!membership.CanManage)
            return WorkflowResult.Fail("Only Team Leaders can edit tasks.");

        var task = FindInTeam(taskId, membership.TeamId);
        if (task is null)
            return WorkflowResult.Fail("That task doesn't exist in this team.");

        var project = _store.GetProject(projectId);
        if (project is null || project.TeamId != membership.TeamId)
            return WorkflowResult.Fail("Choose a project from your team.");

        var assignees = ValidAssignees(membership.TeamId, assigneeIds);
        var newlyAssigned = assignees.Except(task.AssigneeIds).ToList();

        _store.UpdateTask(taskId, t =>
        {
            t.ProjectId = projectId;
            t.Title = title.Trim();
            t.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
            t.DueDate = dueDate.Date;
            t.SetAssignees(assignees);

            // Keep the status consistent with who is assigned.
            if (assignees.Count == 0 && t.Status != TaskItemStatus.Completed)
                t.Status = TaskItemStatus.NotAssigned;
            else if (assignees.Count > 0 && t.Status == TaskItemStatus.NotAssigned)
                t.Status = TaskItemStatus.Assigned;
        });

        _store.AddActivity(membership.TeamId, taskId, $"{actor.FullName} edited \"{task.Title}\".");
        NotifyAssigned(actor, membership.TeamId, task, newlyAssigned);
        _store.SaveChanges();
        return WorkflowResult.Ok();
    }

    public WorkflowResult DeleteTask(User actor, TeamMember membership, int taskId)
    {
        if (!membership.CanManage)
            return WorkflowResult.Fail("Only Team Leaders can delete tasks.");

        var task = FindInTeam(taskId, membership.TeamId);
        if (task is null)
            return WorkflowResult.Fail("That task doesn't exist in this team.");

        _store.DeleteTask(taskId);
        _store.AddActivity(membership.TeamId, null, $"{actor.FullName} deleted \"{task.Title}\".");
        _store.SaveChanges();
        return WorkflowResult.Ok();
    }

    public WorkflowResult ChangeStatus(User actor, TeamMember membership, int taskId, TaskItemStatus newStatus)
    {
        var task = FindInTeam(taskId, membership.TeamId);
        if (task is null)
            return WorkflowResult.Fail("That task doesn't exist in this team.");

        var error = CheckStatusChange(membership, task, newStatus);
        if (error is not null)
            return WorkflowResult.Fail(error);

        ApplyStatus(actor, membership.TeamId, task, newStatus);
        _store.SaveChanges();
        return WorkflowResult.Ok();
    }

    /// <summary>US-07: a member takes an unassigned task from the pool.</summary>
    public WorkflowResult PickUp(User actor, TeamMember membership, int taskId)
    {
        var task = FindInTeam(taskId, membership.TeamId);
        if (task is null)
            return WorkflowResult.Fail("That task doesn't exist in this team.");
        if (task.Status != TaskItemStatus.NotAssigned)
            return WorkflowResult.Fail("Someone has already picked up this task.");

        _store.UpdateTask(taskId, t =>
        {
            t.SetAssignees(new[] { actor.UserId });
            t.Status = TaskItemStatus.Assigned;
        });
        _store.AddActivity(membership.TeamId, taskId, $"{actor.FullName} picked up \"{task.Title}\".");
        _store.SaveChanges();
        return WorkflowResult.Ok();
    }

    /// <summary>US-08: an assignee uploads deliverables, which moves the task to Needs Review.</summary>
    public WorkflowResult SubmitForReview(User actor, TeamMember membership, int taskId, IReadOnlyList<UploadedFile> files, string? note)
    {
        var task = FindInTeam(taskId, membership.TeamId);
        if (task is null)
            return WorkflowResult.Fail("That task doesn't exist in this team.");
        if (!task.AssigneeIds.Contains(actor.UserId))
            return WorkflowResult.Fail("Only people assigned to this task can submit work for it.");
        if (task.Status != TaskItemStatus.InProgress)
            return WorkflowResult.Fail("Move the task to In Progress before submitting files.");

        var fileError = ValidateFiles(files, required: true);
        if (fileError is not null)
            return WorkflowResult.Fail(fileError);

        foreach (var file in files)
            AddFile(actor, taskId, file, AttachmentType.FinalDeliverable);

        if (!string.IsNullOrWhiteSpace(note))
            _store.AddComment(taskId, actor.UserId, note.Trim());

        ApplyStatus(actor, membership.TeamId, task, TaskItemStatus.NeedsReview);
        _store.SaveChanges();
        return WorkflowResult.Ok();
    }

    /// <summary>Leaders attach reference files to a task.</summary>
    public WorkflowResult AddReferenceFiles(User actor, TeamMember membership, int taskId, IReadOnlyList<UploadedFile> files)
    {
        if (!membership.CanManage)
            return WorkflowResult.Fail("Only Team Leaders can add reference files.");
        if (FindInTeam(taskId, membership.TeamId) is null)
            return WorkflowResult.Fail("That task doesn't exist in this team.");

        var fileError = ValidateFiles(files, required: false);
        if (fileError is not null)
            return WorkflowResult.Fail(fileError);

        foreach (var file in files)
            AddFile(actor, taskId, file, AttachmentType.Reference);
        _store.SaveChanges();
        return WorkflowResult.Ok();
    }

    /// <summary>Journey 2 recovery path: the leader explains what to fix and sends the task back to In Progress.</summary>
    public WorkflowResult RequestChanges(User actor, TeamMember membership, int taskId, string? feedback)
    {
        if (!membership.CanManage)
            return WorkflowResult.Fail("Only Team Leaders can request changes.");

        var task = FindInTeam(taskId, membership.TeamId);
        if (task is null)
            return WorkflowResult.Fail("That task doesn't exist in this team.");
        if (task.Status != TaskItemStatus.NeedsReview)
            return WorkflowResult.Fail("Only tasks in Needs Review can be sent back.");
        if (string.IsNullOrWhiteSpace(feedback))
            return WorkflowResult.Fail("Tell the assignee what needs to change.");

        _store.AddComment(taskId, actor.UserId, feedback.Trim());
        ApplyStatus(actor, membership.TeamId, task, TaskItemStatus.InProgress);
        foreach (var userId in task.AssigneeIds.Where(id => id != actor.UserId))
            _store.AddNotification(userId, membership.TeamId, taskId, $"{actor.FullName} requested changes on \"{task.Title}\".");
        _store.SaveChanges();
        return WorkflowResult.Ok();
    }

    public WorkflowResult AddComment(User actor, TeamMember membership, int taskId, string? text)
    {
        if (FindInTeam(taskId, membership.TeamId) is null)
            return WorkflowResult.Fail("That task doesn't exist in this team.");
        if (string.IsNullOrWhiteSpace(text))
            return WorkflowResult.Fail("Write a message before posting.");
        if (text.Trim().Length > 1000)
            return WorkflowResult.Fail("Comments can be at most 1000 characters.");

        _store.AddComment(taskId, actor.UserId, text.Trim());
        _store.SaveChanges();
        return WorkflowResult.Ok();
    }

    // ---------- Helpers ----------

    private TaskItem? FindInTeam(int taskId, int teamId) =>
        _store.GetTeamIdForTask(taskId) == teamId ? _store.GetTask(taskId) : null;

    private List<int> ValidAssignees(int teamId, IEnumerable<int> assigneeIds)
    {
        var memberIds = _store.GetTeamMembers(teamId).Select(m => m.UserId).ToHashSet();
        return assigneeIds.Distinct().Where(memberIds.Contains).ToList();
    }

    private void ApplyStatus(User actor, int teamId, TaskItem task, TaskItemStatus newStatus)
    {
        _store.UpdateTask(task.TaskId, t =>
        {
            t.Status = newStatus;
            t.CompletedAt = newStatus == TaskItemStatus.Completed ? DateTime.UtcNow : null;
            if (newStatus == TaskItemStatus.NotAssigned)
                t.SetAssignees(Array.Empty<int>());
        });

        var verb = newStatus == TaskItemStatus.Completed ? "approved" : $"moved to {newStatus.DisplayName()}";
        _store.AddActivity(teamId, task.TaskId, newStatus == TaskItemStatus.Completed
            ? $"{actor.FullName} approved \"{task.Title}\"."
            : $"{actor.FullName} moved \"{task.Title}\" to {newStatus.DisplayName()}.");

        // FR-4.2: managers hear about work waiting for review.
        if (newStatus == TaskItemStatus.NeedsReview)
        {
            foreach (var manager in _store.GetTeamMembers(teamId).Where(m => m.CanManage && m.UserId != actor.UserId))
                _store.AddNotification(manager.UserId, teamId, task.TaskId, $"{actor.FullName} submitted \"{task.Title}\" for review.");
        }
        else if (newStatus == TaskItemStatus.Completed)
        {
            foreach (var userId in task.AssigneeIds.Where(id => id != actor.UserId))
                _store.AddNotification(userId, teamId, task.TaskId, $"{actor.FullName} {verb} \"{task.Title}\".");
        }
    }

    private void NotifyAssigned(User actor, int teamId, TaskItem task, IEnumerable<int> userIds)
    {
        foreach (var userId in userIds.Where(id => id != actor.UserId))
            _store.AddNotification(userId, teamId, task.TaskId, $"{actor.FullName} assigned you \"{task.Title}\".");
    }

    private static string? ValidateFiles(IReadOnlyList<UploadedFile> files, bool required)
    {
        if (required && files.Count == 0)
            return "Attach at least one file to submit your work.";
        if (files.Count > MaxFilesPerUpload)
            return $"You can upload at most {MaxFilesPerUpload} files at a time.";
        if (files.Any(f => f.Content.Length == 0))
            return "One of the files is empty.";
        if (files.Any(f => f.Content.Length > MaxFileBytes))
            return "Each file must be 5 MB or smaller.";
        return null;
    }

    private void AddFile(User actor, int taskId, UploadedFile file, AttachmentType type)
    {
        _store.AddAttachment(new Attachment
        {
            TaskId = taskId,
            UploadedByUserId = actor.UserId,
            FileName = Path.GetFileName(file.FileName),
            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            FileType = type,
            Content = file.Content
        });
    }
}
