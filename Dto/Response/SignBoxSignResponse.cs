using SignDocumentService.Utils;
using System.Text.Json.Serialization;

namespace SignDocumentService.Dto.Response
{
    public class SignBoxSignResponse
    {
        [JsonPropertyName("result")]
        public bool Result { get; set; }

        [JsonPropertyName("detail")]
        public string Detail { get; set; }

        [JsonPropertyName("status")]
        [JsonConverter(typeof(StringToJsonConverter))]
        public string Status { get; set; }

        [JsonPropertyName("webhookTxt")]
        public string WebhookTxt { get; set; }

        [JsonPropertyName("webhookPdf")]
        public string WebhookPdf { get; set; }
    }




}
