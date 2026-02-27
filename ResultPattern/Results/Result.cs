using System.ComponentModel;
using System.Text.Json.Serialization;
using ResultPattern.Results.Errors;
using ResultPattern.Results.Interfaces;

namespace ResultPattern.Results;

public static class Result
{
	public static Success Success => default;

	/// <summary>
	/// Aggregates multiple <see cref="IResult"/> instances into a single result.
	/// </summary>
	/// <remarks>
	/// <para>
	/// If all supplied results are successful, a successful <see cref="Result{Success}"/> is returned.
	/// </para>
	/// <para>
	/// If one or more results have failed, a failed result is returned containing
	/// the union of all errors from the failed results.
	/// </para>
	/// <para>
	/// This method does not short-circuit; all results are evaluated before aggregation.
	/// </para>
	/// </remarks>
	/// <param name="results">
	/// The collection of results to combine.
	/// </param>
	/// <returns>
	/// A success result when all inputs are successful; otherwise,
	/// a failed result containing the aggregated errors.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="results"/> is <c>null</c>.
	/// </exception>
	public static Result<Success> Combine(IEnumerable<IResult> results)
	{
		ArgumentNullException.ThrowIfNull(results);

		var failedResults = results.Where(r => !r.ISuccess).ToArray();

		if (failedResults.Length is 0)
			return Result.Success;

		var errors = failedResults
			.SelectMany(r => r.Errors!)
			.ToList();

		return errors;
	}

	/// <summary>
	/// Validates a value against one or more predicates and returns a result representing the validation outcome.
	/// </summary>
	/// <typeparam name="T">
	/// The type of the value being validated.
	/// </typeparam>
	/// <remarks>
	/// <para>
	/// Each validation rule consists of a predicate and an associated <see cref="Error"/>.
	/// </para>
	/// <para>
	/// A predicate returning <c>true</c> indicates a validation failure,
	/// and its corresponding error will be collected.
	/// </para>
	/// <para>
	/// If no validation failures occur, a successful <see cref="Result{T}"/> containing
	/// the original value is returned.
	/// </para>
	/// <para>
	/// This method evaluates all predicates and aggregates all validation errors.
	/// </para>
	/// </remarks>
	/// <param name="value">
	/// The value to validate.
	/// </param>
	/// <param name="functions">
	/// An array of validation rules defined as tuples of
	/// (<see cref="Predicate{T}"/> predicate, <see cref="Error"/> error).
	/// </param>
	/// <returns>
	/// A successful result containing <paramref name="value"/> if validation passes;
	/// otherwise, a failed result containing all validation errors.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when any supplied predicate is <c>null</c>.
	/// </exception>
	public static Result<T> Ensure<T>(T value, params ( Predicate<T> predicate, Error error)[] functions)
	{
		var results = new List<Result<T>>();

		foreach (var (predicate, error) in functions)
		{
			if (predicate is null) throw new ArgumentNullException(nameof(predicate));

			if (predicate(value))
				results.Add(error);
		}

		var combinedResult = Result.Combine(results);

		if (combinedResult.ISuccess)
			return value;

		return combinedResult.Errors!.ToList();
	}
}

public sealed class Result<TValue> : IResult<TValue>
{
	private readonly TValue? _value;

	private readonly List<Error>? _errors;

	public TValue Value => _value!;

	public IReadOnlyList<Error>? Errors => _errors;

	public bool ISuccess => Errors is null;

	[JsonConstructor]
	private Result(TValue? value, List<Error>? errors)
	{
		if (value is not null)
		{
			_value = value;
		}
		else
		{
			if (errors == null || errors.Count == 0)
				throw new ArgumentException("Provide at least one error.", nameof(errors));

			_errors = errors;
		}

	}

	private Result(TValue value)
	{
		if (value is null)
			throw new ArgumentNullException(nameof(value));

		_value = value;
	}

	private Result(Error error) => _errors = [error];

	private Result(List<Error> errors)
	{

		if (errors is null || errors.Count == 0)
			throw new ArgumentException("Cannot create Errors from an empty collection of errors. Provide at least one error.",
				nameof(errors));

		_errors = errors;
	}

	public static implicit operator Result<TValue>(TValue value) => new(value);

	public static implicit operator Result<TValue>(Error error) => new(error);

	public static implicit operator Result<TValue>(List<Error> errors) => new(errors);

	public TNextValue Match<TNextValue>(Func<TValue, TNextValue> onValue, Func<List<Error>, TNextValue> onError)
		=> !ISuccess ? onError(_errors!) : onValue(Value);
}

public readonly record struct Success;