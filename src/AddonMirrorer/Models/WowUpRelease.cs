namespace AddonMirrorer.Models;

public class WowUpRelease
{
    public string Name { get; set; }

    public string Version { get; set; }

    public string Filename { get; set; }

    public bool NoLib { get; set; } = false;

    public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
}
