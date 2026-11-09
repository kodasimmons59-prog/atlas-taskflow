using AtlasTaskFlow.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtlasTaskFlow.Controllers;

/// <summary>
/// The developer mailbox: shows the account emails (confirmation and password reset links) that Atlas would have sent,
/// because there is no email service yet (DR-0013). It exists only when the app runs in Development on a developer
/// PC; on the live site this address is a plain 404.
/// </summary>
[AllowAnonymous]
[Route("dev")]
public class DevController : Controller
{
    private readonly IWebHostEnvironment _env;
    private readonly IEmailSender _email;

    public DevController(IWebHostEnvironment env, IEmailSender email)
    {
        _env = env;
        _email = email;
    }

    [HttpGet("mailbox")]
    public IActionResult Mailbox()
    {
        if (!_env.IsDevelopment() || _email is not LogEmailSender outbox) return NotFound();
        return View(outbox.Recent);
    }
}
