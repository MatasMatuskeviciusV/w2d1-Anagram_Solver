using Microsoft.AspNetCore.Mvc;
using AnagramSolver.Contracts;
using AnagramSolver.BusinessLogic;
using AnagramSolver.WebApp.Models;
using System.Text.Json;

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
            if (!string.IsNullOrWhiteSpace(id))
            {
                Response.Cookies.Append("lastSearch", id, new CookieOptions
                {
                    Expires = DateTimeOffset.Now.AddDays(30)
                });
            }

            var lastSearch = Request.Cookies["lastSearch"];
            ViewBag.LastSearch = lastSearch;

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

            if(!string.IsNullOrWhiteSpace(id) && _userProcessor.IsValid(id))
            {
                const string shKey = "searchHistory";

                var json = HttpContext.Session.GetString(shKey);

                List<string> history;

                if (string.IsNullOrEmpty(json))
                {
                    history = new List<string>();
                }
                else
                {
                    history = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
                }

                history.Remove(id);
                history.Insert(0, id);

                HttpContext.Session.SetString(shKey, JsonSerializer.Serialize(history));
            }

            var normalizer = new WordNormalizer();
            var combined = normalizer.NormalizeUserWords(id);
            var key = AnagramKeySorter.BuildKey(combined);

            model.Results = (await _solver.GetAnagramsAsync(key, ct)).ToList();

            return View(model);
        }
    }
}