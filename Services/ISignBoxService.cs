using SignDocumentService.Dto.Request;

namespace SignDocumentService.Services
{
    public interface ISignBoxService
    {
        Task<string> ObtenerTokenAsync();
        Task<bool> FirmarDocumentoAsync(string token, SignBoxSignRequest documento);
    }
}
