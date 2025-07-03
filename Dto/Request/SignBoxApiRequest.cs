namespace SignDocumentService.Dto.Request
{
    public class SignBoxSignRequest
    {
        public string FileIn { get; set; }  // Documento (ruta o contenido Base64)
        public string WebhookId { get; set; }
        public string Image { get; set; }  // Base64 (opcional)
        public string Username { get; set; }
        public string Password { get; set; }
        public string Pin { get; set; }
        public string Reason { get; set; }
        public string Location { get; set; }
        public string Position { get; set; }  // "x1,y1,x2,y2"
        public int? Npage { get; set; }
        public string ParagraphFormat { get; set; }  // JSON o string según spec
    }


}
