

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HackathonGateway.Contollers;

[ApiController]
[Authorize(Roles = "admin")] // Только админы
[Route("[controller]")]
[Authorize]
public class TestController: ControllerBase
{
    [HttpGet("test")]
    public async Task<IActionResult> Test()
    {
        return Ok(new { Message = "Test OK" });
    }
}