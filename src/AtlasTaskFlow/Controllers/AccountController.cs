using System.Text;
using AtlasTaskFlow.Models;
using AtlasTaskFlow.Security;
using AtlasTaskFlow.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace AtlasTaskFlow.Controllers;

/// <summary>
/// Sign-up, sign-in, sign-out, email confirmation and password reset (FT-01, US-01, FR-1.1, FR-1.2), built on
/// ASP.NET Core Identity. Identity stores only a salted password hash, counts failed attempts and locks the account
/// for a while after too many (see Program.cs).
///
/// The messages here are deliberately plain: a failed sign-in never says whether the email or the password was wrong,
/// and "forgot password" answers the same whether or not the email has an account.
/// </summary>
[AllowAnonymous]
public class AccountController : Controller
{
    public const string SignInFailedMessage = "That email and password don't match an account. Please try again.";
    public const string LockedOutMessage =
        "This account is locked for 15 minutes after too many failed sign-in attempts. Please try again later, or reset your password.";

    private readonly UserManager<AtlasAccount> _accounts;
    private readonly SignInManager<AtlasAccount> _signIn;
    private readonly AtlasStore _store;
    private readonly IEmailSender _email;
    private readonly ILogger<AccountController> _logger;

    public AccountController(UserManager<AtlasAccount> accounts, SignInManager<AtlasAccount> signIn, AtlasStore store,
        IEmailSender email, ILogger<AccountController> logger)
    {
        _accounts = accounts;
        _signIn = signIn;
        _store = store;
        _email = email;
        _logger = logger;
    }

    // ---------- Sign in and out ----------

    [HttpGet]
    public IActionResult SignIn(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToLocal(returnUrl);
        return View(new SignInViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    public async Task<IActionResult> SignIn(SignInViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var account = await _accounts.FindByEmailAsync(model.Email.Trim());
        var result = account is null
            ? Microsoft.AspNetCore.Identity.SignInResult.Failed
            : await _signIn.PasswordSignInAsync(account, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            _logger.LogInformation("Account {AccountId} signed in.", account!.Id);
            // A previous person's team choice on this browser must not carry over.
            Response.Cookies.Delete(CurrentUserContext.TeamCookie);
            return RedirectToLocal(model.ReturnUrl);
        }

        if (result.IsLockedOut)
        {
            _logger.LogWarning("Account {AccountId} is locked out after failed sign-ins.", account!.Id);
            ModelState.AddModelError("", LockedOutMessage);
        }
        else
        {
            _logger.LogWarning("Failed sign-in attempt{ForAccount}.", account is null ? " for an unknown email" : $" for account {account.Id}");
            ModelState.AddModelError("", SignInFailedMessage);
        }
        model.Password = "";
        return View(model);
    }

    [HttpPost]
    public new async Task<IActionResult> SignOut()
    {
        await _signIn.SignOutAsync();
        Response.Cookies.Delete(CurrentUserContext.TeamCookie);
        TempData["Success"] = "You've signed out.";
        return RedirectToAction(nameof(SignIn));
    }

    /// <summary>Shown when a signed-in person tries something their role doesn't allow (HTTP 403).</summary>
    [HttpGet]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }

    // ---------- Sign up ----------

    [HttpGet]
    public IActionResult Register() =>
        User.Identity?.IsAuthenticated == true ? RedirectToAction("Index", "Home") : View(new RegisterViewModel());

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var email = model.Email.Trim();
        // An email that already belongs to an Atlas person (with or without an account) can't be claimed by sign-up.
        // Otherwise someone could register with a teammate's email and take over their tasks.
        if (_store.FindUserByEmail(email) is not null || await _accounts.FindByEmailAsync(email) is not null)
        {
            ModelState.AddModelError(nameof(model.Email), "An account with this email already exists. Sign in, or reset your password.");
            return View(model);
        }

        // Check the password rules before creating anything.
        var candidate = new AtlasAccount { UserName = email, Email = email };
        foreach (var validator in _accounts.PasswordValidators)
        {
            var check = await validator.ValidateAsync(_accounts, candidate, model.Password);
            foreach (var error in check.Errors) ModelState.AddModelError(nameof(model.Password), error.Description);
        }
        if (!ModelState.IsValid) return View(model);

        var person = _store.AddUser(model.FullName.Trim(), email);
        var account = new AtlasAccount { UserName = email, Email = email, UserId = person.UserId };
        var created = await _accounts.CreateAsync(account, model.Password);
        if (!created.Succeeded)
        {
            _store.RemoveNewUser(person.UserId);
            foreach (var error in created.Errors) ModelState.AddModelError("", error.Description);
            return View(model);
        }

        _logger.LogInformation("New account {AccountId} registered.", account.Id);
        await SendConfirmationAsync(account);
        await _signIn.SignInAsync(account, isPersistent: false);
        TempData["Success"] = $"Welcome to Atlas, {person.FullName}! We've sent a confirmation link to {email}.";
        return RedirectToAction("Index", "Workspace");
    }

    // ---------- Email confirmation (FR-1.1) ----------

    [HttpGet]
    public async Task<IActionResult> ConfirmEmail(string? userId, string? code)
    {
        var account = userId is null ? null : await _accounts.FindByIdAsync(userId);
        var confirmed = account is not null && code is not null && TryDecode(code, out var token)
                        && (await _accounts.ConfirmEmailAsync(account, token)).Succeeded;
        if (confirmed) _logger.LogInformation("Account {AccountId} confirmed its email.", account!.Id);
        return View(confirmed);
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> ResendConfirmation()
    {
        var account = await _accounts.GetUserAsync(User);
        if (account is not null && !account.EmailConfirmed)
        {
            await SendConfirmationAsync(account);
            TempData["Success"] = $"We've sent a new confirmation link to {account.Email}.";
        }
        return RedirectToAction("Index", "Workspace");
    }

    // ---------- Password reset (FR-1.2) ----------

    [HttpGet]
    public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

    [HttpPost]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var account = await _accounts.FindByEmailAsync(model.Email.Trim());
        if (account is not null)
        {
            var token = await _accounts.GeneratePasswordResetTokenAsync(account);
            var link = Url.Action(nameof(ResetPassword), "Account", new { email = account.Email, code = Encode(token) }, Request.Scheme)!;
            await _email.SendAsync(account.Email!, "Reset your Atlas TaskFlow password",
                "Someone asked to reset the password for this account. If it was you, open the link to choose a new one. It works once and expires in a day.", link);
        }
        // Same answer either way, so this page can't be used to find out who has an account.
        return View("ForgotPasswordSent", model.Email.Trim());
    }

    [HttpGet]
    public IActionResult ResetPassword(string? email, string? code)
    {
        if (string.IsNullOrEmpty(code)) return View("ResetPasswordInvalid");
        return View(new ResetPasswordViewModel { Email = email ?? "", Code = code });
    }

    [HttpPost]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var account = await _accounts.FindByEmailAsync(model.Email.Trim());
        if (account is null || !TryDecode(model.Code, out var token))
            return View("ResetPasswordInvalid");

        var result = await _accounts.ResetPasswordAsync(account, token, model.Password);
        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Code == "InvalidToken")) return View("ResetPasswordInvalid");
            foreach (var error in result.Errors) ModelState.AddModelError(nameof(model.Password), error.Description);
            return View(model);
        }

        // A successful reset also lifts a lockout, so the owner can get straight back in.
        await _accounts.SetLockoutEndDateAsync(account, null);
        await _accounts.ResetAccessFailedCountAsync(account);
        _logger.LogInformation("Account {AccountId} reset its password.", account.Id);
        TempData["Success"] = "Your password has been changed. Sign in with your new password.";
        return RedirectToAction(nameof(SignIn));
    }

    // ---------- Helpers ----------

    private async Task SendConfirmationAsync(AtlasAccount account)
    {
        var token = await _accounts.GenerateEmailConfirmationTokenAsync(account);
        var link = Url.Action(nameof(ConfirmEmail), "Account", new { userId = account.Id, code = Encode(token) }, Request.Scheme)!;
        await _email.SendAsync(account.Email!, "Confirm your Atlas TaskFlow email",
            "Open the link to confirm your email address. You need to confirm it before you can create or join a team.", link);
    }

    /// <summary>Only ever redirects within Atlas, so a crafted sign-in link can't send someone to another site.</summary>
    private IActionResult RedirectToLocal(string? returnUrl) =>
        Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl!) : RedirectToAction("Index", "Home");

    private static string Encode(string token) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

    private static bool TryDecode(string code, out string token)
    {
        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
            return true;
        }
        catch (FormatException)
        {
            token = "";
            return false;
        }
    }
}
