using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace ToDoWebUI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConfigController : ControllerBase
{
    private static string ConfigPath => Path.Combine(AppContext.BaseDirectory, "agent_config.json");

    [HttpGet]
    public IActionResult GetConfig()
    {
        if (!System.IO.File.Exists(ConfigPath))
            return Ok(new { apiKey = "123", model = "gemma-4-e4b-it", endpoint = "http://localhost:1234/v1/chat/completions" });

        var json = System.IO.File.ReadAllText(ConfigPath);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        return Ok(new
        {
            apiKey = root.TryGetProperty("ApiKey", out var k) ? k.GetString() : "123",
            model = root.TryGetProperty("Model", out var m) ? m.GetString() : "gemma-4-e4b-it",
            endpoint = root.TryGetProperty("Endpoint", out var e) ? e.GetString() : "http://localhost:1234/v1/chat/completions"
        });
    }

    [HttpPost]
    public IActionResult SaveConfig([FromBody] SaveConfigRequest req)
    {
        var cfg = new { ApiKey = req.ApiKey, Model = req.Model, Endpoint = req.Endpoint };
        var json = JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true });
        System.IO.File.WriteAllText(ConfigPath, json);
        return Ok(new { saved = true });
    }
}

public record SaveConfigRequest(string ApiKey, string Model, string Endpoint);
