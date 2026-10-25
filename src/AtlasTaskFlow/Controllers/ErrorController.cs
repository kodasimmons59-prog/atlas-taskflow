using System.Diagnostics;
using AtlasTaskFlow.Data;
using AtlasTaskFlow.Models;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AtlasTaskFlow.Controllers;

/// <summary>
/// The page users see when a request fails. It deliberately has no database dependencies (unlike the other
/// controllers), so it can still explain the problem when the database is unreachable.
/// </summary>
[Route("Error")]
public class ErrorController : Controller
{
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Index()
    {
        var model = new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier };
        var error = HttpContext.Features.Get<IExceptionHandlerFeature>()?.Error;

        if (error is not null && DataErrors.IsDatabaseUnavailable(error))
        {
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return View("Database", model);
        }
        return View(model);
    }
}
