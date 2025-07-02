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
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

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
        public async Task<ActionResult<List<RutasDocumentoResponse>>> ObtenerRutas([FromBody] GenericRequest request)
        {
            try
            {
                var rutas = await ObtenerRutasDesdeSp(request);
                return Ok(rutas);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener rutas");
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

      
        [HttpPost("firmar-rutas")]
        public async Task<IActionResult> FirmarRutasSignBox([FromBody] GenericRequest request)
        {
            try
            {
                // Reutiliza la lógica: obtén las rutas
                var rutas = await ObtenerRutasDesdeSp(request);

                if (rutas == null || !rutas.Any())
                    return Ok(new GenericResponse
                    {
                        CodeReturn = 1,
                        Message = "No hay rutas para firmar",
                        Result = null
                    });

                // Envía cada ruta a SignBox
                foreach (var doc in rutas)
                {
                    var signBoxReq = new
                    {
                        solicitud = doc.Solicitud,
                        lote = doc.Lote,
                        codigo = doc.CodigoDocumento,
                        ruta = doc.RutaArchivo
                    };

                    var client = _httpClientFactory.CreateClient();
                    var response = await client.PostAsJsonAsync("https://api.signbox.ec/firmar", signBoxReq);

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogError("Fallo al firmar {Doc}: {Status}", doc.CodigoDocumento, response.StatusCode);
                        return StatusCode((int)response.StatusCode,
                            $"Error al firmar documento {doc.CodigoDocumento}");
                    }
                }

                return Ok(new GenericResponse
                {
                    CodeReturn = 1,
                    Message = "Todos los documentos enviados a SignBox correctamente",
                    Result = null
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en FirmarRutasSignBox");
                return StatusCode(500, new GenericResponse
                {
                    CodeReturn = -1,
                    Message = ex.Message,
                    Result = null
                });
            }
        }

        //?Método obtener rutas desde el sp.
        private async Task<List<RutasDocumentoResponse>> ObtenerRutasDesdeSp(GenericRequest request)
        {
            var connStr = _config.GetConnectionString("Default");
            using var conn = new SqlConnection(connStr);
            using var cmd = new SqlCommand("BancaVirtual.spGenericoExecute", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            // Parámetros de entrada
            cmd.Parameters.AddWithValue("@_UserName", request.UserName ?? "");
            cmd.Parameters.AddWithValue("@_SessionID", request.SessionID);
            cmd.Parameters.AddWithValue("@Action", request.Action ?? "credito-web/obtener-url-documentos");
            cmd.Parameters.AddWithValue("@Request", request.Data ?? "");
            cmd.Parameters.AddWithValue("@_Lat", string.IsNullOrEmpty(request.Lat) ? DBNull.Value : (object)request.Lat);
            cmd.Parameters.AddWithValue("@_Lon", string.IsNullOrEmpty(request.Lon) ? DBNull.Value : (object)request.Lon);
            cmd.Parameters.AddWithValue("@Dispositivo", string.IsNullOrEmpty(request.Dispositivo) ? DBNull.Value : (object)request.Dispositivo);

            // Parámetros de salida
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

            if (codeReturn != 1)
                throw new InvalidOperationException($"SP falló: {message}");

            if (string.IsNullOrWhiteSpace(json))
                return new List<RutasDocumentoResponse>();

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                NumberHandling = JsonNumberHandling.AllowReadingFromString
            };


            var rutas = JsonSerializer.Deserialize<List<RutasDocumentoResponse>>(json, options)
            ?? new List<RutasDocumentoResponse>();

            return rutas;
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
