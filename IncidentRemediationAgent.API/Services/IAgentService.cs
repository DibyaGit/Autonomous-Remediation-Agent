namespace IncidentRemediationAgent.API.Services;

public interface IAgentService
{
    Task<string> AnalyzeLogAsync(string errorLog);
}
