using AtlasTaskFlow.Models;
using AtlasTaskFlow.Security;

namespace AtlasTaskFlow.Services;

/// <summary>
/// Works out who is making this request and which team they are working in.
///
/// Sprint 6: the person comes from the signed-in identity (the encrypted sign-in cookie made by ASP.NET Core Identity),
/// never from anything the browser can edit. The chosen team is remembered in a cookie, but it is only accepted if the
/// person really belongs to that team, so an edited cookie can't open another team's data.
/// </summary>
public class CurrentUserContext
{
    public const string TeamCookie = "atlas_team";

    private readonly User? _user;
    private readonly Team? _team;
    private readonly TeamMember? _membership;

    public CurrentUserContext(AtlasStore store, IHttpContextAccessor accessor)
    {
        var http = accessor.HttpContext;
        var principal = http?.User;
        if (principal?.Identity?.IsAuthenticated == true
            && int.TryParse(principal.FindFirst(AtlasClaimsFactory.UserIdClaim)?.Value, out var userId))
        {
            _user = store.GetUser(userId);
        }

        Teams = _user is null ? Array.Empty<Team>() : store.GetTeamsForUser(_user.UserId);
        _team = (int.TryParse(http?.Request.Cookies[TeamCookie], out var teamId) ? Teams.FirstOrDefault(t => t.TeamId == teamId) : null)
                ?? Teams.FirstOrDefault();
        if (_team is not null && _user is not null)
            _membership = store.GetMembership(_team.TeamId, _user.UserId);
    }

    public bool IsSignedIn => _user is not null;

    /// <summary>Signed in and a member of at least one team. Pages that show team data need this.</summary>
    public bool HasTeam => _membership is not null;

    public User User => _user ?? throw new InvalidOperationException("Nobody is signed in.");
    public Team Team => _team ?? throw new InvalidOperationException("The signed-in user has no team.");
    public TeamMember Membership => _membership ?? throw new InvalidOperationException("The signed-in user has no team.");
    public IReadOnlyList<Team> Teams { get; }

    public bool CanManage => _membership?.CanManage ?? false;
    public bool IsLeader => _membership?.Role == TeamRole.Leader;
}
