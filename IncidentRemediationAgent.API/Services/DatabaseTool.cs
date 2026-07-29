namespace IncidentRemediationAgent.API.Services;

using System.Data;
using Microsoft.Data.SqlClient;

public class DatabaseTool
{
    private readonly string _connectionString;

    public DatabaseTool(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? configuration["ConnectionStrings:DefaultConnection"] 
            ?? string.Empty;
    }

    public async Task<string> GetErrorLogDetailsAsync(string errorType)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return "Connection string 'DefaultConnection' is not configured.";
        }

        // Strict SQL Parameterization: No string concatenation (+) or interpolation ($) used
        const string query = "SELECT TOP 1 Message, StackTrace FROM ErrorLogs WHERE ErrorType = @errorType";

        using var connection = new SqlConnection(_connectionString);
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
