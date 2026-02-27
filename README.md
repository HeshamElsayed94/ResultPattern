# ResultPattern — Solution Overview

A modular, zero-exception result pattern library for .NET.  
Represent operation outcomes without throwing exceptions — with built-in stack trace diagnostics, multi-error aggregation, and first-class HTTP Problem Details support.

---

## 📦 Packages

| Package | Description |
|---|---|
| [`ResultPattern`](#-resultpattern) | Core library — `Result<T>`, `Error`, `Ensure`, `Combine` |
| [`ResultPattern.Extension.MinimalAPI`](#-resultpatternextensionminimalapi) | Problem Details mapping for Minimal API endpoints |
| [`ResultPattern.Extension.MVC`](#-resultpatternextensionmvc) | Problem Details mapping for MVC & Web API controllers |

---

## ✨ Why ResultPattern?

Most result pattern libraries tell you *what* went wrong. This one also tells you **where**.

Every `Error` automatically captures a `ResultStackTrace` at the call site via compiler attributes — no manual tracking, no extra setup.

```
[NotFound Error] Restaurant with id '5' not found.
  at Handle in DeleteRestaurantCommandHandler.cs:line 14
```

Key design principles:
- **No exceptions** for control flow — failures are values, not surprises
- **Full error aggregation** — surface all violations in a single response, never one at a time
- **Zero production leakage** — stack traces are omitted automatically outside Development
- **Implicit conversions** — return errors or values directly without wrapping calls
- **HTTP-ready** — every `Error` carries an `HttpStatusCode` that maps directly to a response

---

## 📦 ResultPattern

The core package. Install this in every project that produces or consumes results.

```bash
dotnet add package ResultPattern
```

### `Result<TValue>`

Holds **either** a success value or a list of errors — never both.

| Member | Type | Description |
|---|---|---|
| `Value` | `TValue` | The success value. Safe only when `ISuccess` is `true`. |
| `Errors` | `IReadOnlyList<Error>?` | `null` on success; populated on failure. |
| `ISuccess` | `bool` | `true` when no errors are present. |

```csharp
Result<int> fromValue  = 42;
Result<int> fromError  = Error.NotFound("Item not found.");
Result<int> fromErrors = new List<Error> { Error.Validation("Name required."), Error.Validation("Email required.") };
```

### `Success`

A `readonly record struct` for void-like operations — equivalent to `Unit` in functional languages.

```csharp
return Result.Success;
```

### `Error` — With Built-in Stack Trace

An immutable `record` that captures failure context automatically via `[CallerFilePath]`, `[CallerLineNumber]`, and `[CallerMemberName]`.

| Property | Type | Description |
|---|---|---|
| `Code` | `string` | Machine-readable category (e.g. `"NotFound"`). |
| `Description` | `string` | Human-readable message. |
| `StatusCode` | `HttpStatusCode` | Maps directly to an HTTP response code. |
| `StackTrace.FileName` | `string` | Source file where the error was created. |
| `StackTrace.MemberName` | `string` | Method that created the error. |
| `StackTrace.LineNumber` | `int` | Exact line number of the error creation. |

> `StackTrace` is `[JsonIgnore]` — it never leaks into API responses.

#### Error Factories

| Method | HTTP Status | Default Description |
|---|---|---|
| `Error.Failure(...)` | `500 Internal Server Error` | `"General failure."` |
| `Error.Unexpected(...)` | `500 Internal Server Error` | `"Unexpected error."` |
| `Error.Validation(...)` | `422 Unprocessable Entity` | `"Validation error"` |
| `Error.Conflict(...)` | `409 Conflict` | `"Conflict error"` |
| `Error.NotFound(...)` | `404 Not Found` | `"Not found error"` |
| `Error.Unauthorized(...)` | `401 Unauthorized` | `"Unauthorized error"` |
| `Error.Forbidden(...)` | `403 Forbidden` | `"Forbidden error"` |

```csharp
// Only pass description — all caller info is compiler-filled
return Error.NotFound($"Restaurant with id '{id}' not found.");
return Error.Forbidden("You do not have permission to perform this action.");
return Error.Validation("Email address is not valid.");
```

---

### `Result.Ensure<T>` — Multi-Rule Validation

Validates a **single value** against one or more rules.  
Each rule is a `(Predicate<T> predicate, Error error)` tuple.

> ⚠️ A predicate returning **`true` signals a failure**.  
> **All predicates always run** — errors are aggregated, not short-circuited.

```csharp
// Validating primitives — method group syntax keeps rules concise
var nameValidation = Result.Ensure(name,
    (string.IsNullOrEmpty,   Error.Validation("Name cannot be null or empty.")),
    (e => e?.Length < 3,     Error.Validation("Name cannot be less than 3 characters."))
);

// Validating a domain object
var ensureResult = Result.Ensure(restaurant,
    (
        r => !restaurantAuthorization.Authorize(r, ResourceOperation.Delete),
        Error.Forbidden($"You are not the owner of restaurant '{request.Id}'.")
    ),
    (
        r => r.HasActiveOrders,
        Error.Conflict($"Restaurant '{request.Id}' has active orders and cannot be deleted.")
    )
);
```

---

### `Result.Combine` — Aggregate Independent Results

Merges a collection of pre-computed `IResult` instances into one.  
**Does not short-circuit** — all results are evaluated and all failures are collected.

```csharp
// Ideal for domain factory methods — validate each field independently
public static Result<User> Create(string name, string email)
{
    var nameValidation = Result.Ensure(name,
        (string.IsNullOrEmpty,   Error.Validation("Name cannot be null or empty.")),
        (e => e?.Length < 3,     Error.Validation("Name cannot be less than 3 characters."))
    );

    var emailValidation = Result.Ensure(email,
        (string.IsNullOrEmpty,           Error.Validation("Email cannot be null or empty.")),
        (e => e?.Length < 3,             Error.Validation("Email cannot be less than 3 characters.")),
        (e => e?.Split('@').Length != 2, Error.Validation("Invalid email address."))
    );

    var combined = Result.Combine([nameValidation, emailValidation]);

    if (!combined.ISuccess)
        return combined.Errors!.ToList();

    return new User(name, email);
}
```

---

### `Match<TNextValue>` — Pattern Matching

Project the result into another type without `if` checks.

```csharp
return result.Match(
    onValue: _ => Results.NoContent(),
    onError: errors => Results.UnprocessableEntity(errors)
);
```

---

### `Ensure` vs `Combine`

| | `Ensure` | `Combine` |
|---|---|---|
| Input | A single value + validation rules | A collection of pre-computed results |
| Evaluation | All predicates always run | All results always evaluated |
| Short-circuits | ❌ No | ❌ No |
| Error aggregation | ✅ All failures collected | ✅ All failures collected |
| Typical use | Single-field or entity validation | Merging multiple independent validations |

---

## 📦 ResultPattern.Extension.MinimalAPI

Problem Details mapping for Minimal API endpoint delegates.

```bash
dotnet add package ResultPattern.Extension.MinimalAPI
```

### Register

```csharp
builder.Services.AddResponseHelper();
```

### Use

```csharp
app.MapDelete("/restaurants/{id}", async (
    Guid id,
    ISender sender,
    ResponseHelper responseHelper,
    CancellationToken ct) =>
{
    var command = new DeleteRestaurantCommand(id);

    Result<Success> result = await sender.Send(command, ct);

    return result.Match(
        _ => Results.NoContent(),
        responseHelper.ToProblemResult);
})
.RequireAuthorization();
```

### `ToProblemResult` Routing

| Condition | Response Type | Status Code |
|---|---|---|
| All errors are `422` | `ValidationProblem` | `422` |
| Mixed or single non-validation error | `Problem` | Error's own `HttpStatusCode` |
| Empty error list | Generic `Problem` | `500` |

---

## 📦 ResultPattern.Extension.MVC

Problem Details mapping for MVC & Web API controllers — any class inheriting `ControllerBase`.

```bash
dotnet add package ResultPattern.Extension.MVC
```

### Register

```csharp
builder.Services.AddResponseHelper();
```

### Use

```csharp
[ApiController]
[Route("api/[controller]")]
public class RestaurantsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ResponseHelper _responseHelper;

    public RestaurantsController(ISender sender, ResponseHelper responseHelper)
    {
        _sender = sender;
        _responseHelper = responseHelper;
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var command = new DeleteRestaurantCommand(id);

        Result<Success> result = await _sender.Send(command, ct);

        return result.Match(
            _ => NoContent(),
            _responseHelper.ToProblemResult);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateRestaurantRequest request, CancellationToken ct)
    {
        var command = new CreateRestaurantCommand(request.Name, request.Address);

        Result<RestaurantResponse> result = await _sender.Send(command, ct);

        return result.Match(
            restaurant => CreatedAtAction(nameof(GetById), new { id = restaurant.Id }, restaurant),
            _responseHelper.ToProblemResult);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var query = new GetRestaurantByIdQuery(id);

        Result<RestaurantResponse> result = await _sender.Send(query, ct);

        return result.Match(
            restaurant => Ok(restaurant),
            _responseHelper.ToProblemResult);
    }
}
```

### `ToProblemResult` Routing

| Condition | Response Type | Status Code |
|---|---|---|
| All errors are `422` | `ValidationProblemDetails` | `422` |
| Mixed or single non-validation error | `ProblemDetails` | Error's own `HttpStatusCode` |
| Null or empty error list | Generic `ProblemDetails` | `500` |

---

## Environment-Aware Diagnostics

Both extension packages share identical environment behavior:

| Field | Development | Production |
|---|---|---|
| `extensions.stackTrace` | ✅ Included | ❌ Omitted |
| `extensions.stackTraces` | ✅ Included | ❌ Omitted |
| Error descriptions | ✅ Included | ✅ Included |
| HTTP status codes | ✅ Included | ✅ Included |

---

## Extension Package Comparison

| | `ResultPattern.Extension.MinimalAPI` | `ResultPattern.Extension.MVC` |
|---|---|---|
| Return type | `IResult` | `ActionResult` |
| Factory | `Results.Problem` / `Results.ValidationProblem` | `ProblemDetailsFactory` |
| `HttpContext` source | Endpoint delegate parameter | `IHttpContextAccessor` |
| Use in | `app.Map*` endpoint delegates | `ControllerBase` subclasses (MVC & Web API) |
| Stack trace in Dev | ✅ Yes | ✅ Yes |
