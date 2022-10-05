namespace AddonPackager.Services;

public interface IReleaseService
{
    Task UpdateMirrorAsync(
        AddonPackagerConfiguration configuration);
}
