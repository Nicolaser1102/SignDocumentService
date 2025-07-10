using Azure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SignBoxWorkerService.Services;
using SignDocumentService.Dto.Request;
using SignDocumentService.Dto.Response;
using SignDocumentService.Services;
using System;
using System.Collections.Generic;
using System.Data;
using System.Net.Http;
using System.Net.Http.Headers;
using System.ServiceProcess;
using System.Text.Json;
using System.Text.Json.Serialization;

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
        private readonly ISignBoxService _signBoxService;
        private readonly SignBoxStatusChecker _signBoxStatusChecker;



        public DocumentoController(ILogger<DocumentoController> logger,
                                    IHttpClientFactory httpClientFactory,
                                    IConfiguration config,
                                    ISignBoxService signBoxService,
                                    SignBoxStatusChecker signBoxStatusChecker)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _config = config;
            _signBoxService = signBoxService;
            _signBoxStatusChecker = signBoxStatusChecker;
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



        [HttpPost("firmar-documentos")]
        //[Authorize]
        public async Task<IActionResult> FirmarDocumentoConTokenSignBoxFunciones([FromBody] GenericRequest request)
        {
            try
            {
                var rutas = await ObtenerRutasDesdeSp(request);

                if (rutas == null || !rutas.Any())
                    return Ok(new GenericResponse
                    {
                        CodeReturn = 1,
                        Message = "No hay rutas para firmar",
                        Result = null
                    });

                // 1. Obtener token usando el servicio inyectado
                var token = await _signBoxService.ObtenerTokenAsync();
                if (string.IsNullOrEmpty(token))
                    return StatusCode(500, new GenericResponse
                    {
                        CodeReturn = -1,
                        Message = "No se pudo autenticar con SignBox",
                        Result = null
                    });

                // 2. Firmar cada documento


                // ...
                foreach (var doc in rutas)
                {
                    var resultado = await FirmarDocumentoAsync(doc, token, "71,473,201,522", 2); // posición ejemplo

                    if (resultado == null)
                    {
                        _logger.LogWarning("❌ Fallo al firmar documento: {Codigo}", doc.CodigoDocumento);

                        await InsertarRegistroDocumentoAsync(doc, new SignBoxSignResponse
                        {
                            Result = false,
                            Status = "400",
                            Detail = "Error al firmar documento o respuesta nula",
                            WebhookTxt = "",
                            WebhookPdf = ""
                        });

                        continue;
                    }

                    await InsertarRegistroDocumentoAsync(doc, resultado);
                }

                // <<< AQUÍ: revisar los pendientes tras el proceso principal
                await _signBoxStatusChecker.EjecutarRevisionAsync(CancellationToken.None);
                _logger.LogInformation("🔁 Revisión de firmas ejecutada automáticamente después de firmar.");


                // Iniciar el servicio si no estaba activo
                var serviceManager = new WorkerService("SignBoxWorkerService");
                if (serviceManager.ObtenerEstado() != ServiceControllerStatus.Running)
                {
                    serviceManager.IniciarServicio();
                    _logger.LogInformation("Servicio SignBoxWorkerService iniciado.");
                }
                else
                {
                    _logger.LogInformation("Servicio SignBoxWorkerService ya estaba corriendo.");
                }



                return Ok(new GenericResponse
                {
                    CodeReturn = 1,
                    Message = "Todos los documentos procesados y worker iniciado",
                    Result = null
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en FirmarDocumentoConTokenSignBoxFunciones");
                return StatusCode(500, new GenericResponse
                {
                    CodeReturn = -1,
                    Message = ex.Message,
                    Result = null
                });
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


        [HttpPost("reintentar-firmas")]
        [Authorize]
        public async Task<IActionResult> ReintentarFirmasDesdeAPI()
        {
            try
            {
                await _signBoxStatusChecker.EjecutarRevisionAsync(CancellationToken.None);

                return Ok(new
                {
                    Message = "✅ Revisión de firmas ejecutada desde API"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error al ejecutar reintento de firmas desde API");

                return StatusCode(500, new
                {
                    Message = "❌ Error ejecutando lógica del worker",
                    Error = ex.Message
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


        //Metodo firmar documentos 
        private async Task<SignBoxSignResponse?> FirmarDocumentoAsync(RutasDocumentoResponse doc, string token, string posicion, int pagina)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();

                // 1) Preparar el multipart
                using var content = new MultipartFormDataContent();

                // a) Documento PDF
                // a) Documento PDF
                if (!System.IO.File.Exists(doc.RutaArchivo))
                {
                    Console.WriteLine($"Ruta del archivo PDF: {doc.RutaArchivo}");
                    _logger.LogError("❌ El PDF no existe: {Ruta}", doc.RutaArchivo);


                    //Descomentar
                    //return null;
                    doc.RutaArchivo = null;
                }

                var pdfStream = System.IO.File.OpenRead(doc.RutaArchivo);
                content.Add(new StreamContent(pdfStream), "fileIn", Path.GetFileName(doc.RutaArchivo));

                // b) Imagen de firma (Base64 en string)
                var imagePath = @"C:\DocumentosPruebaFirmaElectronica\25\firmaPruebaIA.png";
                if (System.IO.File.Exists(imagePath))
                {
                    var imgBytes = await System.IO.File.ReadAllBytesAsync(imagePath);
                    var imageBase64 = Convert.ToBase64String(imgBytes);
                    // Se envía como StringContent, no como StreamContent
                    content.Add(new StringContent(imageBase64), "image");
                }
                else
                {
                    _logger.LogWarning("⚠️ Imagen no encontrada: {Path}", imagePath);
                }

                // c) Campos simples
                content.Add(new StringContent($"pruebaGreensoft1"), "webhookId");
                //Descomentar
                //content.Add(new StringContent("1091583"), "username");
                //content.Add(new StringContent("RY3qn76H"), "password");


                content.Add(new StringContent("Javier123_"), "pin");
                content.Add(new StringContent("Firma de contrato"), "reason");
                content.Add(new StringContent("Quito"), "location");
                content.Add(new StringContent(posicion), "position");
                content.Add(new StringContent(pagina.ToString()), "npage");

                // d) ParagraphFormat como JSON en StringContent
                var pf = "[{ " +
                                "\"font\": [\"Universal-Bold\",6]," +
                                "\"align\": \"right\"," +
                                "\"data_format\": { \"timezone\": \"America/Guayaquil\", \"strtime\": \"%d/%m/%Y %H:%M:%S\" }," +
                                "\"format\": [" +
                                    "\"Firmado por:\"," +
                                    "\"$(CN)s\"," +
                                    "\"ID: $(serialNumber)s\"," +
                                    "\"Oficial de crédito\"" +  // ← al final del array
                                "]" +
                            "}]";
                content.Add(new StringContent(pf), "paragraphFormat");

                // 2) Autenticación
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // 3) Envío
                var response = await client.PostAsync("https://eclipsoft.dev/signbox/api/sign", content);
                var respBody = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("📄 Respuesta firma {Doc}: {Resp}", doc.CodigoDocumento, respBody);

                // 4) Deserializar JSON siempre, incluso si HTTP falla
                SignBoxSignResponse? signResp = null;

                try
                {
                    signResp = JsonSerializer.Deserialize<SignBoxSignResponse>(respBody, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                catch (Exception jsonEx)
                {
                    _logger.LogError(jsonEx, "❌ Error deserializando respuesta JSON para {Doc}", doc.CodigoDocumento);
                }

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("❌ HTTP {Status} al firmar {Doc}. Respuesta: {Body}", response.StatusCode, doc.CodigoDocumento, respBody);
                    return signResp;
                }
                // 5)Validar contenido

                var ok = signResp?.Result == true
                      && signResp.Status?.StartsWith("200") == true
                      && !string.IsNullOrWhiteSpace(signResp.WebhookPdf);

                if (ok)
                    _logger.LogInformation("✅ Documento firmado: {Pdf}", signResp.WebhookPdf);
                else
                    _logger.LogWarning("⚠️ Firma no confirmada: {Detail}", signResp?.Detail);

                return signResp; ;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Excepción firmando {Doc}", doc.CodigoDocumento);
                return null;
            }
        }


        private async Task InsertarRegistroDocumentoAsync(RutasDocumentoResponse doc, SignBoxSignResponse response)
        {
            var connStr = _config.GetConnectionString("Default");

            using var conn = new SqlConnection(connStr);
            using var cmd = new SqlCommand(@"
        INSERT INTO PARAMETROS..RE_DOCUMENTOS_SIGNBOX 
        (Solicitud, Lote, CodigoDocumento, WebhookTxt, WebhookPdf, DetailId, Estado, Intentos, FechaRegistro)
        VALUES (@Solicitud, @Lote, @Codigo, @WebhookTxt, @WebhookPdf, @Detail, @Estado, 0, GETDATE())
    ", conn);

            cmd.Parameters.AddWithValue("@Solicitud", doc.Solicitud);
            cmd.Parameters.AddWithValue("@Lote", doc.Lote);
            cmd.Parameters.AddWithValue("@Codigo", doc.CodigoDocumento);
            cmd.Parameters.AddWithValue("@WebhookTxt", (object?)response.WebhookTxt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@WebhookPdf", (object?)response.WebhookPdf ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Detail", (object?)response.Detail ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Estado", response.Result && response.Status.StartsWith("200") ? "OK" : "PENDIENTE");

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

    }
}