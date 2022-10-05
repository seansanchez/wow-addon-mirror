using System.Text.Json;

namespace AddonPackager;

public static class AddonPackagerConstants
{
    public static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
