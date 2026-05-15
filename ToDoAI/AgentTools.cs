using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace ToDoAI;

public class AgentTools
{
    private readonly string _rootDir;

    public AgentTools(string rootDir) => _rootDir = rootDir;

    [AgentTool("write", "Write File", "Create a file with content")]
    public async Task<string> Write(
        [AgentParam("File path", "Path relative to project root, e.g. src/Program.cs")] string filePath,
        [AgentParam("File content", "Full text content to write")] string content)
    {
        string full = Path.Combine(_rootDir, filePath);
        string? dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(full, content);
        return $"Written {filePath} ({content.Length} chars)";
    }

    [AgentTool("read", "Read File", "Read file content with line numbers")]
    public async Task<string> Read(
        [AgentParam("File path", "Path relative to project root")] string filePath)
    {
        string full = Path.Combine(_rootDir, filePath);
        if (!File.Exists(full)) return $"ERROR: file not found {filePath}";
        var lines = await File.ReadAllLinesAsync(full);
        return string.Join('\n', lines.Select((l, i) => $"{i + 1,4}| {l}"));
    }

    [AgentTool("run", "Run Command", "Execute a terminal command in project directory")]
    public async Task<string> Run(
        [AgentParam("Command", "Command to execute, e.g. dotnet build")] string command)
    {
        bool isWin = OperatingSystem.IsWindows();
        string shell = isWin ? "cmd.exe" : "/bin/bash";
        string args = isWin ? $"/c {command}" : $"-c \"{command.Replace("\"", "\\\"")}\"";
        var psi = new ProcessStartInfo(shell, args)
        {
            RedirectStandardOutput = true, RedirectStandardError = true,
            WorkingDirectory = _rootDir, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
        };
        using var proc = new Process { StartInfo = psi };
        proc.Start();
        string stdout = await proc.StandardOutput.ReadToEndAsync();
        string stderr = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();
        var r = $"Exit code: {proc.ExitCode}";
        if (!string.IsNullOrEmpty(stdout)) r += $"\nOutput:\n{stdout}";
        if (!string.IsNullOrEmpty(stderr)) r += $"\n{(proc.ExitCode == 0 ? "Info" : "Error")}:\n{stderr}";
        return r;
    }

    [AgentTool("tree", "View Directory", "Show directory tree structure")]
    public string Tree(
        [AgentParam("Sub path", "Subdirectory path, leave empty for root (optional)")] string subPath = "")
    {
        string target = string.IsNullOrEmpty(subPath) ? _rootDir : Path.Combine(_rootDir, subPath);
        if (!Directory.Exists(target)) return $"ERROR: directory not found {subPath}";
        return BuildTree(target, "", string.IsNullOrEmpty(subPath) ? "." : subPath);
    }

    [AgentTool("edit", "Edit File", "Replace exact old text with new text in a file. Provide the exact text to find and the replacement.")]
    public async Task<string> Edit(
        [AgentParam("File path", "Path relative to project root")] string filePath,
        [AgentParam("Old text", "The exact existing text to replace (find this)")] string oldText,
        [AgentParam("New text", "The replacement text")] string newText)
    {
        string full = Path.Combine(_rootDir, filePath);
        if (!File.Exists(full)) return $"ERROR: file not found {filePath}";
        string content = await File.ReadAllTextAsync(full);
        if (!content.Contains(oldText, StringComparison.Ordinal))
            return $"ERROR: old text not found in {filePath}";
        content = content.Replace(oldText, newText);
        await File.WriteAllTextAsync(full, content);
        return $"Edited {filePath}: replaced \"{oldText[..Math.Min(30, oldText.Length)]}...\" ({oldText.Length} chars -> {newText.Length} chars)";
    }

    private static string BuildTree(string dir, string indent, string name)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{indent}{name}");
        foreach (var f in Directory.GetFiles(dir)) sb.AppendLine($"{indent}  {Path.GetFileName(f)}");
        foreach (var d in Directory.GetDirectories(dir)) sb.Append(BuildTree(d, indent + "  ", Path.GetFileName(d)));
        return sb.ToString();
    }

    [AgentTool("mkdir", "Create Directory", "Create a directory/folder")]
    public string Mkdir(
        [AgentParam("Directory path", "Path relative to project root")] string dirPath)
    {
        string full = Path.Combine(_rootDir, dirPath);
        if (File.Exists(full)) File.Delete(full);
        Directory.CreateDirectory(full);
        return $"Created directory {dirPath}";
    }

    [AgentTool("delete", "Delete File", "Delete a file")]
    public string Delete(
        [AgentParam("File path", "Path relative to project root")] string filePath)
    {
        string full = Path.Combine(_rootDir, filePath);
        if (File.Exists(full)) { File.Delete(full); return $"Deleted {filePath}"; }
        return $"ERROR: not found {filePath}";
    }

    [AgentTool("grep", "Search Text", "Search for text in files")]
    public string Grep(
        [AgentParam("Keyword", "Text to search for")] string keyword,
        [AgentParam("File path", "File to search (optional, empty = all files)")] string filePath = "")
    {
        var sb = new StringBuilder();
        var files = string.IsNullOrEmpty(filePath)
            ? Directory.GetFiles(_rootDir, "*", SearchOption.AllDirectories)
            : new[] { Path.Combine(_rootDir, filePath) };
        foreach (var f in files.Where(File.Exists))
        {
            var lines = File.ReadAllLines(f);
            for (int i = 0; i < lines.Length; i++)
                if (lines[i].Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    sb.AppendLine($"{f}:{i + 1}: {lines[i].Trim()}");
        }
        return sb.Length > 0 ? sb.ToString() : "No matches found";
    }
}
