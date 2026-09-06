using MpesaAiCopilot.Api.Features.Ai;
using MpesaAiCopilot.Api.Features.Ai.Contracts;
using MpesaAiCopilot.Api.Features.Ai.Providers;
using MpesaAiCopilot.Api.Features.Ai.Validation;


var builder = WebApplication.CreateBuilder(args);


builder.Services.AddHttpClient();

builder.Services.AddHttpClient<OpenAiClient>();
builder.Services.AddHttpClient<AnthropicClient>();
builder.Services.AddHttpClient<GeminiClient>();

builder.Services.AddScoped<AiClientFactory>();

builder.Services.AddOpenApi();

var app = builder.Build();


if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");



app.MapPost("/api/ai/chat", async (
    ChatRequest request,
    AiClientFactory aiClientFactory) =>

{

    try
    {
        AiInputValidator.Validate(request.Message);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }

    var aiClient = aiClientFactory.Create();

    var response = await aiClient.ChatAsync(request.Message);

    return Results.Ok(response);
});


app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
