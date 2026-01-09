namespace Gem.Cli.Tests.TestSupport
{
    internal sealed class CurrentDirectoryScope : IDisposable
    {
        private readonly string _originalDirectory;

        public CurrentDirectoryScope(string targetDirectory)
        {
            if (string.IsNullOrWhiteSpace(targetDirectory))
            {
                throw new ArgumentException("Target directory must be provided.", nameof(targetDirectory));
            }

            _originalDirectory = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(targetDirectory);
        }

        public void Dispose()
        {
            Directory.SetCurrentDirectory(_originalDirectory);
        }
    }
}
