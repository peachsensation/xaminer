using System.Text.Json.Serialization;

namespace Xaminer.Generators.Places
{
    [JsonSerializable(typeof(PlacesRespBase))]
    public partial class PlacesJsonContext : JsonSerializerContext { }

    public sealed record PlacesRespBase(IEnumerable<PlacesResp> Places);

    public sealed record PlacesResp(string Place);
}
