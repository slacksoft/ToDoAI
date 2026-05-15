using Microsoft.AspNetCore.Mvc;
using ToDoAI;

namespace ToDoWebUI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FileController : ControllerBase
{
    [HttpGet("read")]
    public async Task<IActionResult> ReadFile([FromQuery] string path)
    {
        var root = ProjectController.RootDir;
        if (string.IsNullOrEmpty(root)) return BadRequest("No project directory set");
        var tools = new AgentTools(root);
        var result = await tools.Read(path);
        if (result.StartsWith("ERROR")) return BadRequest(result);
        return Ok(new { content = result, path });
    }

    [HttpPost("write")]
    public async Task<IActionResult> WriteFile([FromBody] WriteFileRequest req)
    {
        var root = ProjectController.RootDir;
        if (string.IsNullOrEmpty(root)) return BadRequest("No project directory set");
        var tools = new AgentTools(root);
        var result = await tools.Write(req.Path, req.Content);
        return Ok(new { message = result });
    }

    [HttpGet("list")]
    public IActionResult ListFiles([FromQuery] string? dir)
    {
        var root = ProjectController.RootDir;
        if (string.IsNullOrEmpty(root)) return BadRequest("No project directory set");
        var target = string.IsNullOrEmpty(dir) ? root : Path.Combine(root, dir);
        if (!Directory.Exists(target)) return BadRequest("Directory not found");
        var files = Directory.GetFiles(target).Select(f => new
        {
            name = Path.GetFileName(f),
            path = Path.GetRelativePath(root, f),
            isDirectory = false
        });
        var dirs = Directory.GetDirectories(target).Select(d => new
        {
            name = Path.GetFileName(d),
            path = Path.GetRelativePath(root, d),
            isDirectory = true
        });
        return Ok(new { items = dirs.Concat(files).OrderBy(x => !x.isDirectory).ThenBy(x => x.name) });
    }
}

public record WriteFileRequest(string Path, string Content);
