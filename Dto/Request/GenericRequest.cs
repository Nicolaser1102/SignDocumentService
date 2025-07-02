using SignDocumentService.Interfaces;
namespace SignDocumentService.Dto.Request
{

    public class GenericRequest : BaseRequest, IGenericRequest
    {
        public string Action { get; set; }
        public string Data { get; set; }
        public string Lat { get; set; }
        public string Lon { get; set; }
        public string Dispositivo { get; set; }
    }

}
