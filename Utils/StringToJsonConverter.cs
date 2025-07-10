using System.Text.Json;
using System.Text.Json.Serialization;

namespace SignDocumentService.Utils
{
    public class StringToJsonConverter : JsonConverter<string>
    {
        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.TokenType switch
            {
                JsonTokenType.String => reader.GetString(),
                JsonTokenType.Number => reader.GetDouble().ToString(), // o GetInt32() si estás seguro
                _ => throw new JsonException("Tipo de token inesperado para string.")
            };
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
            => writer.WriteStringValue(value);
    }
}
