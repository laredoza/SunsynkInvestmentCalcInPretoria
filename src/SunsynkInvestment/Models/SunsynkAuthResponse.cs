using System.Text.Json.Serialization;

namespace SunsynkInvestment.Models;

public class SunsynkAuthResponse
{
    public int Code { get; set; }
    public string Msg { get; set; } = "";
    public bool Success { get; set; }
    public SunsynkAuthData Data { get; set; } = new();
}

public class SunsynkAuthData
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = "";

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = "";

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "";

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
}
