using Microsoft.AspNetCore.Mvc;

namespace AnagramSolver.WebApp.Controllers;

public class AiController : Controller
{
    public IActionResult AiChat()
    {
        return View();
    }
}
