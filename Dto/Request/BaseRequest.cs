namespace SignDocumentService.Dto.Request
{
    public class BaseRequest<T>
    {
        public string UserName { get; set; }
        public string SessionID { get; set; }
        public string Action { get; set; }
        public T Data { get; set; }
        public double? Lon { get; set; }
        public double? Lat { get; set; }
        public string Dispositivo { get; set; }
    }
}
