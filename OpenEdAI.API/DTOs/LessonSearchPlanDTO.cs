namespace OpenEdAI.API.DTOs
{
    public class LessonSearchPlanDTO
    {
        public string LessonTitle { get; set; }
        public List<SearchQueryDTO> Queries { get; set; } = new();
    }

    public class SearchQueryDTO
    {
        public string Provider { get; set; } // "Youtube" or "CustomSearch"
        public string Query { get; set; }
        public List<string> ExcludedSites { get; set; }
        public int MaxResults { get; set; }
    }
}
