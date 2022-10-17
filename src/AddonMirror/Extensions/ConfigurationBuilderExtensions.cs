using System;
using System.IO;
using Azure.Identity;
using Microsoft.Extensions.Configuration;

namespace AddonMirror.Extensions;

/// <summary>
///     Provides extension methods for <see cref="IConfigurationBuilder"/>.
/// </summary>
public static class ConfigurationBuilderExtensions
{
    /// <summary>
    ///     Adds the common <see cref="IConfigurationProvider"/> to the <see cref="IConfigurationBuilder"/>.
    /// </summary>
    /// <param name="source">The source <see cref="IConfigurationBuilder"/>.</param>
    /// <param name="applicationRootPath">The application root path.</param>
    /// <param name="environment">The environment.</param>
    public static void Configure(
        this IConfigurationBuilder source,
        string? applicationRootPath = null,
        string? environment = null)
    {
        source.ShouldNotBeNull(nameof(source));

        source.AddAzureKeyVaultConfigurationProvider();
        source.AddEnvironmentVariables();
        source.AddJsonConfigurationProvider(
            applicationRootPath: applicationRootPath,
            environment: environment);
    }

    private static void AddAzureKeyVaultConfigurationProvider(this IConfigurationBuilder source)
    {
        source.ShouldNotBeNull(nameof(source));

        var keyVaultUri = Environment.GetEnvironmentVariable(AddonMirrorConstants.EnvironmentSettings.KeyVaultUri);

        if (!string.IsNullOrWhiteSpace(keyVaultUri))
        {
            _ = bool.TryParse(Environment.GetEnvironmentVariable(AddonMirrorConstants.EnvironmentSettings.UseKeyVaultClientCertificateCredential), out var useKeyVaultClientCertificateCredential);

            if (useKeyVaultClientCertificateCredential)
            {
                var clientId = Environment.GetEnvironmentVariable("azIdentity.clientId");
                clientId.ShouldNotBeNullOrWhiteSpace(nameof(clientId));

                var tenantId = Environment.GetEnvironmentVariable("azIdentity.tenantId");
                tenantId.ShouldNotBeNullOrWhiteSpace(nameof(tenantId));

                var certificatePath = Environment.GetEnvironmentVariable("azIdentity.certificatePath");
                certificatePath.ShouldNotBeNullOrWhiteSpace(nameof(tenantId));

                var tokenCredential = new ClientCertificateCredential(
                    tenantId: tenantId,
                    clientId: clientId,
                    clientCertificatePath: certificatePath);

                source.AddAzureKeyVault(new Uri(keyVaultUri), tokenCredential);
            }
            else
            {
                source.AddAzureKeyVault(new Uri(keyVaultUri), new DefaultAzureCredential());
            }
        }
    }

    private static void AddJsonConfigurationProvider(
        this IConfigurationBuilder source,
        string? applicationRootPath = null,
        string? environment = null)
    {
        source.ShouldNotBeNull(nameof(source));

        var appSettingsFileName = "appsettings.json";

        source
            .AddJsonFile(
                path: applicationRootPath == null ? appSettingsFileName : Path.Combine(applicationRootPath, appSettingsFileName),
                optional: true,
                reloadOnChange: false);

        if (!string.IsNullOrWhiteSpace(environment))
        {
            var appSettingsWithEnvironmentFileName = $"appsettings.{environment}.json";

            source
                .AddJsonFile(
                    path: applicationRootPath == null ? appSettingsWithEnvironmentFileName : Path.Combine(applicationRootPath, appSettingsWithEnvironmentFileName),
                    optional: true,
                    reloadOnChange: false);
        }
    }
}
