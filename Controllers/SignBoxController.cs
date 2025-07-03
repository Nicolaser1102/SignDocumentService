using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using SignDocumentService.Dto.Response;

namespace SignDocumentService.Controllers
{
    [ApiController]
    [Route("api/signbox")]
    public class SignBoxController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<SignBoxController> _logger;
        private readonly IConfiguration _config;

        public SignBoxController(IHttpClientFactory httpClientFactory, ILogger<SignBoxController> logger, IConfiguration config)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _config = config;
        }

        [HttpPost("auth")]
        public async Task<IActionResult> Autenticar()
        {
            try
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
                    _logger.LogError("🔴 Error autenticando en SignBox: {Status} - {Error}", response.StatusCode, error);
                    return StatusCode((int)response.StatusCode, new GenericResponse
                    {
                        CodeReturn = -1,
                        Message = "Falló la autenticación en SignBox",
                        Result = error
                    });
                }

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<SignBoxTokenResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return Ok(new GenericResponse
                {
                    CodeReturn = 1,
                    Message = "Autenticación exitosa",
                    Result = result?.Token
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Excepción autenticando en SignBox");
                return StatusCode(500, new GenericResponse
                {
                    CodeReturn = -1,
                    Message = ex.Message,
                    Result = null
                });
            }
        }
    }
}
