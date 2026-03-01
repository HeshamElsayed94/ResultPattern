# ResultPattern — Solution Overview

A modular, zero-exception result pattern library for .NET.  
Represent operation outcomes without throwing exceptions — with built-in stack trace diagnostics, multi-error aggregation, and first-class HTTP Problem Details support.

---

## 📦 Packages

| Package | Description |
|---|---|
| `ResultPattern` | Core library — `Result<T>`, `Error`, `Ensure`, `Combine` |
| `ResultPattern.Extension.MinimalAPI` | Problem Details mapping for Minimal API endpoints |
| `ResultPattern.Extension.MVC` | Problem Details mapping for MVC & Web API controllers |

---

## ✨ Why ResultPattern?

Most result pattern libraries tell you *what* went wrong. This one also tells you **where**.

Every `Error` automatically captures a `ResultStackTrace` at the call site via compiler attributes — no manual tracking, no extra setup.

```
[NotFound Error] Restaurant with id '5' not found.
  at DeleteRestaurantCommandHandler.cs:line 14
```

### Key Design Principles

- No exceptions for control flow — failures are values
- Full error aggregation — surface all violations at once
- Zero production leakage — stack traces omitted outside Development
- Implicit conversions — return values or errors naturally
- HTTP-ready — each `Error` carries an `HttpStatusCode`

---

# 📦 ResultPattern

Install in any project that produces or consumes results.

```bash
dotnet add package ResultPattern
```

---

# `Result<TValue>`

Represents either a success value or a collection of errors — never both.

| Member | Type | Description |
|---|---|---|
| `Value` | `TValue` | Safe only when `ISuccess` is `true` |
| `Errors` | `IReadOnlyList<Error>?` | `null` on success |
| `ISuccess` | `bool` | `true` when no errors |

### Implicit Conversions

```csharp
Result<int> fromValue  = 42;
Result<int> fromError  = Error.NotFound("Item not found.");
Result<int> fromErrors = new List<Error>
{
    Error.Validation("Name required."),
    Error.Validation("Email required.")
};
```

---

# `Success`

A `readonly record struct` used for operations without a return value.

```csharp
return Result.Success;
```

---

# `Error` — With Built-in Stack Trace

Immutable record representing categorized failure.

| Property | Type | Description |
|---|---|---|
| `Code` | `string` | Machine-readable category |
| `Description` | `string` | Human-readable message |
| `StatusCode` | `HttpStatusCode` | HTTP status mapping |
| `StackTrace.FileName` | `string` | Source file |
| `StackTrace.MemberName` | `string` | Method name |
| `StackTrace.LineNumber` | `int` | Line number |

> `StackTrace` is `[JsonIgnore]` and never appears in API responses.

### Error Factories

| Method | HTTP Status |
|---|---|
| `Error.Failure(...)` | 500 |
| `Error.Unexpected(...)` | 500 |
| `Error.Validation(...)` | 422 |
| `Error.Conflict(...)` | 409 |
| `Error.NotFound(...)` | 404 |
| `Error.Unauthorized(...)` | 401 |
| `Error.Forbidden(...)` | 403 |

```csharp
return Error.NotFound($"Restaurant with id '{id}' not found.");
return Error.Forbidden("You do not have permission.");
return Error.Validation("Invalid email.");
```

---

# 🆕 `Result.Ensure<T>` — Multi-Rule Validation

Validates a single value against multiple rules.

Each rule is:

```
(Predicate<T> predicate, Error error)
```

### Behavior

- Predicate returning `true` = PASS
- Predicate returning `false` = FAIL (error collected)
- All rules always run
- No short-circuiting

---

## Example — Primitive Validation

```csharp
var nameValidation = Result.Ensure(name,
    (n => !string.IsNullOrEmpty(n), Error.Validation("Name cannot be null or empty.")),
    (n => n?.Length >= 3, Error.Validation("Name must be at least 3 characters."))
);
```

---

## Example — Domain Rule Validation

```csharp
var ensureResult = Result.Ensure(restaurant,
    (
        r => restaurantAuthorization.Authorize(r, ResourceOperation.Delete),
        Error.Forbidden($"You are not the owner of restaurant '{request.Id}'.")
    ),
    (
        r => !r.HasActiveOrders,
        Error.Conflict($"Restaurant '{request.Id}' has active orders and cannot be deleted.")
    )
);

if (!ensureResult.ISuccess)
    return ensureResult.Errors!.ToList();
```

---

# 🆕 `Result.Combine`

Aggregates multiple pre-computed results into one.

- All succeed → `Result.Success`
- Any fail → returns all collected errors
- No short-circuiting

```csharp
public static Result<User> Create(string name, string email)
{
    var nameValidation = Result.Ensure(name,
        (n => !string.IsNullOrEmpty(n), Error.Validation("Name required.")),
        (n => n?.Length >= 3, Error.Validation("Name too short."))
    );

    var emailValidation = Result.Ensure(email,
        (e => !string.IsNullOrEmpty(e), Error.Validation("Email required.")),
        (e => e?.Length >= 3, Error.Validation("Email too short.")),
        (e => e?.Contains("@"), Error.Validation("Invalid email."))
    );

    var combined = Result.Combine([nameValidation, emailValidation]);

    if (!combined.ISuccess)
        return combined.Errors!.ToList();

    return new User(name, email);
}
```

---

# `Match<TNextValue>`

Pattern matching without `if` statements.

```csharp
return result.Match(
    onValue: _ => Results.NoContent(),
    onError: errors => Results.UnprocessableEntity(errors)
);
```

---

# `Ensure` vs `Combine`

| | `Ensure` | `Combine` |
|---|---|---|
| Input | Value + rules | Multiple results |
| Evaluation | All rules run | All results evaluated |
| Short-circuit | ❌ No | ❌ No |
| Aggregates errors | ✅ Yes | ✅ Yes |
| Use case | Entity/field validation | Merge validations |

---

# 📦 ResultPattern.Extension.MinimalAPI

```bash
dotnet add package ResultPattern.Extension.MinimalAPI
```

### Register

```csharp
builder.Services.AddResponseHelper();
```

### Usage

```csharp
app.MapDelete("/restaurants/{id}", async (
    Guid id,
    ISender sender,
    ResponseHelper responseHelper,
    CancellationToken ct) =>
{
    var result = await sender.Send(new DeleteRestaurantCommand(id), ct);

    return result.Match(
        _ => Results.NoContent(),
        responseHelper.ToProblemResult);
});
```

---

# 📦 ResultPattern.Extension.MVC

```bash
dotnet add package ResultPattern.Extension.MVC
```

### Register

```csharp
builder.Services.AddResponseHelper();
```

### Usage

```csharp
[HttpDelete("{id:guid}")]
public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
{
    var result = await _sender.Send(new DeleteRestaurantCommand(id), ct);

    return result.Match(
        _ => NoContent(),
        _responseHelper.ToProblemResult);
}
```

---

# Environment-Aware Diagnostics

| Field | Development | Production |
|---|---|---|
| stackTrace | ✅ Included | ❌ Omitted |
| stackTraces | ✅ Included | ❌ Omitted |
| Error descriptions | ✅ Included | ✅ Included |
| HTTP status codes | ✅ Included | ✅ Included |

---

# Extension Package Comparison

| | MinimalAPI | MVC |
|---|---|---|
| Return type | `IResult` | `ActionResult` |
| Factory | `Results.Problem` | `ProblemDetailsFactory` |
| HttpContext source | Endpoint parameter | `IHttpContextAccessor` |
| Use in | `app.Map*` | `ControllerBase` |
| Stack trace in Dev | ✅ | ✅ |

---
