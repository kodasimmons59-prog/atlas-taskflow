namespace AtlasTaskFlow.Models;

/// <summary>
/// A Team Leader's invitation for an email address to join their team (US-03, FT-02). Only a signed-in person whose
/// confirmed account email matches can accept it; they join as a standard Team Member.
/// </summary>
public class TeamInvitation
{
    public int TeamInvitationId { get; set; }
    public int TeamId { get; set; }
    public string Email { get; set; } = "";
    public int InvitedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
