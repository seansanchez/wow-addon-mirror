using AddonMirror.Extensions;
using AddonMirror.Functions;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;

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
    }

    /// <inheritdoc/>
    public override void ConfigureAppConfiguration(IFunctionsConfigurationBuilder builder)
    {
        var context = builder.GetContext();
        builder.ConfigurationBuilder.Configure(context.ApplicationRootPath, context.EnvironmentName);
    }
}
