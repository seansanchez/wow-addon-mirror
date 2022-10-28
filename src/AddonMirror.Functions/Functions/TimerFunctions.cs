using System;
using System.Threading.Tasks;
using AddonMirror.Services;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AddonMirror.Functions;

public class TimerFunctions
{
    private readonly IReleaseService _releaseService;
    private readonly IOptions<AddonMirrorOptions> _options;

    public TimerFunctions(
        IReleaseService releaseService,
        IOptions<AddonMirrorOptions> options)
    {
        _releaseService = releaseService;
        _options = options;
    }

    [FunctionName("TimerFunctions")]
    public async Task RunAsync([TimerTrigger(Constants.CronTriggers.Every1Minute)] TimerInfo myTimer, ILogger log)
    {
        log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

        foreach (var addon in _options.Value.Addons)
        {
            var unmirroredReleases = await _releaseService.GetUnmirroredReleasesAsync(
                addon.SourceOwner,
                addon.SourceRepositoryName,
                addon.MirrorOwner,
                addon.MirrorRepositoryName,
                addon.SkipReleases);

            foreach (var unmirroredRelease in unmirroredReleases)
            {
                await _releaseService.CreateMirrorCommitAsync(
                    unmirroredRelease,
                    addon.MirrorOwner,
                    addon.MirrorRepositoryName,
                    addon.Name,
                    addon.Variants);
            }
        }
    }
}
