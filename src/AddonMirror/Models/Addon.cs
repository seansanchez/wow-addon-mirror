using System.Collections.Generic;

namespace AddonMirror.Models;

public class Addon
{
    public string SourceOwner { get; set; }

    public string SourceRepositoryName { get; set; }

    public string MirrorOwner { get; set; }

    public string MirrorRepositoryName { get; set; }

    public string Name { get; set; }

    public IList<string> SourceExclude { get; set; }

    public IEnumerable<Variant> Variants { get; set; }

    public IEnumerable<string> SkipReleases { get; set; }
}
