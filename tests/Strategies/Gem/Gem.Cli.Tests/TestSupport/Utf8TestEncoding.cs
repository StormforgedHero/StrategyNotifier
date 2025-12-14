using Gem.Cli.Utilities;
using System.Text;

namespace Gem.Cli.Tests.TestSupport
{
    internal static class Utf8TestEncoding
    {
        public static Encoding Utf8NoBom => FileEncodings.Utf8NoBom;

        public static void WriteAllTextUtf8NoBom(string path, string content)
        {
            string normalized = NormalizeContent(content);
            File.WriteAllText(path, normalized, Utf8NoBom);
        }

        private static string NormalizeContent(string content)
        {
            string trimmed = (content ?? string.Empty).Trim();
            return trimmed + "\n";
        }
    }
}
