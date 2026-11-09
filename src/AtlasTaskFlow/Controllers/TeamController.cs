using System.ComponentModel.DataAnnotations;
using AtlasTaskFlow.Models;
using AtlasTaskFlow.Security;
using AtlasTaskFlow.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtlasTaskFlow.Controllers;

public class TeamController : AtlasController
{
    public TeamController(AtlasStore store, CurrentUserContext current) : base(store, current) { }

    public IActionResult Index()
    {
        var tasks = Store.GetTasksForTeam(Current.Team.TeamId);
        var members = Store.GetTeamMembers(Current.Team.TeamId)
            .Select(m => new TeamMemberView(
                Store.GetUser(m.UserId)!,
                m,
                tasks.Count(t => t.AssigneeIds.Contains(m.UserId) && t.Status != TaskItemStatus.Completed)))
            .OrderBy(v => v.Membership.Role).ThenBy(v => v.User.FullName)
            .ToList();
        return View(new TeamPageViewModel
        {
            Members = members,
            // Only the leader manages invitations, so only the leader sees who has been invited.
            Invitations = Current.IsLeader ? Store.GetInvitationsForTeam(Current.Team.TeamId) : Array.Empty<TeamInvitation>()
        });
    }

    /// <summary>FT-10: a leader grants or removes manager permissions for a member.</summary>
    [HttpPost]
    [Authorize(Policy = TeamPermissions.Leader)]
    public IActionResult ToggleManager(int userId)
    {
        var member = Store.GetMembership(Current.Team.TeamId, userId);
        if (member is null || member.Role == TeamRole.Leader) return NotFound();

        Store.SetManagerPermissions(Current.Team.TeamId, userId, !member.HasManagerPermissions);
        var name = Store.GetUser(userId)!.FullName;
        FlashSuccess(member.HasManagerPermissions ? $"{name} can now manage and approve tasks." : $"{name} is back to standard member permissions.");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// US-03: the leader invites someone by email address. The person accepts it on their Workspace page after
    /// signing up or signing in with that address (there is no email service yet, DR-0013).
    /// </summary>
    [HttpPost]
    [Authorize(Policy = TeamPermissions.Leader)]
    public IActionResult Invite(string? email)
    {
        email = email?.Trim() ?? "";
        if (email.Length == 0 || email.Length > 256 || !new EmailAddressAttribute().IsValid(email))
        {
            FlashError("Enter a valid email address to invite.");
            return RedirectToAction(nameof(Index));
        }

        var existing = Store.FindUserByEmail(email);
        if (existing is not null && Store.GetMembership(Current.Team.TeamId, existing.UserId) is not null)
            FlashError($"{email} is already on this team.");
        else if (Store.GetInvitationsForTeam(Current.Team.TeamId).Any(i => i.Email == AtlasStore.NormalizeEmail(email)))
            FlashError($"{email} has already been invited.");
        else
        {
            Store.AddInvitation(Current.Team.TeamId, email, Current.User.UserId);
            FlashSuccess($"Invited {email}. They'll see the invitation when they sign in to Atlas with that email.");
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Policy = TeamPermissions.Leader)]
    public IActionResult CancelInvite(int id)
    {
        var invitation = Store.GetInvitation(id);
        // Only this team's invitations; another team's id behaves as if it doesn't exist.
        if (invitation is null || invitation.TeamId != Current.Team.TeamId) return NotFound();

        Store.RemoveInvitation(id);
        FlashSuccess($"Cancelled the invitation for {invitation.Email}.");
        return RedirectToAction(nameof(Index));
    }
}
