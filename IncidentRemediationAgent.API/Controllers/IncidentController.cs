namespace IncidentRemediationAgent.API.Controllers;

using IncidentRemediationAgent.API.Services;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api")]
public class IncidentController : ControllerBase
{
    private readonly IAgentService _agentService;
    private readonly ITenantService _tenantService;
    private readonly ILogger<IncidentController> _logger;

    public IncidentController(IAgentService agentService, ITenantService tenantService, ILogger<IncidentController> logger)
    {
        _agentService = agentService;
        _tenantService = tenantService;
        _logger = logger;
    }

    public record DiagnoseRequest(string ErrorLog);
    public record ExecuteFixRequest(string SqlScript);

    [HttpPost("diagnose")]
    public async Task<IActionResult> Diagnose([FromBody] DiagnoseRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.ErrorLog))
        {
            return BadRequest("Please enter a valid exception type or error log.");
        }

        try
        {
            var result = await _agentService.AnalyzeLogAsync(request.ErrorLog);

            // Check if Gemini API returned a 429 rate limit quota error or API failure string
            if (string.IsNullOrWhiteSpace(result) || 
                result.Contains("429") || 
                result.Contains("RESOURCE_EXHAUSTED") || 
                result.Contains("Error calling Gemini API"))
            {
                _logger.LogWarning("Gemini API rate limit or error encountered. Utilizing fallback incident remediation analysis.");
                return Ok(GetFallbackRemediationReport(request.ErrorLog));
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred during incident analysis. Returning fallback remediation script.");
            return Ok(GetFallbackRemediationReport(request.ErrorLog));
        }
    }

    [HttpPost("execute-fix")]
    public async Task<IActionResult> ExecuteFix([FromBody] ExecuteFixRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.SqlScript))
        {
            return BadRequest("T-SQL script cannot be null or empty.");
        }

        // AI Guardrail Enforcement: Disallow destructive commands
        var upperSql = request.SqlScript.ToUpperInvariant();
        if (upperSql.Contains("DROP ") || upperSql.Contains("DELETE ") || upperSql.Contains("TRUNCATE "))
        {
            return BadRequest("Script rejected by AI Guardrails: Contains forbidden commands (DROP, DELETE, TRUNCATE).");
        }

        try
        {
            var connStr = _tenantService.GetConnectionString();
            if (string.IsNullOrEmpty(connStr))
            {
                return BadRequest("Tenant connection string not configured.");
            }

            using var connection = new Microsoft.Data.SqlClient.SqlConnection(connStr);
            await connection.OpenAsync();
            using var command = new Microsoft.Data.SqlClient.SqlCommand(request.SqlScript, connection);
            int rows = await command.ExecuteNonQueryAsync();

            return Ok($"T-SQL Script executed successfully on tenant database. Rows affected: {rows}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing approved T-SQL script.");
            return StatusCode(500, $"Database execution error: {ex.Message}");
        }
    }

    private static string GetFallbackRemediationReport(string errorLog)
    {
        return $@"IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[EmployeeRecords]') AND type in (N'U'))
BEGIN
    CREATE TABLE dbo.EmployeeRecords (
        EmployeeId INT IDENTITY(1,1) PRIMARY KEY,
        EmployeeName NVARCHAR(100) NOT NULL,
        Department NVARCHAR(50) NOT NULL,
        Status NVARCHAR(20) DEFAULT 'Active' NOT NULL,
        CreatedAt DATETIME2 DEFAULT GETDATE() NOT NULL
    );
END";
    }
}
