//using System.Text.Json;
//using AddonMirrorer.Services;
//using Microsoft.Extensions.DependencyInjection;
//using Mono.Options;
//using Octokit;

//namespace AddonMirrorer;

//public class Program
//{
//    private static OptionSet Options { get; } = new OptionSet
//    {
//        { "apiKey=", v => ApiKey = v },
//        { "configFile=", v => ConfigFile = v }
//    };

//    public static string ApiKey { get; set; }

//    public static string ConfigFile { get; set; }

//    public static async Task Main(string[] args)
//    {
//        Options.Parse(args);

//        if (string.IsNullOrWhiteSpace(ApiKey))
//        {
//            throw new OptionException($"{nameof(ApiKey)} is required.", "apiKey");
//        }

//        if (string.IsNullOrWhiteSpace(ConfigFile))
//        {
//            throw new OptionException($"{nameof(ConfigFile)} is required.", "configFile");
//        }

//        var json = await File.ReadAllTextAsync(ConfigFile).ConfigureAwait(false);

//        var serviceCollection = new ServiceCollection();

//        serviceCollection.AddHttpClient();
//        serviceCollection.AddSingleton<IReleaseService, ReleaseService>();

//        var gitHubClient = new GitHubClient(new ProductHeaderValue("test"));
//        gitHubClient.Credentials = new Credentials(ApiKey);

//        serviceCollection.AddSingleton<IGitHubClient>(gitHubClient);

//        var serviceProvider = serviceCollection.BuildServiceProvider();

//        var releaseService = serviceProvider.GetRequiredService<IReleaseService>();

//        var configuration = JsonSerializer.Deserialize<AddonMirrorerConfiguration>(json, AddonMirrorerConstants.SerializerOptions);

//        if (configuration != null)
//        {
//            await releaseService.UpdateMirrorAsync(configuration);
//        }

//    }
//}