
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Models;
using SignDocumentService.Dto.Request;
using SignDocumentService.Dto.Response;
using SignDocumentService.Services;
using SignDocumentService.Services.Interfaces;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text.Json;

namespace SigningService.Controllers
{
    [ApiController]
    [Route("api/signDocument")]
    [Authorize]
    public class DocumentoController : ControllerBase
    {
        private readonly ILogger<DocumentoController> _logger;

        private readonly IFirmaElectronicaService _firmaElectronicaService;
        





        public DocumentoController(ILogger<DocumentoController> logger,
                                    IHttpClientFactory httpClientFactory,
                                    IConfiguration config,
                                    IFirmaElectronicaService firmaElectronicaService
                                    
                                    )
        {
            _logger = logger;
            _firmaElectronicaService = firmaElectronicaService;

           
        }


        //Metodo que va a realizar la firma de documentos


        [HttpPost("firmar-documentos")]
        [Authorize]
        public async Task<IActionResult> FirmarDocumentoConTokenSignBoxFunciones([FromBody] SignRequest request)
        {
            try
            {


                // Extraer idSolicitud y lote del campo Data del request
                int idSolicitud = 0;
                int lote = 0;
                 idSolicitud = request.Solicitud;
                lote = request.Lote;
               
                if(idSolicitud ==0 || lote == 0)
                return StatusCode(500, new GenericResponse
                {
                    CodeReturn = -1,
                    Message = "No se pudo obtener los parámetros correctamente",
                    Result = "NO_PARAMS"
                });



                var rutas = await _firmaElectronicaService.ObtenerRutasDocumentos(idSolicitud,lote);
                if (rutas == null || !rutas.Any())
                {
                    return StatusCode(500,new GenericResponse
                    {
                        CodeReturn = -1,
                        Message = "No se encontraron rutas de documentos para firmar",
                        Result = "NO_DOCUMENTS"
                    });
                }

                _logger.LogInformation("🔍 Rutas obtenidas: {rutas}", JsonSerializer.Serialize(rutas));


           
               await _firmaElectronicaService.InsertarRegistroDocumentoAsync(rutas);




                return Ok(new GenericResponse
                {
                    CodeReturn = 1,
                    Message = "Todos los documentos insertados",
                    Result = "OK"
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
        public async Task<IActionResult> ProbarConexion([FromBody] SignRequest request)
        {

            
            string tokenJwt = await _firmaElectronicaService.ObtenerTokenJwtAsync();




            var token = HttpContext.Request.Headers["Authorization"].ToString();
            _logger.LogInformation("🔐 Token recibido: {token}", tokenJwt);

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