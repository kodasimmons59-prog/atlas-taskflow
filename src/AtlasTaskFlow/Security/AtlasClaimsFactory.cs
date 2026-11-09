using System.Security.Claims;
using AtlasTaskFlow.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace AtlasTaskFlow.Security;

/// <summary>
/// Builds the signed-in identity stored in the (encrypted) sign-in cookie. Besides Identity's own claims it adds the
/// Atlas person id, which <see cref="Services.CurrentUserContext"/> uses to find the person on every request.
/// The browser can't change it: the cookie is encrypted and signed by the server.
/// </summary>
public class AtlasClaimsFactory : UserClaimsPrincipalFactory<AtlasAccount>
{
    public const string UserIdClaim = "atlas_user_id";

    public AtlasClaimsFactory(UserManager<AtlasAccount> userManager, IOptions<IdentityOptions> options)
        : base(userManager, options) { }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AtlasAccount account)
    {
        var identity = await base.GenerateClaimsAsync(account);
        identity.AddClaim(new Claim(UserIdClaim, account.UserId.ToString()));
        return identity;
    }
}
