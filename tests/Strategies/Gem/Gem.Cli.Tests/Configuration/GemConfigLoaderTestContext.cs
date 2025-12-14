using Gem.Cli.Configuration;
using Gem.Cli.Tests.TestSupport;

namespace Gem.Cli.Tests.Configuration
{
    internal sealed class GemConfigLoaderTestContext : IDisposable
    {
        public TemporaryWorkspace Workspace { get; }
        public string RootDirectory => Workspace.Root;
        public string ConfigDirectory => Workspace.GetPath("config", "gem");
        public string ConfigPath => Workspace.GetPath("config", "gem", "gem.cli.json");

        private GemConfigLoaderTestContext(TemporaryWorkspace workspace)
        {
            Workspace = workspace;
        }

        public static GemConfigLoaderTestContext Create()
        {
            return new GemConfigLoaderTestContext(new TemporaryWorkspace());
        }

        public void WriteConfig(string jsonContent)
        {
            Workspace.WriteText(jsonContent, "config", "gem", "gem.cli.json");
        }

        public GemCliConfiguration Load()
        {
            var loader = new GemConfigLoader(ConfigPath);
            return loader.Load();
        }

        public void Dispose()
        {
            Workspace.Dispose();
        }
    }
}
