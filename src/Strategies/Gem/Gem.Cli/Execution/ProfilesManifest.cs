namespace Gem.Cli.Execution
{
    public sealed class ProfilesManifest
    {
        public IReadOnlyList<ProfilesManifestEntry> Profiles { get; set; } = Array.Empty<ProfilesManifestEntry>();
    }
}
