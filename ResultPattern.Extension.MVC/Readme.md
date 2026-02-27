# ResultPattern.Extension.MVC

MVC & Web API response helpers for [ResultPattern](https://www.nuget.org/packages/ResultPattern).  
Maps `Result<TValue>` and `List<Error>` directly to standards-compliant HTTP Problem Details `ActionResult` responses — powered by ASP.NET Core's built-in `ProblemDetailsFactory`.

> This package works with **any class that inherits `ControllerBase`** — including `[ApiController]` Web API controllers and traditional MVC controllers alike.

---

## 📦 Installation

```bash
dotnet add package ResultPattern.Extension.MVC
```

> **Requires:** `ResultPattern` core package.

---

## 🚀 Quick Start

### 1. Register Services

```csharp
builder.Services.AddResponseHelper();
```

### 2. Inject and Use in Controllers

```csharp
[ApiController]
[Route("api/[controller]")]
public class BasketController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ResponseHelper _responseHelper;

    public BasketController(ISender sender, ResponseHelper responseHelper)
    {
        _sender = sender;
        _responseHelper = responseHelper;
    }

    [HttpGet("{userName}")]
    public async Task<IActionResult> GetBasket(string userName, CancellationToken ct)
    {
        var query = new GetBasketQuery(userName);

        Result<GetBasketResult> result = await _sender.Send(query, ct);

        return result.Match(
            val => Ok(val.Adapt<GetBasketResponse>()),
            _responseHelper.ToProblemResult);
    }
}
```

`ResponseHelper` is registered as a scoped service and uses `IHttpContextAccessor` internally — inject it into any controller that needs result-to-response mapping.

---

## `ResponseHelper`

The core service exposed by this package. It takes a `List<Error>` and produces an `ActionResult` shaped as an [RFC 7807](https://www.rfc-editor.org/rfc/rfc7807) Problem Details response, using ASP.NET Core's `ProblemDetailsFactory` for full framework compatibility.

### `ToProblemResult(List<Error> errors)`

The main entry point. Inspects the error list and delegates to the appropriate response builder:

| Condition | Response Type | Status Code |
|---|---|---|
| All errors are `422 Unprocessable Entity` | `ValidationProblemDetails` | `422` |
| Mixed or single non-validation error | `ProblemDetails` | Error's own `HttpStatusCode` |
| Null or empty error list | Generic `ProblemDetails` | `500` |

---

## Response Shapes

### Validation Errors — `422 Unprocessable Entity`

Triggered when **all** errors in the list carry a `422` status code (i.e. all created via `Error.Validation(...)`).  
All descriptions are collected, deduplicated, and added to `ModelStateDictionary` under the `"errors"` key — fully compatible with ASP.NET Core's standard validation response shape.

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

> Stack traces are **never exposed in Production**. `IHostEnvironment` is checked internally — no configuration required.

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

## Full Controller Example

A complete Web API controller using `ResultPattern` and `ResponseHelper` across multiple endpoints:

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

---

## MVC & Web API vs Minimal API

Both extension packages expose an identical `ResponseHelper` API surface and produce the same Problem Details response shapes. Choose based on how your application is structured:

| | `ResultPattern.Extension.MVC` | `ResultPattern.Extension.MinimalAPI` |
|---|---|---|
| Return type | `ActionResult` | `IResult` |
| Factory | `ProblemDetailsFactory` | `Results.Problem` / `Results.ValidationProblem` |
| `HttpContext` source | `IHttpContextAccessor` | Endpoint delegate parameter |
| Use in | `ControllerBase` subclasses (MVC & Web API) | `app.Map*` endpoint delegates |
| Stack trace in Dev | ✅ Yes | ✅ Yes |

---

## Related Packages

| Package | Purpose |
|---|---|
| [`ResultPattern`](https://www.nuget.org/packages/ResultPattern) | Core `Result<T>`, `Error`, `Ensure`, `Combine` |
| `ResultPattern.Extension.MinimalAPI` | HTTP Problem Details mapping for Minimal APIs |
| `ResultPattern.Extension.MVC` | This package — HTTP Problem Details mapping for MVC & Web API Controllers |