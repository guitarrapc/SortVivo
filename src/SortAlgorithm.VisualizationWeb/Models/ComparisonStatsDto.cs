using System.Text.Json.Serialization;

namespace SortAlgorithm.VisualizationWeb.Models;

public sealed class ComparisonStatsDto
{
    [JsonPropertyName("rank")]
    public int Rank { get; set; }

    [JsonPropertyName("algorithm")]
    public required string Algorithm { get; set; }

    [JsonPropertyName("complexity")]
    public required string Complexity { get; set; }

    [JsonPropertyName("compares")]
    public ulong Compares { get; set; }

    [JsonPropertyName("swaps")]
    public ulong Swaps { get; set; }

    [JsonPropertyName("reads")]
    public ulong Reads { get; set; }

    [JsonPropertyName("writes")]
    public ulong Writes { get; set; }

    [JsonPropertyName("progress")]
    public int Progress { get; set; }

    [JsonPropertyName("isCompleted")]
    public bool IsCompleted { get; set; }

    [JsonPropertyName("executionTime")]
    public required ExecutionTimeDto ExecutionTime { get; set; }
}

public sealed class ExecutionTimeDto
{
    [JsonPropertyName("totalNanoseconds")]
    public double TotalNanoseconds { get; set; }

    [JsonPropertyName("formatted")]
    public required string Formatted { get; set; }
}

[JsonSerializable(typeof(List<ComparisonStatsDto>))]
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public partial class ComparisonStatsJsonContext : JsonSerializerContext
{
}
