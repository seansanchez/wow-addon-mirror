using System.Collections.Generic;
using System.Threading.Tasks;
using AddonMirror.Models;
using Octokit;

namespace AddonMirror.Services;

public interface IReleaseService
{
    Task<IEnumerable<Release>> GetUnmirroredReleasesAsync(
        string sourceOwner,
        string sourceRepositoryName,
        string mirrorOwner,
        string mirrorRepositoryName,
        IEnumerable<string> releasesToSkip);

    Task CreateMirrorCommitAsync(
        Release unmirroredRelease,
        string mirrorOwner,
        string mirrorRepositoryName,
        string addonName,
        IEnumerable<Variant> variants);
}
