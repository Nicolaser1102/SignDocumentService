using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SignDocumentService.Dto.Request;
using SignDocumentService.Dto.Response;

using System;
using System.Collections.Generic;
using System.Data;
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
        [Authorize]
        public async Task<IActionResult> ObtenerRutas([FromBody] GenericRequest request)
        {
            try
            {
                var connStr = _config.GetConnectionString("Default");
                using var conn = new SqlConnection(connStr);
                using var cmd = new SqlCommand("BancaVirtual.spGenericoExecute", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };

                // Entradas requeridas
                cmd.Parameters.AddWithValue("@_UserName", request.UserName ?? "");
                cmd.Parameters.AddWithValue("@_SessionID", request.SessionID);
                cmd.Parameters.AddWithValue("@Action", request.Action ?? "credito-web/obtener-url-documentos");
                cmd.Parameters.AddWithValue("@Request", request.Data ?? "");

                // Entradas opcionales
                cmd.Parameters.AddWithValue("@_Lat", string.IsNullOrEmpty(request.Lat) ? DBNull.Value : request.Lat);
                cmd.Parameters.AddWithValue("@_Lon", string.IsNullOrEmpty(request.Lon) ? DBNull.Value : request.Lon);
                cmd.Parameters.AddWithValue("@Dispositivo", string.IsNullOrEmpty(request.Dispositivo) ? DBNull.Value : request.Dispositivo);

                // Salidas
                var pCode = new SqlParameter("@_CodeReturn", SqlDbType.Int) { Direction = ParameterDirection.Output };
                var pMessage = new SqlParameter("@_Message", SqlDbType.NVarChar, 200) { Direction = ParameterDirection.Output };
                var pResult = new SqlParameter("@Result", SqlDbType.NVarChar, -1) { Direction = ParameterDirection.Output };
                cmd.Parameters.Add(pCode);
                cmd.Parameters.Add(pMessage);
                cmd.Parameters.Add(pResult);

                await conn.OpenAsync();
                await cmd.ExecuteNonQueryAsync();

                var codeReturn = (int)(pCode.Value ?? -99);
                var message = pMessage.Value?.ToString();
                var json = pResult.Value?.ToString();

                _logger.LogInformation("SP ejecutado. Código: {Code} | Mensaje: {Message}", codeReturn, message);

                return Ok(new GenericResponse
                {
                    CodeReturn = codeReturn,
                    Message = message,
                    Result = json
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al ejecutar spGenericoExecute para obtener rutas");
                return StatusCode(500, new GenericResponse
                {
                    CodeReturn = -1,
                    Message = ex.Message,
                    Result = null
                });
            }
        }





        //[HttpPost("sign")]
        //public async Task<IActionResult> SignDocument([FromBody] SignRequest request)
        //{
        //    try
        //    {
        //        var svcBase = _config["SigningService:BaseUrl"];
        //        var client = _httpClientFactory.CreateClient();
        //        var rutasUrl = new Uri(new Uri(svcBase), "/api/signDocument/rutas");

        //        var genReq = new GenericRequest
        //        {
        //            UserName = request.UserName,
        //            SessionID = request.SessionID,
        //            Data = new { Solicitud = request.Solicitud, Lote = request.Lote },
        //            Dispositivo = "ORIGINAL-BACKEND",
        //            Lat = -55888,
        //            Lon=55555

        //        };

        //        var reqMessage = new HttpRequestMessage(HttpMethod.Post, rutasUrl)
        //        {
        //            Content = JsonContent.Create(genReq)
        //        };

        //        // Pasa el token actual como Bearer si existe
        //        var token = HttpContext.Request.Headers["Authorization"].ToString();
        //        if (!string.IsNullOrWhiteSpace(token))
        //        {
        //            reqMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Replace("Bearer ", ""));
        //        }

        //        var resp = await client.SendAsync(reqMessage);
        //        if (!resp.IsSuccessStatusCode)
        //        {
        //            _logger.LogWarning("Error fetching routes: {Status}", resp.StatusCode);
        //            return StatusCode((int)resp.StatusCode);
        //        }

        //        var rutas = await resp.Content.ReadFromJsonAsync<List<RutasDocumentoResponse>>();

        //        _logger.LogInformation("Obtained {Count} documents to sign", rutas?.Count);

        //        return Ok(rutas);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error signing documents");
        //        return StatusCode(500, "Internal server error.");
        //    }
        //}


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
