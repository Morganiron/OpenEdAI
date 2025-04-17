using OpenEdAI.API.DTOs;

namespace OpenEdAI.API.Services
{
    public interface IContentSearchService
    {
            /// <summary>
            /// Searches for content links based on lesson details and the student's profile
            /// </summary>
            Task<List<string>> SearchContentLinksAsync(
                CoursePersonalizationInput userInput,
                CoursePlanDTO coursePlan,
                LessonSearchPlanDTO lessonSearchPlan,
                StudentProfileDTO studentProfile,
                CancellationToken cancellationToken);
        
    }
}
