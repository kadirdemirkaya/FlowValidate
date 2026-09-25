## NuGet Package Information

| Package | Downloads | License |
|---------|-----------|---------|
| [![NuGet](https://img.shields.io/nuget/v/FlowValidate)](https://www.nuget.org/packages/FlowValidate) | [![Downloads](https://img.shields.io/nuget/dt/FlowValidate)](https://www.nuget.org/packages/FlowValidate) | [![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/kadirdemirkaya/FlowValidate/blob/main/LICENSE.txt) |
| [![NuGet](https://img.shields.io/nuget/v/FlowValidate.AspNetCore)](https://www.nuget.org/packages/FlowValidate.AspNetCore) | [![Downloads](https://img.shields.io/nuget/dt/FlowValidate.AspNetCore)](https://www.nuget.org/packages/FlowValidate.AspNetCore) | [![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/kadirdemirkaya/FlowValidate/blob/main/LICENSE.txt) |


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
- **Bounded Regular Expressions**: `MatchesRegex` accepts a match timeout or a pre-built `Regex`, so a costly pattern on untrusted input is reported as a rule failure instead of occupying the thread.  
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

Calling `FlowValidationService` more than once with the same assembly is safe. The repeated call does not add duplicate validator or assembly registrations. Validators stay `Scoped`. Calls with different assemblies register all of them, but `UseFlowValidation()` only scans the most recently registered assembly for controllers on its own.

For a modular app whose controllers live in more than one assembly, also call `FlowValidationAssemblies` for the earlier ones so `UseFlowValidation()` scans all of them instead of only the last one registered:

```csharp
builder.Services.FlowValidationService(typeof(Program).Assembly);
builder.Services.FlowValidationAssemblies(typeof(OrdersModule).Assembly, typeof(PaymentsModule).Assembly);
```

`FlowValidationAssemblies` is safe to call more than once and with more than one assembly per call: every assembly passed across all calls is merged, and passing the same assembly again does not scan it twice.

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

`ValidateNested`, `ValidateCollection` and `ValidateRegistryRules` return `ValidationNestedBuilder<T, TProperty>`, `ValidationCollectionBuilder<T, TCollection, TElement>` and `ValidationRegistryRules<T, TProperty>`. All three expose `Task<ValidationResult> ValidateAsync(T instance)`; `ValidationCollectionBuilder<T, TCollection, TElement>` additionally exposes `WithIndexedPropertyNames(string collectionName)` (see below). `RuleFor` already wires the returned builder into the parent validator's rule pipeline, so `Validate(user)` / `ValidateAsync(user)` on `UserValidator` runs it automatically — you do not need to call `ValidateAsync` on the child builder yourself. It is there mainly so a test can exercise one composition step in isolation, for example `await new UserDetailsValidator().ValidateAsync(details)` directly, or `await new ValidationNestedBuilder<User, UserDetails>(u => u.Details, new UserDetailsValidator()).ValidateAsync(user)`.

##### Indexed Property Names for Collection Failures

By default a failure coming from a collection element carries the element validator's own property name (`Name`) and only the error message tells you which element failed (`"Element 2: ..."`). Chain `WithIndexedPropertyNames("Baskets")` on the builder returned by `ValidateCollection` to also get the index into `ValidationFailure.PropertyName`, so a client can map the failure to an element programmatically:

```csharp
ValidateCollection(u => u.Baskets, new UserBasketValidator(), item => item)
    .WithIndexedPropertyNames("Baskets");
```

```
PropertyName : Baskets[1].Name
ErrorMessage : Element 2: Name is required.
```

- The index in `PropertyName` is **zero-based**; the `"Element n: "` message prefix stays **one-based** and byte-for-byte unchanged, as does every other part of the failure (`AttemptedValue`, `ErrorCode`, `Severity`).
- The option is **opt-in per collection**. Without the call, property names and messages stay exactly as they were.
- The collection name is passed explicitly because `ValidateCollection` takes a delegate, not an expression, so the name cannot be inferred from the selector. An empty or whitespace name throws `ArgumentException`.
- Paths compose: a collection inside a collection that both opt in yields `Orders[0].Lines[2].Qty`, and a nested validator inside an opted-in collection yields `Orders[1].Street`.
- The resulting names go straight into `ValidationResult.ToDictionary()` keys and into the `400 Bad Request` body written by `app.UseFlowValidation()`.

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

##### Conditional Chains with `When` and `Unless`

`When(Func<T, bool> condition)` and `Unless(Func<T, bool> condition)` gate a `RuleFor` chain behind a condition on the **root instance**, so the condition can look at *another* property. When the condition is not met the chain's rules are skipped and **no failure is produced** — the property is not even read.

```csharp
public class CustomerValidator : BaseValidator<Customer>
{
    public CustomerValidator()
    {
        RuleFor(x => x.VatNumber)
            .IsNotEmpty()
            .WithMessage("VAT number is required for companies.", "VAT_REQUIRED")
            .When(x => x.IsCompany);

        RuleFor(x => x.Name)
            .IsNotEmpty()
            .Unless(x => x.IsDraft);
    }
}
```

- `IsCompany = false` → the `VatNumber` chain is skipped entirely, `Failures` stays empty.
- `IsCompany = true, VatNumber = null` → fails with `"VAT number is required for companies."`.
- `IsDraft = true` → the `Name` chain is skipped; `IsDraft = false` → it runs.

**Scope:** the condition guards the **whole chain**, not just the rule written before it — `.IsNotEmpty().When(...)` and `.When(...).IsNotEmpty()` are equivalent. Start a second `RuleFor` chain on the same property when it needs a different condition. Several `When`/`Unless` calls on one chain are combined with AND.

**Async:** `MustAsync` and `ShouldAsync` are gated the same way and are never awaited when the condition is not met. A skipped chain still counts towards `HasAsyncRules`, so `Validate()` keeps throwing for a validator that declares async rules — use `ValidateAsync()`.

**Composition:** `When`/`Unless` work inside any validator, including the ones passed to `ValidateNested` and `ValidateCollection`; there the "root instance" is the nested object or the individual element being validated.

###### `When`/`Unless` vs `RequiredIf`

| | `When` / `Unless` | `RequiredIf` |
|---|---|---|
| Condition argument | the root instance (`T`) — can read other properties | the property's own value (`TProperty`) |
| Condition not met | chain is skipped silently, no failure | fails with `"Property is required."` (`errorCode: "Required"`) and skips the remaining rules |
| Condition met | the chain's rules run | the value must be non-`null` (non-blank for `string`), then the remaining rules run |
| Scope | the whole `RuleFor` chain | the rules chained after it |

Use `When`/`Unless` when a rule set only applies in some cases and its absence is not an error. Use `RequiredIf` when the property must be present and you want the failure. `RequiredIf` is unchanged and can be used inside a `When`-gated chain.

##### `Severity`

`ValidationFailure.Severity` is a `FlowValidate.Enums.Severity` value (`Info`, `Warning`, `Error`). Every failure produced by the built-in rules and `RequiredIf` defaults to `Severity.Error`. Chain `WithSeverity(Severity)` after a rule to change it, exactly the way `WithMessage` targets the most recently added rule:

```csharp
RuleFor(x => x.BackupEmail)
    .IsNotEmpty()
    .WithMessage("Backup email is missing.", "BACKUP_EMAIL_MISSING")
    .WithSeverity(Severity.Warning);
```

- `WithSeverity` has no effect when no rule has been added yet, and can be chained together with `WithMessage` in either order.
- It has **no effect on failures raised from inside a `Should`/`ShouldAsync` callback** — those build their own `ValidationFailure` directly and keep `Severity.Error`, the same way `WithMessage` already leaves their messages untouched.
- `ValidationResult.IsValid` semantics are unchanged: a `Warning` or `Info` failure still makes the result invalid, exactly like `Error` does today.

To report a severity without a `RuleFor` rule at all, construct the failure yourself, either with `ValidationResult.Failure(message, propertyName, attemptedValue, errorCode, severity)` or `new ValidationFailure(...)`, and merge it into the validator's result:

```csharp
var result = validator.Validate(user);

result.Merge(ValidationResult.Failure(
    "Backup email is missing.",
    propertyName: "BackupEmail",
    severity: Severity.Warning));
```

`Severity` is also part of the middleware's JSON error body (see the `Errors[].Severity` field above).

##### Grouping Failures by Property with `ToDictionary`

`ValidationResult.ToDictionary()` groups `Failures` by `PropertyName` into an `IDictionary<string, string[]>`, in insertion order — handy for building an ASP.NET `ValidationProblemDetails`-style error body without writing your own `GroupBy`:

```csharp
var result = validator.Validate(user);
IDictionary<string, string[]> errors = result.ToDictionary();
// { "Email": ["must not be empty", "must be a valid email"], "Age": ["must be positive"] }
```

A successful result returns an empty dictionary. A `null`/empty `PropertyName` is grouped under `"<root>"`, and failures of every `Severity` are included, not only `Severity.Error`.

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

##### Cancellation

`ValidateAsync(instance, cancellationToken)` runs the same rules and passes the token to every rule registered through a `MustAsync` or `ShouldAsync` overload that takes one, so a database or HTTP call made inside a rule is aborted when the caller gives up. The token is also checked between rules and between collection elements. Calling `ValidateAsync(instance)` without a token is exactly the same as passing `CancellationToken.None`, and the rule overloads without a token are unchanged.

```csharp
public class UserValidator : BaseValidator<User>
{
    public UserValidator(IUserRepository userRepository)
    {
        RuleFor(u => u.Email)
            .MustAsync(async (email, cancellationToken) => !await userRepository.ExistsAsync(email, cancellationToken))
            .WithMessage("This email address is already in use.");

        RuleFor(u => u.Username)
            .ShouldAsync(async (username, addError, cancellationToken) =>
            {
                if (await userRepository.IsBlacklistedAsync(username, cancellationToken))
                {
                    addError("Username is blacklisted.");
                }
            });

        RuleFor(u => u.Bio)
            .ShouldAsync(async (bio, cancellationToken) =>
            {
                await userRepository.ValidateBioFormatAsync(bio, cancellationToken);
            }, "Bio format is invalid.");
    }
}

var result = await validator.ValidateAsync(user, cancellationToken);
```

A cancelled token makes `ValidateAsync` throw `OperationCanceledException`; cancellation is **not** reported as a validation failure. An aborted run has no verdict about the instance, so turning it into a failure would tell the caller the instance is invalid when it may well be valid. This is the one case where `ShouldAsync` does not convert an exception into a failure: an `OperationCanceledException` raised after *your* token is cancelled is rethrown, while every other exception — including one from a token the rule owns itself — is still recorded as a failure as before.

The `errorMessage` argument is required on `ShouldAsync(async (value, cancellationToken) => ..., errorMessage)`; pass `null` for the default message. It is not optional so that an existing `ShouldAsync(async (value, addError) => ...)` call keeps compiling to the callback overload.

`UseFlowValidation()` passes `HttpContext.RequestAborted`, so validation for a request whose client has disconnected stops instead of running to completion. The obsolete `FlowValidationApp()` middleware is frozen and keeps validating without a token.

`IBaseValidator<T>.ValidateAsync(T, CancellationToken)` has a default implementation, so a type that implements the interface by hand keeps compiling; the default observes the token once and then delegates to `ValidateAsync(T)`. Code that resolves the method by name through reflection must select it by signature — `GetMethod("ValidateAsync", new[] { typeof(T) })` — because the name alone is now ambiguous.

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

##### Regular Expressions with a Match Timeout

`MatchesRegex(pattern)` matches without a time limit. **On untrusted input — anything coming from a request body — use the timed overloads instead**, so a pattern that backtracks catastrophically cannot occupy the thread while a single value is validated:

```csharp
public class SignUpValidator : BaseValidator<SignUp>
{
    private static readonly Regex UsernamePattern =
        new Regex("^[a-z0-9_]{3,20}$", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));

    public SignUpValidator()
    {
        RuleFor(x => x.Phone).MatchesRegex(@"^\d{10}$", TimeSpan.FromMilliseconds(100));
        RuleFor(x => x.Username).MatchesRegex(UsernamePattern);
    }
}
```

- `MatchesRegex(string pattern, TimeSpan matchTimeout)` compiles the pattern once, when the rule is added, and applies the timeout to every match attempt.
- `MatchesRegex(Regex regex)` takes a pre-built expression that carries its own `RegexOptions` and its own `MatchTimeout` — useful for sharing one `static readonly Regex` across validators.
- A timed-out match **does not throw**: the property is reported invalid with the error code `RegexTimeout` and a message naming the timeout, so it can be told apart from a value that simply did not match. `WithMessage` overrides the ordinary no-match failure; the timeout failure keeps its own message.
- A `null` or non-string value fails exactly as it does with `MatchesRegex(pattern)`.
- The existing `MatchesRegex(pattern)` overload is unchanged and still has no timeout.

For more examples and unit tests, check the [FlowValidate.Test](https://github.com/kadirdemirkaya/FlowValidate/tree/main/test/FlowValidate.Test) project in the repository.  

- [API Examples](https://github.com/kadirdemirkaya/FlowValidate/tree/main/test/FlowValidate.Api)  
- [Console Examples](https://github.com/kadirdemirkaya/FlowValidate/tree/main/test/FlowValidate.Console)

Want to contribute? See [CONTRIBUTING.md](https://github.com/kadirdemirkaya/FlowValidate/blob/main/CONTRIBUTING.md).


