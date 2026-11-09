using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace AtlasTaskFlow.Tests;

/// <summary>
/// The live Azure site runs on SQLite (DR-0010). This starts the app the way the live site does, on a new SQLite file,
/// and checks that the SQLite migrations, sample data, sample accounts and sign-in all work there too.
/// </summary>
public class SqliteTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"atlas-test-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(_file);
    }

    [Fact]
    public async Task LiveSiteSetup_OnSqlite_SignInWorks_AndPagesAreProtected()
    {
        using var factory = AtlasFactory.Create($"Data Source={_file}", "Production", provider: "Sqlite");
        var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        Assert.Equal("/Account/SignIn", (await client.GetAsync("/Tasks")).RequestMessage!.RequestUri!.AbsolutePath);
        Assert.Equal("Healthy", await client.GetStringAsync("/health"));

        var page = await client.GetStringAsync("/Account/SignIn");
        var token = Regex.Match(page, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        var response = await client.PostAsync("/Account/SignIn", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token, ["Email"] = "sarah@brightside.example", ["Password"] = AtlasFactory.DemoPassword
        }));
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Sarah Chen", html);
        Assert.Contains("Product photo retouching", html);
        Assert.True(File.Exists(_file));
    }
}
