using AtlasTaskFlow.Models;
using AtlasTaskFlow.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AtlasTaskFlow.Controllers;

/// <summary>
/// The signed-in person's teams: create a team (US-02), accept or decline invitations (US-03), and switch between
/// teams (US-04, FR-1.3). Unlike the other pages this one works for someone who has no team yet, so it is where new
/// accounts land.
///
/// Creating or joining a team needs a confirmed email (FR-1.1). Invitations are matched to the account's own email,
/// so nobody can accept an invitation meant for someone else, even by guessing its number.
/// </summary>
public class WorkspaceController : Controller
{
    private readonly AtlasStore _store;
    private readonly CurrentUserContext _current;
    private readonly UserManager<AtlasAccount> _accounts;

    public WorkspaceController(AtlasStore store, CurrentUserContext current, UserManager<AtlasAccount> accounts)
    {
        _store = store;
        _current = current;
        _accounts = accounts;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var account = await _accounts.GetUserAsync(User);
        if (account is null || !_current.IsSignedIn) return await SignOutStale();
        return View(BuildModel(account, new CreateTeamViewModel()));
    }

    [HttpPost]
    public async Task<IActionResult> Create([Bind(Prefix = "NewTeam")] CreateTeamViewModel model)
    {
        var account = await _accounts.GetUserAsync(User);
        if (account is null || !_current.IsSignedIn) return await SignOutStale();
        if (!account.EmailConfirmed) return ConfirmFirst();
        if (!ModelState.IsValid) return View("Index", BuildModel(account, model));

        var team = _store.CreateTeam(model.TeamName.Trim(), _current.User.UserId);
        _store.AddActivity(team.TeamId, null, $"{_current.User.FullName} created the team.");
        _store.SaveChanges();
        SelectTeam(team.TeamId);
        TempData["Success"] = $"Created {team.TeamName}. You're its Team Leader.";
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public async Task<IActionResult> Accept(int id)
    {
        var account = await _accounts.GetUserAsync(User);
        if (account is null || !_current.IsSignedIn) return await SignOutStale();
        var invitation = OwnInvitation(account, id);
        if (invitation is null) return NotFound();
        if (!account.EmailConfirmed) return ConfirmFirst();

        var team = _store.GetTeam(invitation.TeamId)!;
        if (_store.GetMembership(team.TeamId, _current.User.UserId) is null)
        {
            // Invited people always join as standard members; a leader can give more permissions later (least privilege).
            _store.AddMember(team.TeamId, _current.User.UserId, TeamRole.Member);
            _store.AddActivity(team.TeamId, null, $"{_current.User.FullName} joined the team.");
        }
        _store.RemoveInvitation(invitation.TeamInvitationId);
        SelectTeam(team.TeamId);
        TempData["Success"] = $"You've joined {team.TeamName}.";
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public async Task<IActionResult> Decline(int id)
    {
        var account = await _accounts.GetUserAsync(User);
        if (account is null || !_current.IsSignedIn) return await SignOutStale();
        var invitation = OwnInvitation(account, id);
        if (invitation is null) return NotFound();

        _store.RemoveInvitation(invitation.TeamInvitationId);
        TempData["Success"] = "Invitation declined.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>US-04: switch team without signing out. Only teams the person belongs to are accepted.</summary>
    [HttpPost]
    public IActionResult Switch(int teamId)
    {
        var team = _current.Teams.FirstOrDefault(t => t.TeamId == teamId);
        if (team is null) return NotFound();

        SelectTeam(team.TeamId);
        TempData["Success"] = $"Switched to {team.TeamName.TrimEnd('.')}.";
        return RedirectToAction("Index", "Home");
    }

    // ---------- Helpers ----------

    private WorkspaceViewModel BuildModel(AtlasAccount account, CreateTeamViewModel newTeam) => new()
    {
        EmailConfirmed = account.EmailConfirmed,
        Email = account.Email!,
        Teams = _current.Teams,
        Invitations = _store.GetInvitationsForEmail(account.Email!)
            .Select(i => new InvitationView(i, _store.GetTeam(i.TeamId)!.TeamName, _store.GetUser(i.InvitedByUserId)?.FullName ?? ""))
            .ToList(),
        NewTeam = newTeam
    };

    /// <summary>The invitation, only if it was sent to this account's email address.</summary>
    private TeamInvitation? OwnInvitation(AtlasAccount account, int id)
    {
        var invitation = _store.GetInvitation(id);
        return invitation is not null && invitation.Email == AtlasStore.NormalizeEmail(account.Email!) ? invitation : null;
    }

    private IActionResult ConfirmFirst()
    {
        TempData["Error"] = "Please confirm your email address first. Use the link we sent you, or send a new one below.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>A sign-in cookie for an account that no longer exists: sign out and start again.</summary>
    private async Task<IActionResult> SignOutStale()
    {
        await HttpContext.RequestServices.GetRequiredService<SignInManager<AtlasAccount>>().SignOutAsync();
        return RedirectToAction("SignIn", "Account");
    }

    private void SelectTeam(int teamId) =>
        Response.Cookies.Append(CurrentUserContext.TeamCookie, teamId.ToString(), new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = true,
            IsEssential = true,
            Expires = DateTimeOffset.UtcNow.AddDays(30)
        });
}
