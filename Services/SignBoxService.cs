namespace SignDocumentService.Services
{
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Configuration;

    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text.Json;
    using System.Threading.Tasks;
    using global::SignDocumentService.Dto.Request;

    namespace SignDocumentService.Services
    {
        public class SignBoxService : ISignBoxService
        {
            private readonly IHttpClientFactory _httpClientFactory;
            private readonly ILogger<SignBoxService> _logger;
            private readonly IConfiguration _config;

            public SignBoxService(IHttpClientFactory httpClientFactory, ILogger<SignBoxService> logger, IConfiguration config)
            {
                _httpClientFactory = httpClientFactory;
                _logger = logger;
                _config = config;
            }

            public async Task<string> ObtenerTokenAsync()
            {
                var client = _httpClientFactory.CreateClient();

                var credentials = new
                {
                    username = "greensoft",
                    password = "greensoft"
                };

                var response = await client.PostAsJsonAsync("https://eclipsoft.dev/signbox/api/authenticate", credentials);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError("🔴 Fallo autenticación SignBox: {Status} - {Error}", response.StatusCode, error);
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<SignBoxTokenResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return result?.Token;
            }

            public async Task<bool> FirmarDocumentoAsync(string token, SignBoxSignRequest documento)
            {
                var client = _httpClientFactory.CreateClient();

                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var response = await client.PostAsJsonAsync("https://eclipsoft.dev/signbox/api/firmar", documento);

                if (!response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    _logger.LogError("❌ Error al firmar documento: {Status} - {Body}", response.StatusCode, content);
                    return false;
                }

                return true;
            }
        }
    }

}
