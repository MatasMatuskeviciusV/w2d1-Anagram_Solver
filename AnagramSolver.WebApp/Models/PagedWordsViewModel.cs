namespace AnagramSolver.WebApp.Models
{
    public class PagedWordsViewModel
    {
        public List<string> Items { get; set; } = new();
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public bool HasPrevious => CurrentPage > 1;
        public bool HasNext => CurrentPage < TotalPages;
    }
}
