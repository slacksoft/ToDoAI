using System.Text;
using System.Text.Json;
using ToDoAI;

Console.OutputEncoding = Encoding.UTF8;

var cwd = Environment.CurrentDirectory;
Console.WriteLine($"Working directory: {cwd}");

var configPath = Path.Combine(AppContext.BaseDirectory, "agent_config.json");

string apiKey, model, endpoint;

if (File.Exists(configPath))
{
    var json = File.ReadAllText(configPath);
    var cfg = JsonSerializer.Deserialize<Config>(json);
    apiKey = cfg?.ApiKey ?? "123";
    model = cfg?.Model ?? "gemma-4-e4b-it";
    endpoint = cfg?.Endpoint ?? "http://localhost:1234/v1/chat/completions";
    Console.WriteLine($"Loaded config: {model} @ {endpoint}");
}
else
{
    Console.WriteLine("First launch - please configure (press Enter to use defaults):");
    Console.Write("API Key (default: 123): "); var k = Console.ReadLine()?.Trim(); apiKey = string.IsNullOrEmpty(k) ? "123" : k;
    Console.Write("Model (default: gemma-4-e4b-it): "); var m = Console.ReadLine()?.Trim(); model = string.IsNullOrEmpty(m) ? "gemma-4-e4b-it" : m;
    Console.Write("Endpoint (default: http://localhost:1234/v1/chat/completions): "); var e = Console.ReadLine()?.Trim(); endpoint = string.IsNullOrEmpty(e) ? "http://localhost:1234/v1/chat/completions" : e;
    var cfg = new Config(apiKey, model, endpoint);
    File.WriteAllText(configPath, JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"Config saved to {configPath}");
}

var todo = new ToDoList
{
    ApiKey = apiKey,
    Model = model,
    Endpoint = endpoint,
    RootDir = cwd,
    MaxSteps = 6
};

todo.OnWriteLine += s => Console.WriteLine(s);
todo.OnWrite += s => { Console.Write(s); Console.Out.Flush(); };
todo.OnPlanGenerated += steps =>
{
    Console.WriteLine($"\nPlan ({steps.Count} steps):");
    for (int i = 0; i < steps.Count; i++) Console.WriteLine($"  {i + 1}. {steps[i]}");
};
todo.OnStepStarting += i => Console.WriteLine($"\nStep {i + 1}...");
todo.OnStepCompleted += (i, ok) => Console.WriteLine(ok ? "Done" : "Failed");
todo.OnAllCompleted += () => Console.WriteLine($"\nProject completed: {todo.RootDir}");

while (true)
{
    Console.Write("\nRequest (two blank lines to submit, one blank line to quit): ");
    var lines = new List<string>();
    int blanks = 0;
    while (blanks < 1)
    {
        string? line = Console.ReadLine();
        if (line == null) break;
        if (string.IsNullOrEmpty(line)) { blanks++; if (blanks < 2) lines.Add(""); }
        else { blanks = 0; lines.Add(line); }
    }
    string desc = string.Join('\n', lines).Trim();
    if (string.IsNullOrEmpty(desc)) break;
    todo.ProjectDescription = desc;
    await todo.RunAsync();
}

record Config(string ApiKey, string Model, string Endpoint);
