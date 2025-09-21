using FlowValidate.Extensions;
using System.Reflection.Metadata;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddOpenApi();

// dependency injection
builder.Services.FlowValidationService(typeof(Program).Assembly);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// middleware 
app.FlowValidationApp();

app.UseAuthorization();

app.MapControllers();

app.Run();
