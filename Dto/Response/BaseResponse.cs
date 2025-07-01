
namespace SignDocumentService.Dto.Response
{
    public class BaseResponse : IResponse
    {
        public string Message { get; set; }
        public int CodeReturn { get; set; }
    }

}
