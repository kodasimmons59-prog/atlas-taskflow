using AtlasTaskFlow.Data;
using AtlasTaskFlow.Models;
using AtlasTaskFlow.Services;

namespace AtlasTaskFlow.Tests;

/// <summary>The task rules, run against a real (throwaway) SQL Server database.</summary>
public class TaskWorkflowServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly AtlasDbContext _db;
    private readonly AtlasStore _store;
    private readonly TaskWorkflowService _workflow;
    private readonly User _leader;
    private readonly User _member;
    private readonly User _otherMember;
    private readonly Team _team;
    private readonly Project _project;

    public TaskWorkflowServiceTests()
    {
        _db = _database.CreateMigratedContext();
        _store = new AtlasStore(_db);
        _workflow = new TaskWorkflowService(_store);
        _leader = _store.AddUser("Sarah Chen", "sarah@example.com");
        _member = _store.AddUser("Marcus Vance", "marcus@example.com");
        _otherMember = _store.AddUser("Priya Patel", "priya@example.com");
        _team = _store.CreateTeam("Brightside", _leader.UserId);
        _store.AddMember(_team.TeamId, _member.UserId, TeamRole.Member);
        _store.AddMember(_team.TeamId, _otherMember.UserId, TeamRole.Member);
        _project = _store.GetProjects(_team.TeamId).Single();
    }

    public void Dispose()
    {
        _db.Dispose();
        _database.Dispose();
    }

    private TeamMember M(User u) => _store.GetMembership(_team.TeamId, u.UserId)!;

    private TaskItem CreateTask(params User[] assignees)
    {
        var result = _workflow.CreateTask(_leader, M(_leader), _project.ProjectId, "Design banner", null,
            DateTime.Today.AddDays(3), assignees.Select(a => a.UserId), out var task);
        Assert.True(result.Succeeded, result.Error);
        return task!;
    }

    private static UploadedFile File(string name = "banner.png") => new(name, "image/png", new byte[] { 1, 2, 3 });

    [Fact]
    public void CreateTeam_AddsDefaultTasksProject()
    {
        Assert.True(_project.IsDefaultTasksProject);
        Assert.Equal("Tasks", _project.ProjectName);
    }

    [Fact]
    public void CreateTask_WithoutAssignees_IsNotAssigned()
    {
        Assert.Equal(TaskItemStatus.NotAssigned, CreateTask().Status);
    }

    [Fact]
    public void CreateTask_WithMultipleAssignees_IsAssigned_AndNotifiesEach()
    {
        var task = CreateTask(_member, _otherMember);

        Assert.Equal(TaskItemStatus.Assigned, task.Status);
        Assert.Equal(new[] { _member.UserId, _otherMember.UserId }, task.AssigneeIds);
        Assert.Equal(1, _store.GetUnreadCount(_member.UserId, _team.TeamId));
        Assert.Equal(1, _store.GetUnreadCount(_otherMember.UserId, _team.TeamId));
    }

    [Fact]
    public void CreateTask_ByMember_IsRejected()
    {
        var result = _workflow.CreateTask(_member, M(_member), _project.ProjectId, "x", null, DateTime.Today, Array.Empty<int>(), out _);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public void CreateTask_IgnoresAssigneesOutsideTheTeam()
    {
        var outsider = _store.AddUser("Out Sider", "out@example.com");
        var task = CreateTask(outsider);
        Assert.Empty(task.AssigneeIds);
        Assert.Equal(TaskItemStatus.NotAssigned, task.Status);
    }

    [Fact]
    public void Member_CannotMarkCompleted_GetsLeaderOnlyMessage()
    {
        var task = CreateTask(_member);
        _workflow.ChangeStatus(_member, M(_member), task.TaskId, TaskItemStatus.InProgress);
        _workflow.ChangeStatus(_member, M(_member), task.TaskId, TaskItemStatus.NeedsReview);

        var result = _workflow.ChangeStatus(_member, M(_member), task.TaskId, TaskItemStatus.Completed);

        Assert.False(result.Succeeded);
        Assert.Equal("Only Team Leaders can mark tasks as Completed.", result.Error);
        Assert.Equal(TaskItemStatus.NeedsReview, _store.GetTask(task.TaskId)!.Status);
    }

    [Fact]
    public void Leader_CanApprove_SetsCompletedTimestamp()
    {
        var task = CreateTask(_member);
        _workflow.ChangeStatus(_member, M(_member), task.TaskId, TaskItemStatus.InProgress);
        _workflow.ChangeStatus(_member, M(_member), task.TaskId, TaskItemStatus.NeedsReview);

        var result = _workflow.ChangeStatus(_leader, M(_leader), task.TaskId, TaskItemStatus.Completed);

        Assert.True(result.Succeeded);
        Assert.Equal(TaskItemStatus.Completed, task.Status);
        Assert.NotNull(task.CompletedAt);
    }

    [Fact]
    public void DelegatedManager_CanApprove()
    {
        _store.SetManagerPermissions(_team.TeamId, _otherMember.UserId, true);
        var task = CreateTask(_member);
        _workflow.ChangeStatus(_member, M(_member), task.TaskId, TaskItemStatus.InProgress);
        _workflow.ChangeStatus(_member, M(_member), task.TaskId, TaskItemStatus.NeedsReview);

        Assert.True(_workflow.ChangeStatus(_otherMember, M(_otherMember), task.TaskId, TaskItemStatus.Completed).Succeeded);
    }

    [Fact]
    public void Member_CannotUpdateSomeoneElsesTask()
    {
        var task = CreateTask(_otherMember);
        var result = _workflow.ChangeStatus(_member, M(_member), task.TaskId, TaskItemStatus.InProgress);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public void MovingToNeedsReview_NotifiesLeader()
    {
        var task = CreateTask(_member);
        _workflow.ChangeStatus(_member, M(_member), task.TaskId, TaskItemStatus.InProgress);
        _workflow.ChangeStatus(_member, M(_member), task.TaskId, TaskItemStatus.NeedsReview);

        var note = Assert.Single(_store.GetNotifications(_leader.UserId, _team.TeamId));
        Assert.Contains("submitted", note.Message);
    }

    [Fact]
    public void PickUp_AssignsTaskToMember()
    {
        var task = CreateTask();
        Assert.True(_workflow.PickUp(_member, M(_member), task.TaskId).Succeeded);
        Assert.Equal(TaskItemStatus.Assigned, task.Status);
        Assert.Equal(new[] { _member.UserId }, task.AssigneeIds);
    }

    [Fact]
    public void PickUp_AlreadyAssigned_Fails()
    {
        var task = CreateTask(_otherMember);
        Assert.False(_workflow.PickUp(_member, M(_member), task.TaskId).Succeeded);
    }

    [Fact]
    public void SubmitForReview_SavesFiles_MovesToNeedsReview()
    {
        var task = CreateTask(_member);
        _workflow.ChangeStatus(_member, M(_member), task.TaskId, TaskItemStatus.InProgress);

        var result = _workflow.SubmitForReview(_member, M(_member), task.TaskId, new[] { File() }, "Done!");

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(TaskItemStatus.NeedsReview, task.Status);
        var attachment = Assert.Single(_store.GetAttachments(task.TaskId));
        Assert.Equal(AttachmentType.FinalDeliverable, attachment.FileType);
        Assert.Single(_store.GetComments(task.TaskId));
        Assert.Equal(1, _store.GetUnreadCount(_leader.UserId, _team.TeamId));
    }

    [Fact]
    public void SubmitForReview_WithoutFiles_Fails()
    {
        var task = CreateTask(_member);
        _workflow.ChangeStatus(_member, M(_member), task.TaskId, TaskItemStatus.InProgress);

        var result = _workflow.SubmitForReview(_member, M(_member), task.TaskId, Array.Empty<UploadedFile>(), null);

        Assert.False(result.Succeeded);
        Assert.Equal(TaskItemStatus.InProgress, task.Status);
    }

    [Fact]
    public void SubmitForReview_TooLargeFile_Fails()
    {
        var task = CreateTask(_member);
        _workflow.ChangeStatus(_member, M(_member), task.TaskId, TaskItemStatus.InProgress);
        var big = new UploadedFile("huge.zip", "application/zip", new byte[TaskWorkflowService.MaxFileBytes + 1]);

        Assert.False(_workflow.SubmitForReview(_member, M(_member), task.TaskId, new[] { big }, null).Succeeded);
    }

    [Fact]
    public void RequestChanges_SendsBackToInProgress_WithComment()
    {
        var task = CreateTask(_member);
        _workflow.ChangeStatus(_member, M(_member), task.TaskId, TaskItemStatus.InProgress);
        _workflow.SubmitForReview(_member, M(_member), task.TaskId, new[] { File() }, null);

        var result = _workflow.RequestChanges(_leader, M(_leader), task.TaskId, "Please use the new logo.");

        Assert.True(result.Succeeded);
        Assert.Equal(TaskItemStatus.InProgress, task.Status);
        Assert.Contains(_store.GetComments(task.TaskId), c => c.MessageText == "Please use the new logo.");
    }

    [Fact]
    public void RequestChanges_WithoutFeedback_Fails()
    {
        var task = CreateTask(_member);
        _workflow.ChangeStatus(_member, M(_member), task.TaskId, TaskItemStatus.InProgress);
        _workflow.SubmitForReview(_member, M(_member), task.TaskId, new[] { File() }, null);

        Assert.False(_workflow.RequestChanges(_leader, M(_leader), task.TaskId, "  ").Succeeded);
    }

    [Fact]
    public void UpdateTask_RemovingAllAssignees_ReturnsToNotAssigned()
    {
        var task = CreateTask(_member);
        var result = _workflow.UpdateTask(_leader, M(_leader), task.TaskId, _project.ProjectId, task.Title, null, task.DueDate, Array.Empty<int>());

        Assert.True(result.Succeeded);
        Assert.Equal(TaskItemStatus.NotAssigned, task.Status);
    }

    [Fact]
    public void Tasks_FromAnotherTeam_AreNotReachable()
    {
        var task = CreateTask(_member);
        var outsiderLeader = _store.AddUser("Leo Martinez", "leo@example.com");
        var otherTeam = _store.CreateTeam("Corner Bakery", outsiderLeader.UserId);
        var outsiderMembership = _store.GetMembership(otherTeam.TeamId, outsiderLeader.UserId)!;

        Assert.False(_workflow.ChangeStatus(outsiderLeader, outsiderMembership, task.TaskId, TaskItemStatus.Completed).Succeeded);
        Assert.False(_workflow.DeleteTask(outsiderLeader, outsiderMembership, task.TaskId).Succeeded);
    }

    [Fact]
    public void AddComment_Empty_Fails()
    {
        var task = CreateTask(_member);
        Assert.False(_workflow.AddComment(_member, M(_member), task.TaskId, "").Succeeded);
    }
}
