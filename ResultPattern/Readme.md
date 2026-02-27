# ResultPattern

A zero-exception, discriminated union for representing operation outcomes in .NET.  
Built around `Result<TValue>`, `Error`, and `Success` — designed for clean, expressive handler code.

> 🆕 **New in this release:** `Result.Ensure<T>` and `Result.Combine` — aggregate and validate with full error collection, no short-circuiting.

---

## ✨ What Makes This Package Different

Most result pattern libraries tell you *what* went wrong. This one also tells you **where**.

Every `Error` created through this library automatically captures a `ResultStackTrace` at the call site — no extra setup, no manual tracking. The compiler does it for you via `[CallerFilePath]`, `[CallerLineNumber]`, and `[CallerMemberName]`.

```
[NotFound Error] Restaurant with id '5' not found.
  at DeleteRestaurantCommandHandler.cs:line 14
```

You get structured diagnostics on every failure, for free.

---

## 📦 Installation

```bash
dotnet add package ResultPattern
```

---

## Core Types

### `Result<TValue>`

A sealed generic type that holds **either** a value on success, or a list of `Error` objects on failure. It can never be in both states simultaneously.

| Member | Type | Description |
|---|---|---|
| `Value` | `TValue` | The success value. Only safe to access when `ISuccess` is `true`. |
| `Errors` | `IReadOnlyList<Error>?` | The list of errors. `null` when the result is successful. |
| `ISuccess` | `bool` | `true` when no errors are present. |

**Implicit conversions** allow natural return syntax inside handlers:

```csharp
Result<int> fromValue  = 42;
Result<int> fromError  = Error.NotFound("Item not found.");
Result<int> fromErrors = new List<Error> { Error.Validation("Name required."), Error.Validation("Email required.") };
```

---

### `Success`

A `readonly record struct` used as the value type for operations that produce no meaningful return value — equivalent to `Unit` in functional languages.

```csharp
return Result.Success;
```

---

### `Error` — With Built-in Stack Trace

An immutable `record` representing a categorized failure. Every error automatically captures its call-site context through compiler attributes — **you never pass these manually**.

| Property | Type | Description |
|---|---|---|
| `Code` | `string` | Machine-readable category (e.g. `"NotFound"`). |
| `Description` | `string` | Human-readable message. |
| `StatusCode` | `HttpStatusCode` | Maps directly to an HTTP response code. |
| `StackTrace.FileName` | `string` | Source file where the error was created. |
| `StackTrace.MemberName` | `string` | Method or property that created the error. |
| `StackTrace.LineNumber` | `int` | Exact line number of the error creation. |

> `StackTrace` is marked `[JsonIgnore]` — it is for diagnostics only and never leaks into API responses.

`Error.ToString()` produces a structured diagnostic string:

```
[NotFound Error] Restaurant with id '5' not found.
  at Handle in DeleteRestaurantCommandHandler.cs:line 14
```

#### Available Error Factories

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
// Only pass description — caller info is auto-filled by the compiler
return Error.NotFound($"Restaurant with id '{id}' not found.");
return Error.Forbidden("You do not have permission to perform this action.");
return Error.Validation("Email address is not valid.");
```

---

## Static API — `Result`

### `Result.Success`

Returns a successful `Result<Success>` for void-like operations.

```csharp
return Result.Success;
```

---

## 🆕 `Result.Ensure<T>` — Multi-Rule Validation

Validates a **single value** against one or more rules in one call.  
Each rule is a `(Predicate<T> predicate, Error error)` tuple.

> ⚠️ A predicate returning **`true` signals a failure** — think of it as "is this condition a problem?".

**All predicates are always evaluated.** All failures are collected and returned together.

#### Signature

```csharp
Result<T> Result.Ensure<T>(T value, params (Predicate<T> predicate, Error error)[] rules)
```

#### Example — Validating a Primitive Value

The simplest use case is validating a single field. Notice how method group syntax keeps the rules concise:

```csharp
var nameValidation = Result.Ensure(name,
    (string.IsNullOrEmpty,      Error.Validation("Name cannot be null or empty.")),
    (e => e?.Length < 3,        Error.Validation("Name cannot be less than 3 characters."))
);
```

#### Example — Validating a Domain Object

`Ensure` works equally well against complex objects, such as checking business rules on an entity before a destructive operation:

```csharp
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

if (!ensureResult.ISuccess)
    return ensureResult.Errors!.ToList();
```

---

## 🆕 `Result.Combine` — Aggregate Independent Results

Merges a collection of **pre-computed** `IResult` instances into one.  
Use this when you have results from **multiple independent validations** and want to surface all failures at once.

> ℹ️ **Does not short-circuit.** Every result is evaluated before aggregation.

- All succeed → returns `Result.Success`
- Any fail → returns a failed result with **every error from every failure** combined

#### Signature

```csharp
Result<Success> Result.Combine(IEnumerable<IResult> results)
```

#### Example — Combining Field Validations

The real power of `Combine` emerges in factory methods, where you validate each field independently and then merge all violations together before constructing the domain object:

```csharp
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

    var combinedResult = Result.Combine([nameValidation, emailValidation]);

    if (!combinedResult.ISuccess)
        return combinedResult.Errors!.ToList();

    return new User(name, email);
}
```

If a caller passes both an invalid name and an invalid email, **all violations are returned in a single response** — the caller never needs to fix one issue just to discover the next.

#### Example — Combining Independent Operation Results

```csharp
var ownershipResult = restaurantAuthorization.Authorize(restaurant, ResourceOperation.Delete)
    ? Result.Success
    : (Result<Success>)Error.Forbidden($"You are not the owner of restaurant '{request.Id}'.");

var billingResult = await billingService.CanDeleteAsync(restaurant.Id, ct);

var combined = Result.Combine([ownershipResult, billingResult]);

if (!combined.ISuccess)
    return combined.Errors!.ToList();
```

---

## `Match<TNextValue>` — Pattern Matching

Project the result into another type based on its state — no `if` checks required.

#### Signature

```csharp
TNextValue Match<TNextValue>(Func<TValue, TNextValue> onValue, Func<List<Error>, TNextValue> onError)
```

#### Example — Mapping to a Minimal API Response

```csharp
return result.Match(
    onValue: _ => Results.NoContent(),
    onError: errors => Results.UnprocessableEntity(errors)
);
```

---

## Full Handler Example

Combining everything — entity lookup, business rule validation, and a clean single exit point:

```csharp
public async ValueTask<Result<Success>> Handle(DeleteRestaurantCommand request, CancellationToken ct)
{
    var restaurant = await dbContext.Restaurants
        .FirstOrDefaultAsync(x => x.Id == request.Id, ct);

    if (restaurant is null)
        return Error.NotFound($"Restaurant with id '{request.Id}' not found.");

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

    if (!ensureResult.ISuccess)
        return ensureResult.Errors!.ToList();

    dbContext.Restaurants.Remove(restaurant);
    await dbContext.SaveChangesAsync(ct);

    return Result.Success;
}
```

---

## `Ensure` vs `Combine` at a Glance

| | `Ensure` | `Combine` |
|---|---|---|
| Input | A single value + validation rules | A collection of pre-computed results |
| Evaluation | All predicates always run | All results always evaluated |
| Short-circuits | ❌ No | ❌ No |
| Error aggregation | ✅ All failures collected | ✅ All failures collected |
| Typical use | Single-field or entity validation | Merging multiple independent validations |