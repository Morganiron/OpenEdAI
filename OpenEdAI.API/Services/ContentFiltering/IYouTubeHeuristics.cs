using System.Threading;
using System.Threading.Tasks;

namespace OpenEdAI.Services.ContentFiltering
{
    /// <summary>
    /// Contract for deciding whether a YouTube video is suitable for a lesson.
    /// </summary>
    public interface IYouTubeHeuristics
    {
        /// <summary>
        /// Returns <c>true</c> when <paramref name="videoUrlOrId"/> is relevant to
        /// <paramref name="lessonTopic"/> and satisfies all acceptance rules.
        /// </summary>
        Task<bool> IsRelevantAsync(string videoUrlOrId,
                                   string lessonTopic,
                                   CancellationToken ct);
    }
}
