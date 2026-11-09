using AtlasTaskFlow.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;

namespace AtlasTaskFlow.Data;

/// <summary>
/// Turns a save that the database rejected (for example two people creating a project with the same name at the
/// same moment) into a friendly message on the page the user came from, instead of an error page. Nothing from the
/// failed action is stored, because each action saves in one transaction. Other database failures carry on to the
/// error page (<see cref="ErrorController"/>).
/// </summary>
public class DataErrorFilter : IExceptionFilter
{
    public const string SaveRejectedMessage =
        "We couldn't save that because it conflicts with data that's already saved. Nothing was changed. Please check and try again.";
    public const string ChangedByOthersMessage =
        "Someone else changed or deleted this while you were working on it. Nothing was saved. Please reload and try again.";

    private readonly ITempDataDictionaryFactory _tempData;
    private readonly ILogger<DataErrorFilter> _logger;

    public DataErrorFilter(ITempDataDictionaryFactory tempData, ILogger<DataErrorFilter> logger)
    {
        _tempData = tempData;
        _logger = logger;
    }

    public void OnException(ExceptionContext context)
    {
        var ex = context.Exception;
        var message = ex is DbUpdateConcurrencyException ? ChangedByOthersMessage
            : DataErrors.IsConstraintViolation(ex) ? SaveRejectedMessage
            : null;
        if (message is null || !HttpMethods.IsPost(context.HttpContext.Request.Method)) return;

        // Full details go to the log for the developer; the user only sees the plain message.
        _logger.LogWarning(ex, "A save was rejected by the database on {Path}", context.HttpContext.Request.Path);
        _tempData.GetTempData(context.HttpContext)["Error"] = message;

        var referer = context.HttpContext.Request.Headers.Referer.ToString();
        var back = Uri.TryCreate(referer, UriKind.Absolute, out var uri) && uri.Host == context.HttpContext.Request.Host.Host
            ? uri.PathAndQuery
            : "/";
        context.Result = new LocalRedirectResult(back);
        context.ExceptionHandled = true;
    }
}
