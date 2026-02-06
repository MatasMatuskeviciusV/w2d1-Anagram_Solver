using Microsoft.AspNetCore.Mvc;
using AnagramSolver.Contracts;
using AnagramSolver.BusinessLogic;
using AnagramSolver.WebApp.Models;

namespace AnagramSolver.WebApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly IAnagramSolver _solver;
        private readonly UserProcessor _userProcessor;

        public HomeController(IAnagramSolver solver, UserProcessor userProcessor)
        {
            _solver = solver;
            _userProcessor = userProcessor;
        }

        public async Task<IActionResult> Index(string? id, CancellationToken ct = default)
        {
            var model = new AnagramViewModel
            {
                Query = id ?? "",
                Results = new List<string>()
            };

            if (string.IsNullOrWhiteSpace(id))
            {
                return View(model);
            }

            if (!_userProcessor.IsValid(id))
            {
                model.Error = "Įvestas per trumpas žodis.";
                return View(model);
            }

            var normalizer = new WordNormalizer();
            var combined = normalizer.NormalizeUserWords(id);
            var key = AnagramKeyBuilder.BuildKey(combined);

            model.Results = (await _solver.GetAnagramsAsync(key, ct)).ToList();

            return View(model);
        }
    }
}