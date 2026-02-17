using Microsoft.AspNetCore.Mvc;
using AnagramSolver.Contracts;
using AnagramSolver.BusinessLogic;

namespace AnagramSolver.WebApp.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    public class AnagramsController : ControllerBase
    {
        private readonly IAnagramSolver _solver;
        private readonly UserProcessor _userProcessor;

        public AnagramsController(IAnagramSolver solver, UserProcessor userProcessor)
        {
            _solver = solver;
            _userProcessor = userProcessor;
        }

        [HttpGet("{word}")]
        public async Task<ActionResult<IEnumerable<string>>> Get(string word, CancellationToken ct)
        {
            if(string.IsNullOrWhiteSpace(word))
            {
                return BadRequest("Word is required.");
            }

            if (!_userProcessor.IsValid(word))
            {
                return BadRequest("Word is too short.");
            }

            var normalizer = new WordNormalizer();
            var combined = normalizer.NormalizeUserWords(word);
            var key = AnagramKeySorter.BuildKey(combined);

            var anagrams = await _solver.GetAnagramsAsync(key, ct);
            return Ok(anagrams);
        }

    }
}
