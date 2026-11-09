using System.Net;
using AtlasTaskFlow.Models;
using AtlasTaskFlow.Security;
using AtlasTaskFlow.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AtlasTaskFlow.Tests;

/// <summary>
/// Sprint 6: authentication, authorization, roles and data isolation, tested from both sides: what each person may
/// do, and what is refused when they try something they may not, including typing addresses or posting forms directly.
/// Test names match the security test matrix in docs/SECURITY.md.
/// </summary>
public partial class AppIntegrationTests
{
    private HttpClient Anonymous() => NewClient();

    private async Task<HttpResponseMessage> PostAsync(HttpClient client, string url, Dictionary<string, string> fields, string tokenFrom)
    {
        var html = await client.GetStringAsync(tokenFrom);
        fields["__RequestVerificationToken"] = TokenRegex().Match(html).Groups[1].Value;
        return await client.PostAsync(url, new FormUrlEncodedContent(fields));
    }

    private static string PathOf(HttpResponseMessage response) => response.RequestMessage!.RequestUri!.AbsolutePath;

    private OutgoingEmail LatestEmailTo(string email) =>
        ((LogEmailSender)_factory.Services.GetRequiredService<IEmailSender>()).Recent.First(m => m.To == email);

    private async Task<HttpResponseMessage> RegisterAsync(string name, string email, string password = "NewPerson2026")
    {
        _client = NewClient();
        return await PostFormAsync("/Account/Register", new()
        {
            ["FullName"] = name, ["Email"] = email, ["Password"] = password, ["ConfirmPassword"] = password
        }, "/Account/Register");
    }

    private async Task ConfirmLatestEmailAsync(string email)
    {
        var link = new Uri(LatestEmailTo(email).Link!);
        var html = await _client.GetStringAsync(link.PathAndQuery);
        Assert.Contains("Email confirmed", html);
    }

    // ---------- 1. Authentication ----------

    [Theory]
    [InlineData("/")]
    [InlineData("/Tasks")]
    [InlineData("/Tasks/Details/1")]
    [InlineData("/Tasks/Create")]
    [InlineData("/Projects")]
    [InlineData("/Team")]
    [InlineData("/Notifications")]
    [InlineData("/Notifications/Summary")]
    [InlineData("/Tasks/Download/1")]
    [InlineData("/Workspace")]
    public async Task S01_SignedOut_ProtectedPage_RedirectsToSignIn(string url)
    {
        _client = NewClient();
        var response = await NoRedirectClient().GetAsync(url);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/SignIn", response.Headers.Location!.AbsolutePath);
        Assert.Contains("ReturnUrl=", response.Headers.Location.Query);
    }

    [Fact]
    public async Task S02_SignedOut_PostedAction_IsRefused_AndNothingChanges()
    {
        var task = SeededTask("Product photo retouching");
        var anonymous = Anonymous();

        var response = await PostAsync(anonymous, $"/Tasks/ChangeStatus/{task.TaskId}", new() { ["status"] = "Completed" }, "/Account/SignIn");

        Assert.Equal("/Account/SignIn", PathOf(response));
        Assert.Equal(TaskItemStatus.NeedsReview, SavedStatus(task));
    }

    [Theory]
    [InlineData("/Account/SignIn")]
    [InlineData("/Account/Register")]
    [InlineData("/Account/ForgotPassword")]
    [InlineData("/health")]
    public async Task S03_PublicPages_OpenWithoutSigningIn(string url)
    {
        var response = await Anonymous().GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task S04_SignIn_WithCorrectPassword_ShowsOwnDashboard()
    {
        var response = await SignInAsAsync(MarcusEmail);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal("/", PathOf(response));
        Assert.Contains("Marcus Vance", html);
        Assert.Contains("Team Member", html);
    }

    [Theory]
    [InlineData(SarahEmail, "WrongPassword1")]
    [InlineData("nobody@nowhere.example", "AtlasDemo2026")]
    public async Task S05_SignIn_WithWrongDetails_GivesSameGenericMessage(string email, string password)
    {
        var response = await SignInAsAsync(email, password);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal("/Account/SignIn", PathOf(response));
        Assert.Contains("That email and password don&#x27;t match an account.", html);
        // Still signed out.
        Assert.Equal("/Account/SignIn", PathOf(await _client.GetAsync("/Tasks")));
    }

    [Fact]
    public async Task S06_FiveWrongPasswords_LockTheAccount_EvenForTheRightPassword()
    {
        for (var i = 0; i < 5; i++) await SignInAsAsync(PriyaEmail, $"Wrong{i}Password");

        var response = await SignInAsAsync(PriyaEmail);

        Assert.Contains("This account is locked for 15 minutes", await response.Content.ReadAsStringAsync());
        Assert.Equal("/Account/SignIn", PathOf(await _client.GetAsync("/")));
    }

    [Fact]
    public async Task S07_SignIn_ReturnsToTheRequestedPage_ButNeverToAnotherSite()
    {
        _client = NewClient();
        var back = await PostFormAsync("/Account/SignIn",
            new() { ["Email"] = SarahEmail, ["Password"] = AtlasFactory.DemoPassword, ["ReturnUrl"] = "/Team" }, "/Account/SignIn");
        Assert.Equal("/Team", PathOf(back));

        _client = NewClient();
        var html = await _client.GetStringAsync("/Account/SignIn");
        var noRedirect = NoRedirectClient();
        var outside = await noRedirect.PostAsync("/Account/SignIn", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = TokenRegex().Match(html).Groups[1].Value,
            ["Email"] = SarahEmail, ["Password"] = AtlasFactory.DemoPassword, ["ReturnUrl"] = "https://evil.example/phish"
        }));
        Assert.Equal(HttpStatusCode.Redirect, outside.StatusCode);
        Assert.Equal("/", outside.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task S08_SignOut_EndsTheSession()
    {
        var response = await PostFormAsync("/Account/SignOut", new());
        Assert.Contains("You&#x27;ve signed out.", await response.Content.ReadAsStringAsync());

        Assert.Equal("/Account/SignIn", PathOf(await _client.GetAsync("/Tasks")));
    }

    [Fact]
    public async Task S09_SignInCookie_IsHttpOnly_Secure_AndSameSite()
    {
        _client = NewClient();
        var html = await _client.GetStringAsync("/Account/SignIn");
        var response = await NoRedirectClient().PostAsync("/Account/SignIn", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = TokenRegex().Match(html).Groups[1].Value,
            ["Email"] = SarahEmail, ["Password"] = AtlasFactory.DemoPassword
        }));

        var cookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("atlas_auth="));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        // The cookie holds an encrypted ticket, not the user's id or email.
        Assert.DoesNotContain("sarah", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task S10_EveryResponse_CarriesBrowserSecurityHeaders()
    {
        var response = await Anonymous().GetAsync("/Account/SignIn");
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
    }

    // ---------- 2. Sign-up, email confirmation and password reset ----------

    [Fact]
    public async Task S11_SignUp_ConfirmEmail_CreateTeam_SeesOnlyOwnTeam()
    {
        var response = await RegisterAsync("Dana Rivers", "dana@newco.example");
        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal("/Workspace", PathOf(response));
        Assert.Contains("Confirm your email address", html);

        // Team pages need a team first.
        Assert.Equal("/Workspace", PathOf(await _client.GetAsync("/Tasks")));

        // FR-1.1: no team before the email is confirmed.
        var early = await PostFormAsync("/Workspace/Create", new() { ["NewTeam.TeamName"] = "NewCo" }, "/Workspace");
        Assert.Contains("Please confirm your email address first.", await early.Content.ReadAsStringAsync());
        Assert.DoesNotContain(Store.GetTeamsForUser(Store.FindUserByEmail("dana@newco.example")!.UserId), t => true);

        await ConfirmLatestEmailAsync("dana@newco.example");
        var created = await PostFormAsync("/Workspace/Create", new() { ["NewTeam.TeamName"] = "NewCo" }, "/Workspace");
        var dashboard = await created.Content.ReadAsStringAsync();

        Assert.Contains("You&#x27;re its Team Leader.", dashboard);
        Assert.DoesNotContain("Brightside", dashboard);
        Assert.DoesNotContain("Product photo retouching", await _client.GetStringAsync("/Tasks?filter=all"));
    }

    [Fact]
    public async Task S12_SignUp_WithAnExistingPersonsEmail_IsRefused()
    {
        var response = await RegisterAsync("Not Sarah", "SARAH@brightside.example");

        Assert.Contains("An account with this email already exists.", await response.Content.ReadAsStringAsync());
        Assert.Single(Store.GetUsers(), u => u.Email == SarahEmail);
    }

    [Fact]
    public async Task S13_SignUp_WithWeakPassword_IsRefused()
    {
        var response = await RegisterAsync("Weak Pass", "weak@newco.example", "password");

        Assert.Equal("/Account/Register", PathOf(response));
        Assert.Null(Store.FindUserByEmail("weak@newco.example"));
    }

    [Fact]
    public async Task S14_ForgotPassword_SameAnswerForAnyone_AndResetLinkWorksOnce()
    {
        _client = NewClient();
        var unknown = await PostFormAsync("/Account/ForgotPassword", new() { ["Email"] = "nobody@nowhere.example" }, "/Account/ForgotPassword");
        var known = await PostFormAsync("/Account/ForgotPassword", new() { ["Email"] = PriyaEmail }, "/Account/ForgotPassword");
        Assert.Contains("Check your email", await unknown.Content.ReadAsStringAsync());
        Assert.Contains("Check your email", await known.Content.ReadAsStringAsync());

        var link = new Uri(LatestEmailTo(PriyaEmail).Link!).PathAndQuery;
        var reset = new Dictionary<string, string> { ["Email"] = PriyaEmail, ["Password"] = "BrandNew2026", ["ConfirmPassword"] = "BrandNew2026" };
        var resetPage = await _client.GetStringAsync(link);
        reset["Code"] = System.Text.RegularExpressions.Regex.Match(resetPage, "name=\"Code\" value=\"([^\"]+)\"").Groups[1].Value;
        var done = await PostFormAsync("/Account/ResetPassword", new(reset), link);
        Assert.Contains("Your password has been changed.", await done.Content.ReadAsStringAsync());

        // Old password no longer works, the new one does, and the link can't be used again.
        Assert.Equal("/Account/SignIn", PathOf(await SignInAsAsync(PriyaEmail)));
        Assert.Equal("/", PathOf(await SignInAsAsync(PriyaEmail, "BrandNew2026")));
        reset["Password"] = reset["ConfirmPassword"] = "Another2026x";
        var again = await PostFormAsync("/Account/ResetPassword", reset, "/Account/SignIn");
        Assert.Contains("This reset link doesn't work", await again.Content.ReadAsStringAsync());
    }

    // ---------- 3. Roles and permissions ----------

    [Theory]
    [InlineData("/Tasks/Create")]
    [InlineData("/Projects/Create")]
    public async Task S15_Member_TypingAManagerAddress_GetsNotAllowedPage(string url)
    {
        await SignInAsAsync(MarcusEmail);
        var response = await _client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("/Account/AccessDenied", PathOf(response));
        Assert.Contains("You don't have permission to do that", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task S16_Member_PostingManagerFormsDirectly_IsBlocked_AndNothingChanges()
    {
        await SignInAsAsync(MarcusEmail);
        var task = SeededTask("Write launch email copy");
        var project = Store.GetProjects(BrightsideId).First();
        var before = Store.GetTasksForTeam(BrightsideId).Count;

        var create = await PostFormAsync("/Tasks/Create", new()
        {
            ["Title"] = "Sneaky task", ["DueDate"] = DateTime.Today.AddDays(3).ToString("yyyy-MM-dd"), ["ProjectId"] = project.ProjectId.ToString()
        });
        var delete = await PostFormAsync($"/Tasks/Delete/{task.TaskId}", new());
        var edit = await PostFormAsync($"/Tasks/Edit/{task.TaskId}", new()
        {
            ["Title"] = "Hijacked", ["DueDate"] = DateTime.Today.AddDays(3).ToString("yyyy-MM-dd"), ["ProjectId"] = project.ProjectId.ToString()
        });
        var newProject = await PostFormAsync("/Projects/Create", new() { ["ProjectName"] = "Sneaky project" });

        Assert.All(new[] { create, delete, edit, newProject }, r => Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode));
        Assert.Equal(before, Store.GetTasksForTeam(BrightsideId).Count);
        Assert.Equal("Write launch email copy", Store.GetTask(task.TaskId)!.Title);
        Assert.DoesNotContain(Store.GetProjects(BrightsideId), p => p.ProjectName == "Sneaky project");
    }

    [Fact]
    public async Task S17_Member_CannotGiveThemselvesManagerPermissions_OrInvitePeople()
    {
        await SignInAsAsync(MarcusEmail);

        var promote = await PostFormAsync("/Team/ToggleManager", new() { ["userId"] = Marcus.UserId.ToString() });
        var invite = await PostFormAsync("/Team/Invite", new() { ["email"] = "friend@outside.example" });

        Assert.Equal(HttpStatusCode.Forbidden, promote.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, invite.StatusCode);
        Assert.False(Store.GetMembership(BrightsideId, Marcus.UserId)!.HasManagerPermissions);
        Assert.Empty(Store.GetInvitationsForTeam(BrightsideId));
    }

    [Fact]
    public async Task S18_Member_CanDoTheirOwnWork()
    {
        await SignInAsAsync(MarcusEmail);
        var task = SeededTask("Design spring promo banner"); // In Progress, assigned to Marcus

        var comment = await PostFormAsync($"/Tasks/Comment/{task.TaskId}", new() { ["message"] = "Halfway there." });
        var pickUp = await PostFormAsync($"/Tasks/PickUp/{SeededTask("Book print shop for flyers").TaskId}", new());

        Assert.Equal(HttpStatusCode.OK, comment.StatusCode);
        Assert.Contains(Store.GetComments(task.TaskId), c => c.MessageText == "Halfway there.");
        Assert.Contains("You picked up this task.", await pickUp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task S19_DelegatedManager_CanManageTasks_ButNotLeaderOnlyActions()
    {
        var priya = Store.FindUserByEmail(PriyaEmail)!;
        await PostFormAsync("/Team/ToggleManager", new() { ["userId"] = priya.UserId.ToString() }, "/Team");
        await SignInAsAsync(PriyaEmail);

        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/Tasks/Create")).StatusCode);
        var promote = await PostFormAsync("/Team/ToggleManager", new() { ["userId"] = Marcus.UserId.ToString() });
        var invite = await PostFormAsync("/Team/Invite", new() { ["email"] = "friend@outside.example" });

        Assert.Equal(HttpStatusCode.Forbidden, promote.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, invite.StatusCode);
    }

    [Fact]
    public async Task S20_Leader_CanUseLeaderActions()
    {
        var promote = await PostFormAsync("/Team/ToggleManager", new() { ["userId"] = Marcus.UserId.ToString() }, "/Team");
        Assert.Contains("Marcus Vance can now manage and approve tasks.", await promote.Content.ReadAsStringAsync());
        Assert.True(Store.GetMembership(BrightsideId, Marcus.UserId)!.HasManagerPermissions);
    }

    // ---------- 4. Data isolation (another team's or person's data) ----------

    [Fact]
    public async Task S21_ChangingTheIdInTheAddress_CantOpenAnotherTeamsData()
    {
        // Sarah only belongs to Brightside. Try Corner Bakery's task and project by number.
        var leo = Store.FindUserByEmail(LeoEmail)!;
        var bakery = Store.GetTeamsForUser(leo.UserId).Single();
        var bakeryTask = Store.GetTasksForTeam(bakery.TeamId).First();
        var bakeryProject = Store.GetProjects(bakery.TeamId).First();

        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/Tasks/Details/{bakeryTask.TaskId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/Projects/Details/{bakeryProject.ProjectId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/Tasks/Edit/{bakeryTask.TaskId}")).StatusCode);

        var status = await PostFormAsync($"/Tasks/ChangeStatus/{bakeryTask.TaskId}", new() { ["status"] = "InProgress" });
        var delete = await PostFormAsync($"/Tasks/Delete/{bakeryTask.TaskId}", new());
        Assert.Contains("That task doesn&#x27;t exist in this team.", await status.Content.ReadAsStringAsync());
        Assert.Contains("That task doesn&#x27;t exist in this team.", await delete.Content.ReadAsStringAsync());
        Assert.Equal(bakeryTask.Status, Store.GetTask(bakeryTask.TaskId)!.Status);
    }

    [Fact]
    public async Task S22_OtherTeamsFiles_CantBeDownloaded()
    {
        var file = Store.GetAttachments(SeededTask("Product photo retouching").TaskId).Single();
        await SignInAsAsync(LeoEmail);

        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/Tasks/Download/{file.AttachmentId}")).StatusCode);
    }

    [Fact]
    public async Task S23_EditedTeamCookie_IsIgnored()
    {
        var bakery = Store.GetTeamsForUser(Store.FindUserByEmail(LeoEmail)!.UserId).Single();
        _cookies.Add(new Uri("https://localhost"), new Cookie(CurrentUserContext.TeamCookie, bakery.TeamId.ToString()));

        var html = await _client.GetStringAsync("/");
        Assert.Contains("Brightside Marketing", html);
        Assert.DoesNotContain("Design holiday menu board", html);
    }

    [Fact]
    public async Task S24_SomeoneElsesNotification_CantBeOpened()
    {
        var sarahsNotification = Store.GetNotifications(Store.FindUserByEmail(SarahEmail)!.UserId, BrightsideId).First();
        await SignInAsAsync(MarcusEmail);

        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/Notifications/Open/{sarahsNotification.NotificationId}")).StatusCode);
        Assert.False(Store.GetNotifications(Store.FindUserByEmail(SarahEmail)!.UserId, BrightsideId).First().IsRead);
    }

    [Fact]
    public async Task S25_Member_CanOnlyUpdateTheirOwnTasks()
    {
        await SignInAsAsync(MarcusEmail);
        var priyasTask = SeededTask("Write launch email copy"); // assigned to Priya only

        var response = await PostFormAsync($"/Tasks/ChangeStatus/{priyasTask.TaskId}", new() { ["status"] = "InProgress" });

        Assert.Contains("You can only update tasks assigned to you.", await response.Content.ReadAsStringAsync());
        Assert.Equal(TaskItemStatus.Assigned, SavedStatus(priyasTask));
    }

    // ---------- 5. Invitations ----------

    [Fact]
    public async Task S26_Invitation_OnlyTheInvitedEmailCanAccept_AndJoinsAsMember()
    {
        await PostFormAsync("/Team/Invite", new() { ["email"] = "Jamie@NewHire.example" }, "/Team");
        var invitation = Store.GetInvitationsForTeam(BrightsideId).Single();

        // Leo is signed in, but the invitation isn't his.
        await SignInAsAsync(LeoEmail);
        Assert.Equal(HttpStatusCode.NotFound, (await PostFormAsync($"/Workspace/Accept/{invitation.TeamInvitationId}", new(), "/Workspace")).StatusCode);

        // Jamie signs up with the invited address (in different capitals), confirms, and joins.
        await RegisterAsync("Jamie Fox", "jamie@newhire.example");
        Assert.Contains("Brightside Marketing", await _client.GetStringAsync("/Workspace"));
        await ConfirmLatestEmailAsync("jamie@newhire.example");
        var joined = await PostFormAsync($"/Workspace/Accept/{invitation.TeamInvitationId}", new(), "/Workspace");

        Assert.Contains("You&#x27;ve joined Brightside Marketing.", await joined.Content.ReadAsStringAsync());
        var jamie = Store.FindUserByEmail("jamie@newhire.example")!;
        Assert.Equal(TeamRole.Member, Store.GetMembership(BrightsideId, jamie.UserId)!.Role);
        Assert.Empty(Store.GetInvitationsForTeam(BrightsideId));
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync("/Tasks/Create")).StatusCode);
    }

    // ---------- 6. Live-site behaviour ----------

    [Fact]
    public async Task S27_DeveloperMailbox_DoesNotExistOnTheLiveSite()
    {
        using var live = AtlasFactory.Create(_database.ConnectionString, "Production");
        var client = live.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/dev/mailbox")).StatusCode);
        var signIn = await client.GetStringAsync("/Account/SignIn");
        Assert.DoesNotContain(AtlasFactory.DemoPassword, signIn);
        Assert.DoesNotContain("developer mailbox", signIn);
    }
}
