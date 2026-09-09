using MpesaAiCopilot.Api.Features.Ai;
using MpesaAiCopilot.Api.Features.Ai.Contracts;
using MpesaAiCopilot.Api.Features.Ai.Providers;
using MpesaAiCopilot.Api.Features.Ai.Validation;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddHttpClient();

builder.Services.AddHttpClient<OpenAiClient>();
builder.Services.AddHttpClient<AnthropicClient>();
builder.Services.AddHttpClient<GeminiClient>();

builder.Services.AddScoped<AiClientFactory>();




builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

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

//app.MapPost("/api/ai/analyze", async (
//    ChatRequest request,
//    AiClientFactory aiClientFactory) =>
//{
//    try
//    {
//        AiInputValidator.Validate(request.Message);
//    }
//    catch (ArgumentException ex)
//    {
//        return Results.BadRequest(new { error = ex.Message });
//    }

//    var aiClient = aiClientFactory.Create();

//    var analysis = await aiClient.AnalyzeAsync(request.Message);

//    return Results.Ok(analysis);
//});



app.MapPost("/api/ai/analyze", async (
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

    SecurityAnalysis analysis;

    try
    {
        analysis = await aiClient.AnalyzeAsync(request.Message);
    }
    catch (HttpRequestException ex)
    {
        app.Logger.LogError(ex, "AI provider request failed during security analysis.");

        return Results.Problem(
            title: "AI provider unavailable",
            detail: "The AI provider could not process this request. Please try again shortly.",
            statusCode: StatusCodes.Status502BadGateway);
    }
    catch (JsonException ex)
    {
        app.Logger.LogError(ex, "AI provider returned malformed structured output.");

        return Results.Problem(
            title: "AI provider returned an unreadable response",
            detail: "The AI provider's response could not be parsed. Please try again.",
            statusCode: StatusCodes.Status502BadGateway);
    }
    catch (InvalidOperationException ex)
    {
        app.Logger.LogError(ex, "AI provider response was missing expected data.");

        return Results.Problem(
            title: "AI provider returned an incomplete response",
            detail: "The AI provider's response did not contain the expected data. Please try again.",
            statusCode: StatusCodes.Status502BadGateway);
    }

    var validated = SecurityAnalysisValidator.Validate(analysis);
    foreach (var warning in validated.ValidationWarnings)
    {
        app.Logger.LogWarning(
            "Security analysis validation warning: {Warning}",
            warning);
    }

    return Results.Ok(validated);
});



app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
