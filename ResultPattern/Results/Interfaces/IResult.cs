using ResultPattern.Results.Errors;

namespace ResultPattern.Results.Interfaces;
/// <summary>Base interface for all result types, exposing error state and success flag.</summary>
public interface IResult
{
	/// <summary>The list of errors when the result is failed; <c>null</c> when successful.</summary>
	IReadOnlyList<Error>? Errors { get; }

	/// <summary><c>true</c> if the result is successful; <c>false</c> if it contains errors.</summary>
	bool IsSuccess { get; }
}

/// <summary>Extends <see cref="IResult"/> with a typed success value.</summary>
/// <typeparam name="TValue">The type of the value returned on success.</typeparam>
public interface IResult<out TValue> : IResult
{
	/// <summary>The success value. Only access this when <see cref="IResult.IsSuccess"/> is <c>true</c>.</summary>
	TValue Value { get; }
}