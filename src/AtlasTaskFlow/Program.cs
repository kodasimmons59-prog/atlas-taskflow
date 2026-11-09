using AtlasTaskFlow.Data;
using AtlasTaskFlow.Models;
using AtlasTaskFlow.Security;
using AtlasTaskFlow.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options =>
{
    // Every POST form must carry an anti-forgery token.
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    // A save the database rejects becomes a friendly message instead of an error page.
    options.Filters.Add<DataErrorFilter>();
});
builder.Services.AddHttpContextAccessor();

// Data lives in SQL Server (LocalDB on a developer PC) or, on the live Azure site, SQLite (DR-0010). The engine and
// connection string come from configuration, so no password is ever stored in the repo. See Data/DatabaseSetup.cs.
builder.Services.AddAtlasDatabase();
builder.Services.AddScoped<AtlasStore>();
builder.Services.AddScoped<TaskWorkflowService>();
builder.Services.AddScoped<CurrentUserContext>();

// ---------- Sprint 6: authentication (who are you?) ----------
// ASP.NET Core Identity keeps accounts in the Atlas database: a salted, hashed password (never the password itself),
// email confirmation, lockout after repeated failures, and single-use tokens for email links (DR-0011).
builder.Services.AddIdentityCore<AtlasAccount>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;
        // Slow down password guessing: 5 wrong passwords lock the account for 15 minutes.
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;
        // People may sign in before confirming their email; creating or joining a team needs it (FR-1.1).
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddSignInManager()
    .AddEntityFrameworkStores<AtlasDbContext>()
    .AddDefaultTokenProviders()
    .AddClaimsPrincipalFactory<AtlasClaimsFactory>();

builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/SignIn";
    options.LogoutPath = "/Account/SignOut";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.Name = "atlas_auth";
    options.Cookie.HttpOnly = true;                              // page scripts can't read it
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;     // only sent over HTTPS
    options.Cookie.SameSite = SameSiteMode.Lax;                  // not sent with forms posted from other sites
    options.ExpireTimeSpan = TimeSpan.FromHours(8);              // a working day; "keep me signed in" makes it last
    options.SlidingExpiration = true;
});
// A changed password signs the account out everywhere else within 5 minutes.
builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(5));

// ---------- Sprint 6: authorization (what are you allowed to do?) ----------
// Every page needs a signed-in user unless it is marked [AllowAnonymous] (sign-in, sign-up, password reset, error
// pages, /health). Leader and manager actions have their own policies (Security/TeamPermissions.cs).
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    TeamPermissions.AddPolicies(options);
});
builder.Services.AddScoped<IAuthorizationHandler, TeamRoleHandler>();
builder.Services.AddSingleton<IEmailSender, LogEmailSender>();
// /health also checks that the database answers.
builder.Services.AddHealthChecks().AddDbContextCheck<AtlasDbContext>("database");

var app = builder.Build();

// Bring the database schema up to date, add the sample data the first time, and make sure the sample people have
// sign-in accounts. If the database can't be reached, log it and keep starting: pages then show the friendly
// "database unavailable" page instead of the site being down.
using (var scope = app.Services.CreateScope())
{
    try
    {
        scope.ServiceProvider.GetRequiredService<AtlasDbContext>().Database.Migrate();
        var store = scope.ServiceProvider.GetRequiredService<AtlasStore>();
        if (SeedData.EnsureSeeded(store, DateTime.Today))
            app.Logger.LogInformation("Empty database: added the sample teams.");
        await DemoAccounts.EnsureAsync(store, scope.ServiceProvider.GetRequiredService<UserManager<AtlasAccount>>(),
            app.Configuration["DemoAccounts:Password"], app.Logger);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Could not prepare the database at startup.");
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Friendly pages for 404 and other error status codes instead of a blank browser error.
app.UseStatusCodePagesWithReExecute("/Home/Status/{0}");

app.UseHttpsRedirection();

// Basic browser protections on every response: no guessing file types, no showing Atlas inside another site's frame
// (clickjacking), and no full page addresses leaked to other sites.
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Lightweight endpoint for the post-deployment smoke test and Azure health checks. It reveals nothing but
// "Healthy"/"Unhealthy", so it stays open without signing in.
app.MapHealthChecks("/health").AllowAnonymous();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

// Lets the integration tests start the app with WebApplicationFactory<Program>.
public partial class Program { }
