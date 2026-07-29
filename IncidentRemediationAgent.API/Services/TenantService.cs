namespace IncidentRemediationAgent.API.Services;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

public interface ITenantService
{
    string GetConnectionString();
}

public class TenantService : ITenantService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;

    public TenantService(IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
    {
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
    }

    public string GetConnectionString()
    {
        // 1. Get the Tenant ID from the Angular frontend request header
        var tenantId = _httpContextAccessor.HttpContext?.Request.Headers["X-Tenant-ID"].ToString();

        // 2. Route to the correct database based on the Tenant ID
        if (tenantId == "client-wipro")
        {
            var wiproConn = _configuration.GetConnectionString("WiproConnection");
            if (!string.IsNullOrEmpty(wiproConn)) return wiproConn;
            return "Server=DIBYA-IT\\SQLEXPRESS;Database=WiproDb;Trusted_Connection=True;TrustServerCertificate=True;";
        }

        return _configuration.GetConnectionString("DefaultConnection") 
            ?? "Server=DIBYA-IT\\SQLEXPRESS;Database=IncidentAgentDb;Trusted_Connection=True;TrustServerCertificate=True;";
    }
}