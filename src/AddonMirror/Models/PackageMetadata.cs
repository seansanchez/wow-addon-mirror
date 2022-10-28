using System.Collections.Generic;

namespace AddonMirror.Models;

public class PackageMetadata
{
    public IDictionary<string, string> Externals { get; set; } = new Dictionary<string, string>();
}
