using System;
using System.IO;

namespace Gem.Cli.Tests;

public sealed class GoldenJsonFixturesEncodingTests
{
    [Fact]
    public void GoldenJsonFiles_AreUtf8WithoutBom()
    {
        string goldenRoot = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "TestData", "Golden"));

        string[] files = Directory.GetFiles(goldenRoot, "*.json", SearchOption.AllDirectories);

        foreach (string file in files)
        {
            using var stream = File.OpenRead(file);
            var buffer = new byte[3];
            int read = stream.Read(buffer, 0, buffer.Length);

            bool hasBom = read >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF;
            Assert.False(hasBom, $"Golden JSON file contains UTF-8 BOM: {file}");
        }
    }
}
