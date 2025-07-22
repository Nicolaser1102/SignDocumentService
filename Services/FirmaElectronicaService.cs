namespace SignDocumentService.Services
{
    using global::SignDocumentService.Dto.Request;
    using global::SignDocumentService.Dto.Response;
    using global::SignDocumentService.Services.Interfaces;
    using Microsoft.Data.SqlClient;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Models;
    using System;
    using System.Data;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
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
                _logger.LogInformation("📡 Solicitando token JWT...");
                string url = _urls.LoginUrl;

                var login = new LoginRequestGS
                {
                    UserName = _urls.LoginUser,
                    Password = _urls.LoginPassword
                };

                var json = JsonSerializer.Serialize(login);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(url, content);
                var body = await response.Content.ReadAsStringAsync();

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
                        idSolicitud = idSolicitud,
                        lote = lote,
                    })
                };


                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(url, content);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new ApplicationException($"Error HTTP: {response.StatusCode} - {response.ReasonPhrase}");
                }

                Console.WriteLine("BODY:");
                Console.WriteLine(body);

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



            public async Task<string> ObtenerTokenSignBoxAsync()
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




            public async Task<GenericResponse> FirmarLoteDocumentosAsync(List<RutasDocumentoResponse> rutas, string token)
            {
                try
                {

                    if (rutas == null || !rutas.Any())
                    {
                        return new GenericResponse
                        {
                            CodeReturn = -1,
                            Message = "No hay documentos para firmar",
                            Result = "NO_DOCUMENTS"
                        };
                    }

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
                    return new GenericResponse
                    {
                        CodeReturn = 1,
                        Message = "Documentos firmados correctamente",
                        Result = "OK"
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error inesperado en FirmarLoteDocumentosAsync");

                    return new GenericResponse
                    {
                        CodeReturn = -3,
                        Message = $"Error inesperado al firmar documentos: {ex.Message}",
                        Result = "ERROR_GENERAL"
                    };
                }
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
                    if (!System.IO.File.Exists(doc.RutaArchivo))
                    {
                        Console.WriteLine($"Ruta del archivo PDF: {doc.RutaArchivo}");
                        _logger.LogError("❌ El PDF no existe: {Ruta}", doc.RutaArchivo);

                    }

                    //Añadir como parámetro el PDF
                    var pdfStream = System.IO.File.OpenRead(doc.RutaArchivo);
                    content.Add(new StreamContent(pdfStream), "fileIn", Path.GetFileName(doc.RutaArchivo));

                    // b) Imagen de firma (Base64 en string) parametrizar con la firma de cada Oficial de Credito
                    var imagePath = @"C:\DocumentosPruebaFirmaElectronica\25\firmaPruebaIA.png";
                    if (System.IO.File.Exists(imagePath))
                    {
                        var imgBytes = await System.IO.File.ReadAllBytesAsync(imagePath);
                        var imageBase64 = Convert.ToBase64String(imgBytes);
                        content.Add(new StringContent(imageBase64), "image");
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ Imagen no encontrada: {Path}", imagePath);
                    }

                    // c) Campos simples


                    var webhookId = $"{doc.CodigoDocumento}_{doc.Solicitud}_{doc.Lote}";
                    content.Add(new StringContent(webhookId), "webhookId");

                    //Descomentar/ Comentar para pruebas
                    content.Add(new StringContent("1091583"), "username");
                    content.Add(new StringContent("RY3qn76H"), "password");


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
}

        
