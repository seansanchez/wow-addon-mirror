using System.Threading.Tasks;

namespace AddonMirror.Services;

public interface IReleaseService
{
    Task UpdateMirrorAsync(AddonMirrorConfiguration configuration);
}
