using Microsoft.AspNetCore.Mvc;
using ToDoAI;

namespace ToDoWebUI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProjectController : ControllerBase
{
    public static string RootDir { get; private set; } = "";
    private static AgentTools? _tools;

    [HttpPost("set-directory")]
    public IActionResult SetDirectory([FromBody] SetDirRequest req)
    {
        if (!Directory.Exists(req.Path)) return BadRequest("Directory not found");
        RootDir = req.Path;
        _tools = new AgentTools(RootDir);
        return Ok(new { root = RootDir });
    }

    [HttpGet("tree")]
    public IActionResult GetTree([FromQuery] string? dir)
    {
        if (string.IsNullOrEmpty(RootDir)) return BadRequest("No project directory set");
        if (_tools == null) _tools = new AgentTools(RootDir);
        var result = _tools.Tree(dir ?? "");
        return Ok(new { tree = result });
    }
}

public record SetDirRequest(string Path);
