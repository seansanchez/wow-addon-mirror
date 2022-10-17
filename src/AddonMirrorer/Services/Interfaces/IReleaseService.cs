namespace AddonMirrorer.Services;

public interface IReleaseService
{
    Task UpdateMirrorAsync(AddonMirrorerConfiguration configuration);
}
