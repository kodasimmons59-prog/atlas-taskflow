using System.Net;
using System.Text.RegularExpressions;
using AtlasTaskFlow.Models;
using AtlasTaskFlow.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace AtlasTaskFlow.Tests;

/// <summary>
/// Runs the real app in memory and drives it over HTTP, the same way a browser would.
/// Each test gets its own new SQL Server database, which the app migrates and fills with sample data on startup.
/// </summary>
public partial class AppIntegrationTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly List<IServiceScope> _scopes = new();
    private WebApplicationFactory<Program> _factory;
    private HttpClient _client;

    public AppIntegrationTests()
    {
        _factory = AtlasFactory.Create(_database.ConnectionString);
        _client = NewClient();
    }

    public void Dispose()
    {
        _scopes.ForEach(s => s.Dispose());
        _factory.Dispose();
        _database.Dispose();
    }

    private HttpClient NewClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    /// <summary>A store with its own fresh DbContext, so assertions read what is really saved in the database.</summary>
    private AtlasStore Store
    {
        get
        {
            var scope = _factory.Services.CreateScope();
            _scopes.Add(scope);
            return scope.ServiceProvider.GetRequiredService<AtlasStore>();
        }
    }

    private TaskItemStatus SavedStatus(TaskItem task) => Store.GetTask(task.TaskId)!.Status;

    private User Marcus => Store.GetUsers().Single(u => u.FullName == "Marcus Vance");

    /// <summary>Sarah's team, which the app shows by default.</summary>
    private int BrightsideId => Store.GetTeamsForUser(Store.GetUsers().Single(u => u.FullName == "Sarah Chen").UserId).Single().TeamId;

    private TaskItem SeededTask(string title) =>
        Store.GetTeamsForUser(Marcus.UserId).SelectMany(t => Store.GetTasksForTeam(t.TeamId)).First(t => t.Title == title);

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenRegex();

    private async Task<string> GetTokenAsync(string url)
    {
        var html = await _client.GetStringAsync(url);
        return TokenRegex().Match(html).Groups[1].Value;
    }

    private async Task<HttpResponseMessage> PostFormAsync(string url, Dictionary<string, string> fields, string tokenFrom = "/")
    {
        fields["__RequestVerificationToken"] = await GetTokenAsync(tokenFrom);
        return await _client.PostAsync(url, new FormUrlEncodedContent(fields));
    }

    private Task<HttpResponseMessage> ViewAsAsync(string fullName)
    {
        var user = Store.GetUsers().Single(u => u.FullName == fullName);
        return PostFormAsync("/Demo/SwitchUser", new() { ["userId"] = user.UserId.ToString() });
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Tasks")]
    [InlineData("/Tasks?filter=pool")]
    [InlineData("/Tasks?filter=review")]
    [InlineData("/Tasks?filter=all")]
    [InlineData("/Tasks/Create")]
    [InlineData("/Projects")]
    [InlineData("/Projects/Create")]
    [InlineData("/Team")]
    [InlineData("/Notifications")]
    public async Task MainPages_Load(string url)
    {
        var response = await _client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Atlas TaskFlow", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Dashboard_ShowsCoreWidgets()
    {
        var html = await _client.GetStringAsync("/");
        Assert.Contains("Review queue", html);
        Assert.Contains("My tasks", html);
        Assert.Contains("Projects", html);
        Assert.Contains("Recent activity", html);
        Assert.Contains("Brightside Marketing", html);
    }

    [Fact]
    public async Task UnknownTask_ShowsFriendly404()
    {
        var response = await _client.GetAsync("/Tasks/Details/999999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("We couldn't find that page", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task CreateTask_WithValidInput_AppearsOnBoard()
    {
        var project = Store.GetProjects(BrightsideId).Single(p => p.IsDefaultTasksProject);
        var response = await PostFormAsync("/Tasks/Create", new()
        {
            ["Title"] = "Prepare client report",
            ["DueDate"] = DateTime.Today.AddDays(5).ToString("yyyy-MM-dd"),
            ["ProjectId"] = project.ProjectId.ToString(),
            ["AssigneeIds"] = Marcus.UserId.ToString()
        }, "/Tasks/Create");

        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Created &quot;Prepare client report&quot;.", html);
        Assert.Contains(Store.GetTasksForTeam(BrightsideId), t => t.Title == "Prepare client report" && t.Status == TaskItemStatus.Assigned);
    }

    [Fact]
    public async Task CreateTask_WithoutTitle_ShowsValidationMessage()
    {
        var before = Store.GetTasksForTeam(BrightsideId).Count;
        var response = await PostFormAsync("/Tasks/Create", new()
        {
            ["Title"] = "",
            ["DueDate"] = DateTime.Today.AddDays(5).ToString("yyyy-MM-dd"),
            ["ProjectId"] = Store.GetProjects(BrightsideId)[0].ProjectId.ToString()
        }, "/Tasks/Create");

        Assert.Contains("Give the task a title.", await response.Content.ReadAsStringAsync());
        Assert.Equal(before, Store.GetTasksForTeam(BrightsideId).Count);
    }

    [Fact]
    public async Task CreateTask_WithPastDueDate_IsRejected()
    {
        var response = await PostFormAsync("/Tasks/Create", new()
        {
            ["Title"] = "Back to the future",
            ["DueDate"] = DateTime.Today.AddDays(-2).ToString("yyyy-MM-dd"),
            ["ProjectId"] = Store.GetProjects(BrightsideId)[0].ProjectId.ToString()
        }, "/Tasks/Create");

        Assert.Contains("The due date can&#x27;t be in the past.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task CreateTask_WithGarbageDate_IsRejected()
    {
        var response = await PostFormAsync("/Tasks/Create", new()
        {
            ["Title"] = "Bad date",
            ["DueDate"] = "not-a-date",
            ["ProjectId"] = Store.GetProjects(BrightsideId)[0].ProjectId.ToString()
        }, "/Tasks/Create");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(Store.GetTasksForTeam(BrightsideId), t => t.Title == "Bad date");
    }

    [Fact]
    public async Task Post_WithoutAntiforgeryToken_IsRejected()
    {
        var response = await _client.PostAsync("/Tasks/Create", new FormUrlEncodedContent(new Dictionary<string, string> { ["Title"] = "x" }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Member_CannotComplete_SeesLeaderOnlyMessage()
    {
        var task = SeededTask("Product photo retouching");
        await ViewAsAsync("Marcus Vance");

        var response = await PostFormAsync($"/Tasks/ChangeStatus/{task.TaskId}", new() { ["status"] = "Completed" });

        Assert.Contains("Only Team Leaders can mark tasks as Completed.", await response.Content.ReadAsStringAsync());
        Assert.Equal(TaskItemStatus.NeedsReview, SavedStatus(task));
    }

    [Fact]
    public async Task Leader_ApprovesTaskInReview()
    {
        var task = SeededTask("Product photo retouching");

        var response = await PostFormAsync($"/Tasks/ChangeStatus/{task.TaskId}", new() { ["status"] = "Completed" });

        Assert.Contains("Task approved and marked Completed.", await response.Content.ReadAsStringAsync());
        Assert.Equal(TaskItemStatus.Completed, SavedStatus(task));
    }

    [Fact]
    public async Task Member_SubmitsFiles_TaskMovesToNeedsReview()
    {
        var task = SeededTask("Design spring promo banner"); // In Progress, assigned to Marcus
        await ViewAsAsync("Marcus Vance");

        var form = new MultipartFormDataContent
        {
            { new StringContent(await GetTokenAsync($"/Tasks/Details/{task.TaskId}")), "__RequestVerificationToken" },
            { new StringContent("Banners attached."), "note" },
            { new ByteArrayContent(new byte[] { 1, 2, 3 }), "files", "banner.png" }
        };
        var response = await _client.PostAsync($"/Tasks/Submit/{task.TaskId}", form);

        Assert.Contains("Work submitted.", await response.Content.ReadAsStringAsync());
        Assert.Equal(TaskItemStatus.NeedsReview, SavedStatus(task));
        Assert.Contains(Store.GetAttachments(task.TaskId), a => a.FileName == "banner.png");
    }

    [Fact]
    public async Task InvalidStatusValue_IsHandled()
    {
        var task = SeededTask("Product photo retouching");
        var response = await PostFormAsync($"/Tasks/ChangeStatus/{task.TaskId}", new() { ["status"] = "42" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("That isn&#x27;t a valid status.", await response.Content.ReadAsStringAsync());
        Assert.Equal(TaskItemStatus.NeedsReview, SavedStatus(task));
    }

    [Fact]
    public async Task SwitchingTeams_HidesOtherTeamsTasks()
    {
        var brightsideTask = SeededTask("Design spring promo banner");
        var bakery = Store.GetTeamsForUser(Marcus.UserId).Single(t => t.TeamName.StartsWith("Corner"));
        await ViewAsAsync("Marcus Vance");
        await PostFormAsync("/Demo/SwitchTeam", new() { ["teamId"] = bakery.TeamId.ToString() });

        var dashboard = await _client.GetStringAsync("/");
        Assert.Contains("Design holiday menu board", dashboard);
        Assert.DoesNotContain("Design spring promo banner", dashboard);

        var direct = await _client.GetAsync($"/Tasks/Details/{brightsideTask.TaskId}");
        Assert.Equal(HttpStatusCode.NotFound, direct.StatusCode);
    }

    [Fact]
    public async Task HealthEndpoint_ReportsHealthy()
    {
        Assert.Equal("Healthy", await _client.GetStringAsync("/health"));
    }

    [Fact]
    public async Task NotificationSummary_ReturnsUnreadCount()
    {
        var json = await _client.GetStringAsync("/Notifications/Summary");
        Assert.Equal("{\"unread\":1}", json);
    }
}
