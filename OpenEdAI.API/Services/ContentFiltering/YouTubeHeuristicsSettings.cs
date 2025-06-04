namespace OpenEdAI.Configuration;

/// <summary>
/// Tunable parameters for evaluating YouTube videos.
/// Bound from the <c>YouTubeHeuristics</c> section of configuration.
/// </summary>
public sealed class YouTubeHeuristicsSettings
{
    /// <summary>Minimum duration (minutes, inclusive). Default = 3 min.</summary>
    public int MinDurationMinutes { get; set; } = 3;

    /// <summary>Maximum duration (minutes, inclusive). Default = 30 min.</summary>
    public int MaxDurationMinutes { get; set; } = 30;

    /// <summary>Fuzzy-match threshold (0-100). Default = 70.</summary>
    public int FuzzyThreshold { get; set; } = 60;

    /// <summary>
    /// If <see langword="true"/>, videos without captions are rejected.  
    /// Default = <see langword="true"/>.
    /// </summary>
    public bool RequireCaptions { get; set; } = true;
}
