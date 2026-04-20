using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using MyAgent.Orchestrator.Configuration;

namespace MyAgent.Orchestrator.Mcp;

public class McpClientManager : IAsyncDisposable
{
    private readonly AgentConfig _config;
    private readonly ILogger<McpClientManager> _logger;
    private readonly List<McpServerProcess> _servers = new();
    private readonly Dictionary<string, McpServerProcess> _toolToServer = new();

    public McpClientManager(AgentConfig config, ILogger<McpClientManager> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task ConnectAllAsync(CancellationToken ct = default)
    {
        foreach (var (name, serverConfig) in _config.McpServers)
        {
            if (!serverConfig.Enabled)
            {
                _logger.LogInformation("Skipping disabled MCP server: {Name}", name);
                continue;
            }

            try
            {
                var server = new McpServerProcess(name, serverConfig, _logger);
                await server.StartAsync(ct);
                _servers.Add(server);
                _logger.LogInformation("Connected to MCP server: {Name}", name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to MCP server: {Name}", name);
            }
        }
    }

    public async Task<List<McpToolDefinition>> DiscoverToolsAsync(CancellationToken ct = default)
    {
        var tools = new List<McpToolDefinition>();
        _toolToServer.Clear();

        foreach (var server in _servers)
        {
            try
            {
                var serverTools = await server.ListToolsAsync(ct);
                foreach (var tool in serverTools)
                {
                    tools.Add(tool);
                    _toolToServer[tool.Name] = server;
                    _logger.LogDebug("Discovered tool '{Tool}' from server '{Server}'", tool.Name, server.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to list tools from MCP server: {Name}", server.Name);
            }
        }

        return tools;
    }

    public async Task<string> CallToolAsync(string toolName, JsonObject arguments, CancellationToken ct = default)
    {
        if (!_toolToServer.TryGetValue(toolName, out var server))
            throw new KeyNotFoundException($"Unknown tool: {toolName}");

        return await server.CallToolAsync(toolName, arguments, ct);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var server in _servers)
            await server.DisposeAsync();
        _servers.Clear();
    }
}

internal class McpServerProcess : IAsyncDisposable
{
    public string Name { get; }
    private readonly McpServerConfig _config;
    private readonly ILogger _logger;
    private Process? _process;
    private StreamWriter? _stdin;
    private StreamReader? _stdout;
    private int _requestId = 1;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public McpServerProcess(string name, McpServerConfig config, ILogger logger)
    {
        Name = name;
        _config = config;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _config.Command,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = Encoding.UTF8,
            StandardOutputEncoding = Encoding.UTF8,
        };
        foreach (var arg in _config.Args)
            psi.ArgumentList.Add(arg);

        // Copy current env then overlay server-specific entries
        foreach (System.Collections.DictionaryEntry entry in System.Environment.GetEnvironmentVariables())
            psi.Environment[entry.Key.ToString()!] = entry.Value?.ToString() ?? "";
        foreach (var (k, v) in _config.Env)
        {
            var resolved = System.Environment.GetEnvironmentVariable(v) ?? v;
            psi.Environment[k] = resolved;
        }

        _process = new Process { StartInfo = psi };
        _process.Start();
        _stdin = _process.StandardInput;
        _stdout = _process.StandardOutput;

        var initRequest = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = _requestId++,
            ["method"] = "initialize",
            ["params"] = new JsonObject
            {
                ["protocolVersion"] = "2024-11-05",
                ["capabilities"] = new JsonObject(),
                ["clientInfo"] = new JsonObject
                {
                    ["name"] = "MyAgent.Orchestrator",
                    ["version"] = "1.0.0"
                }
            }
        };

        var initResponse = await SendRequestAsync(initRequest, ct);
        _logger.LogDebug("MCP server {Name} initialized: {Response}", Name, initResponse?.ToJsonString());

        var initialized = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["method"] = "notifications/initialized"
        };
        await SendNotificationAsync(initialized);
    }

    public async Task<List<McpToolDefinition>> ListToolsAsync(CancellationToken ct)
    {
        var request = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = _requestId++,
            ["method"] = "tools/list",
            ["params"] = new JsonObject()
        };

        var response = await SendRequestAsync(request, ct);
        var tools = new List<McpToolDefinition>();

        if (response?["result"]?["tools"] is JsonArray toolsArray)
        {
            foreach (var toolNode in toolsArray)
            {
                if (toolNode is not JsonObject toolObj) continue;
                tools.Add(new McpToolDefinition
                {
                    Name = toolObj["name"]?.GetValue<string>() ?? "",
                    Description = toolObj["description"]?.GetValue<string>() ?? "",
                    InputSchema = toolObj["inputSchema"]?.AsObject() ?? new JsonObject()
                });
            }
        }

        return tools;
    }

    public async Task<string> CallToolAsync(string toolName, JsonObject arguments, CancellationToken ct)
    {
        var request = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = _requestId++,
            ["method"] = "tools/call",
            ["params"] = new JsonObject
            {
                ["name"] = toolName,
                ["arguments"] = arguments
            }
        };

        var response = await SendRequestAsync(request, ct);

        var sb = new StringBuilder();
        if (response?["result"]?["content"] is JsonArray content)
        {
            foreach (var item in content)
            {
                if (item?["text"]?.GetValue<string>() is string text)
                    sb.AppendLine(text);
            }
        }
        else if (response?["error"] is JsonObject error)
        {
            return $"Tool error: {error["message"]?.GetValue<string>()}";
        }

        return sb.ToString().TrimEnd();
    }

    private async Task<JsonObject?> SendRequestAsync(JsonObject request, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var json = request.ToJsonString();
            await _stdin!.WriteLineAsync(json.AsMemory(), ct);
            await _stdin.FlushAsync(ct);

            while (true)
            {
                var line = await _stdout!.ReadLineAsync(ct);
                if (line == null) return null;
                line = line.Trim();
                if (!line.StartsWith('{')) continue;
                return JsonNode.Parse(line)?.AsObject();
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task SendNotificationAsync(JsonObject notification)
    {
        await _lock.WaitAsync();
        try
        {
            var json = notification.ToJsonString();
            await _stdin!.WriteLineAsync(json);
            await _stdin.FlushAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            _stdin?.Close();
            if (_process != null && !_process.HasExited)
            {
                _process.Kill();
                await _process.WaitForExitAsync();
            }
            _process?.Dispose();
        }
        catch { /* ignore cleanup errors */ }
    }
}
