using AtlasTaskFlow.Models;
using AtlasTaskFlow.Security;
using AtlasTaskFlow.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtlasTaskFlow.Controllers;

public class ProjectsController : AtlasController
{
    public ProjectsController(AtlasStore store, CurrentUserContext current) : base(store, current) { }

    public IActionResult Index()
    {
        var tasks = Store.GetTasksForTeam(Current.Team.TeamId);
        var summaries = Store.GetProjects(Current.Team.TeamId).Select(p =>
        {
            var own = tasks.Where(t => t.ProjectId == p.ProjectId).ToList();
            var done = own.Count(t => t.Status == TaskItemStatus.Completed);
            return new ProjectSummary(p, own.Count - done, done, own.Count);
        }).ToList();
        return View(summaries);
    }

    /// <summary>Status board for one project, one column per status.</summary>
    public IActionResult Details(int id)
    {
        var project = Store.GetProject(id);
        if (project is null || project.TeamId != Current.Team.TeamId) return NotFound();

        var cards = ToCards(Store.GetTasksForTeam(Current.Team.TeamId).Where(t => t.ProjectId == id));
        var columns = Enum.GetValues<TaskItemStatus>()
            .ToDictionary(s => s, s => (IReadOnlyList<TaskCard>)cards.Where(c => c.Task.Status == s).ToList());
        return View(new ProjectBoardViewModel { Project = project, Columns = columns });
    }

    [HttpGet]
    [Authorize(Policy = TeamPermissions.Manager)]
    public IActionResult Create() => View(new ProjectFormViewModel());

    [HttpPost]
    [Authorize(Policy = TeamPermissions.Manager)]
    public IActionResult Create(ProjectFormViewModel model)
    {

        var name = model.ProjectName?.Trim() ?? "";
        if (Store.GetProjects(Current.Team.TeamId).Any(p => string.Equals(p.ProjectName, name, StringComparison.OrdinalIgnoreCase)))
            ModelState.AddModelError(nameof(model.ProjectName), "Your team already has a project with that name.");
        if (!ModelState.IsValid) return View(model);

        var project = Store.AddProject(Current.Team.TeamId, name, string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim());
        Store.AddActivity(Current.Team.TeamId, null, $"{Current.User.FullName} created the {project.ProjectName} project.");
        FlashSuccess($"Created the {project.ProjectName} project.");
        return RedirectToAction(nameof(Details), new { id = project.ProjectId });
    }
}
