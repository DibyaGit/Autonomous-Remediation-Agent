namespace IncidentRemediationAgent.API.Services;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

public class GeminiAgentService : IAgentService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly DatabaseTool _databaseTool;

    public GeminiAgentService(HttpClient httpClient, IConfiguration configuration, DatabaseTool databaseTool)
    {
        _httpClient = httpClient;
        _apiKey = configuration["GeminiAI:ApiKey"] ?? string.Empty;
        _databaseTool = databaseTool;
    }

    public async Task<string> AnalyzeLogAsync(string errorLog)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            return "Gemini API key is not configured.";
        }

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-3.6-flash:generateContent?key={_apiKey}";

        var systemInstruction = new
        {
            parts = new[]
            {
                new { text = "You are an Enterprise Autonomous .NET Database Remediation Agent. CRITICAL RULES: 1. You only generate T-SQL scripts. 2. You are strictly forbidden from using DROP, DELETE, or TRUNCATE commands. 3. You must wrap all table creations in 'IF NOT EXISTS'. 4. Output ONLY valid, executable T-SQL code without markdown or conversational text." }
            }
        };

        var tools = new[]
        {
            new
            {
                functionDeclarations = new[]
                {
                    new
                    {
                        name = "GetErrorLogDetails",
                        description = "Fetches error log details (Message and StackTrace) from the database for a given error type.",
                        parameters = new
                        {
                            type = "OBJECT",
                            properties = new
                            {
                                errorType = new
                                {
                                    type = "STRING",
                                    description = "The type of error to look up, e.g. NullReferenceException"
                                }
                            },
                            required = new[] { "errorType" }
                        }
                    }
                }
            }
        };

        var userMessage = new
        {
            role = "user",
            parts = new[]
            {
                new { text = errorLog }
            }
        };

        var firstRequestBody = new
        {
            systemInstruction = systemInstruction,
            tools = tools,
            contents = new object[] { userMessage }
        };

        try
        {
            var firstResponseJson = await PostJsonAsync(url, firstRequestBody);
            using var doc1 = JsonDocument.Parse(firstResponseJson);
            var root1 = doc1.RootElement;

            if (root1.TryGetProperty("error", out var apiError1))
            {
                return $"Error calling Gemini API: {apiError1.GetRawText()}";
            }

            if (!root1.TryGetProperty("candidates", out var candidates1) || candidates1.GetArrayLength() == 0)
            {
                return "No response candidates returned from Gemini.";
            }

            var firstCandidateContent = candidates1[0].GetProperty("content");
            var parts1 = firstCandidateContent.GetProperty("parts");

            JsonElement? functionCallElement = null;
            string? textResponse = null;

            foreach (var part in parts1.EnumerateArray())
            {
                if (part.TryGetProperty("functionCall", out var fc))
                {
                    functionCallElement = fc;
                }
                if (part.TryGetProperty("text", out var t))
                {
                    textResponse = t.GetString();
                }
            }

            // If no function call requested, return direct response
            if (!functionCallElement.HasValue)
            {
                return textResponse ?? "No response text received from AI.";
            }

            // Process Function Call
            var fcObj = functionCallElement.Value;
            var functionName = fcObj.GetProperty("name").GetString();
            var args = fcObj.GetProperty("args");

            string toolResult;
            if (functionName == "GetErrorLogDetails")
            {
                string errorType = args.TryGetProperty("errorType", out var etProp) 
                    ? etProp.GetString() ?? errorLog 
                    : errorLog;

                toolResult = await _databaseTool.GetErrorLogDetailsAsync(errorType);
            }
            else
            {
                toolResult = $"Unknown function call: {functionName}";
            }

            // Step 2: Build follow-up payload with conversation history & function response
            var modelContentNode = JsonNode.Parse(firstCandidateContent.GetRawText());

            var functionResponsePart = new
            {
                functionResponse = new
                {
                    name = functionName,
                    response = new
                    {
                        name = functionName,
                        content = new
                        {
                            result = toolResult
                        }
                    }
                }
            };

            var functionResponseMessage = new
            {
                role = "user",
                parts = new object[] { functionResponsePart }
            };

            var secondRequestBody = new
            {
                systemInstruction = systemInstruction,
                tools = tools,
                contents = new object[]
                {
                    userMessage,
                    modelContentNode!,
                    functionResponseMessage
                }
            };

            var secondResponseJson = await PostJsonAsync(url, secondRequestBody);
            using var doc2 = JsonDocument.Parse(secondResponseJson);
            var root2 = doc2.RootElement;

            if (root2.TryGetProperty("error", out var apiError2))
            {
                return $"Error calling Gemini API on step 2: {apiError2.GetRawText()}";
            }

            if (!root2.TryGetProperty("candidates", out var candidates2) || candidates2.GetArrayLength() == 0)
            {
                return "No response candidates returned from Gemini on step 2.";
            }

            var parts2 = candidates2[0].GetProperty("content").GetProperty("parts");
            foreach (var part in parts2.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var t))
                {
                    return t.GetString() ?? "Empty response text.";
                }
            }

            return "No text part found in final Gemini response.";
        }
        catch (Exception ex)
        {
            return $"An error occurred during agent execution: {ex.Message}";
        }
    }

    private async Task<string> PostJsonAsync(string url, object body)
    {
        var json = JsonSerializer.Serialize(body);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(url, content);
        var responseString = await response.Content.ReadAsStringAsync();

        return responseString;
    }
}
