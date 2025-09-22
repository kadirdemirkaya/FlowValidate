## NuGet Package Information

| Package | Downloads | License |
|---------|-----------|---------|
| [![NuGet](https://img.shields.io/nuget/v/FlowValidate)](https://www.nuget.org/packages/FlowValidate) | [![Downloads](https://img.shields.io/nuget/dt/FlowValidate)](https://www.nuget.org/packages/FlowValidate) | [![License](https://img.shields.io/nuget/l/EventFlux)](https://github.com/kadirdemirkaya/EventFlux/blob/main/LICENSE.txt) |


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
- **Async / Task-based Validation**: Rules can run asynchronously,
- **DI Support**: Easy integration with dependency injection.  
- **Clear Error Messages**: Provides detailed validation feedback.  
- **Lightweight & Fast**: Optimized for high performance.  
- **Middleware Ready**: Can validate models automatically on each request.


#### Installation

You can install **FlowValidate** via NuGet Package Manager

```bash
dotnet add package FlowValidate
```


#### Injection

```bash
var builder = WebApplication.CreateBuilder(args);

builder.Services.FlowValidationService(AssemblyReference.Assembly); 

var app = builder.Build();

app.FlowValidationApp();

app.Run();
```

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

For more examples and unit tests, check the [FlowValidate.Test](https://github.com/kadirdemirkaya/FlowValidate/tree/main/test/FlowValidate.Test) project in the repository.  

- [API Examples](https://github.com/kadirdemirkaya/FlowValidate/tree/main/test/FlowValidate.Api)  
- [Console Examples](https://github.com/kadirdemirkaya/FlowValidate/tree/main/test/FlowValidate.Console)


