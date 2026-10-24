using Portfolio.AI.Api.Services;

namespace Portfolio.AI.Api.Tests.AI;

public sealed class FakePortfolioKnowledgeSource : IPortfolioKnowledgeSource
{
    public string Knowledge { get; set; } = """
        <structured_facts>
        {"profile":{"name":"Darwin M. Lubugan","role":"Software Engineer"}}
        </structured_facts>
        <supplementary_profile>
        # Public Professional Profile

        Supplementary professional context.
        </supplementary_profile>
        """;

    public int CallCount { get; private set; }

    public string GetKnowledgeContext()
    {
        CallCount++;
        return Knowledge;
    }
}
