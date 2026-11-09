using AtlasTaskFlow.Services;
using Microsoft.AspNetCore.Authorization;

namespace AtlasTaskFlow.Security;

/// <summary>
/// The Atlas permission levels, as ASP.NET Core authorization policies. Roles belong to a team (someone can lead one
/// team and be a member of another), so the policies check the person's membership in the team they are working in.
///
/// <list type="bullet">
/// <item>Every page needs a signed-in user (the fallback policy in Program.cs).</item>
/// <item><see cref="Manager"/>: Team Leaders, and members a leader has given manager permissions (FR-3.1, FT-10).
/// Create, edit, reassign and delete tasks; add reference files; request changes; create projects.</item>
/// <item><see cref="Leader"/>: Team Leaders only. Invite people and grant or remove manager permissions.</item>
/// </list>
/// Rules that depend on the task itself (only assignees submit work; only managers complete) are checked in
/// <see cref="TaskWorkflowService"/>, which also re-checks the role, so a missing attribute can't open a hole.
/// </summary>
public static class TeamPermissions
{
    public const string Manager = "TeamManager";
    public const string Leader = "TeamLeader";

    public static void AddPolicies(AuthorizationOptions options)
    {
        options.AddPolicy(Manager, p => p.RequireAuthenticatedUser().AddRequirements(new TeamRoleRequirement(leaderOnly: false)));
        options.AddPolicy(Leader, p => p.RequireAuthenticatedUser().AddRequirements(new TeamRoleRequirement(leaderOnly: true)));
    }
}

public class TeamRoleRequirement : IAuthorizationRequirement
{
    public TeamRoleRequirement(bool leaderOnly) => LeaderOnly = leaderOnly;
    public bool LeaderOnly { get; }
}

public class TeamRoleHandler : AuthorizationHandler<TeamRoleRequirement>
{
    private readonly CurrentUserContext _current;

    public TeamRoleHandler(CurrentUserContext current) => _current = current;

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, TeamRoleRequirement requirement)
    {
        if (_current.HasTeam && (requirement.LeaderOnly ? _current.IsLeader : _current.CanManage))
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
