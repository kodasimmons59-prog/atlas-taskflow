using System.Net;
using AtlasTaskFlow.Data;
using AtlasTaskFlow.Models;
using AtlasTaskFlow.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AtlasTaskFlow.Tests;

/// <summary>Sprint 5: create, read, update, delete, validation, data errors and persistence across a restart.</summary>
public partial class AppIntegrationTests
{
    // ---------- Persistence ----------

    [Fact]
    public async Task Persistence_TaskSurvivesAppRestart()
    {
        var project = Store.GetProjects(BrightsideId).Single(p => p.ProjectName == "Website Refresh");
        await PostFormAsync("/Tasks/Create", new()
        {
            ["Title"] = "Persistence check: client logo files",
            ["Description"] = "Created before the restart.",
            ["DueDate"] = DateTime.Today.AddDays(10).ToString("yyyy-MM-dd"),
            ["ProjectId"] = project.ProjectId.ToString(),
            ["AssigneeIds"] = Marcus.UserId.ToString()
        }, "/Tasks/Create");

        // Stop the app completely, then start a brand-new instance on the same database.
        _scopes.ForEach(s => s.Dispose());
        _scopes.Clear();
        _factory.Dispose();
        _factory = AtlasFactory.Create(_database.ConnectionString);
        _client = NewClient();

        var html = await _client.GetStringAsync("/Tasks?filter=all");
        Assert.Contains("Persistence check: client logo files", html);

        var saved = Store.GetTasksForTeam(BrightsideId).Single(t => t.Title == "Persistence check: client logo files");
        Assert.Equal("Created before the restart.", saved.Description);
        Assert.Equal(project.ProjectId, saved.ProjectId);
        Assert.Equal(new[] { Marcus.UserId }, saved.AssigneeIds);
        Assert.Equal(TaskItemStatus.Assigned, saved.Status);

        // The restart must not add the sample data a second time.
        Assert.Equal(4, Store.GetUsers().Count);
    }

    // ---------- Read ----------

    [Fact]
    public async Task Read_TaskDetailsShowSavedCommentAndFile()
    {
        var task = SeededTask("Product photo retouching");
        var html = await _client.GetStringAsync($"/Tasks/Details/{task.TaskId}");

        Assert.Contains("All 12 shots are retouched and uploaded.", html);
        Assert.Contains("retouched-photos-notes.txt", html);

        var file = Store.GetAttachments(task.TaskId).Single();
        Assert.Empty(file.Content); // the list doesn't load file contents
        var download = await _client.GetStringAsync($"/Tasks/Download/{file.AttachmentId}");
        Assert.Equal("Sample deliverable: 12 retouched product photos.", download);
    }

    // ---------- Update ----------

    [Fact]
    public async Task Update_EditTask_SavesNewValues()
    {
        var task = SeededTask("Write launch email copy");
        var priya = Store.GetUsers().Single(u => u.FullName == "Priya Patel");
        var newDue = DateTime.Today.AddDays(6);

        var response = await PostFormAsync($"/Tasks/Edit/{task.TaskId}", new()
        {
            ["Title"] = "Write launch email copy (v2)",
            ["Description"] = "Three subject lines now.",
            ["DueDate"] = newDue.ToString("yyyy-MM-dd"),
            ["ProjectId"] = task.ProjectId.ToString(),
            ["AssigneeIds"] = Marcus.UserId.ToString()
        }, $"/Tasks/Edit/{task.TaskId}");

        Assert.Contains("Task updated.", await response.Content.ReadAsStringAsync());
        var saved = Store.GetTask(task.TaskId)!;
        Assert.Equal("Write launch email copy (v2)", saved.Title);
        Assert.Equal("Three subject lines now.", saved.Description);
        Assert.Equal(newDue, saved.DueDate);
        Assert.Equal(new[] { Marcus.UserId }, saved.AssigneeIds);
        Assert.DoesNotContain(priya.UserId, saved.AssigneeIds);
    }

    [Fact]
    public async Task Update_TitleTooLong_IsRejectedAndNothingChanges()
    {
        var task = SeededTask("Write launch email copy");
        var response = await PostFormAsync($"/Tasks/Edit/{task.TaskId}", new()
        {
            ["Title"] = new string('x', 101),
            ["DueDate"] = task.DueDate.ToString("yyyy-MM-dd"),
            ["ProjectId"] = task.ProjectId.ToString()
        }, $"/Tasks/Edit/{task.TaskId}");

        Assert.Contains("Keep the title under 100 characters.", await response.Content.ReadAsStringAsync());
        Assert.Equal("Write launch email copy", Store.GetTask(task.TaskId)!.Title);
    }

    // ---------- Delete ----------

    [Fact]
    public async Task Delete_RemovesTaskWithItsCommentsAndFiles_KeepsNotifications()
    {
        var task = SeededTask("Product photo retouching"); // has a comment, a file and a notification

        var response = await PostFormAsync($"/Tasks/Delete/{task.TaskId}", new(), $"/Tasks/Delete/{task.TaskId}");

        Assert.Contains("Deleted &quot;Product photo retouching&quot;.", await response.Content.ReadAsStringAsync());
        Assert.Null(Store.GetTask(task.TaskId));
        Assert.Empty(Store.GetComments(task.TaskId));
        Assert.Empty(Store.GetAttachments(task.TaskId));
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/Tasks/Details/{task.TaskId}")).StatusCode);

        // The leader's notification about it is kept, but no longer links to the deleted task.
        var sarah = Store.GetUsers().Single(u => u.FullName == "Sarah Chen");
        var note = Store.GetNotifications(sarah.UserId, BrightsideId).Single(n => n.Message.Contains("Product photo retouching"));
        Assert.Null(note.TaskId);
    }

    // ---------- Validation ----------

    [Fact]
    public async Task CreateProject_DuplicateName_ShowsValidationMessage()
    {
        var before = Store.GetProjects(BrightsideId).Count;
        var response = await PostFormAsync("/Projects/Create", new() { ["ProjectName"] = "website refresh" }, "/Projects/Create");

        Assert.Contains("Your team already has a project with that name.", await response.Content.ReadAsStringAsync());
        Assert.Equal(before, Store.GetProjects(BrightsideId).Count);
    }

    [Fact]
    public async Task CreateProject_IsSaved()
    {
        var response = await PostFormAsync("/Projects/Create", new()
        {
            ["ProjectName"] = "Trade Show Booth",
            ["Description"] = "Banners and handouts for the spring expo."
        }, "/Projects/Create");

        Assert.Contains("Created the Trade Show Booth project.", await response.Content.ReadAsStringAsync());
        Assert.Contains(Store.GetProjects(BrightsideId), p => p.ProjectName == "Trade Show Booth");
    }

    // ---------- Database rules and errors ----------

    [Fact]
    public void Database_RejectsDuplicateEmail()
    {
        var ex = Assert.Throws<DbUpdateException>(() => Store.AddUser("Second Sarah", "sarah@brightside.example"));
        Assert.True(DataErrors.IsConstraintViolation(ex));
    }

    [Fact]
    public void RejectedSave_BecomesFriendlyMessage_OnThePageYouCameFrom()
    {
        var ex = Assert.Throws<DbUpdateException>(() => Store.AddUser("Second Sarah", "sarah@brightside.example"));

        var http = new DefaultHttpContext { RequestServices = _factory.Services };
        http.Request.Method = "POST";
        http.Request.Host = new HostString("localhost");
        http.Request.Headers.Referer = "https://localhost/Projects/Create";
        var context = new ExceptionContext(new ActionContext(http, new RouteData(), new ActionDescriptor()), new List<IFilterMetadata>())
        {
            Exception = ex
        };
        var tempData = _factory.Services.GetRequiredService<ITempDataDictionaryFactory>();

        new DataErrorFilter(tempData, NullLogger<DataErrorFilter>.Instance).OnException(context);

        Assert.True(context.ExceptionHandled);
        Assert.Equal("/Projects/Create", Assert.IsType<LocalRedirectResult>(context.Result).Url);
        Assert.Equal(DataErrorFilter.SaveRejectedMessage, tempData.GetTempData(http)["Error"]);
    }

    [Fact]
    public async Task DatabaseUnavailable_ShowsFriendlyPage_WithoutTechnicalDetails()
    {
        // A copy of the app in Production mode whose database server doesn't exist.
        using var broken = AtlasFactory.Create(_database.ConnectionString, "Production").WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<AtlasDbContext>>();
                services.AddDbContext<AtlasDbContext>(o =>
                    o.UseSqlServer("Server=localhost,1;Database=Nowhere;User Id=sa;Password=wrong;Connect Timeout=2;TrustServerCertificate=True"));
            }));
        var client = broken.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        var response = await client.GetAsync("/Tasks");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("Atlas can't reach its database right now", html);
        Assert.DoesNotContain("SqlException", html);
        Assert.DoesNotContain("localhost,1", html);

        var health = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, health.StatusCode);
    }
}
