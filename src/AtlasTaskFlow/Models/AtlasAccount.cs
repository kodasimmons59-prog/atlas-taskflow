using Microsoft.AspNetCore.Identity;

namespace AtlasTaskFlow.Models;

/// <summary>
/// A sign-in account, managed by ASP.NET Core Identity (email, password hash, lockout, email confirmation).
/// Each account belongs to exactly one Atlas <see cref="User"/> profile, which is what tasks, teams and comments point
/// to. Keeping the two apart means the Sprint 5 tables did not change shape when sign-in arrived.
/// </summary>
public class AtlasAccount : IdentityUser
{
    public int UserId { get; set; }
}
