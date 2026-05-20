using System.ComponentModel;
using Mcp.ComputerUse.Core;
using Mcp.ComputerUse.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace Mcp.ComputerUse.Tools;

[McpServerToolType]
public sealed class FileTools
{
    private readonly FileService _files;
    private readonly ILogger<FileTools> _log;
    public FileTools(FileService files, ILogger<FileTools> log)
    {
        _files = files;
        _log = log;
    }

    [McpServerTool, Description("Read a file. encoding: utf8|ascii|utf16|binary. binary returns base64.")]
    public ReadFileResult ReadFile(string path, string encoding = "utf8")
    {
        _log.LogDebug("tool_call tool={Tool} path={Path} encoding={Encoding}", nameof(ReadFile), path, encoding);
        return new(_files.ReadFile(path, encoding), encoding);
    }

    [McpServerTool, Description("Write a file. encoding: utf8|ascii|utf16|binary. For binary, pass base64 in content. overwrite controls whether to replace an existing file.")]
    public OkResult WriteFile(string path, string content, string encoding = "utf8", bool overwrite = false)
    {
        _log.LogDebug("tool_call tool={Tool} path={Path} encoding={Encoding} overwrite={Overwrite}", nameof(WriteFile), path, encoding, overwrite);
        _files.WriteFile(path, content, encoding, overwrite);
        return new OkResult();
    }

    [McpServerTool, Description("Create a folder (and any missing parents).")]
    public OkResult CreateFolder(string path)
    {
        _log.LogDebug("tool_call tool={Tool} path={Path}", nameof(CreateFolder), path);
        _files.CreateFolder(path);
        return new OkResult();
    }

    [McpServerTool, Description("Launch a program. Returns the new process id. UseShellExecute=true so this respects file associations and PATH.")]
    public LaunchAppResult LaunchApp(string path, string? args = null, string? workingDir = null)
    {
        _log.LogDebug("tool_call tool={Tool} path={Path}", nameof(LaunchApp), path);
        return new(_files.LaunchApp(path, args, workingDir));
    }

    [McpServerTool, Description("Run a PowerShell command and capture stdout/stderr/exit_code. timeout_ms kills the process if exceeded.")]
    public ShellResult Shell(string command, string? workingDir = null, int timeoutMs = 30000)
    {
        _log.LogDebug("tool_call tool={Tool} timeoutMs={Timeout}", nameof(Shell), timeoutMs);
        var (code, so, se) = _files.Shell(command, workingDir, timeoutMs);
        return new ShellResult(code, so, se);
    }
}
