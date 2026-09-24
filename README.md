## NuGet Package Information

| Package | Downloads | License |
|---------|-----------|---------|
| [![NuGet](https://img.shields.io/nuget/v/FlowValidate)](https://www.nuget.org/packages/FlowValidate) | [![Downloads](https://img.shields.io/nuget/dt/FlowValidate)](https://www.nuget.org/packages/FlowValidate) | [![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/kadirdemirkaya/FlowValidate/blob/main/LICENSE.txt) |


#### Repository

You can find the source code and contribute on [GitHub](https://github.com/kadirdemirkaya/FlowValidate)


#### FlowValidate

**FlowValidate** is a lightweight, fluent-style validation library for .NET.  
It provides an intuitive API for validating models, making it easy to add and enforce rules while reducing boilerplate code.

Targets `net6.0`, `net7.0`, `net8.0`, `net9.0` and `net10.0`.


#### Features

- **Property Validation**: Validate standard properties, nested objects, and collections.  
- **Nested & Collection Support**: Automatically validates complex types and lists.  
- **Custom Rules**: Use `Should`, `Must`, `IsNotEmpty`, `IsEqual` or define your own logic.  
- **Typed Ranges**: `IsInRange`, `IsGreaterThan` and `IsLessThan` work on `decimal`, `double`, `long`, `DateTime` and any other `IComparable<T>` type, including nullable ones.  
- **Multi-error per Rule**: Single property rules can produce multiple error messages.  
- **Reusable & Property-specific Validators**: Create modular validators like `UserNameValidator` and apply them to properties.  
- **Async / Task-based Validation**: Rules can run asynchronously (`MustAsync` / `ShouldAsync`) with a synchronous validation fallback bridge.  
- **DI Support**: Easy integration with dependency injection.  
- **Clear Error Messages**: Provides detailed validation feedback.  
- **Detailed Error Messages**: Provides rich validation feedback with property name, attempted value, and optional error code.
- **Lightweight & Fast**: Optimized for high performance.  
- **Middleware Ready**: Can validate models automatically on each request with the separate `FlowValidate.AspNetCore` package.


#### Installation

You can install **FlowValidate** via NuGet Package Manager

```bash
dotnet add package FlowValidate
```

To validate ASP.NET Core requests automatically, also install the middleware package:

```bash
dotnet add package FlowValidate.AspNetCore
```

`FlowValidate` contains the validation API only. Console, worker and other non-web apps need just this package.


#### Injection

```csharp
using FlowValidate.AspNetCore;
using FlowValidate.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.FlowValidationService(AssemblyReference.Assembly); 

var app = builder.Build();

app.UseFlowValidation();

app.Run();
```

Calling `FlowValidationService` more than once with the same assembly is safe. The repeated call does not add duplicate validator or assembly registrations. Validators stay `Scoped`. Calls with different assemblies register all of them, and the middleware keeps scanning the most recently registered assembly.

When a controller action's model fails validation, `UseFlowValidation()` responds with `400 Bad Request` and a JSON body:

```json
{"Errors":[{"PropertyName":"Name","ErrorMessage":"Name is required.","AttemptedValue":null,"ErrorCode":"NAME_REQUIRED","Severity":2}]}
```

##### Which action parameter is validated

`UseFlowValidation()` reads the request body for a single parameter of the matched action: the one marked `[FromBody]`, or — when no parameter is marked — the single complex-typed parameter that has no binding source attribute. Parameters bound from somewhere else (`[FromQuery]`, `[FromRoute]`, `[FromHeader]`, `[FromForm]`, `[FromServices]`) and simple types such as `int` or `string` are never deserialized from the body, so a mixed signature like `Create([FromBody] Order order, [FromQuery] OrderFilter filter)` validates `order` only. If the action has no parameter that binds from the body, the request passes through untouched and the body is not read at all. The body is buffered and rewound, so the model binder and later middleware still read it normally.

##### Middleware limits

- **Requires routing to have run first.** The middleware reads `HttpContext.GetRouteData()` to find the matched controller and action, so `app.UseFlowValidation()` must be registered after `app.UseRouting()` (or after endpoint routing has otherwise populated route values). If route data has no `controller`/`action` — request didn't match any route, or the middleware runs before routing — the request passes through untouched and no validation happens.
- **No registered validator, no error.** If no `IBaseValidator<T>` is registered in DI for the resolved body-parameter type, the middleware also passes the request through untouched; it does not throw or report a missing validator.
- **Body deserialization uses `Newtonsoft.Json`**, independent of whatever JSON stack the host application uses for model binding (`System.Text.Json` by default in ASP.NET Core). A type that binds correctly through the host's own JSON settings can still be deserialized differently by the middleware if the two serializers disagree (e.g. custom converters, naming policies).
- **A body the middleware cannot turn into a model is not validated.** Malformed JSON, an empty or whitespace-only body, a literal `null` body, and a body whose values do not fit the model type all make the request pass through untouched, so model binding answers it the way the framework normally would — on an `[ApiController]` that is `400 Bad Request` with the framework's `application/problem+json` body, not FlowValidate's `Errors` body. The middleware never converts an unreadable body into a server error, and it never reports a validation failure for a model it never built.

##### Migrating from `FlowValidationApp()`

`app.FlowValidationApp()` and `ModelValidationMiddleware` in the core package are obsolete. They keep working in this version and will be removed from the core package in the next major version. To migrate, install `FlowValidate.AspNetCore` and replace `app.FlowValidationApp()` with `app.UseFlowValidation()`. The error body is identical. One difference: the obsolete `FlowValidationApp()` answers invalid requests with `200 OK`, while `UseFlowValidation()` answers them with `400 Bad Request`. If a client checks for `200` together with the `Errors` body, update the client when you migrate. The obsolete core middleware also still throws on a body it cannot deserialize; only `UseFlowValidation()` passes such a request on to model binding.

#### For example, we create a uservalidator 

##### Reusable Registry Rule
```bash
public class UserNameValidator : BaseValidator<string>
{
    public UserNameValidator()
    {
        RuleFor(name => name).IsNotEmpty().WithMessage("Name cannot be empty.")
                             .Length(3, 100).WithMessage("Name must be at least 3 characters.");

    }
}
```

##### Nested Validator
```bash
public class UserDetailsValidator : BaseValidator<UserDetails>
{
    public UserDetailsValidator()
    {
        RuleFor(x => x.Email).IsEmail().WithMessage("Email is invalid.");
        RuleFor(x => x.Phone).MatchesRegex(@"^\d{10}$").WithMessage("Phone must be 10 digits.");
    }
}
```

##### Collection Validator
```bash
public class UserBasketValidator : BaseValidator<UserBasket>
{
    public UserBasketValidator()
    {
        RuleFor(x => x.Name).IsNotEmpty();
        RuleFor(x => x.Count).IsGreaterThan(0);
    }
}
```

##### Main User Validator
```bash 
public class UserValidator : BaseValidator<User>
{
    public UserValidator()
    {
        // Registry rule
        ValidateRegistryRules(u => u.Name, new UserNameValidator());

        // Nested validator
        ValidateNested(u => u.Details, new UserDetailsValidator());

        // Collection validator
        ValidateCollection(u => u.Baskets, new UserBasketValidator(), item => item);

        // Custom validation with Should
        RuleFor(u => u.Nickname)
            .Should((nickname, addError) =>
            {
                if (!string.IsNullOrEmpty(nickname))
                {
                    if (nickname.Length < 3)
                        addError("Nickname must be at least 3 characters long.");
                    if (nickname.Contains(" "))
                        addError("Nickname cannot contain spaces.");
                }
            });
    }
}
```

##### Builder Types Returned by `ValidateNested`, `ValidateCollection` and `ValidateRegistryRules`

`ValidateNested`, `ValidateCollection` and `ValidateRegistryRules` return `ValidationNestedBuilder<T, TProperty>`, `ValidationCollectionBuilder<T, TCollection, TElement>` and `ValidationRegistryRules<T, TProperty>`. Today each of the three exposes only one public member, `Task<ValidationResult> ValidateAsync(T instance)`. `RuleFor` already wires the returned builder into the parent validator's rule pipeline, so `Validate(user)` / `ValidateAsync(user)` on `UserValidator` runs it automatically — you do not need to call `ValidateAsync` on the child builder yourself. It is there mainly so a test can exercise one composition step in isolation, for example `await new UserDetailsValidator().ValidateAsync(details)` directly, or `await new ValidationNestedBuilder<User, UserDetails>(u => u.Details, new UserDetailsValidator()).ValidateAsync(user)`.

##### `null` Handling

- **A `null` collection selected by `ValidateCollection` is skipped**, the same way `ValidateNested` already skips a `null` nested object — neither reports a failure and neither throws.
- **A `null` root instance passed to `Validate` or `ValidateAsync` throws `ArgumentNullException`.** Both methods behave the same way, since `Validate` calls `ValidateAsync` internally.

##### Conditional Rules with `RequiredIf`

`RequiredIf(Func<TProperty, bool> condition)` gates the rules chained after it behind a check on the property's own value. When `condition(value)` is `false`, the rule fails immediately with `"Property is required."` (`errorCode: "Required"`) and every rule chained after `RequiredIf` on that property is skipped (`ValidationResult.SetSkipRemainingRules(true)`) — other properties' `RuleFor` chains are unaffected. When `condition(value)` is `true`, `RequiredIf` itself only fails if the value is `null` or, for `string`, blank; on success the remaining chained rules run as usual.

```csharp
public class PromoValidator : BaseValidator<Promo>
{
    public PromoValidator()
    {
        RuleFor(x => x.PromoCode)
            .RequiredIf(code => code is not null && code.StartsWith("PROMO"))
            .Length(8, 12)
            .Contains("-");
    }
}
```

- `PromoCode = "ABC"` → condition is `false` → fails with `"Property is required."`; `Length`/`Contains` do not run.
- `PromoCode = "PROMO12"` → condition is `true` → `Length`/`Contains` run and report their own failures.
- `PromoCode = "PROMO-123"` → condition is `true` and the remaining rules pass → valid.

##### `Severity`

`ValidationFailure.Severity` is a `FlowValidate.Enums.Severity` value (`Info`, `Warning`, `Error`). Every failure produced by the built-in rules, `Should`/`ShouldAsync`, and `RequiredIf` defaults to `Severity.Error` — there is no fluent option on `RuleFor`/`WithMessage` to change it. To report a lower severity, construct the failure yourself, either with `ValidationResult.Failure(message, propertyName, attemptedValue, errorCode, severity)` or `new ValidationFailure(...)`, and merge it into the validator's result:

```csharp
var result = validator.Validate(user);

result.Merge(ValidationResult.Failure(
    "Backup email is missing.",
    propertyName: "BackupEmail",
    severity: Severity.Warning));
```

`Severity` is also part of the middleware's JSON error body (see the `Errors[].Severity` field above).

#### Asynchronous Validation (Async Support)

FlowValidate fully supports asynchronous validation rules for operations that require external or asynchronous calls (e.g., database queries or external API requests). You can use `MustAsync` and `ShouldAsync` inside your validators.

##### Asynchronous Rules Example

```csharp
public class UserValidator : BaseValidator<User>
{
    private readonly IUserRepository _userRepository;

    public UserValidator(IUserRepository userRepository)
    {
        _userRepository = userRepository;

        // Using MustAsync for custom async boolean conditions
        RuleFor(u => u.Email)
            .MustAsync(async email => !await _userRepository.ExistsAsync(email))
            .WithMessage("This email address is already in use.");

        // Using ShouldAsync with a multi-error delegate callback
        RuleFor(u => u.Username)
            .ShouldAsync(async (username, addError) =>
            {
                var isBlacklisted = await _userRepository.IsBlacklistedAsync(username);
                if (isBlacklisted)
                {
                    addError("Username is blacklisted.");
                }
            });

        // Using ShouldAsync with an action exception boundary
        RuleFor(u => u.Bio)
            .ShouldAsync(async bio => 
            {
                await _userRepository.ValidateBioFormatAsync(bio); // Throws if invalid
            }, "Bio format is invalid.");
    }
}
```

##### Executing Asynchronously vs Synchronously

```csharp
var validator = new UserValidator(userRepository);

// 1. Asynchronous execution (Recommended when using async rules)
var resultAsync = await validator.ValidateAsync(user);

// 2. Synchronous execution bridge (Executes async rules synchronously under the hood)
var resultSync = validator.Validate(user);
```

##### Using the Validator
```bash
var user = new User
{
    Name = "Jo",
    Age = 25,
    Details = new UserDetails { Email = "invalid-email", Phone = "12345" },
    Baskets = new List<UserBasket>
    {
        new UserBasket { Name = "", Count = 0 },
        new UserBasket { Name = "Apple", Count = 3 }
    }
};

var validator = new UserValidator();
var result = validator.Validate(user);
```

##### Property Names in Failures

Every `ValidationFailure.PropertyName` is non-null. A plain member access reports the member name; any other expression reports its expression text.

| Rule | `PropertyName` |
|---|---|
| `RuleFor(x => x.Name)` | `Name` |
| `RuleFor(x => x.Details.Email)` | `Email` |
| `RuleFor(x => x.Tags[0])` (array) | `x.Tags[0]` |
| `RuleFor(x => x.Baskets[0])` (list) | `x.Baskets.get_Item(0)` |
| `RuleFor(x => x.Name.Trim())` | `x.Name.Trim()` |
| `RuleFor(x => x)` | `x` |

##### `WithMessage` Placement

`WithMessage(...)` overrides the message and code of the rule that was just added, and must be chained
directly after that rule. Calling it before any rule has been added on the current `RuleFor(...)` chain
is a no-op: the call is silently ignored, and no rule is added or changed.

```bash
RuleFor(x => x.Name).WithMessage("This is ignored");
```

`ValidationFailure.AttemptedValue` on a `WithMessage`-annotated failure is always the real value that
was validated, the same as when `WithMessage` is omitted.

##### Ranges and Comparisons for Any Ordered Type

`IsInRange`, `IsGreaterThan` and `IsLessThan` work on any property type that implements `IComparable<T>`: `decimal`, `double`, `long`, `DateTime`, `DateOnly`, `string` and their nullable forms. Pass bounds of the property's own type; a bound of another type does not compile.

```csharp
public class ProductValidator : BaseValidator<Product>
{
    public ProductValidator()
    {
        RuleFor(x => x.Price).IsInRange(0.01m, 999.99m);
        RuleFor(x => x.Rating).IsGreaterThan(0.0).IsLessThan(5.5);
        RuleFor(x => x.Views).IsLessThan(10_000_000_000L);
        RuleFor(x => x.ReleasedAt).IsGreaterThan(new DateTime(2020, 1, 1));
        RuleFor(x => x.Discount).IsInRange(0m, 0.5m);
    }
}
```

- `IsInRange` includes both bounds; `IsGreaterThan` and `IsLessThan` are strict.
- A `null` value fails these rules. For an optional property that may be `null`, use `Must`, for example `RuleFor(x => x.Discount).Must(d => d is null or (>= 0m and <= 0.5m))`.
- Integer bounds such as `IsInRange(1, 10)` still bind to the original `int` overloads, which behave exactly as before and only accept `int`-convertible values. On a `decimal`, `double` or `long` property, write the bounds with the matching literal suffix (`1m`, `1.0`, `1L`).
- The `int` overloads of `IsGreaterThan` and `IsLessThan` round the value to an `int` before comparing, so on a `decimal` or `double` property they compare the rounded value, not the real one: `IsGreaterThan(5)` on `5.4m` fails and `IsLessThan(6)` on `5.6m` fails. This is why the matching literal suffix matters — `IsGreaterThan(5m)` and `IsLessThan(6m)` compare the value itself and both pass.
- A value that cannot be converted to `int` at all — a `long` outside the `int` range, a non-numeric `string`, a `DateTime` — now fails the rule and is reported as a normal validation failure. Earlier versions let the conversion exception escape `Validate` / `ValidateAsync`.

For more examples and unit tests, check the [FlowValidate.Test](https://github.com/kadirdemirkaya/FlowValidate/tree/main/test/FlowValidate.Test) project in the repository.  

- [API Examples](https://github.com/kadirdemirkaya/FlowValidate/tree/main/test/FlowValidate.Api)  
- [Console Examples](https://github.com/kadirdemirkaya/FlowValidate/tree/main/test/FlowValidate.Console)

Want to contribute? See [CONTRIBUTING.md](https://github.com/kadirdemirkaya/FlowValidate/blob/main/CONTRIBUTING.md).


