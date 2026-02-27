# ResultPattern.Extension.MinimalAPI

Minimal API response helpers for [ResultPattern](https://www.nuget.org/packages/ResultPattern).  
Maps `Result<TValue>` and `List<Error>` directly to standards-compliant HTTP Problem Details responses — with zero boilerplate.

---

## 📦 Installation

```bash
dotnet add package ResultPattern.Extension.MinimalAPI
```

> **Requires:** `ResultPattern` core package.

---

## 🚀 Quick Start

### 1. Register Services

```csharp
builder.Services.AddResponseHelper();
```

### 2. Inject and Use in Endpoints

```csharp
app.MapGet("/basket/{userName}", async (
    string userName,
    ISender sender,
    ResponseHelper responseHelper,
    CancellationToken ct) =>
{
    var query = new GetBasketQuery(userName);

    Result<GetBasketResult> result = await sender.Send(query, ct);

    return result.Match(
        val => Results.Ok(val.Adapt<GetBasketResponse>()),
        responseHelper.ToProblemResult);
})
```

`ResponseHelper` is registered as a scoped service. Inject it directly into your endpoint delegate and pass `responseHelper.ToProblemResult` as the error branch of `Match` — no extra wiring needed.

---

## `ResponseHelper`

The core service exposed by this package. It takes a `List<Error>` and produces an `IResult` shaped as an [RFC 7807](https://www.rfc-editor.org/rfc/rfc7807) Problem Details response.

### `ToProblemResult(List<Error> errors)`

The main entry point. Inspects the error list and delegates to the appropriate response builder:

| Condition | Response Type | Status Code |
|---|---|---|
| All errors are `422 Unprocessable Entity` | `ValidationProblem` | `422` |
| Mixed or single non-validation error | `Problem` | Error's own `HttpStatusCode` |
| Empty error list | Generic `Problem` | `500` |

---

## Response Shapes

### Validation Errors — `422 Unprocessable Entity`

Triggered when **all** errors in the list carry a `422` status code (i.e. all created via `Error.Validation(...)`).  
All descriptions are collected, deduplicated, and returned under a single `"errors"` key.

```json
{
  "title": "Validation",
  "status": 422,
  "detail": "One or more validation errors occurred.",
  "errors": {
    "errors": [
      "Name cannot be null or empty.",
      "Name cannot be less than 3 characters.",
      "Invalid email address."
    ]
  }
}
```

In **Development**, each error's full stack trace is appended under `extensions.stackTraces`:

```json
{
  "title": "Validation",
  "status": 422,
  "detail": "One or more validation errors occurred.",
  "errors": {
    "errors": [
      "Name cannot be null or empty.",
      "Invalid email address."
    ]
  },
  "extensions": {
    "stackTraces": [
      {
        "description": "Name cannot be null or empty.",
        "stackTrace": {
          "memberName": "Create",
          "fileName": "User.cs",
          "lineNumber": 12
        }
      },
      {
        "description": "Invalid email address.",
        "stackTrace": {
          "memberName": "Create",
          "fileName": "User.cs",
          "lineNumber": 18
        }
      }
    ]
  }
}
```

---

### Single / Mixed Errors — Problem Details

Triggered when errors are not all `422` — e.g. `404`, `403`, `409`, or `500`.  
The **first error** in the list drives the response status and title.

```json
{
  "title": "NotFound",
  "status": 404,
  "detail": "Restaurant with id '5' not found."
}
```

In **Development**, the error's stack trace is appended under `extensions.stackTrace`:

```json
{
  "title": "NotFound",
  "status": 404,
  "detail": "Restaurant with id '5' not found.",
  "extensions": {
    "stackTrace": {
      "memberName": "Handle",
      "fileName": "DeleteRestaurantCommandHandler.cs",
      "lineNumber": 14
    }
  }
}
```

> Stack traces are **never exposed in Production**. The `IHostEnvironment` is checked internally — no configuration required.

---

## Environment-Aware Diagnostics

`ResponseHelper` automatically adjusts its response detail based on the current environment:

| Field | Development | Production |
|---|---|---|
| `extensions.stackTrace` | ✅ Included | ❌ Omitted |
| `extensions.stackTraces` | ✅ Included | ❌ Omitted |
| Error descriptions | ✅ Included | ✅ Included |
| HTTP status codes | ✅ Included | ✅ Included |

---

## Full Endpoint Example

A complete delete endpoint using `ResultPattern` and `ResponseHelper` together:

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

```csharp
app.MapPost("/users", async (
    CreateUserRequest request,
    ISender sender,
    ResponseHelper responseHelper,
    CancellationToken ct) =>
{
    var command = new CreateUserCommand(request.Name, request.Email);

    Result<UserResponse> result = await sender.Send(command, ct);

    return result.Match(
        user => Results.Created($"/users/{user.Id}", user),
        responseHelper.ToProblemResult);
})
```

---

## MVC & Web API vs Minimal API

Both extension packages expose an identical `ResponseHelper` API surface and produce the same Problem Details response shapes. Choose based on how your application is structured:

| | `ResultPattern.Extension.MinimalAPI` | `ResultPattern.Extension.MVC` |
|---|---|---|
| Return type | `IResult` | `ActionResult` |
| Factory | `Results.Problem` / `Results.ValidationProblem` | `ProblemDetailsFactory` |
| `HttpContext` source | Endpoint delegate parameter | `IHttpContextAccessor` |
| Use in | `app.Map*` endpoint delegates | `ControllerBase` subclasses (MVC & Web API) |
| Stack trace in Dev | ✅ Yes | ✅ Yes |

---

## Related Packages

| Package | Purpose |
|---|---|
| [`ResultPattern`](https://www.nuget.org/packages/ResultPattern) | Core `Result<T>`, `Error`, `Ensure`, `Combine` |
| `ResultPattern.Extension.MinimalAPI` | This package — HTTP Problem Details mapping for Minimal APIs |
| `ResultPattern.Extension.MVC` | HTTP Problem Details mapping for MVC & Web API Controllers |