using System.Text.Json.Serialization;

public class SignBoxTokenResponse
{
    [JsonPropertyName("id_token")]
    public string Token { get; set; }
}