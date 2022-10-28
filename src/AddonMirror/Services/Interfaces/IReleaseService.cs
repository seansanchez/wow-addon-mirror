using System.Collections.Generic;
using System.Threading.Tasks;
using AddonMirror.Models;
using Octokit;

namespace AddonMirror.Services;

public interface IReleaseService
{
    Task<IEnumerable<Release>> GetUnmirroredReleasesAsync(
        Addon addon);

    Task CreateMirrorCommitAsync(
        Addon addon,
        Release unmirroredRelease);
}
