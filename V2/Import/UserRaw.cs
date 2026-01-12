using System.Text.Json.Serialization;

namespace V2.Import;

public class UserRaw
{
    public string? id { get; set; }
    public string? created_at { get; set; }

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int? birth_year { get; set; }

    [JsonConverter(typeof(BoolConverter))]
    public bool? active { get; set; }

    public string? username { get; set; }
    public string? password { get; set; }
    public string? name { get; set; }
    public string? email { get; set; }
    public string? phone { get; set; }
    public string? role { get; set; }
}