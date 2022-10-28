using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AddonMirror.Models;

namespace AddonMirror;

public class AddonMirrorOptions
{
    [Required]
    public string AzureStorageConnectionString { get; set; }

    [Required]
    public string GitHubToken { get; set; }

    public IEnumerable<Addon> Addons { get; set; }
}
