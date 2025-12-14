namespace Gem.Cli.Tests.TestSupport
{
    internal sealed class TemporaryWorkspace : IDisposable
    {
        public TemporaryWorkspace()
        {
            Root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public string GetPath(params string[] segments)
        {
            string path = Root;

            foreach (string segment in segments)
            {
                path = Path.Combine(path, segment);
            }

            return path;
        }

        public void EnsureDirectory(params string[] segments)
        {
            string path = GetPath(segments);
            Directory.CreateDirectory(path);
        }

        public void WriteCsv(string content, params string[] relativePathSegments)
        {
            string fullPath = GetPath(relativePathSegments);
            EnsureParentDirectory(fullPath);
            Utf8TestEncoding.WriteAllTextUtf8NoBom(fullPath, content);
        }

        public void WriteText(string content, params string[] relativePathSegments)
        {
            string fullPath = GetPath(relativePathSegments);
            EnsureParentDirectory(fullPath);
            Utf8TestEncoding.WriteAllTextUtf8NoBom(fullPath, content);
        }

        public string ReadAllText(params string[] relativePathSegments)
        {
            string fullPath = GetPath(relativePathSegments);
            return File.ReadAllText(fullPath, Utf8TestEncoding.Utf8NoBom);
        }

        public byte[] ReadAllBytes(params string[] relativePathSegments)
        {
            string fullPath = GetPath(relativePathSegments);
            return File.ReadAllBytes(fullPath);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch
            {
                // Best-effort cleanup for tests.
            }
        }

        private static void EnsureParentDirectory(string filePath)
        {
            string? directory = Path.GetDirectoryName(filePath);

            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
    }
}
