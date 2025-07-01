using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SignDocumentService.Dto.Request;
using SignDocumentService.Dto.Response;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace SigningService.Controllers
{
    [ApiController]
    [Route("api/signDocument")]
    //[Authorize]
    public class DocumentoController : ControllerBase
    {
        private readonly ILogger<DocumentoController> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;

        public DocumentoController(ILogger<DocumentoController> logger,
                                    IHttpClientFactory httpClientFactory,
                                    IConfiguration config)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _config = config;
        }

        [HttpPost("rutas")]
        public async Task<IActionResult> ObtenerRutas([FromBody] GenericRequest dataRequest)
        {
            try
            {
                var orionUrl = _config["CoreService:BaseUrl"];
                // Llamamos a Orion en /api/signDocument/rutas
                var downstreamUrl = new Uri(new Uri(orionUrl), "/api/signDocument/rutas");

                var client = _httpClientFactory.CreateClient();
                var resp = await client.PostAsJsonAsync(downstreamUrl, dataRequest);

                if (!resp.IsSuccessStatusCode)
                    return StatusCode((int)resp.StatusCode);

                var rutas = await resp.Content.ReadFromJsonAsync<List<RutasDocumentoResponse>>();
                return Ok(rutas);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obtaining routes");
                return StatusCode(500, "Internal server error.");
            }
        }




        [HttpPost("sign")]
        public async Task<IActionResult> SignDocument([FromBody] SignRequest request)
        {
            try
            {
                var svcBase = _config["SigningService:BaseUrl"];
                var client = _httpClientFactory.CreateClient();
                var rutasUrl = new Uri(new Uri(svcBase), "/api/signDocument/rutas");

                var genReq = new GenericRequest
                {
                    UserName = request.UserName,
                    SessionID = request.SessionID,
                    Data = new { Solicitud = request.Solicitud, Lote = request.Lote },
                    Dispositivo = "ORIGINAL-BACKEND",
                    Lat = -55888,
                    Lon=55555
                  
                };

                var reqMessage = new HttpRequestMessage(HttpMethod.Post, rutasUrl)
                {
                    Content = JsonContent.Create(genReq)
                };

                // Pasa el token actual como Bearer si existe
                var token = HttpContext.Request.Headers["Authorization"].ToString();
                if (!string.IsNullOrWhiteSpace(token))
                {
                    reqMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Replace("Bearer ", ""));
                }

                var resp = await client.SendAsync(reqMessage);
                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Error fetching routes: {Status}", resp.StatusCode);
                    return StatusCode((int)resp.StatusCode);
                }

                var rutas = await resp.Content.ReadFromJsonAsync<List<RutasDocumentoResponse>>();

                _logger.LogInformation("Obtained {Count} documents to sign", rutas?.Count);

                return Ok(rutas);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error signing documents");
                return StatusCode(500, "Internal server error.");
            }
        }


        [HttpPost("test")]
        [Authorize]
        public IActionResult ProbarConexion([FromBody] SignRequest request)
        {
            var token = HttpContext.Request.Headers["Authorization"].ToString();
            _logger.LogInformation("🔐 Token recibido: {token}", token);

            var response = new GenericResponse
            {
                CodeReturn = 1,
                Message = "Petición recibida exitosamente en SignService",
                Result = $"Solicitud: {request.Solicitud}, Lote: {request.Lote}, Usuario: {request.UserName}, SessionID: {request.SessionID}"
            };

            return Ok(response);


        }

    }
}
