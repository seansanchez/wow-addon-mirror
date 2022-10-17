using System.Text.Json;

namespace AddonMirror;

public static class Constants
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

    /// <summary>
    ///     Provides common cron trigger timers.
    /// </summary>
    public static class CronTriggers
    {
        public const string Default = Every10Seconds;

        // Minutes
        public const string Every1Second = "* * * * * *";
        public const string Every5Seconds = "*/5 * * * * *";
        public const string Every10Seconds = "*/10 * * * * *";
        public const string Every15Seconds = "*/15 * * * * *";
        public const string Every30Seconds = "*/30 * * * * *";

        // Minutes
        public const string Every1Minute = "0 * * * * *";
        public const string Every5Minutes = "0 */5 * * * *";
        public const string Every10Minutes = "0 */10 * * * *";
        public const string Every15Minutes = "0 */15 * * * *";
        public const string Every30Minutes = "0 */30 * * * *";

        // Hours
        public const string Every1Hour = "0 0 * * * *";
        public const string Every6Hours = "0 0 */6 * * *";
        public const string Every12Hours = "0 0 */12 * * *";

        // Days
        public const string Every1Day = "0 0 12 * * *";
    }
}
