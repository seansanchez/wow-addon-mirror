namespace AddonMirrorer.Models;

public class Variant
{
    public string Name { get; set; }

    public bool NoLib { get; set; } = false;

    public string Flavor { get; set; } = "mainline";

    public string PathInSource { get; set; }
}
