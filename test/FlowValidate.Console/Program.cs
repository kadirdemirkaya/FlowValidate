using FlowValidate.Console;
using FlowValidate.Console.Models;
using FlowValidate.Console.Validators;

var user = new User
{
    //Name = "XoarkanX S 12 e",
    Name = "Xasd rt 123a sd asd as dd@gmail.com",
    Age = 18,
    PastTime = DateTime.UtcNow.AddDays(-1),
    UserCustomer = new()
    {
        Email = "",
        PhoneNumber = "1234f56789"
    },
    UserBaskets = new()
    {
        new UserBasket(){Count = 0,Name = ""},
        new UserBasket(){Count = 52,Name = ""}
    },
    Email = "kadir@gmail.com",
    Emails = new() { "kadiro@gmail.com", "kadir2@gmail.com" }
};

var validator = new UserValidator();
var result = await validator.ValidateAsync(user);

if (!result.IsValid)
{
    Console.WriteLine("Validation failed:");
    foreach (var error in result.Failures)
    {
        Console.WriteLine(error);
    }
}
else
{
    Console.WriteLine("Validation passed!");
}