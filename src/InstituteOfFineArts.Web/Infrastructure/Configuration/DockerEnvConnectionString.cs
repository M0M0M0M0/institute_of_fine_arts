using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Configuration.Memory;

namespace InstituteOfFineArts.Web.Infrastructure.Configuration;

/// <summary>
/// Development only: builds the database connection string from the repository's <c>.env</c> file – the same file
/// docker-compose.yml reads for the SQL Server container (MSSQL_SA_PASSWORD, SQL_PORT).
/// A fresh clone therefore runs with: copy .env.example to .env, <c>docker compose up -d</c>, <c>dotnet run</c> –
/// no User Secrets, and the password stays out of Git (.env is git-ignored).
/// <para>
/// Precedence is unchanged for anyone who configured more: the value is inserted right after
/// appsettings.{Environment}.json, so User Secrets, environment variables and the command line still win.
/// Without a .env (e.g. a machine with SQL Server installed locally) nothing is added and appsettings.json applies.
/// </para>
/// </summary>
public static class DockerEnvConnectionString
{
    /// <summary>Name of the file shared with docker-compose.yml.</summary>
    public const string EnvFileName = ".env";

    /// <summary>Configuration key of the connection string used by AppDbContext.</summary>
    public const string ConnectionStringKey = "ConnectionStrings:DefaultConnection";

    /// <summary>
    /// In Development, adds the Docker SQL Server connection string built from the nearest .env above the content root.
    /// </summary>
    /// <param name="builder">The web application builder (before services are registered)</param>
    /// <returns>The .env path that was used, or null when nothing was added</returns>
    public static string? AddDockerEnvConnectionString(this WebApplicationBuilder builder)
    {
        if (!builder.Environment.IsDevelopment())
        {
            return null;
        }

        var envFile = FindEnvFile(builder.Environment.ContentRootPath);
        if (envFile == null)
        {
            return null;
        }

        var connectionString = BuildConnectionString(ReadEnvFile(envFile));
        if (connectionString == null)
        {
            return null;
        }

        // Insert after appsettings.{Environment}.json so User Secrets / environment variables / command line still override it
        var sources = builder.Configuration.Sources;
        var envJson = $"appsettings.{builder.Environment.EnvironmentName}.json";
        var index = sources.Select((s, i) => (s, i))
            .Where(x => x.s is JsonConfigurationSource json && string.Equals(json.Path, envJson, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.i + 1)
            .DefaultIfEmpty(sources.Count)
            .First();
        sources.Insert(index, new MemoryConfigurationSource
        {
            InitialData = new Dictionary<string, string?> { [ConnectionStringKey] = connectionString },
        });
        return envFile;
    }

    /// <summary>Looks for .env in the content root and its parents (src/InstituteOfFineArts.Web → repository root).</summary>
    /// <param name="startDirectory">Directory to start from</param>
    /// <returns>Full path of the first .env found, or null</returns>
    public static string? FindEnvFile(string startDirectory)
    {
        for (var dir = new DirectoryInfo(startDirectory); dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, EnvFileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }
        return null;
    }

    /// <summary>
    /// Reads KEY=VALUE lines like docker compose does: blank lines and # comments are skipped,
    /// surrounding single or double quotes are removed.
    /// </summary>
    /// <param name="path">Path of the .env file</param>
    public static IReadOnlyDictionary<string, string> ReadEnvFile(string path) => ParseEnv(File.ReadAllLines(path));

    /// <summary>Parses the lines of a .env file (see <see cref="ReadEnvFile"/>).</summary>
    /// <param name="lines">File lines</param>
    public static IReadOnlyDictionary<string, string> ParseEnv(IEnumerable<string> lines)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }
            var eq = line.IndexOf('=');
            if (eq <= 0)
            {
                continue;
            }
            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();
            // "value" or 'value' → value
            if (value.Length >= 2 && (value[0] == '"' || value[0] == '\'') && value[^1] == value[0])
            {
                value = value[1..^1];
            }
            values[key] = value;
        }
        return values;
    }

    /// <summary>
    /// Connection string to the Docker SQL Server for the given .env values, or null when MSSQL_SA_PASSWORD is missing.
    /// </summary>
    /// <param name="env">Values read from .env</param>
    public static string? BuildConnectionString(IReadOnlyDictionary<string, string> env)
    {
        if (!env.TryGetValue("MSSQL_SA_PASSWORD", out var password) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }
        var port = env.TryGetValue("SQL_PORT", out var p) && int.TryParse(p, out var parsed) ? parsed : 1433;
        // Password quoted for SqlClient so ; or = inside it do not break the string
        var quoted = "\"" + password.Replace("\"", "\"\"") + "\"";
        return $"Server=localhost,{port};Database=IFA_DB;User Id=sa;Password={quoted};TrustServerCertificate=True;MultipleActiveResultSets=true";
    }
}
