using System.Collections.Generic;
using AddonMirror.Models;

namespace AddonMirror;

public class AddonMirrorOptions
{
    public string SourceOwner { get; set; }

    public string SourceRepositoryName { get; set; }

    public string MirrorOwner { get; set; }

    public string MirrorRepositoryName { get; set; }

    public IEnumerable<Variant> Variants { get; set; }

    public IEnumerable<string> SkipReleases { get; set; } = new List<string>();
}
