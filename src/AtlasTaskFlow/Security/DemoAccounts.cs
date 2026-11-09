using AtlasTaskFlow.Models;
using AtlasTaskFlow.Services;
using Microsoft.AspNetCore.Identity;

namespace AtlasTaskFlow.Security;

/// <summary>
/// Gives the sample people (Sarah, Marcus, Priya and Leo) real sign-in accounts so the app can be demonstrated and
/// graded (DR-0015). They share one demo password, which comes from configuration ("DemoAccounts:Password"): on a
/// developer PC from appsettings.Development.json, and on Azure from an App Service setting, so the live password is
/// never in the repo. Without a password, no sample accounts are created, and nobody can sign in as them.
///
/// Runs at every start, so it also adds accounts to a database created before Sprint 6, and never changes an account
/// that already exists.
/// </summary>
public static class DemoAccounts
{
    public static async Task EnsureAsync(AtlasStore store, UserManager<AtlasAccount> accounts, string? password, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("DemoAccounts:Password is not set, so the sample people have no sign-in accounts.");
            return;
        }

        foreach (var email in SeedData.SampleEmails)
        {
            var person = store.FindUserByEmail(email);
            if (person is null || await accounts.FindByEmailAsync(email) is not null) continue;

            var account = new AtlasAccount { UserName = email, Email = email, EmailConfirmed = true, UserId = person.UserId };
            var result = await accounts.CreateAsync(account, password);
            if (result.Succeeded)
                logger.LogInformation("Created the sample sign-in account for {Name}.", person.FullName);
            else
                logger.LogError("Couldn't create the sample account for {Name}: {Errors}", person.FullName,
                    string.Join(" ", result.Errors.Select(e => e.Description)));
        }
    }
}
