using System.Text;

namespace Gem.Cli.Tests.TestSupport
{
    internal static class TestFileSystem
    {
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        public static string CreateTemporaryDirectory()
        {
            string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        public static void DeleteDirectoryIfExists(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                return;
            }

            if (!Directory.Exists(directory))
            {
                return;
            }

            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch
            {
                // Ignore cleanup failures in tests.
            }
        }

        public static void WriteCsv(string directory, string fileName, string content)
        {
            Directory.CreateDirectory(directory);

            string fullPath = Path.Combine(directory, fileName);

            // Keep behavior consistent with existing tests:
            // - trim to avoid accidental leading/trailing blank lines
            // - ensure newline at end
            string normalized = content.Trim() + Environment.NewLine;

            File.WriteAllText(fullPath, normalized, Utf8NoBom);
        }

        public static void WriteTextFile(string directory, string fileName, string content)
        {
            Directory.CreateDirectory(directory);

            string fullPath = Path.Combine(directory, fileName);
            File.WriteAllText(fullPath, content, Utf8NoBom);
        }

        public static string ReadAllText(string path)
        {
            return File.ReadAllText(path, Utf8NoBom);
        }

        public static byte[] ReadAllBytes(string path)
        {
            return File.ReadAllBytes(path);
        }
    }
}
