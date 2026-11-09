using System.Diagnostics;
using AtlasTaskFlow.Data;
using AtlasTaskFlow.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AtlasTaskFlow.Controllers;

/// <summary>
/// The page users see when a request fails. It deliberately has no database dependencies (unlike the other
/// controllers), so it can still explain the problem when the database is unreachable.
/// </summary>
[AllowAnonymous]
public class ErrorController : Controller
{
    [Route("Error")]
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

    /// <summary>Friendly pages for 404 and other status codes, for signed-in and signed-out visitors alike.</summary>
    [Route("Home/Status/{code:int}")]
    public IActionResult Status(int code)
    {
        Response.StatusCode = code;
        return View(code);
    }
}
