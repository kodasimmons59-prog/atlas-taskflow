using AtlasTaskFlow.Data;
using AtlasTaskFlow.Models;
using Microsoft.EntityFrameworkCore;

namespace AtlasTaskFlow.Services;

/// <summary>
/// Reads and writes Atlas data through Entity Framework Core. One store (and one DbContext) is created per web
/// request.
///
/// Writes are staged and saved together by <see cref="SaveChanges"/>, so one user action (for example "submit work":
/// files, a comment, a status change and notifications) is saved in a single database transaction. The exceptions
/// are methods that create a record whose id the caller needs straight away; those save immediately.
/// </summary>
public class AtlasStore
{
    private readonly AtlasDbContext _db;

    public AtlasStore(AtlasDbContext db) => _db = db;

    /// <summary>Saves every staged change in one transaction.</summary>
    public void SaveChanges() => _db.SaveChanges();

    // ---------- Reads ----------

    public IReadOnlyList<User> GetUsers() => _db.Users.OrderBy(u => u.UserId).ToList();

    public User? GetUser(int userId) => _db.Users.Find(userId);

    public Team? GetTeam(int teamId) => _db.Teams.Find(teamId);

    public IReadOnlyList<Team> GetTeamsForUser(int userId) =>
        _db.Teams.Where(t => _db.TeamMembers.Any(m => m.TeamId == t.TeamId && m.UserId == userId))
            .OrderBy(t => t.TeamName).ToList();

    public TeamMember? GetMembership(int teamId, int userId) =>
        _db.TeamMembers.FirstOrDefault(m => m.TeamId == teamId && m.UserId == userId);

    public IReadOnlyList<TeamMember> GetTeamMembers(int teamId) =>
        _db.TeamMembers.Where(m => m.TeamId == teamId).OrderBy(m => m.TeamMemberId).ToList();

    public IReadOnlyList<Project> GetProjects(int teamId) =>
        _db.Projects.Where(p => p.TeamId == teamId)
            .OrderByDescending(p => p.IsDefaultTasksProject).ThenBy(p => p.ProjectName).ToList();

    public Project? GetProject(int projectId) => _db.Projects.Find(projectId);

    public IReadOnlyList<TaskItem> GetTasksForTeam(int teamId) =>
        TasksWithAssignees()
            .Where(t => _db.Projects.Any(p => p.ProjectId == t.ProjectId && p.TeamId == teamId))
            .OrderBy(t => t.DueDate).ThenBy(t => t.TaskId).ToList();

    public TaskItem? GetTask(int taskId) => TasksWithAssignees().FirstOrDefault(t => t.TaskId == taskId);

    /// <summary>Returns the team that owns a task, so callers can enforce team isolation.</summary>
    public int? GetTeamIdForTask(int taskId) =>
        _db.Tasks.Where(t => t.TaskId == taskId)
            .Join(_db.Projects, t => t.ProjectId, p => p.ProjectId, (t, p) => (int?)p.TeamId)
            .FirstOrDefault();

    public IReadOnlyList<Comment> GetComments(int taskId) =>
        _db.Comments.Where(c => c.TaskId == taskId).OrderBy(c => c.CreatedAt).ThenBy(c => c.CommentId).ToList();

    /// <summary>A task's files without their contents, which are only loaded for a download.</summary>
    public IReadOnlyList<Attachment> GetAttachments(int taskId) =>
        _db.Attachments.AsNoTracking().Where(a => a.TaskId == taskId).OrderBy(a => a.UploadedAt).ThenBy(a => a.AttachmentId)
            .Select(a => new Attachment
            {
                AttachmentId = a.AttachmentId,
                TaskId = a.TaskId,
                UploadedByUserId = a.UploadedByUserId,
                FileName = a.FileName,
                ContentType = a.ContentType,
                FileType = a.FileType,
                UploadedAt = a.UploadedAt
            })
            .ToList();

    public Attachment? GetAttachment(int attachmentId) => _db.Attachments.AsNoTracking().FirstOrDefault(a => a.AttachmentId == attachmentId);

    public IReadOnlyList<Notification> GetNotifications(int userId, int teamId) =>
        _db.Notifications.Where(n => n.UserId == userId && n.TeamId == teamId)
            .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.NotificationId).ToList();

    public int GetUnreadCount(int userId, int teamId) =>
        _db.Notifications.Count(n => n.UserId == userId && n.TeamId == teamId && !n.IsRead);

    public IReadOnlyList<ActivityEntry> GetRecentActivity(int teamId, int count) =>
        _db.ActivityEntries.Where(a => a.TeamId == teamId)
            .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.ActivityEntryId).Take(count).ToList();

    private IQueryable<TaskItem> TasksWithAssignees() => _db.Tasks.Include(t => t.Assignees);

    // ---------- Writes that save immediately (the caller needs the new id) ----------

    public User AddUser(string fullName, string email)
    {
        var user = new User { FullName = fullName, Email = email };
        _db.Users.Add(user);
        _db.SaveChanges();
        return user;
    }

    /// <summary>Creates a team, makes the creator its leader, and adds the default "Tasks" project (FR-2.1).</summary>
    public Team CreateTeam(string teamName, int createdByUserId)
    {
        var team = new Team { TeamName = teamName, CreatedByUserId = createdByUserId };
        _db.Teams.Add(team);
        _db.SaveChanges();

        _db.TeamMembers.Add(new TeamMember { TeamId = team.TeamId, UserId = createdByUserId, Role = TeamRole.Leader });
        _db.Projects.Add(new Project
        {
            TeamId = team.TeamId,
            ProjectName = "Tasks",
            Description = "General tasks that don't belong to a specific project.",
            IsDefaultTasksProject = true
        });
        _db.SaveChanges();
        return team;
    }

    public TeamMember AddMember(int teamId, int userId, TeamRole role)
    {
        var member = new TeamMember { TeamId = teamId, UserId = userId, Role = role };
        _db.TeamMembers.Add(member);
        _db.SaveChanges();
        return member;
    }

    public void SetManagerPermissions(int teamId, int userId, bool value)
    {
        var member = GetMembership(teamId, userId);
        if (member is null) return;
        member.HasManagerPermissions = value;
        _db.SaveChanges();
    }

    public Project AddProject(int teamId, string name, string? description)
    {
        var project = new Project { TeamId = teamId, ProjectName = name, Description = description };
        _db.Projects.Add(project);
        _db.SaveChanges();
        return project;
    }

    public TaskItem AddTask(TaskItem task)
    {
        _db.Tasks.Add(task);
        _db.SaveChanges();
        return task;
    }

    public void MarkNotificationRead(int notificationId, int userId)
    {
        var n = _db.Notifications.FirstOrDefault(x => x.NotificationId == notificationId && x.UserId == userId);
        if (n is null) return;
        n.IsRead = true;
        _db.SaveChanges();
    }

    public void MarkAllNotificationsRead(int userId, int teamId)
    {
        foreach (var n in _db.Notifications.Where(n => n.UserId == userId && n.TeamId == teamId && !n.IsRead))
            n.IsRead = true;
        _db.SaveChanges();
    }

    // ---------- Staged writes (saved by SaveChanges) ----------

    /// <summary>Applies a change to a task. Saved with the next <see cref="SaveChanges"/>.</summary>
    public void UpdateTask(int taskId, Action<TaskItem> change)
    {
        var task = GetTask(taskId) ?? throw new InvalidOperationException($"Task {taskId} was not found.");
        change(task);
    }

    /// <summary>
    /// Removes a task. The database removes its assignees, comments and files (cascade); notifications and activity
    /// lines are kept but no longer point at the task.
    /// </summary>
    public void DeleteTask(int taskId)
    {
        var task = GetTask(taskId);
        if (task is null) return;

        foreach (var n in _db.Notifications.Where(n => n.TaskId == taskId)) n.TaskId = null;
        foreach (var a in _db.ActivityEntries.Where(a => a.TaskId == taskId)) a.TaskId = null;
        _db.Tasks.Remove(task);
    }

    public Comment AddComment(int taskId, int userId, string text)
    {
        var comment = new Comment { TaskId = taskId, UserId = userId, MessageText = text };
        _db.Comments.Add(comment);
        return comment;
    }

    public Attachment AddAttachment(Attachment attachment)
    {
        _db.Attachments.Add(attachment);
        return attachment;
    }

    public void AddNotification(int userId, int teamId, int? taskId, string message) =>
        _db.Notifications.Add(new Notification { UserId = userId, TeamId = teamId, TaskId = taskId, Message = message });

    public void AddActivity(int teamId, int? taskId, string message) =>
        _db.ActivityEntries.Add(new ActivityEntry { TeamId = teamId, TaskId = taskId, Message = message });
}
