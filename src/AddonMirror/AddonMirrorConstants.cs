using System.Text.Json;

namespace AddonMirror;

public static class AddonMirrorConstants
{
    public static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    ///     Represents the environment settings constants.
    /// </summary>
    public static class EnvironmentSettings
    {
        /// <summary>
        ///     Gets the key vault uri.
        /// </summary>
        public const string KeyVaultUri = "KeyVaultUri";

        public const string UseKeyVaultClientCertificateCredential = "UseKeyVaultClientCertificateCredential";
    }
}
