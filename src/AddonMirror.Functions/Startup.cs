using AddonMirror.Extensions;
using AddonMirror.Functions;
using AddonMirror.Repositories;
using AddonMirror.Services;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Octokit;

#pragma warning disable SA1516 // Elements should be separated by blank line
[assembly: FunctionsStartup(typeof(Startup))]
namespace AddonMirror.Functions;
#pragma warning restore SA1516 // Elements should be separated by blank line

/// <inheritdoc/>
public class Startup : FunctionsStartup
{
    /// <inheritdoc/>
    public override void Configure(IFunctionsHostBuilder builder)
    {
        builder.Services
            .AddOptions<AddonMirrorOptions>()
            .Configure<IConfiguration>((settings, configuration) =>
            {
                configuration.GetSection(nameof(AddonMirrorOptions)).Bind(settings);
            })
            .ValidateDataAnnotations();

        builder.Services.AddHttpClient();
        builder.Services.AddSingleton<IMemoryCache, MemoryCache>();
        builder.Services.AddSingleton<IAzureTableStorageRepository, AzureTableStorageRepository>();
        builder.Services.AddSingleton<IGitHubClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AddonMirrorOptions>>();

            var gitHubClient = new GitHubClient(new ProductHeaderValue("AddonMirror"));
            gitHubClient.Credentials = new Credentials(options.Value.GitHubToken);

            return gitHubClient;
        });
        builder.Services.AddSingleton<IReleaseService, ReleaseService>();
    }

    /// <inheritdoc/>
    public override void ConfigureAppConfiguration(IFunctionsConfigurationBuilder builder)
    {
        var context = builder.GetContext();
        builder.ConfigurationBuilder.Configure(context.ApplicationRootPath, context.EnvironmentName);
    }
}
