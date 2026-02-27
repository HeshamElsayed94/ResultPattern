using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace ResultPattern.Results.Errors;

public record Error
{
	public string Code { get; }

	public string Description { get; }

	public HttpStatusCode StatusCode { get; }

	[JsonIgnore]
	public ResultStackTrace StackTrace { get; }

	[JsonConstructor]
	private Error(string code, string description, HttpStatusCode statusCode, ResultStackTrace stackTrace)
	{
		Code = code;
		Description = description;
		StatusCode = statusCode;

		var fileName = string.IsNullOrWhiteSpace(stackTrace.FileName)
			? string.Empty
			: Path.GetFileName(stackTrace.FileName);

		StackTrace = stackTrace with { FileName = fileName };

	}

	/// <summary>
	/// Creates an Internal Server Error (500) representing a general failure.
	/// </summary>
	/// <param name="description">Human-readable error message.</param>
	/// <param name="lineNumber">⚠️ Do not pass — auto-filled by compiler.</param>
	/// <param name="filePath">⚠️ Do not pass — auto-filled by compiler.</param>
	/// <param name="memberName">⚠️ Do not pass — auto-filled by compiler.</param>
	public static Error Failure(string description = "General failure.",
		[CallerLineNumber] int lineNumber = 0,
		[CallerFilePath] string filePath = "",
		[CallerMemberName] string memberName = "")
		=> new(nameof(Failure), description, HttpStatusCode.InternalServerError,
			new(lineNumber, filePath, memberName));

	/// <summary>
	/// Creates an Internal Server Error (500) representing an unexpected exception.
	/// </summary>
	/// <param name="description">Human-readable error message.</param>
	/// <param name="lineNumber">⚠️ Do not pass — auto-filled by compiler.</param>
	/// <param name="filePath">⚠️ Do not pass — auto-filled by compiler.</param>
	/// <param name="memberName">⚠️ Do not pass — auto-filled by compiler.</param>
	public static Error Unexpected(string description = "Unexpected error.",
		[CallerLineNumber] int lineNumber = 0,
		[CallerFilePath] string filePath = "",
		[CallerMemberName] string memberName = "")
		=> new(nameof(Unexpected), description, HttpStatusCode.InternalServerError,
			new(lineNumber, filePath, memberName));

	/// <summary>
	/// Creates a Validation error (422 - Unprocessable Entity).
	/// </summary>
	/// <param name="description">Details about the validation failure.</param>
	/// <param name="lineNumber">⚠️ Do not pass — auto-filled by compiler.</param>
	/// <param name="filePath">⚠️ Do not pass — auto-filled by compiler.</param>
	/// <param name="memberName">⚠️ Do not pass — auto-filled by compiler.</param>
	public static Error Validation(string description = "Validation error",
		[CallerLineNumber] int lineNumber = 0,
		[CallerFilePath] string filePath = "",
		[CallerMemberName] string memberName = "")
		=> new(nameof(Validation), description, HttpStatusCode.UnprocessableEntity,
			new(lineNumber, filePath, memberName));

	/// <summary>
	/// Creates a Conflict error (409).
	/// </summary>
	/// <param name="description">Details about the resource conflict.</param>
	/// <param name="lineNumber">⚠️ Do not pass — auto-filled by compiler.</param>
	/// <param name="filePath">⚠️ Do not pass — auto-filled by compiler.</param>
	/// <param name="memberName">⚠️ Do not pass — auto-filled by compiler.</param>
	public static Error Conflict(string description = "Conflict error",
		[CallerLineNumber] int lineNumber = 0,
		[CallerFilePath] string filePath = "",
		[CallerMemberName] string memberName = "")
		=> new(nameof(Conflict), description, HttpStatusCode.Conflict,
			new(lineNumber, filePath, memberName));

	/// <summary>
	/// Creates a Not Found error (404).
	/// </summary>
	/// <param name="description">Details about the missing resource.</param>
	/// <param name="lineNumber">⚠️ Do not pass — auto-filled by compiler.</param>
	/// <param name="filePath">⚠️ Do not pass — auto-filled by compiler.</param>
	/// <param name="memberName">⚠️ Do not pass — auto-filled by compiler.</param>
	public static Error NotFound(string description = "Not found error",
		[CallerLineNumber] int lineNumber = 0,
		[CallerFilePath] string filePath = "",
		[CallerMemberName] string memberName = "")
		=> new(nameof(NotFound), description, HttpStatusCode.NotFound,
			new(lineNumber, filePath, memberName));

	/// <summary>
	/// Creates an Unauthorized error (401).
	/// </summary>
	/// <param name="description">Details about the authentication failure.</param>
	/// <param name="lineNumber">⚠️ Do not pass — auto-filled by compiler.</param>
	/// <param name="filePath">⚠️ Do not pass — auto-filled by compiler.</param>
	/// <param name="memberName">⚠️ Do not pass — auto-filled by compiler.</param>
	public static Error Unauthorized(string description = "Unauthorized error",
		[CallerLineNumber] int lineNumber = 0,
		[CallerFilePath] string filePath = "",
		[CallerMemberName] string memberName = "")
		=> new(nameof(Unauthorized), description, HttpStatusCode.Unauthorized,
			new(lineNumber, filePath, memberName));

	/// <summary>
	/// Creates a Forbidden error (403).
	/// </summary>
	/// <param name="description">Details about the authorization failure.</param>
	/// <param name="lineNumber">⚠️ Do not pass — auto-filled by compiler.</param>
	/// <param name="filePath">⚠️ Do not pass — auto-filled by compiler.</param>
	/// <param name="memberName">⚠️ Do not pass — auto-filled by compiler.</param>
	public static Error Forbidden(string description = "Forbidden error",
		[CallerLineNumber] int lineNumber = 0,
		[CallerFilePath] string filePath = "",
		[CallerMemberName] string memberName = "")
		=> new(nameof(Forbidden), description, HttpStatusCode.Forbidden,
			new(lineNumber, filePath, memberName));

	public override string ToString()
		=> $"[{Code} Error] {Description}  at {StackTrace.MemberName} in {StackTrace.FileName}:line {StackTrace.LineNumber}";

}