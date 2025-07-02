using SignDocumentService.Interfaces;
namespace SignDocumentService.Dto.Request

{
    public class BaseRequest : IRequest, IDefaultInputRequest
    {
        public string UserName { get; set; }
        public int SessionID { get; set; }
    }
}
