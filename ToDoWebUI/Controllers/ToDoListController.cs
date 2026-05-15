using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using ToDoAI;

namespace ToDoWebUI.Controllers;

[ApiController]
[Route("api/todolist")]
public class ToDoListController : ControllerBase
{
    [HttpPost("run")]
    public async Task Run([FromBody] ToDoRunRequest req)
    {
        var root = ProjectController.RootDir;
        if (string.IsNullOrEmpty(root))
        {
            Response.Headers["Content-Type"] = "text/event-stream";
            await Response.WriteAsync($"data: {JsonSerializer.Serialize(new { type = "error", text = "No project directory set" })}\n\n");
            await Response.Body.FlushAsync();
            return;
        }

        var cfgPath = Path.Combine(AppContext.BaseDirectory, "agent_config.json");
        string apiKey = "123", model = "gemma-4-e4b-it", endpoint = "http://localhost:1234/v1/chat/completions";
        if (System.IO.File.Exists(cfgPath))
        {
            var json = System.IO.File.ReadAllText(cfgPath);
            using var doc = JsonDocument.Parse(json);
            var rootEl = doc.RootElement;
            if (rootEl.TryGetProperty("ApiKey", out var k)) apiKey = k.GetString() ?? apiKey;
            if (rootEl.TryGetProperty("Model", out var m)) model = m.GetString() ?? model;
            if (rootEl.TryGetProperty("Endpoint", out var e)) endpoint = e.GetString() ?? endpoint;
        }

        Response.Headers["Content-Type"] = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["Connection"] = "keep-alive";

        var todo = new ToDoList
        {
            ApiKey = apiKey,
            Model = model,
            Endpoint = endpoint,
            RootDir = root,
            MaxSteps = 6,
            MaxRetries = 3,
            MaxStepRetries = 3,
            ProjectDescription = req.Description
        };

        todo.OnWriteLine += s =>
        {
            try
            {
                var json = JsonSerializer.Serialize(new { type = "line", text = s });
                Response.WriteAsync($"data: {json}\n\n").Wait();
                Response.Body.FlushAsync().Wait();
            }
            catch { }
        };

        todo.OnWrite += s =>
        {
            try
            {
                var json = JsonSerializer.Serialize(new { type = "write", text = s });
                Response.WriteAsync($"data: {json}\n\n").Wait();
                Response.Body.FlushAsync().Wait();
            }
            catch { }
        };

        todo.OnPlanGenerated += steps =>
        {
            try
            {
                var json = JsonSerializer.Serialize(new { type = "plan", steps });
                Response.WriteAsync($"data: {json}\n\n").Wait();
                Response.Body.FlushAsync().Wait();
            }
            catch { }
        };

        todo.OnStepStarting += i =>
        {
            try
            {
                var json = JsonSerializer.Serialize(new { type = "step-start", index = i });
                Response.WriteAsync($"data: {json}\n\n").Wait();
                Response.Body.FlushAsync().Wait();
            }
            catch { }
        };

        todo.OnStepCompleted += (i, ok) =>
        {
            try
            {
                var json = JsonSerializer.Serialize(new { type = "step-end", index = i, ok });
                Response.WriteAsync($"data: {json}\n\n").Wait();
                Response.Body.FlushAsync().Wait();
            }
            catch { }
        };

        todo.OnAllCompleted += () =>
        {
            try
            {
                var json = JsonSerializer.Serialize(new { type = "done" });
                Response.WriteAsync($"data: {json}\n\n").Wait();
                Response.Body.FlushAsync().Wait();
            }
            catch { }
        };

        try
        {
            await todo.RunAsync();
        }
        catch (Exception ex)
        {
            try
            {
                var json = JsonSerializer.Serialize(new { type = "error", text = ex.Message });
                await Response.WriteAsync($"data: {json}\n\n");
                await Response.Body.FlushAsync();
            }
            catch { }
        }
        finally
        {
            todo.Dispose();
        }
    }
}

public record ToDoRunRequest(string Description);
