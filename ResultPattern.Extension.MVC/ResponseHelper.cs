using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Hosting;
using ResultPattern.Results.Errors;

namespace ResultPattern.Extension.MVC;

public class ResponseHelper
{
    private readonly IHostEnvironment _env;
    private readonly ProblemDetailsFactory _problemDetailsFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ResponseHelper(
        IHostEnvironment env,
        ProblemDetailsFactory problemDetailsFactory,
        IHttpContextAccessor httpContextAccessor)
    {
        _env = env;
        _problemDetailsFactory = problemDetailsFactory;
        _httpContextAccessor = httpContextAccessor;
    }

    private HttpContext HttpContext =>
        _httpContextAccessor.HttpContext
        ?? throw new InvalidOperationException("No active HttpContext. ResponseHelper can only be used within an HTTP request.");

    public ActionResult ToProblemResult(List<Error> errors)
    {
        if (errors is null || errors.Count == 0)
        {
            var pd = _problemDetailsFactory.CreateProblemDetails(HttpContext, statusCode: StatusCodes.Status500InternalServerError);
            return new ObjectResult(pd) { StatusCode = pd.Status };
        }

        if (errors.All(e => e.StatusCode == HttpStatusCode.UnprocessableEntity))
            return ValidationProblemResult(errors);

        return ProblemResult(errors[0]);
    }

    private ActionResult ValidationProblemResult(List<Error> errors)
    {
        var messages = errors
            .Select(e => e.Description)
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Distinct()
            .ToArray();

        var modelState = new ModelStateDictionary();
        foreach (var msg in messages.Length > 0 ? messages : new[] { "Validation error" })
            modelState.AddModelError("errors", msg);

        var vpd = _problemDetailsFactory.CreateValidationProblemDetails(
            HttpContext,
            modelState,
            statusCode: StatusCodes.Status422UnprocessableEntity,
            title: "Validation",
            detail: "One or more validation errors occurred."
        );

        if (_env.IsDevelopment())
        {
            vpd.Extensions["stackTraces"] = errors.Select(e => new
            {
                e.Description,
                e.StackTrace
            }).ToArray();
        }

        return new ObjectResult(vpd) { StatusCode = vpd.Status };
    }

    private ActionResult ProblemResult(Error error)
    {
        var pd = _problemDetailsFactory.CreateProblemDetails(
            HttpContext,
            statusCode: (int)error.StatusCode,
            title: error.Code,
            detail: error.Description
        );

        if (_env.IsDevelopment())
            pd.Extensions["stackTrace"] = error.StackTrace;

        return new ObjectResult(pd) { StatusCode = pd.Status };
    }
}