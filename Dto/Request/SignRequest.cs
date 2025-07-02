namespace SignDocumentService.Dto.Request
{
    public class SignRequest:BaseRequest
    {
        public int Solicitud { get; set; }
        public int Lote { get; set; }
    }
}
