using AtlasTaskFlow.Models;

namespace AtlasTaskFlow.Services;

/// <summary>
/// Sample teams, people and tasks based on the Sprint 3 personas, so the app can be demonstrated before real sign-up
/// exists. They are added once, when the database is empty; after that the database keeps whatever users change.
/// Due dates are relative to the day the sample data was added.
/// </summary>
public static class SeedData
{
    /// <summary>Adds the sample data if the database has no users yet. Returns true if it added anything.</summary>
    public static bool EnsureSeeded(AtlasStore store, DateTime today)
    {
        if (store.GetUsers().Count > 0) return false;
        Populate(store, today);
        return true;
    }

    public static void Populate(AtlasStore store, DateTime today)
    {
        var sarah = store.AddUser("Sarah Chen", "sarah@brightside.example");
        var marcus = store.AddUser("Marcus Vance", "marcus@brightside.example");
        var priya = store.AddUser("Priya Patel", "priya@brightside.example");
        var leo = store.AddUser("Leo Martinez", "leo@cornerbakery.example");

        // Team 1: Sarah's marketing agency.
        var brightside = store.CreateTeam("Brightside Marketing", sarah.UserId);
        store.AddMember(brightside.TeamId, marcus.UserId, TeamRole.Member);
        store.AddMember(brightside.TeamId, priya.UserId, TeamRole.Member);

        var general = store.GetProjects(brightside.TeamId).Single(p => p.IsDefaultTasksProject);
        var campaign = store.AddProject(brightside.TeamId, "Spring Campaign Launch", "Social, email and print assets for the spring promotion.");
        var website = store.AddProject(brightside.TeamId, "Website Refresh", "Update the agency site with new case studies.");

        Task(store, campaign, sarah, "Design spring promo banner", "Three sizes: 1200x628, 1080x1080 and 728x90. Use the new brand palette.",
            today.AddDays(2), TaskItemStatus.InProgress, marcus);
        Task(store, campaign, sarah, "Write launch email copy", "Subject line plus two body variants for A/B testing.",
            today.AddDays(4), TaskItemStatus.Assigned, priya);
        var review = Task(store, campaign, sarah, "Product photo retouching", "Clean up the 12 product shots from Tuesday's shoot.",
            today.AddDays(1), TaskItemStatus.NeedsReview, marcus);
        Task(store, campaign, sarah, "Book print shop for flyers", null,
            today.AddDays(9), TaskItemStatus.NotAssigned);
        Task(store, website, sarah, "Draft two new case studies", "Pick from the Q3 client list. 500 words each.",
            today.AddDays(12), TaskItemStatus.Assigned, priya, marcus);
        Task(store, website, sarah, "Fix broken contact form link", null,
            today.AddDays(-1), TaskItemStatus.InProgress, priya);
        var done = Task(store, general, sarah, "Order new office printer toner", null,
            today.AddDays(-3), TaskItemStatus.Completed, priya);
        store.UpdateTask(done.TaskId, t => t.CompletedAt = DateTime.UtcNow.AddDays(-3));
        Task(store, general, sarah, "Update team holiday calendar", null,
            today.AddDays(6), TaskItemStatus.NotAssigned);

        store.AddComment(review.TaskId, marcus.UserId, "All 12 shots are retouched and uploaded. Let me know if the colors look right.");
        store.AddAttachment(new Attachment
        {
            TaskId = review.TaskId,
            UploadedByUserId = marcus.UserId,
            FileName = "retouched-photos-notes.txt",
            ContentType = "text/plain",
            FileType = AttachmentType.FinalDeliverable,
            Content = System.Text.Encoding.UTF8.GetBytes("Sample deliverable: 12 retouched product photos.")
        });
        store.AddNotification(sarah.UserId, brightside.TeamId, review.TaskId, "Marcus Vance submitted \"Product photo retouching\" for review.");
        store.AddNotification(marcus.UserId, brightside.TeamId, null, "Sarah Chen assigned you \"Design spring promo banner\".");

        store.AddActivity(brightside.TeamId, done.TaskId, "Sarah Chen approved \"Order new office printer toner\".");
        store.AddActivity(brightside.TeamId, review.TaskId, "Marcus Vance moved \"Product photo retouching\" to Needs Review.");

        // Team 2: a second business Marcus freelances for, to demonstrate the team switcher (US-04).
        var bakery = store.CreateTeam("Corner Bakery Co.", leo.UserId);
        store.AddMember(bakery.TeamId, marcus.UserId, TeamRole.Member);
        var holiday = store.AddProject(bakery.TeamId, "Holiday Menu", "New seasonal menu boards and social posts.");
        Task(store, holiday, leo, "Design holiday menu board", "Chalkboard style, 24x36 in.",
            today.AddDays(5), TaskItemStatus.Assigned, marcus);
        Task(store, holiday, leo, "Photograph seasonal pastries", null,
            today.AddDays(8), TaskItemStatus.NotAssigned);
        store.AddActivity(bakery.TeamId, null, "Leo Martinez created the Holiday Menu project.");
        store.SaveChanges();
    }

    private static TaskItem Task(AtlasStore store, Project project, User createdBy, string title, string? description,
        DateTime due, TaskItemStatus status, params User[] assignees)
    {
        var task = new TaskItem
        {
            ProjectId = project.ProjectId,
            Title = title,
            Description = description,
            DueDate = due.Date,
            Status = status,
            CreatedByUserId = createdBy.UserId
        };
        task.SetAssignees(assignees.Select(a => a.UserId));
        return store.AddTask(task);
    }
}
