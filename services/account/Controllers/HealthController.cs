using Microsoft.AspNetCore.Mvc;

namespace EBanking.AccountService.Controllers;

[ApiController]
[Route("[controller]")]
public class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;

    public HealthController(ILogger<HealthController> logger)
    {
        _logger = logger;
    }

    [HttpGet]
    [Route("/healthz")]
    public IActionResult Health()
    {
        return Ok(new
        {
            status = "healthy",
            service = "account-service",
            timestamp = DateTime.UtcNow,
            version = "1.0.0"
        });
    }

    [HttpGet]
    [Route("/ready")]
    public IActionResult Ready()
    {
        // Add readiness checks here (database connectivity, etc.)
        return Ok(new
        {
            status = "ready",
            service = "account-service",
            timestamp = DateTime.UtcNow
        });
    }
}
