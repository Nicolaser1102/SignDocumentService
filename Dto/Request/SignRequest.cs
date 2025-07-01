namespace SignDocumentService.Dto.Request
{
    public class SignRequest
    {
        public string UserName { get; set; }
        public int SessionID { get; set; }
        public int Solicitud { get; set; }
        public int Lote { get; set; }
    }
}
