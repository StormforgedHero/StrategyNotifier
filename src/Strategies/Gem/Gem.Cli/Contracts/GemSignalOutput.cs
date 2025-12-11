using System.Text.Json.Serialization;

namespace Gem.Cli.Contracts
{
    /// <summary>
    /// Represents a single GEM signal in the JSON output file.
    /// </summary>
    public sealed record GemSignalOutput(
        [property: JsonPropertyName("period")] string Period,
        [property: JsonPropertyName("position")] string Position);
}
