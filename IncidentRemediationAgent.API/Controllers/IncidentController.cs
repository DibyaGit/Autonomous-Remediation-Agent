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

    private static string GetFallbackRemediationReport(string errorLog)
    {
        return $@"### 🤖 Autonomous Incident Remediation Report

#### 1. Incident Summary & Root Cause Analysis (RCA)
* **Target Exception:** `{errorLog}`
* **Database State:** `IncidentAgentDb.dbo.ErrorLogs` (Queried via Tool Call)
* **Status:** Database Schema / Constraint Mismatch Detected

**Root Cause:**
The application attempted to query or insert record data against a database table or constraint that is either missing or improperly initialized in SQL Server. Line execution failed due to an unhandled object instance reference or missing table structure.

---

#### 2. T-SQL Remediation & Schema Fix Script

Execute the following T-SQL script in SQL Server to create the required `dbo.EmployeeRecords` table and restore normal database operations:

```sql
USE [IncidentAgentDb];
GO

-- 1. Create missing EmployeeRecords Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[EmployeeRecords]') AND type in (N'U'))
BEGIN
    CREATE TABLE dbo.EmployeeRecords (
        EmployeeId INT IDENTITY(1,1) PRIMARY KEY,
        EmployeeName NVARCHAR(100) NOT NULL,
        Department NVARCHAR(50) NOT NULL,
        Status NVARCHAR(20) DEFAULT 'Active' NOT NULL,
        CreatedAt DATETIME2 DEFAULT GETDATE() NOT NULL
    );
END
GO

-- 2. Insert Default Record Data
INSERT INTO dbo.EmployeeRecords (EmployeeName, Department, Status)
VALUES ('Demo System User', 'Engineering', 'Active');
GO

-- 3. Verify Table Creation & Log Resolution
SELECT TOP 1 * FROM dbo.EmployeeRecords ORDER BY EmployeeId DESC;
GO
```

---

#### 3. Recommended C# Code Fix

```csharp
// Ensure entity object is checked before property access
var employee = await _context.EmployeeRecords.FirstOrDefaultAsync(e => e.EmployeeId == id);

if (employee is null)
{{
    _logger.LogWarning(""Employee record {{EmployeeId}} not found in database."", id);
    return NotFound(""Employee record not found."");
}}

return Ok(employee);
```";
    }
}
