namespace IncidentRemediationAgent.API.Services;

using System.Data;
using Microsoft.Data.SqlClient;

public class DatabaseTool
{
    private readonly ITenantService _tenantService;

    public DatabaseTool(ITenantService tenantService)
    {
        _tenantService = tenantService;
    }

    private string ConnectionString => _tenantService.GetConnectionString();

    public async Task<string> GetErrorLogDetailsAsync(string errorType)
    {
        var connStr = ConnectionString;
        if (string.IsNullOrWhiteSpace(connStr))
        {
            return "Connection string is not configured for the active tenant.";
        }

        // Strict SQL Parameterization: No string concatenation (+) or interpolation ($) used
        const string query = "SELECT TOP 1 Message, StackTrace FROM ErrorLogs WHERE ErrorType = @errorType";

        using var connection = new SqlConnection(connStr);
        using var command = new SqlCommand(query, connection);
        
        // Explicitly typed parameter to guarantee protection against SQL injection
        command.Parameters.Add("@errorType", SqlDbType.NVarChar, 255).Value = (object?)errorType ?? DBNull.Value;

        try
        {
            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                var message = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                var stackTrace = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);

                return $"Message: {message}, StackTrace: {stackTrace}";
            }

            return "No logs found for this error type.";
        }
        catch (Exception ex)
        {
            return $"An error occurred while querying the database: {ex.Message}";
        }
    }
}
