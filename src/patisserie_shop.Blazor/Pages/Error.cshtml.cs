using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace patisserie_shop.Blazor.Pages;

[AllowAnonymous]
[IgnoreAntiforgeryToken(Order = 1001)]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class WarmErrorModel : PageModel
{
    public int HttpStatusCode { get; private set; }
    public string TitleKey { get; private set; } = "ErrorPage:UnexpectedTitle";
    public string DescriptionKey { get; private set; } = "ErrorPage:UnexpectedDescription";
    public string IconName { get; private set; } = "error_outline";
    public string? OriginalPath { get; private set; }

    public void OnGet(int? statusCode) => Prepare(statusCode);
    public void OnPost(int? statusCode) => Prepare(statusCode);
    public void OnPut(int? statusCode) => Prepare(statusCode);
    public void OnDelete(int? statusCode) => Prepare(statusCode);
    public void OnPatch(int? statusCode) => Prepare(statusCode);

    private void Prepare(int? statusCode)
    {
        HttpStatusCode = statusCode is >= 400 and <= 599 ? statusCode.Value : 500;
        Response.StatusCode = HttpStatusCode;

        (TitleKey, DescriptionKey, IconName) = HttpStatusCode switch
        {
            400 => ("ErrorPage:BadRequestTitle", "ErrorPage:BadRequestDescription", "rule"),
            401 => ("ErrorPage:AuthenticationTitle", "ErrorPage:AuthenticationDescription", "login"),
            403 => ("Account:AccessDeniedTitle", "Account:AccessDeniedDescription", "lock_person"),
            404 => ("ErrorPage:NotFoundTitle", "ErrorPage:NotFoundDescription", "search_off"),
            _ => ("ErrorPage:UnexpectedTitle", "ErrorPage:UnexpectedDescription", "error_outline")
        };

        var statusFeature = HttpContext.Features.Get<IStatusCodeReExecuteFeature>();
        var exceptionFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
        var originalPath = statusFeature?.OriginalPath ?? exceptionFeature?.Path;
        if (!string.IsNullOrWhiteSpace(originalPath) &&
            !originalPath.StartsWith("/error", StringComparison.OrdinalIgnoreCase))
        {
            OriginalPath = originalPath;
        }
    }
}
