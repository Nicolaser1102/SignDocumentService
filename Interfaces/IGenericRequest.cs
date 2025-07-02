namespace SignDocumentService.Interfaces
{
    public interface IGenericRequest
    {
        string Action { get; set; }
        string Data { get; set; }
        string Lat { get; set; }
        string Lon { get; set; }
    }

}
