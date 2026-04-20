using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyAgent.Orchestrator.Agent;
using MyAgent.Orchestrator.Configuration;
using MyAgent.Orchestrator.Mcp;

// Load .env — repo root first, then current directory (current dir takes precedence)
var repoRoot = FindRepoRoot(Directory.GetCurrentDirectory());
if (repoRoot != null)
{
    var rootEnvPath = Path.Combine(repoRoot, ".env");
    if (File.Exists(rootEnvPath))
        LoadEnvFile(rootEnvPath);
}

var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
if (File.Exists(envPath))
    LoadEnvFile(envPath);

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((_, cfg) =>
    {
        cfg.AddJsonFile("Configuration/appsettings.json", optional: false, reloadOnChange: false);
        cfg.AddEnvironmentVariables();
    })
    .ConfigureServices((ctx, services) =>
    {
        var config = ctx.Configuration.Get<AgentConfig>() ?? new AgentConfig();
        services.AddSingleton(config);
        services.AddSingleton<McpClientManager>();
        services.AddSingleton<EngineerAgent>();
    })
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        logging.SetMinimumLevel(LogLevel.Information);
    });

var host = builder.Build();
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

var agent = host.Services.GetRequiredService<EngineerAgent>();
await agent.RunAsync(cts.Token);

static void LoadEnvFile(string path)
{
    foreach (var line in File.ReadAllLines(path))
    {
        if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) continue;
        var parts = line.Split('=', 2);
        if (parts.Length == 2)
            Environment.SetEnvironmentVariable(parts[0].Trim(), parts[1].Trim());
    }
}

static string? FindRepoRoot(string startDir)
{
    var dir = startDir;
    while (dir != null)
    {
        if (Directory.Exists(Path.Combine(dir, ".git"))) return dir;
        dir = Directory.GetParent(dir)?.FullName;
    }
    return null;
}
