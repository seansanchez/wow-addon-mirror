using AddonMirrorer.Extensions;
using AddonMirrorer.Functions;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(Startup))]
namespace AddonMirrorer.Functions;

/// <inheritdoc/>
public class Startup : FunctionsStartup
{
    /// <inheritdoc/>
    public override void Configure(IFunctionsHostBuilder builder)
    {
    }

    /// <inheritdoc/>
    public override void ConfigureAppConfiguration(IFunctionsConfigurationBuilder builder)
    {
        var context = builder.GetContext();
        builder.ConfigurationBuilder.Configure(context.ApplicationRootPath, context.EnvironmentName);
    }
}

