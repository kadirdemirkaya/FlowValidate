## NuGet Package Information

| Package | Downloads | License |
|---------|-----------|---------|
| [![NuGet](https://img.shields.io/nuget/v/FlowValidate)](https://www.nuget.org/packages/FlowValidate) | [![Downloads](https://img.shields.io/nuget/dt/FlowValidate)](https://www.nuget.org/packages/FlowValidate) | [![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/kadirdemirkaya/FlowValidate/blob/main/LICENSE.txt) |


#### Repository

You can find the source code and contribute on [GitHub](https://github.com/kadirdemirkaya/FlowValidate)


#### FlowValidate

**FlowValidate** is a lightweight, fluent-style validation library for .NET.  
It provides an intuitive API for validating models, making it easy to add and enforce rules while reducing boilerplate code.


#### Features

- **Property Validation**: Validate standard properties, nested objects, and collections.  
- **Nested & Collection Support**: Automatically validates complex types and lists.  
- **Custom Rules**: Use `Should`, `Must`, `IsNotEmpty`, `IsEqual` or define your own logic.  
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

When a controller action's model fails validation, `UseFlowValidation()` responds with `400 Bad Request` and a JSON body:

```json
{"Errors":[{"PropertyName":"Name","ErrorMessage":"Name is required.","AttemptedValue":null,"ErrorCode":"NAME_REQUIRED","Severity":2}]}
```

##### Migrating from `FlowValidationApp()`

`app.FlowValidationApp()` and `ModelValidationMiddleware` in the core package are obsolete. They keep working in this version and will be removed from the core package in the next major version. To migrate, install `FlowValidate.AspNetCore` and replace `app.FlowValidationApp()` with `app.UseFlowValidation()`. The error body is identical. One difference: the obsolete `FlowValidationApp()` answers invalid requests with `200 OK`, while `UseFlowValidation()` answers them with `400 Bad Request`. If a client checks for `200` together with the `Errors` body, update the client when you migrate.

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

For more examples and unit tests, check the [FlowValidate.Test](https://github.com/kadirdemirkaya/FlowValidate/tree/main/test/FlowValidate.Test) project in the repository.  

- [API Examples](https://github.com/kadirdemirkaya/FlowValidate/tree/main/test/FlowValidate.Api)  
- [Console Examples](https://github.com/kadirdemirkaya/FlowValidate/tree/main/test/FlowValidate.Console)


