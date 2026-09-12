namespace MpesaAiCopilot.Api.Features.Ai.Rag;

public class SemanticKnowledgeRetriever
{
    private const float MinimumSimilarity = 0.65f;
    private const float KeywordBonusWeight = 0.15f;
    private const int MinimumResultCount = 2;

    private readonly GeminiEmbeddingClient _embeddingClient;
    private readonly SemaphoreSlim _indexLock = new(1, 1);
    private List<(KnowledgeDocument Document, float[] Embedding)>? _index;

    public SemanticKnowledgeRetriever(GeminiEmbeddingClient embeddingClient)
    {
        _embeddingClient = embeddingClient;
    }

    private async Task EnsureIndexBuiltAsync()
    {
        if (_index is not null)
        {
            return;
        }

        await _indexLock.WaitAsync();

        try
        {
            if (_index is not null)
            {
                return;
            }

            var index = new List<(KnowledgeDocument, float[])>();

            foreach (var document in KnowledgeBase.Documents)
            {
                var embedding = await _embeddingClient.GenerateEmbeddingAsync(
                    document.Content);

                index.Add((document, embedding));
            }

            _index = index;
        }
        finally
        {
            _indexLock.Release();
        }
    }

    private static float CalculateKeywordBonus(string query, string title)
    {
        var titleWords = title
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.ToLowerInvariant())
            .ToList();

        if (titleWords.Count == 0)
        {
            return 0f;
        }

        var queryLower = query.ToLowerInvariant();

        var matchedWords = titleWords.Count(word => queryLower.Contains(word));

        var overlapRatio = (float)matchedWords / titleWords.Count;

        return overlapRatio * KeywordBonusWeight;
    }

    public async Task<List<(KnowledgeDocument Document, float Score)>> RetrieveWithScoresAsync(
        string query)
    {
        await EnsureIndexBuiltAsync();

        var queryEmbedding =
            await _embeddingClient.GenerateEmbeddingAsync(query);

        var safeIndex = _index!
     .Where(entry => !PromptInjectionDetector.ContainsSuspiciousInstructions(entry.Document.Content))
     .ToList();

        var allScored = safeIndex
            .Select(entry =>
            {
                var semanticScore = CosineSimilarity.Calculate(
                    queryEmbedding,
                    entry.Embedding);

                var keywordBonus = CalculateKeywordBonus(
                    query,
                    entry.Document.Title);

                return (
                    entry.Document,
                    Score: semanticScore + keywordBonus);
            })
            .OrderByDescending(x => x.Score)
            .ToList();

        var aboveThreshold = allScored
            .Where(x => x.Score >= MinimumSimilarity)
            .ToList();

        if (aboveThreshold.Count >= MinimumResultCount)
        {
            return aboveThreshold;
        }

        return allScored
            .Take(MinimumResultCount)
            .ToList();
    }

    public async Task<List<KnowledgeDocument>> RetrieveAsync(
        string query,
        int topN = 2)
    {
        var scored = await RetrieveWithScoresAsync(query);

        return scored
            .Take(topN)
            .Select(x => x.Document)
            .ToList();
    }
}