using AtlasTaskFlow.Data;
using AtlasTaskFlow.Services;
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

// Sprint 5: data lives in SQL Server through Entity Framework Core. The connection string is "DefaultConnection":
// LocalDB in appsettings.Development.json on a developer PC, and an App Service connection string setting on Azure,
// so no password is ever stored in the repo.
builder.Services.AddDbContext<AtlasDbContext>((services, options) =>
{
    var connectionString = services.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(connectionString))
        throw new InvalidOperationException(
            "No database connection string. Set ConnectionStrings:DefaultConnection (see docs/DEPLOYMENT.md).");

    // Retry brief connection drops, such as Azure SQL waking up from auto-pause.
    options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure());
});
builder.Services.AddScoped<AtlasStore>();
builder.Services.AddScoped<TaskWorkflowService>();
builder.Services.AddScoped<CurrentUserContext>();
// /health also checks that the database answers.
builder.Services.AddHealthChecks().AddDbContextCheck<AtlasDbContext>("database");

var app = builder.Build();

// Bring the database schema up to date and add the sample data the first time. If the database can't be reached,
// log it and keep starting: pages then show the friendly "database unavailable" page instead of the site being down.
using (var scope = app.Services.CreateScope())
{
    try
    {
        scope.ServiceProvider.GetRequiredService<AtlasDbContext>().Database.Migrate();
        if (SeedData.EnsureSeeded(scope.ServiceProvider.GetRequiredService<AtlasStore>(), DateTime.Today))
            app.Logger.LogInformation("Empty database: added the sample teams.");
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
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

// Lightweight endpoint for the post-deployment smoke test and Azure health checks.
app.MapHealthChecks("/health");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

// Lets the integration tests start the app with WebApplicationFactory<Program>.
public partial class Program { }
