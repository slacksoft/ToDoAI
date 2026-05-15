using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ToDoAI;

public class AIService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _endpoint;
    private readonly string _model;

    public AIService(string apiKey, string model = "gemma-4-e4b-it",
        string endpoint = "http://localhost:1234/v1/chat/completions")
    {
        _model = model;
        _endpoint = endpoint;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        _httpClient.Timeout = TimeSpan.FromMinutes(5);
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<string> ChatAsync(List<ChatMessage> messages, double temperature = 0.7, int? maxTokens = null)
    {
        var bodyDict = new Dictionary<string, object?>
        {
            ["model"] = _model,
            ["messages"] = messages.Select(m => new { role = m.Role, content = m.Content }).ToList(),
            ["temperature"] = temperature,
            ["stream"] = false
        };

        string json = JsonSerializer.Serialize(bodyDict, JsonOpts);
        using var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint) { Content = httpContent };
            HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            if (!response.IsSuccessStatusCode)
            {
                string errBody = await response.Content.ReadAsStringAsync();
                return $"[HTTP {response.StatusCode}] {errBody}";
            }

            string responseJson = await response.Content.ReadAsStringAsync();
            try
            {
                using JsonDocument doc = JsonDocument.Parse(responseJson);
                var root = doc.RootElement;
                if (root.TryGetProperty("choices", out JsonElement choices) &&
                    choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0)
                {
                    var message = choices[0].GetProperty("message");
                    return message.GetProperty("content").GetString() ?? "";
                }
            }
            catch { }
            return $"[ERROR] Unexpected response: {responseJson[..Math.Min(500, responseJson.Length)]}";
        }
        catch (HttpRequestException ex) { return $"[HTTP ERROR] {ex.Message}"; }
        catch (TaskCanceledException) { return "[HTTP ERROR] Request timed out"; }
    }

    public async Task<string> ChatStreamAsync(List<ChatMessage> messages,
        Action<string>? onDelta = null, double temperature = 0.7, int? maxTokens = null)
    {
        var bodyDict = new Dictionary<string, object?>
        {
            ["model"] = _model,
            ["messages"] = messages.Select(m => new { role = m.Role, content = m.Content }).ToList(),
            ["temperature"] = temperature,
            ["stream"] = true
        };
        if (maxTokens.HasValue) bodyDict["max_tokens"] = maxTokens.Value;

        string json = JsonSerializer.Serialize(bodyDict, JsonOpts);
        using var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint) { Content = httpContent };
            HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                string errBody = await response.Content.ReadAsStringAsync();
                return $"[HTTP {response.StatusCode}] {errBody}";
            }

            using Stream stream = await response.Content.ReadAsStreamAsync();
            using StreamReader reader = new(stream);
            StringBuilder fullContent = new();
            while (!reader.EndOfStream)
            {
                string? line = await reader.ReadLineAsync();
                if (string.IsNullOrEmpty(line)) continue;
                if (line.StartsWith("data: "))
                {
                    string data = line[6..];
                    if (data == "[DONE]") break;
                    try
                    {
                        using JsonDocument doc = JsonDocument.Parse(data);
                        JsonElement choices = doc.RootElement.GetProperty("choices");
                        if (choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0)
                        {
                            JsonElement delta = choices[0].GetProperty("delta");
                            if (delta.TryGetProperty("content", out JsonElement text))
                            {
                                string? textStr = text.GetString();
                                if (!string.IsNullOrEmpty(textStr)) { fullContent.Append(textStr); onDelta?.Invoke(textStr); }
                            }
                        }
                    }
                    catch (JsonException) { }
                }
            }
            return fullContent.ToString();
        }
        catch (HttpRequestException ex)
        {
            return $"[HTTP ERROR] {ex.Message}";
        }
        catch (TaskCanceledException)
        {
            return "[HTTP ERROR] Request timed out";
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
