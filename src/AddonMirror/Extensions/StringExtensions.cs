using System;

namespace AddonMirror.Extensions;

/// <summary>
///     Provides extension methods for <see cref="string"/>.
/// </summary>
public static class StringExtensions
{
    /// <summary>
    ///     Throws an <see cref="ArgumentNullException"/> if the specified <paramref name="source"/> <see cref="string"/> is null or empty.
    /// </summary>
    /// <param name="source">The source <see cref="string"/>.</param>
    /// <param name="name">The name of the source parameter.</param>
    public static void ShouldNotBeNullOrEmpty(this string? source, string name)
    {
        if (string.IsNullOrEmpty(source))
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }

    /// <summary>
    ///     Throws an <see cref="ArgumentNullException"/> if the specified <paramref name="source"/> <see cref="string"/> is null, empty, or whitespace.
    /// </summary>
    /// <param name="source">The source <see cref="string"/>.</param>
    /// <param name="name">The name of the source parameter.</param>
    public static void ShouldNotBeNullOrWhiteSpace(this string? source, string name)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }
}