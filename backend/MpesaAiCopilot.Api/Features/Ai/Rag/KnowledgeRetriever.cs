namespace MpesaAiCopilot.Api.Features.Ai.Rag;

public class KnowledgeRetriever
{
    public List<KnowledgeDocument> Retrieve(string query)
    {
        var results = new List<KnowledgeDocument>();

        foreach (var document in KnowledgeBase.Documents)
        {
            if (query.Contains(document.Title, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(document);
            }
        }

        return results;
    }
}