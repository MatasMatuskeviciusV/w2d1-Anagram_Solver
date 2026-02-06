using Microsoft.AspNetCore.Mvc;
using AnagramSolver.Contracts;
using AnagramSolver.WebApp.Models;

namespace AnagramSolver.WebApp.Controllers
{
    public class WordsController : Controller
    {
        private readonly IWordRepository _repo;
        private int PageSize = 100;

        public WordsController(IWordRepository repo)
        {
            _repo = repo;
        }

        public async Task<IActionResult> Index(int page = 1)
        {
            if (page < 1)
            {
                page = 1;
            }

            var all = (await _repo.GetAllWordsAsync()).Select(w => (w ?? "").Trim()).Where(w => w.Length > 0).ToList();

            var totalPages = (int)Math.Ceiling(all.Count / (double)PageSize);
            if (totalPages == 0)
            {
                totalPages = 1;
            }
            if (page > totalPages)
            {
                page = totalPages;
            }

            var items = all.Skip((page - 1) * PageSize).Take(PageSize).ToList();

            var model = new PagedWordsViewModel
            {
                Items = items,
                CurrentPage = page,
                TotalPages = totalPages
            };

            return View(model);
        }
    }
}
