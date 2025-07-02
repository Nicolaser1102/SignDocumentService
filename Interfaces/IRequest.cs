namespace SignDocumentService.Interfaces
{
    public interface IRequest { }
    public interface IDefaultInputRequest
    {
        string UserName { get; set; }
        int SessionID { get; set; }
    }
}
