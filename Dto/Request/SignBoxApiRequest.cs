namespace SignDocumentService.Dto.Request
{
    public class SignBoxApiRequest
    {
        public int Solicitud { get; set; }
        public int Lote { get; set; }
        public string Codigo { get; set; }
        public string Ruta { get; set; }
    }

}
