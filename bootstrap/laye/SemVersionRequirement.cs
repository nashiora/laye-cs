namespace laye;

internal sealed record class SemVersionRequirement(SemVersion MinVersion, SemVersion MaxVersion)
{
    public static SemVersionRequirement? TryParse(string requirementString)
    {
        return null;
    }
}
