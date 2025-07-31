using SignDocumentService.Dto.Request;
using SignDocumentService.Dto.Response;

namespace SignDocumentService.Services.Interfaces
{
    public interface IFirmaElectronicaService
    {
        Task<List<RutasDocumentoResponse>> ObtenerRutasDocumentos(int idSolicitud, int lote);
        Task InsertarRegistroDocumentoAsync(List<RutasDocumentoResponse> doc);
        Task<string> ObtenerTokenJwtAsync();
    }
}
