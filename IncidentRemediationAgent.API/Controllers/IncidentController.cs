namespace IncidentRemediationAgent.API.Controllers;

using IncidentRemediationAgent.API.Services;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api")]
public class IncidentController : ControllerBase
{
    private readonly IAgentService _agentService;
    private readonly ILogger<IncidentController> _logger;

    public IncidentController(IAgentService agentService, ILogger<IncidentController> logger)
    {
        _agentService = agentService;
        _logger = logger;
    }

    public record DiagnoseRequest(string ErrorLog);

    [HttpPost("diagnose")]
    public async Task<IActionResult> Diagnose([FromBody] DiagnoseRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.ErrorLog))
        {
            return BadRequest("Error log cannot be null or whitespace.");
        }

        try
        {
            var result = await _agentService.AnalyzeLogAsync(request.ErrorLog);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred while processing the incident log.");
            return StatusCode(500, "An internal server error occurred");
        }
    }
}
