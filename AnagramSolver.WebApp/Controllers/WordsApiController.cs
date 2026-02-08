using Microsoft.AspNetCore.Mvc;
using AnagramSolver.Contracts;

namespace AnagramSolver.WebApp.Controllers
{
    [ApiController]
    [Route("api/words")]
    public class WordsApiController : ControllerBase
    {
        private readonly IWordRepository _repo;
        private readonly IConfiguration _cfg;
        private readonly IWebHostEnvironment _env;

        public WordsApiController(IWordRepository repo, IConfiguration config, IWebHostEnvironment env)
        {
            _repo = repo;
            _cfg = config;
            _env = env;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<string>>> Get(int page = 1, int pageSize = 100)
        {
            if (page < 1)
            {
                page = 1;
            }

            if(pageSize < 1)
            {
                pageSize = 1;
            }

            var all = (await _repo.GetAllWordsAsync()).Select(w => (w ?? "").Trim()).Where(w => w.Length > 0).ToList();

            var items = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return Ok(items);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<IEnumerable<string>>> GetById(int id)
        {
            var all = (await _repo.GetAllWordsAsync()).Where(w => !string.IsNullOrWhiteSpace(w)).ToList();

            if(id < 0 || id >= all.Count)
            {
                return NotFound();
            }

            return Ok(all[id]);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] string word, CancellationToken ct)
        {
            var result = await _repo.AddWordAsync(word, ct);

            if (result == AddWordResult.Added)
            {
                return Created("", new { status = "added", word });
            }

            if(result == AddWordResult.AlreadyExists)
            {
                return Conflict(new { status = "already-exists" });
            }

            if(result == AddWordResult.Invalid)
            {
                return BadRequest(new { status = "invalid" });
            }

            return StatusCode(500);
        }

        [HttpDelete("{id:int}")]
        public ActionResult Delete(int id)
        {
            return Ok($"Žodis '{id}' ištrintas.");    //cia fake metodas, priminimas pridet funkcionaluma
        }

        [HttpGet("download")]
        public IActionResult DownloadDictionary()
        {
            var relative = _cfg["Dictionary:WordFilePath"];
            if (string.IsNullOrWhiteSpace(relative))
            {
                return StatusCode(500);
            }

            var fullPath = System.IO.Path.Combine(_env.ContentRootPath, relative);

            if (!System.IO.File.Exists(fullPath))
            {
                return NotFound();
            }

            var downloadName = System.IO.Path.GetFileName(fullPath);

            return PhysicalFile(fullPath, "text/plain", downloadName);
        }
    }
}
