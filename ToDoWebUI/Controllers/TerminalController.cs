using Microsoft.AspNetCore.Mvc;
using ToDoAI;

namespace ToDoWebUI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TerminalController : ControllerBase
{
    [HttpPost("run")]
    public async Task<IActionResult> RunCommand([FromBody] RunCommandRequest req)
    {
        var root = ProjectController.RootDir;
        if (string.IsNullOrEmpty(root)) return BadRequest("No project directory set");
        var tools = new AgentTools(root);
        var result = await tools.Run(req.Command);
        return Ok(new { output = result });
    }
}

public record RunCommandRequest(string Command);
