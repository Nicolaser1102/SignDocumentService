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
using SignDocumentService.Services.Interfaces;
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


        //Metodo que va a realizar la firma de documentos


        [HttpPost("firmar-documentos")]
        [Authorize]
        public async Task<IActionResult> FirmarDocumentoConTokenSignBoxFunciones([FromBody] GenericRequest request)
        {
            try
            {

                //**************************************** INICIO SIGN BOX FIRMADO ******************************************//


                // 1. Obtener rutas
                var rutas = await _signBoxService.ObtenerRutasDesdeSp(request);
                if (rutas == null || !rutas.Any())
                {
                    return StatusCode(500,new GenericResponse
                    {
                        CodeReturn = -1,
                        Message = "No se encontraron rutas de documentos para firmar",
                        Result = "NO_DOCUMENTS"
                    });
                }



                // 2. Obtener token usando el servicio inyectado
                var token = await _signBoxService.ObtenerTokenSignBoxAsync();
                if (string.IsNullOrEmpty(token))
                    return StatusCode(500, new GenericResponse
                    {
                        CodeReturn = -2,
                        Message = "No se pudo autenticar con SignBox",
                        Result = "FALLO AL OBTENER TOKEN SIGNBOX"
                    });


                // 3. Firmar cada documento
                var resultadoFirmarLote = await _signBoxService.FirmarLoteDocumentosAsync(rutas,token);
                if (resultadoFirmarLote.CodeReturn != 1)
                {
                    return StatusCode(500,resultadoFirmarLote); 
                }

                //4. Iniciar worker SignBoxWorkerService
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

                //5.Revisar los documentos pendientes tras el proceso principal
                await _signBoxStatusChecker.EjecutarRevisionAsync(CancellationToken.None);
                _logger.LogInformation("🔁 Revisión de firmas ejecutada automáticamente después de firmar.");


                //**************************************** FIN SIGN BOX FIRMADO ******************************************


                // ————————————————
                // A. Extraer idSolicitud y lote de request.Data
                using var docData = JsonDocument.Parse(request.Data ?? "{}");
                var root = docData.RootElement;
                var idSol = root.GetProperty("idSolicitud").GetInt32();
                var lote = root.GetProperty("lote").GetInt32();
                Console.WriteLine($"idSol: {idSol}, lote: {lote}");


                // B. Comprobar si quedan pendientes para esta solicitud/lote
                bool quedanPendientes = await _signBoxStatusChecker
                    .HayPendientesPorSolicitudAsync(idSol.ToString(), lote);

                if (!quedanPendientes)
                {
                    _logger.LogInformation("✅ Todos los documentos OK para Solicitud {S}, Lote {L}. Llamando a Onboarding…",
                        idSol, lote);

                    // C. Invocar al servicio de Onboarding
                    //var exitoOnb = await _onboarding.EnviarAServicioAsync(idSol, lote);
                    //if (!exitoOnb)
                       // _logger.LogWarning("⚠️ Onboarding falló para Solicitud {S}, Lote {L}", idSol, lote);
                }
                else
                {
                    _logger.LogInformation("🔁 Aún hay documentos pendientes para Solicitud {S}, Lote {L}", idSol, lote);
                }
                // ————————————————



                return Ok(new GenericResponse
                {
                    CodeReturn = 1,
                    Message = "Todos los documentos procesados y worker iniciado",
                    Result = null
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error inesperado en FirmarDocumentoConTokenSignBoxFunciones");

                return StatusCode(500, new GenericResponse
                {
                    CodeReturn = -99,
                    Message = "Error inesperado en el servidor",
                    Result = ex.Message
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












        

    }
}