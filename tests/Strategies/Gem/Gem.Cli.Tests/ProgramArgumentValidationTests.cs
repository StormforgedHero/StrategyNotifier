using Gem.Cli.Tests.TestSupport;

namespace Gem.Cli.Tests;

public sealed class ProgramArgumentValidationTests
{
    [Fact]
    public void Run_NoUpdateWithForce_ReturnsConfigurationError()
    {
        using var workspace = new TemporaryWorkspace();
        var output = new StringWriter();
        var error = new StringWriter();

        int exitCode = Program.Run(workspace.Root, output, error, new[] { "--no-update", "--force-update" });

        Assert.Equal(Program.ExitCodeConfigurationError, exitCode);
        Assert.Contains("cannot be combined", error.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Run_UpdateFlagNotSupported_ReturnsConfigurationError()
    {
        using var workspace = new TemporaryWorkspace();
        var output = new StringWriter();
        var error = new StringWriter();

        int exitCode = Program.Run(workspace.Root, output, error, new[] { "--update" });

        Assert.Equal(Program.ExitCodeConfigurationError, exitCode);
        Assert.Contains("not supported", error.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
