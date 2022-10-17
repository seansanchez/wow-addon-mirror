namespace AddonMirrorer.Extensions;

/// <summary>
///     Provides extension methods for <see cref="object"/>.
/// </summary>
public static class ObjectExtensions
{
    /// <summary>
    ///     Throws an <see cref="ArgumentNullException"/> if the specified <paramref name="source"/> <see cref="object"/> is null.
    /// </summary>
    /// <param name="source">The source <see cref="object"/>.</param>
    /// <param name="name">The name of the source parameter.</param>
    public static void ShouldNotBeNull(this object? source, string name)
    {
        if (source == null)
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }
}