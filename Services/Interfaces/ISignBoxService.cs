using SignDocumentService.Dto.Request;
using SignDocumentService.Dto.Response;

namespace SignDocumentService.Services.Interfaces
{
    public interface ISignBoxService
    {
        Task<List<RutasDocumentoResponse>> ObtenerRutasDesdeSp(GenericRequest request);
        Task<string> ObtenerTokenSignBoxAsync();

        Task<GenericResponse> FirmarLoteDocumentosAsync(List<RutasDocumentoResponse> rutas, string token);
    }
}
