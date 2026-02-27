using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using ResultPattern.Results.Errors;

namespace ResultPattern.Extension.MinimalAPI;

public class ResponseHelper
{

	private readonly IHostEnvironment _hostEnvironment;

	public ResponseHelper(IHostEnvironment hostEnvironment) => _hostEnvironment = hostEnvironment;

	public IResult ToProblemResult(List<Error> errors)
	{
		if (errors.Count is 0)
			return Microsoft.AspNetCore.Http.Results.Problem();

		if (errors.All(err => err.StatusCode == HttpStatusCode.UnprocessableEntity))
			return ValidationProblem(errors);

		return Problem(errors[0]);
	}

	private IResult ValidationProblem(List<Error> errors)
	{

		var messages = errors
			.Select(e => e.Description)
			.Where(d => !string.IsNullOrWhiteSpace(d))
			.Distinct()
			.ToArray();

		var errorsDic = new Dictionary<string, string[]>
		{
			["errors"] = messages.Length > 0 ? messages : ["Validation error"]
		};

		Dictionary<string, object?>? extensions = null;

		if (_hostEnvironment.IsDevelopment())
		{
			extensions = new Dictionary<string, object?>
			{
				["stackTraces"] = errors.Select(e => new
					{
						e.Description,
						e.StackTrace
					})
					.ToArray()
			};
		}

		return Microsoft.AspNetCore.Http.Results.ValidationProblem(
			errors: errorsDic,
			statusCode: StatusCodes.Status422UnprocessableEntity,
			title: "Validation",
			detail: "One or more validation errors occurred.",
			extensions: extensions);
	}

	private IResult Problem(Error error)
	{
		var extensions = new Dictionary<string, object?>();

		if (_hostEnvironment.IsDevelopment())
		{
			extensions["stackTrace"] = error.StackTrace;
		}

		return Microsoft.AspNetCore.Http.Results.Problem(title: error.Code, detail: error.Description,
			extensions: extensions.Count > 0 ? extensions : null, statusCode: (int)error.StatusCode);
	}
}