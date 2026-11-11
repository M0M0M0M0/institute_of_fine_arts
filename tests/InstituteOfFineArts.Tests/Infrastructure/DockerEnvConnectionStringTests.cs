using InstituteOfFineArts.Web.Infrastructure.Configuration;

namespace InstituteOfFineArts.Tests.Infrastructure;

/// <summary>
/// Development connection string built from the repository's .env (shared with docker-compose.yml),
/// so a fresh clone runs with "docker compose up -d" + "dotnet run" without User Secrets.
/// </summary>
public class DockerEnvConnectionStringTests
{
    /// <summary>Comments and blank lines are skipped, quotes removed, the last value of a key wins.</summary>
    [Fact]
    public void ParseEnv_LikeDockerCompose()
    {
        var env = DockerEnvConnectionString.ParseEnv([
            "# comment",
            "",
            "MSSQL_SA_PASSWORD=\"Str0ng!Pass\"",
            "SQL_PORT = 14330 ",
            "BROKEN LINE",
            "QUOTED='a=b'",
        ]);

        Assert.Equal("Str0ng!Pass", env["MSSQL_SA_PASSWORD"]);
        Assert.Equal("14330", env["SQL_PORT"]);
        Assert.Equal("a=b", env["QUOTED"]);
        Assert.False(env.ContainsKey("BROKEN LINE"));
    }

    /// <summary>The connection string targets the Docker SQL Server with sa, the .env port and a quoted password.</summary>
    [Fact]
    public void BuildConnectionString_FromEnv()
    {
        var cs = DockerEnvConnectionString.BuildConnectionString(new Dictionary<string, string>
        {
            ["MSSQL_SA_PASSWORD"] = "Pa;ss=\"word\"",
            ["SQL_PORT"] = "14330",
        });

        Assert.NotNull(cs);
        var parsed = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(cs);
        Assert.Equal("localhost,14330", parsed.DataSource);
        Assert.Equal("IFA_DB", parsed.InitialCatalog);
        Assert.Equal("sa", parsed.UserID);
        Assert.Equal("Pa;ss=\"word\"", parsed.Password);
        Assert.False(parsed.IntegratedSecurity);
    }

    /// <summary>Default port 1433 when SQL_PORT is missing or invalid; no password → nothing is built.</summary>
    [Fact]
    public void BuildConnectionString_Defaults()
    {
        var cs = DockerEnvConnectionString.BuildConnectionString(new Dictionary<string, string> { ["MSSQL_SA_PASSWORD"] = "x", ["SQL_PORT"] = "abc" });
        Assert.Contains("Server=localhost,1433;", cs);
        Assert.Null(DockerEnvConnectionString.BuildConnectionString(new Dictionary<string, string> { ["SQL_PORT"] = "1433" }));
        Assert.Null(DockerEnvConnectionString.BuildConnectionString(new Dictionary<string, string> { ["MSSQL_SA_PASSWORD"] = " " }));
    }

    /// <summary>The .env is found from the web project folder up to the repository root.</summary>
    [Fact]
    public void FindEnvFile_WalksUp()
    {
        var root = Directory.CreateTempSubdirectory("ifa-env-");
        try
        {
            var web = Directory.CreateDirectory(Path.Combine(root.FullName, "src", "Web"));
            // No .env inside the temp tree yet (a .env higher up the real disk is outside the tree)
            var none = DockerEnvConnectionString.FindEnvFile(web.FullName);
            Assert.True(none == null || !none.StartsWith(root.FullName));

            File.WriteAllText(Path.Combine(root.FullName, ".env"), "MSSQL_SA_PASSWORD=x");
            Assert.Equal(Path.Combine(root.FullName, ".env"), DockerEnvConnectionString.FindEnvFile(web.FullName));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }
}
