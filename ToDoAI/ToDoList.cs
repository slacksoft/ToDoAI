using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace ToDoAI;

public class ToDoList : IDisposable
{
    private AIService? _ai;
    private AgentTools? _tools;
    private string _lastError = "";
    private string _lastSummary = "";
    private readonly StringBuilder _fullSummary = new();

    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public string RootDir { get; set; } = "";
    public string ProjectDescription { get; set; } = "";
    public int MaxSteps { get; set; } = 6;
    public int MaxRetries { get; set; } = 3;
    public int MaxStepRetries { get; set; } = 3;

    public event Action<string>? OnWriteLine;
    public event Action<string>? OnWrite;
    public event Action<List<string>>? OnPlanGenerated;
    public event Action<int>? OnStepStarting;
    public event Action<int, bool>? OnStepCompleted;
    public event Action? OnAllCompleted;

    public List<string> CurrentPlan { get; private set; } = new();
    private readonly List<ToolInfo> _toolsRegistry = new();
    private readonly List<StepRecord> _completed = new();
    private readonly List<string> _planTools = new();
    private static readonly Regex StepPattern = new(@"step\s*\d+[:\-.]\s*(?:\[(\w+)\])?\s*(.+)", RegexOptions.IgnoreCase);
    private static string? _systemInfo;

    public record StepRecord(string Task, string Tool, Dictionary<string, object?> Params, string Result, Dictionary<string, string> Files);

    public ToDoList() { RegisterTools(); }

    private void Ensure() { _ai ??= new AIService(ApiKey, Model, Endpoint); _tools ??= new AgentTools(RootDir); }

    private void RegisterTools()
    {
        foreach (var m in typeof(AgentTools).GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            var attr = m.GetCustomAttribute<AgentToolAttribute>();
            if (attr == null) continue;
            var tool = new ToolInfo { Name = attr.Name, DisplayName = attr.DisplayName, Description = attr.Description, Method = m };
            foreach (var p in m.GetParameters())
            {
                var pa = p.GetCustomAttribute<AgentParamAttribute>();
                tool.Parameters.Add(new ToolParameter { Name = pa?.Name ?? p.Name ?? "", Description = pa?.Description ?? "", Type = p.ParameterType, IsOptional = p.HasDefaultValue, DefaultValue = p.DefaultValue });
            }
            _toolsRegistry.Add(tool);
        }
    }

    private static string GetSystemInfo()
    {
        if (_systemInfo != null) return _systemInfo;
        var sb = new StringBuilder();
        sb.AppendLine($"OS: {(OperatingSystem.IsWindows() ? "Windows" : OperatingSystem.IsLinux() ? "Linux" : "macOS")}");
        try
        {
            if (OperatingSystem.IsWindows()) sb.AppendLine($"Windows version: {Environment.OSVersion.Version}");
            sb.AppendLine($"CPU cores: {Environment.ProcessorCount}");
            sb.AppendLine($"Machine: {Environment.MachineName}");
            sb.AppendLine($".NET version: {RuntimeInformation.FrameworkDescription}");
        }
        catch { }
        _systemInfo = sb.ToString();
        return _systemInfo;
    }

    private string BuildState(int? stepIdx = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## System");
        sb.Append(GetSystemInfo());
        sb.AppendLine($"Project root: {RootDir}");
        sb.AppendLine();
        sb.AppendLine("## Directory Tree");
        sb.AppendLine(_tools?.Tree("") ?? "(empty)");
        sb.AppendLine();
        sb.AppendLine("## User Request");
        sb.AppendLine(ProjectDescription);
        sb.AppendLine();
        if (!string.IsNullOrEmpty(_lastSummary)) { sb.AppendLine("## Previous Summary"); sb.AppendLine(_lastSummary); sb.AppendLine(); }
        if (_completed.Count > 0)
        {
            sb.AppendLine("## Completed Steps");
            for (int i = 0; i < _completed.Count; i++)
            {
                var s = _completed[i];
                sb.AppendLine($"  {i + 1}. [{s.Tool}] {s.Task} -> {s.Result}");
                foreach (var f in s.Files) { sb.AppendLine($"    File {f.Key}:\n```\n{f.Value}\n```"); }
            }
            sb.AppendLine();
        }
        if (!string.IsNullOrEmpty(_lastError)) { sb.AppendLine("## Last Error"); sb.AppendLine(_lastError); sb.AppendLine(); }
        if (stepIdx.HasValue && stepIdx < CurrentPlan.Count)
        {
            sb.AppendLine($"## Current ({stepIdx.Value + 1}/{CurrentPlan.Count}): {CurrentPlan[stepIdx.Value]}");
            sb.AppendLine("Full Plan:");
            for (int i = 0; i < CurrentPlan.Count; i++) sb.AppendLine($"  {(i == stepIdx.Value ? ">" : " ")} {i + 1}. [{_planTools.ElementAtOrDefault(i)}] {CurrentPlan[i]}");
        }
        return sb.ToString();
    }

    public async Task<List<string>> GeneratePlanAsync(string? feedback = null)
    {
        Ensure();
        _lastError = "";
        string tree = _tools!.Tree("");
        string fb = feedback != null ? $"\nUser feedback: {feedback}\n" : "";
        string prompt = $@"You are an AI coding assistant. Create a step-by-step plan.

User request:
{ProjectDescription}

{GetSystemInfo()}Project root: {RootDir}

Current directory:
{tree}
{fb}
Rules:
1. Each step does ONE thing using these tools:
   - write(path, content) - ONLY for NEW files or COMPLETE rewrites
   - edit(path, old, new) - for MODIFYING existing files
   - read(path) - read file with line numbers
   - run(command) - execute terminal command
   - tree(path?) - show directory structure
   - mkdir(path) - create directory
   - delete(path) - delete file
   - grep(keyword, path?) - search text in files
2. Max {MaxSteps} steps.
3. Stick to request. No extra files.
4. If user asks to run/verify, include 'run' step.
5. For multi-file projects, one step per file.
6. Paths relative to project root.
7. For [edit] steps, include old='' new='' values.

Format: Step 1: [tool] description ...";

        OnWriteLine?.Invoke("");
        var resp = new StringBuilder();
        var planRaw = await _ai!.ChatStreamAsync(new List<ChatMessage> { new("user", prompt) }, d => { OnWrite?.Invoke(d); resp.Append(d); });
        OnWriteLine?.Invoke("");

        var raw = planRaw;
        if (string.IsNullOrWhiteSpace(raw)) raw = resp.ToString();
        if (raw.StartsWith("[HTTP") || raw.StartsWith("[ERROR"))
        {
            OnWriteLine?.Invoke($"[API] {raw}");
            CurrentPlan = new List<string>(); _planTools.Clear();
            OnPlanGenerated?.Invoke(CurrentPlan);
            return CurrentPlan;
        }
        CurrentPlan = new List<string>();
        _planTools.Clear();
        foreach (var line in raw.Split('\n'))
        {
            var m = StepPattern.Match(line.Trim());
            if (m.Success) { _planTools.Add((m.Groups[1].Value ?? "").ToLower()); CurrentPlan.Add(m.Groups[2].Value.Trim()); }
        }
        if (CurrentPlan.Count > MaxSteps) { CurrentPlan = CurrentPlan.Take(MaxSteps).ToList(); while (_planTools.Count > MaxSteps) _planTools.RemoveAt(_planTools.Count - 1); }
        OnPlanGenerated?.Invoke(CurrentPlan);
        return CurrentPlan;
    }

    private async Task<bool> ExecuteStep(int index)
    {
        Ensure();
        string todo = CurrentPlan[index];
        OnStepStarting?.Invoke(index);

        for (int attempt = 0; attempt <= MaxStepRetries; attempt++)
        {
            string toolName = await SelectToolName(todo, index);
            if (string.IsNullOrEmpty(toolName)) { OnWriteLine?.Invoke("Tool selection failed"); return false; }
            var tool = _toolsRegistry.FirstOrDefault(t => t.Name == toolName);
            if (tool == null) { OnWriteLine?.Invoke($"Unknown tool: {toolName}"); return false; }
            OnWriteLine?.Invoke($"> {tool.DisplayName} ({tool.Name})");

            var paramVals = new Dictionary<string, object?>();
            var args = new List<object?>();
            foreach (var p in tool.Parameters) { var v = await FillParam(tool, p, todo, index); args.Add(v); paramVals[p.Name] = v; }

            OnWrite?.Invoke("Executing... ");
            string result;
            try { var obj = tool.Method.Invoke(_tools, args.ToArray())!; result = obj is Task<string> t ? await t : obj?.ToString() ?? ""; }
            catch (Exception ex) { OnWriteLine?.Invoke($"\nException: {ex.Message}"); _lastError = $"Exception: {ex.Message}"; continue; }
            OnWriteLine?.Invoke("Done");
            if (result.Contains("[HTTP "))
            {
                OnWriteLine?.Invoke(result);
                _lastError = result;
                OnStepCompleted?.Invoke(index, false);
                return false;
            }
            OnWriteLine?.Invoke(result);

            var files = new Dictionary<string, string>();
            if (paramVals.TryGetValue("filePath", out var fp) && fp is string f && !string.IsNullOrEmpty(f))
            { var full = Path.Combine(RootDir, f); if (File.Exists(full)) try { files[f] = await File.ReadAllTextAsync(full); } catch { } }
            _completed.Add(new StepRecord(todo, toolName, paramVals, result, files));
            _fullSummary.AppendLine($"- [{toolName}] {todo}: {result}");
            foreach (var kv in files) _fullSummary.AppendLine($"  File {kv.Key}: {kv.Value.Length} chars");

            if (!files.Values.Any(v => string.IsNullOrWhiteSpace(v))) { OnStepCompleted?.Invoke(index, true); _lastError = ""; return true; }
            _completed.RemoveAt(_completed.Count - 1);
            _lastError = $"Step failed: {result}";
            OnWriteLine?.Invoke($"Quality check failed, retry ({attempt + 1}/{MaxStepRetries})");
        }
        OnStepCompleted?.Invoke(index, false);
        return false;
    }

    private async Task<string> SelectToolName(string todo, int index)
    {
        if (index < _planTools.Count && !string.IsNullOrEmpty(_planTools[index]) && _toolsRegistry.Any(t => t.Name == _planTools[index]))
            return _planTools[index];
        string state = BuildState(index);
        string prompt = $@"{state}
Choose a tool for: {todo}
Reply with ONLY the tool name.
Available: {string.Join(" ", _toolsRegistry.Select(t => t.Name))}";
        for (int r = 0; r < 3; r++)
        {
            var resp = await _ai!.ChatStreamAsync(new List<ChatMessage> { new("system", "Reply with only the tool name."), new("user", prompt) }, null, 0.2);
            resp = (resp ?? "").Trim().ToLower();
            if (string.IsNullOrWhiteSpace(resp)) continue;
            foreach (var t in _toolsRegistry) if (resp == t.Name || resp.Contains(t.Name)) return t.Name;
            if (r < 2) prompt = $"Reply with ONLY: {string.Join("/", _toolsRegistry.Select(t => t.Name))}";
        }
        return "";
    }

    private static object? ExtractParamFromStep(string step, string paramName)
    {
        var key = paramName.ToLower();
        if (key is "filepath" or "file path") key = "path";
        if (key.Contains("old")) key = "old";
        if (key.Contains("new")) key = "new";
        var m = Regex.Match(step, $@"{key}\s*=\s*""([^""]+)""", RegexOptions.IgnoreCase);
        if (m.Success) return m.Groups[1].Value;
        m = Regex.Match(step, $@"{key}\s*=\s*'([^']+)'", RegexOptions.IgnoreCase);
        if (m.Success) return m.Groups[1].Value;
        if (key == "old") { m = Regex.Match(step, @"replace\s+'([^']+)'\s+with", RegexOptions.IgnoreCase); if (m.Success) return m.Groups[1].Value; }
        if (key == "new") { m = Regex.Match(step, @"with\s+'([^']+)'", RegexOptions.IgnoreCase); if (m.Success) return m.Groups[1].Value; }
        return null;
    }

    private async Task<object?> FillParam(ToolInfo tool, ToolParameter param, string todo, int idx)
    {
        var stepVal = ExtractParamFromStep(todo, param.Name);
        if (stepVal != null) return stepVal;

        string state = BuildState(idx);
        bool isContent = param.Type == typeof(string) && param.Name.Contains("content", StringComparison.OrdinalIgnoreCase);
        string prompt = $@"{state}
Tool: {tool.Name}  Parameter: {param.Name}
Description: {param.Description}
{(isContent ? "Output the plain text content only. No shell commands, no tool calls." : $"Output ONLY the value for {param.Name}.")}";

        for (int r = 0; r < 3; r++)
        {
            string resp;
            if (isContent)
            {
                OnWriteLine?.Invoke($"\nGenerating {param.Name}...");
                resp = await _ai!.ChatStreamAsync(new List<ChatMessage> { new("system", "Output plain text."), new("user", prompt) }, d => OnWrite?.Invoke(d));
                OnWriteLine?.Invoke("");
            }
            else
            {
                string sys = param.Type == typeof(int) ? "Reply with a number." : param.Type == typeof(bool) ? "Reply true/false." : "Output ONLY the value.";
                resp = await _ai!.ChatStreamAsync(new List<ChatMessage> { new("system", sys), new("user", prompt) }, null, 0.3);
            }
            resp = (resp ?? "").Trim();
            if (string.IsNullOrWhiteSpace(resp)) { if (param.IsOptional) return param.DefaultValue; if (r < 2) continue; return param.DefaultValue; }
            resp = CleanParam(resp, param.Name, param.Type, isContent);
            if (ParseParam(resp, param.Type, out var v)) return v;
            if (r < 2) OnWriteLine?.Invoke("Parse failed, retry...");
        }
        return param.DefaultValue;
    }

    private static string CleanParam(string text, string name, Type type, bool isContent = false)
    {
        text = Regex.Replace(text, @"</?think>", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<\|?(assistant|endoftext|im_end)\|?>", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"```[\w]*\n?", ""); text = text.Trim();
        if (type == typeof(int)) { var m = Regex.Match(text, @"\d+"); return m.Success ? m.Value : "1"; }
        if (type == typeof(bool)) return text.Contains("true") ? "true" : "false";
        if (isContent)
        {
            var ls = text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0
                && !l.StartsWith("echo") && !l.StartsWith("type ") && !l.StartsWith("notepad")
                && !l.StartsWith("findstr") && !l.StartsWith("set ") && !l.StartsWith("```")
                && !l.StartsWith("Step") && !Regex.IsMatch(l, @"^\w+\(")).ToList();
            return ls.Count > 0 ? string.Join("\n", ls) : text;
        }
        if (name.Contains("text", StringComparison.OrdinalIgnoreCase)) { return Regex.Replace(text, @"^[^a-zA-Z0-9]*", "").Trim(); }
        if (name.Contains("path", StringComparison.OrdinalIgnoreCase) || name == "filePath")
        {
            var ls = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var line in ls.Reverse())
            {
                var t = line.Trim('"', ' ', '\t', '\r', '>', '<');
                if (t.Length > 0 && t.Length < 50 && !t.Contains(' ')) return t;
            }
            return ls.Length > 0 ? ls[^1].Trim('"') : text;
        }
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var line in lines) { var t = line.Trim('"'); if (t.Length > 0 && t.Length < 100) return t; }
        return lines.Length > 0 ? lines[^1].Trim('"') : text;
    }

    private static bool ParseParam(string input, Type type, out object? result)
    {
        result = null;
        if (type == typeof(string)) { result = input; return true; }
        if (type == typeof(int) && int.TryParse(input, out var i)) { result = i; return true; }
        if (type == typeof(bool)) { result = input is "true" or "True"; return true; }
        return false;
    }

    public async Task RunAsync()
    {
        _fullSummary.Clear();
        for (int retry = 0; retry <= MaxRetries; retry++)
        {
            if (retry > 0) OnWriteLine?.Invoke($"\nRe-planning ({retry}/{MaxRetries})...");
            await GeneratePlanAsync();
            if (CurrentPlan.Count == 0) break;
            bool ok = true;
            for (int i = 0; i < CurrentPlan.Count; i++) if (!await ExecuteStep(i)) { ok = false; break; }
            if (ok) { _lastSummary = _fullSummary.ToString(); OnAllCompleted?.Invoke(); return; }
        }
        _lastSummary = _fullSummary.ToString();
        OnAllCompleted?.Invoke();
    }

    public void Dispose() { _ai?.Dispose(); _ai = null; _tools = null; }
}
