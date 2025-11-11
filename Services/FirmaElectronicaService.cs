namespace SignDocumentService.Services
{
    using global::SignDocumentService.Dto.Response;
    using global::SignDocumentService.Services.Interfaces;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Models;
    using System;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;

    namespace SignDocumentService.Services
    {
        public class FirmaElectronicaService : IFirmaElectronicaService
        {
            private readonly IHttpClientFactory _httpClientFactory;
            private readonly ILogger<FirmaElectronicaService> _logger;
            private readonly IConfiguration _config;
            private readonly ExternalUrls _urls;

            public FirmaElectronicaService(IHttpClientFactory httpClientFactory, ILogger<FirmaElectronicaService> logger, IConfiguration config, IOptions<ExternalUrls> urls)
            {
                _httpClientFactory = httpClientFactory;
                _logger = logger;
                _config = config;
                _urls = urls.Value;

                Console.WriteLine(_urls);
                if (_urls == null)
                {
                    _logger.LogError("❌ ExternalUrls es null. Verifica appsettings.json");
                    throw new NullReferenceException("ExternalUrls es null.");
                }

            }

            //1 étodo para obtener el token JWT desde el servicio Orion API
            private async Task<string> ObtenerTokenJwtAsync()

            {

                var client = _httpClientFactory.CreateClient();
                string url = _urls.LoginUrl;

                var login = new LoginRequestGS
                {
                    UserName = _urls.LoginUser,
                    Password = _urls.LoginPassword
                };

                var json = JsonSerializer.Serialize(login);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _logger.LogWarning("Antes de obtener token");

                var response = await client.PostAsync(url, content);
                var body = await response.Content.ReadAsStringAsync();

                _logger.LogWarning("Después de obtener token");

                Console.WriteLine(login);

                if (!response.IsSuccessStatusCode)
                {
                    throw new ApplicationException($"Login fallido: {body}");
                }

                var result = JsonSerializer.Deserialize<LoginResponse>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (result == null || string.IsNullOrEmpty(result.Token))
                {
                    throw new ApplicationException($"Error de autenticación: {"Respuesta vacía"}");
                }

                return result.Token;
            }

            Task<string> IFirmaElectronicaService.ObtenerTokenJwtAsync()
            {
                return ObtenerTokenJwtAsync();
            }



            // 2. Método obtener rutas desde el sp.
            public async Task<List<RutasDocumentoResponse>> ObtenerRutasDocumentos(int idSolicitud, int lote)
            {

                //RutaPara Ejecutar Sps
                string url = _urls.GenericExecuteUrl;

                //Obtener token para usar la Orion Api
                string tokenJwt = await ObtenerTokenJwtAsync();

                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenJwt);

                var request = new GenericRequestIntegracion
                {
                    Action = "credito-web/obtener-url-documentos",
                    Data = JsonSerializer.Serialize(new
                    {
                        Solicitud = idSolicitud,
                        Lote = lote,
                    })
                };


                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _logger.LogWarning("Antes de obtener documentos");

                var response = await client.PostAsync(url, content);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new ApplicationException($"Error HTTP: {response.StatusCode} - {response.ReasonPhrase}");
                }

                  
                _logger.LogWarning("Despues de obtener documentos");


                var result = JsonSerializer.Deserialize<GenericResponse<List<RutasDocumentoResponse>>>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (result == null)
                {
                    _logger.LogWarning("Respuesta vacía del backend al obtener rutas.");
                    return new List<RutasDocumentoResponse>();
                }

                if (result.CodeReturn != 1)
                {
                    _logger.LogWarning("Backend respondió sin éxito: {Message}", result.Message);
                    return new List<RutasDocumentoResponse>();
                }

                if (result.Result == null || !result.Result.Any())
                {
                    _logger.LogInformation("No hay lotes de documentos por procesar");
                    return new List<RutasDocumentoResponse>();
                }


                return result.Result;
            }


            private async Task InsertarRegistroDocumentoAsync(List<RutasDocumentoResponse> documentos)
            {
                string url = _urls.GenericExecuteUrl;
                string tokenJwt = await ObtenerTokenJwtAsync();

                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenJwt);

                var request = new GenericRequestIntegracion
                {
                    Action = "credito-web/insertar-registros_firmaElectronica",
                    Data = JsonSerializer.Serialize(new
                    {
                        Documentos = documentos  // Esto crea una propiedad "Documentos" con la lista como valor
                    })
                };



                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");


                _logger.LogWarning("Antes de obtener insertar registros de firma electronica");

                var response = await client.PostAsync(url, content);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new ApplicationException($"Error HTTP: {response.StatusCode} - {response.ReasonPhrase}");
                }

                _logger.LogWarning("Despues de obtener insertar registros de firma electronica");

                var result = JsonSerializer.Deserialize<GenericResponse<List<RutasDocumentoResponse>>>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });


            }


            Task IFirmaElectronicaService.InsertarRegistroDocumentoAsync(List<RutasDocumentoResponse> doc)
            {
                return InsertarRegistroDocumentoAsync(doc);
            }

         
        }

    } 
}

        
