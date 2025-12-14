using System.Text;

namespace Gem.Cli.Utilities
{
    public static class FileEncodings
    {
        public static Encoding Utf8NoBom { get; } =
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    }
}
