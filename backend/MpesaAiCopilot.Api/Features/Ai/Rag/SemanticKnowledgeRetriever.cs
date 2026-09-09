namespace MpesaAiCopilot.Api.Features.Ai.Rag;

public class SemanticKnowledgeRetriever
{
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
                var embedding = await _embeddingClient.GenerateEmbeddingAsync(document.Content);
                index.Add((document, embedding));
            }

            _index = index;
        }
        finally
        {
            _indexLock.Release();
        }
    }

    public async Task<List<(KnowledgeDocument Document, float Score)>> RetrieveWithScoresAsync(string query)
    {
        await EnsureIndexBuiltAsync();

        var queryEmbedding = await _embeddingClient.GenerateEmbeddingAsync(query);

        return _index!
            .Select(entry => (
                entry.Document,
                Score: CosineSimilarity.Calculate(queryEmbedding, entry.Embedding)))
            .OrderByDescending(x => x.Score)
            .ToList();
    }

    public async Task<List<KnowledgeDocument>> RetrieveAsync(string query, int topN = 2)
    {
        var scored = await RetrieveWithScoresAsync(query);

        return scored
            .Take(topN)
            .Select(x => x.Document)
            .ToList();
    }
    private Task<float[]> GetQueryEmbeddingAsync(string query) =>
        _embeddingClient.GenerateEmbeddingAsync(query);
}