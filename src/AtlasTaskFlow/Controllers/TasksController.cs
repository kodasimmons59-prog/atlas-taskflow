using AtlasTaskFlow.Models;
using AtlasTaskFlow.Security;
using AtlasTaskFlow.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtlasTaskFlow.Controllers;

public class TasksController : AtlasController
{
    private readonly TaskWorkflowService _workflow;

    public TasksController(AtlasStore store, CurrentUserContext current, TaskWorkflowService workflow)
        : base(store, current) => _workflow = workflow;

    /// <summary>Task lists: my tasks, the Not Assigned pool (US-07), the review queue, or everything.</summary>
    public IActionResult Index(string? filter)
    {
        filter = filter is "pool" or "review" or "all" ? filter : "mine";
        var tasks = Store.GetTasksForTeam(Current.Team.TeamId).AsEnumerable();
        tasks = filter switch
        {
            "pool" => tasks.Where(t => t.Status == TaskItemStatus.NotAssigned),
            "review" => tasks.Where(t => t.Status == TaskItemStatus.NeedsReview),
            "all" => tasks,
            _ => tasks.Where(t => t.AssigneeIds.Contains(Current.User.UserId))
        };
        return View(new TaskListViewModel { Filter = filter, Tasks = ToCards(tasks.OrderBy(t => t.Status == TaskItemStatus.Completed).ThenBy(t => t.DueDate)) });
    }

    public IActionResult Details(int id)
    {
        var task = FindTask(id);
        if (task is null) return NotFound();

        var users = Store.GetUsers().ToDictionary(u => u.UserId);
        var attachments = Store.GetAttachments(id).Select(a => new AttachmentView(a, users.GetValueOrDefault(a.UploadedByUserId))).ToList();

        return View(new TaskDetailsViewModel
        {
            Task = task,
            Project = Store.GetProject(task.ProjectId)!,
            Creator = users.GetValueOrDefault(task.CreatedByUserId),
            Assignees = task.AssigneeIds.Where(users.ContainsKey).Select(uid => users[uid]).ToList(),
            Comments = Store.GetComments(id).Select(c => new CommentView(c, users.GetValueOrDefault(c.UserId))).ToList(),
            References = attachments.Where(a => a.Attachment.FileType == AttachmentType.Reference).ToList(),
            Deliverables = attachments.Where(a => a.Attachment.FileType == AttachmentType.FinalDeliverable).ToList(),
            CanManage = Current.CanManage,
            IsAssignee = task.AssigneeIds.Contains(Current.User.UserId)
        });
    }

    // Managers only (Team Leaders and delegated managers). The policy runs before the action, so a member who types
    // the address or posts the form directly gets the "not allowed" page; TaskWorkflowService checks again.
    [HttpGet]
    [Authorize(Policy = TeamPermissions.Manager)]
    public IActionResult Create(int? projectId)
    {

        var defaultProject = Store.GetProjects(Current.Team.TeamId).First(p => p.IsDefaultTasksProject);
        var model = new TaskFormViewModel
        {
            // US-05: new tasks land in the default "Tasks" project unless another is chosen.
            ProjectId = projectId ?? defaultProject.ProjectId,
            DueDate = DateTime.Today.AddDays(7)
        };
        return View("Form", FillOptions(model));
    }

    [HttpPost]
    [Authorize(Policy = TeamPermissions.Manager)]
    public IActionResult Create(TaskFormViewModel model)
    {

        ValidateDueDate(model, isNew: true);
        if (!ModelState.IsValid) return View("Form", FillOptions(model));

        var result = _workflow.CreateTask(Current.User, Current.Membership, model.ProjectId!.Value, model.Title,
            model.Description, model.DueDate!.Value, model.AssigneeIds, out var task);
        if (!result.Succeeded)
        {
            ModelState.AddModelError("", result.Error!);
            return View("Form", FillOptions(model));
        }

        FlashSuccess($"Created \"{task!.Title}\".");
        return RedirectToAction(nameof(Details), new { id = task.TaskId });
    }

    [HttpGet]
    [Authorize(Policy = TeamPermissions.Manager)]
    public IActionResult Edit(int id)
    {
        var task = FindTask(id);
        if (task is null) return NotFound();

        return View("Form", FillOptions(new TaskFormViewModel
        {
            TaskId = task.TaskId,
            Title = task.Title,
            Description = task.Description,
            DueDate = task.DueDate,
            ProjectId = task.ProjectId,
            AssigneeIds = task.AssigneeIds.ToList()
        }));
    }

    [HttpPost]
    [Authorize(Policy = TeamPermissions.Manager)]
    public IActionResult Edit(int id, TaskFormViewModel model)
    {
        if (FindTask(id) is null) return NotFound();

        model.TaskId = id;
        ValidateDueDate(model, isNew: false);
        if (!ModelState.IsValid) return View("Form", FillOptions(model));

        var result = _workflow.UpdateTask(Current.User, Current.Membership, id, model.ProjectId!.Value, model.Title,
            model.Description, model.DueDate!.Value, model.AssigneeIds);
        if (!result.Succeeded)
        {
            ModelState.AddModelError("", result.Error!);
            return View("Form", FillOptions(model));
        }

        FlashSuccess("Task updated.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    [Authorize(Policy = TeamPermissions.Manager)]
    public IActionResult Delete(int id)
    {
        var task = FindTask(id);
        return task is null ? NotFound() : View(ToCards(new[] { task }).Single());
    }

    [HttpPost, ActionName("Delete")]
    [Authorize(Policy = TeamPermissions.Manager)]
    public IActionResult DeleteConfirmed(int id)
    {
        var task = FindTask(id);
        var result = _workflow.DeleteTask(Current.User, Current.Membership, id);
        if (!result.Succeeded)
        {
            FlashError(result.Error!);
            return task is null ? RedirectToAction(nameof(Index)) : RedirectToAction(nameof(Details), new { id });
        }

        FlashSuccess($"Deleted \"{task!.Title}\".");
        return RedirectToAction("Details", "Projects", new { id = task.ProjectId });
    }

    [HttpPost]
    public IActionResult ChangeStatus(int id, TaskItemStatus status)
    {
        // An unknown value fails model binding and leaves status at its default (Not Assigned),
        // so check ModelState as well as the value itself.
        if (!ModelState.IsValid || !Enum.IsDefined(status))
        {
            FlashError("That isn't a valid status.");
            return RedirectToAction(nameof(Details), new { id });
        }

        var result = _workflow.ChangeStatus(Current.User, Current.Membership, id, status);
        Report(result, status == TaskItemStatus.Completed ? "Task approved and marked Completed." : $"Moved to {status.DisplayName()}.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public IActionResult PickUp(int id)
    {
        Report(_workflow.PickUp(Current.User, Current.Membership, id), "You picked up this task. It's now assigned to you.");
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>The "Submit Files" button (US-08).</summary>
    [HttpPost]
    [RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<IActionResult> Submit(int id, List<IFormFile> files, string? note)
    {
        var uploads = await ReadFiles(files);
        Report(_workflow.SubmitForReview(Current.User, Current.Membership, id, uploads, note),
            "Work submitted. Your team leader has been notified to review it.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [Authorize(Policy = TeamPermissions.Manager)]
    [RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<IActionResult> AddReference(int id, List<IFormFile> files)
    {
        var uploads = await ReadFiles(files);
        Report(_workflow.AddReferenceFiles(Current.User, Current.Membership, id, uploads), "Reference files added.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [Authorize(Policy = TeamPermissions.Manager)]
    public IActionResult RequestChanges(int id, string? feedback)
    {
        Report(_workflow.RequestChanges(Current.User, Current.Membership, id, feedback),
            "Sent back to In Progress with your feedback.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public IActionResult Comment(int id, string? message)
    {
        var result = _workflow.AddComment(Current.User, Current.Membership, id, message);
        if (!result.Succeeded) FlashError(result.Error!);
        return Redirect(Url.Action(nameof(Details), new { id }) + "#discussion");
    }

    public IActionResult Download(int id)
    {
        var attachment = Store.GetAttachment(id);
        if (attachment is null || FindTask(attachment.TaskId) is null) return NotFound();
        return File(attachment.Content, attachment.ContentType, attachment.FileName);
    }

    // ---------- Helpers ----------

    /// <summary>Finds a task only if it belongs to the current team (multi-team data isolation).</summary>
    private TaskItem? FindTask(int id) =>
        Store.GetTeamIdForTask(id) == Current.Team.TeamId ? Store.GetTask(id) : null;

    private TaskFormViewModel FillOptions(TaskFormViewModel model)
    {
        model.ProjectOptions = Store.GetProjects(Current.Team.TeamId);
        var memberIds = Store.GetTeamMembers(Current.Team.TeamId).Select(m => m.UserId).ToHashSet();
        model.MemberOptions = Store.GetUsers().Where(u => memberIds.Contains(u.UserId)).OrderBy(u => u.FullName).ToList();
        return model;
    }

    private void ValidateDueDate(TaskFormViewModel model, bool isNew)
    {
        // Existing tasks may already be overdue; only block choosing a past date for new tasks.
        if (isNew && model.DueDate is { } due && due.Date < DateTime.Today)
            ModelState.AddModelError(nameof(model.DueDate), "The due date can't be in the past.");
    }

    private void Report(WorkflowResult result, string successMessage)
    {
        if (result.Succeeded) FlashSuccess(successMessage);
        else FlashError(result.Error!);
    }

    private static async Task<List<UploadedFile>> ReadFiles(IEnumerable<IFormFile> files)
    {
        var uploads = new List<UploadedFile>();
        foreach (var file in files)
        {
            using var stream = file.OpenReadStream();
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            uploads.Add(new UploadedFile(file.FileName, file.ContentType, buffer.ToArray()));
        }
        return uploads;
    }
}
