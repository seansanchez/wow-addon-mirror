using System.Threading.Tasks;

namespace AddonMirror.Services;

public interface IReleaseService
{
    Task UpdateMirrorAsync(AddonMirrorOptions configuration);
}
