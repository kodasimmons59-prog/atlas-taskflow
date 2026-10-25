using System.Globalization;
using AtlasTaskFlow.Models;
using AtlasTaskFlow.Services;
using Microsoft.AspNetCore.Mvc;

namespace AtlasTaskFlow.Controllers;

public class HomeController : AtlasController
{
    public HomeController(AtlasStore store, CurrentUserContext current) : base(store, current) { }

    /// <summary>Dashboard Overview, the landing page (FR-4.1).</summary>
    public IActionResult Index(string? month)
    {
        var today = DateTime.Today;
        var teamTasks = Store.GetTasksForTeam(Current.Team.TeamId);
        var mine = teamTasks.Where(t => t.AssigneeIds.Contains(Current.User.UserId)).ToList();

        // Leaders see the whole team's schedule on the calendar; members see their own work.
        var calendarTasks = Current.CanManage ? teamTasks : mine;

        var shownMonth = DateTime.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var m)
            ? m
            : new DateTime(today.Year, today.Month, 1);

        var model = new DashboardViewModel
        {
            MyTasks = ToCards(mine.Where(t => t.Status != TaskItemStatus.Completed)),
            ReviewQueue = Current.CanManage
                ? ToCards(teamTasks.Where(t => t.Status == TaskItemStatus.NeedsReview))
                : Array.Empty<TaskCard>(),
            PoolCount = teamTasks.Count(t => t.Status == TaskItemStatus.NotAssigned),
            OverdueCount = calendarTasks.Count(t => t.IsOverdue(today)),
            Projects = Store.GetProjects(Current.Team.TeamId).Select(p =>
            {
                var tasks = teamTasks.Where(t => t.ProjectId == p.ProjectId).ToList();
                var done = tasks.Count(t => t.Status == TaskItemStatus.Completed);
                return new ProjectSummary(p, tasks.Count - done, done, tasks.Count);
            }).ToList(),
            Calendar = BuildCalendar(shownMonth, today, ToCards(calendarTasks)),
            RecentActivity = Store.GetRecentActivity(Current.Team.TeamId, 8)
        };
        return View(model);
    }

    /// <summary>The "My Tasks" panel on its own, polled by the dashboard so it updates without a refresh (FR-4.3).</summary>
    public IActionResult MyTasksPanel()
    {
        var mine = Store.GetTasksForTeam(Current.Team.TeamId)
            .Where(t => t.AssigneeIds.Contains(Current.User.UserId) && t.Status != TaskItemStatus.Completed);
        return PartialView("_MyTasks", ToCards(mine));
    }

    private static CalendarMonth BuildCalendar(DateTime month, DateTime today, IReadOnlyList<TaskCard> cards)
    {
        var first = new DateTime(month.Year, month.Month, 1);
        var start = first.AddDays(-(int)first.DayOfWeek); // weeks start on Sunday
        var byDate = cards.ToLookup(c => c.Task.DueDate.Date);
        var days = Enumerable.Range(0, 42)
            .Select(i => start.AddDays(i))
            .Select(d => new CalendarDay(d, d.Month == month.Month, d == today, byDate[d].ToList()))
            .ToList();

        // Drop a trailing week that belongs entirely to the next month.
        if (days.Skip(35).All(d => !d.InMonth)) days = days.Take(35).ToList();
        return new CalendarMonth { Month = first, Days = days };
    }

    [Route("Home/Status/{code:int}")]
    public IActionResult Status(int code)
    {
        Response.StatusCode = code;
        return View(code);
    }
}
