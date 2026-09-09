using MpesaAiCopilot.Api.Features.Ai;
using MpesaAiCopilot.Api.Features.Ai.Contracts;
using MpesaAiCopilot.Api.Features.Ai.Providers;
using MpesaAiCopilot.Api.Features.Ai.Rag;
using MpesaAiCopilot.Api.Features.Ai.Validation;
using System.Text.Json;
using System.Text.Json.Serialization;


var builder = WebApplication.CreateBuilder(args);


builder.Services.AddHttpClient();

builder.Services.AddHttpClient<OpenAiClient>();
builder.Services.AddHttpClient<AnthropicClient>();
builder.Services.AddHttpClient<GeminiClient>();
builder.Services.AddHttpClient<GeminiEmbeddingClient>();
builder.Services.AddScoped<AiClientFactory>();
builder.Services.AddSingleton<KnowledgeRetriever>();

builder.Services.AddSingleton<SemanticKnowledgeRetriever>();


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
    AiClientFactory aiClientFactory,
    SemanticKnowledgeRetriever retriever) =>
{
    try
    {
        AiInputValidator.Validate(request.Message);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    var relevantDocuments = await retriever.RetrieveAsync(
    request.Message,
    topN: KnowledgeBase.Documents.Count);

    var augmentedMessage = BuildAugmentedMessage(request.Message, relevantDocuments);
    app.Logger.LogInformation("Augmented message sent to AI:\n{Message}", augmentedMessage);
    var aiClient = aiClientFactory.Create();

    SecurityAnalysis analysis;

    try
    {
        analysis = await aiClient.AnalyzeAsync(augmentedMessage);
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


app.MapGet("/api/rag/search", (
    string query,
    KnowledgeRetriever retriever) =>
{
    var results = retriever.Retrieve(query);

    return Results.Ok(results);
});


app.MapGet("/api/rag/embed", async (
    string text,
    GeminiEmbeddingClient embeddingClient) =>
{
    var embedding = await embeddingClient.GenerateEmbeddingAsync(text);

    return Results.Ok(new
    {
        text,
        dimensions = embedding.Length,
        embedding
    });
});

app.MapGet("/api/rag/similarity-test", async (
    GeminiEmbeddingClient embeddingClient) =>
{
    var vectorA = await embeddingClient.GenerateEmbeddingAsync(
        "Webhook Security");

    var vectorB = await embeddingClient.GenerateEmbeddingAsync(
        "How can I prevent spoofed payment callbacks?");

    var vectorC = await embeddingClient.GenerateEmbeddingAsync(
        "What is a good recipe for cooking pasta?");

    var similarityToRelated = CosineSimilarity.Calculate(vectorA, vectorB);
    var similarityToUnrelated = CosineSimilarity.Calculate(vectorA, vectorC);

    return Results.Ok(new
    {
        relatedScore = similarityToRelated,
        unrelatedScore = similarityToUnrelated
    });
});

app.MapGet("/api/rag/semantic-search", async (
    string query,
    SemanticKnowledgeRetriever retriever) =>
{
    var results = await retriever.RetrieveAsync(query);

    return Results.Ok(results);
});


static string BuildAugmentedMessage(string question, List<KnowledgeDocument> documents)
{
    if (documents.Count == 0)
    {
        return question;
    }

    var knowledgeSection = string.Join(
        "\n\n",
        documents.Select(d => $"### {d.Title}\n{d.Content}"));

    return $"""
        Relevant security knowledge:

        {knowledgeSection}

        Question:
        {question}
        """;
}
app.MapGet("/api/rag/debug-scores", async (
    string query,
    SemanticKnowledgeRetriever retriever) =>
{
    var scored = await retriever.RetrieveWithScoresAsync(query);

    return Results.Ok(scored.Select(x => new
    {
        x.Document.Title,
        x.Score
    }));
});




app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
