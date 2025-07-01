namespace SignDocumentService.Dto.Request
{

    public class GenericRequest
    {
        public string UserName { get; set; }
        public int SessionID { get; set; }
        public object Data { get; set; }
        public double? Lon { get; set; }
        public double? Lat { get; set; }
        public string Dispositivo { get; set; }
    }
}
