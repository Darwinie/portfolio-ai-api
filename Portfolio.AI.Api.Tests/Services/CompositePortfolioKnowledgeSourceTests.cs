using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.AI.Api.AI;
using Portfolio.AI.Api.Services;
using Portfolio.AI.Api.Tests.Support;

namespace Portfolio.AI.Api.Tests.Services;

public class CompositePortfolioKnowledgeSourceTests
{
    private const string JsonRelativePath = "Data/portfolio-knowledge.json";
    private const string MarkdownRelativePath = "Data/portfolio-profile-public.md";

    [Fact]
    public void GetKnowledgeContext_WithBothFiles_CombinesStructuredFactsAndSupplementaryProfile()
    {
        var root = CreateTempRoot();
        try
        {
            WriteJson(root, """{"profile":{"name":"Darwin M. Lubugan","role":"Software Engineer"}}""");
            WriteMarkdown(root, "# Public Profile\n\nDarwin used Azure Service Bus workflows.");

            var source = CreateSource(root);

            var context = source.GetKnowledgeContext();

            Assert.Contains("<structured_facts>", context, StringComparison.Ordinal);
            Assert.Contains("</structured_facts>", context, StringComparison.Ordinal);
            Assert.Contains("<supplementary_profile>", context, StringComparison.Ordinal);
            Assert.Contains("</supplementary_profile>", context, StringComparison.Ordinal);
            Assert.Contains("Darwin M. Lubugan", context);
            Assert.Contains("Azure Service Bus", context);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void GetKnowledgeContext_CachesCombinedContext()
    {
        var root = CreateTempRoot();
        try
        {
            WriteJson(root, """{"profile":{"name":"Darwin M. Lubugan"}}""");
            WriteMarkdown(root, "# Public Profile\n\nSupplementary detail.");

            var source = CreateSource(root);

            var first = source.GetKnowledgeContext();
            File.Delete(Path.Combine(root, JsonRelativePath));
            File.Delete(Path.Combine(root, MarkdownRelativePath));
            var second = source.GetKnowledgeContext();

            Assert.Equal(first, second);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void GetKnowledgeContext_WhenSupplementaryProfileIsMissing_ThrowsSafeConfigurationError()
    {
        var root = CreateTempRoot();
        try
        {
            WriteJson(root, """{"profile":{"name":"Darwin M. Lubugan"}}""");

            var source = CreateSource(root);

            var exception = Assert.Throws<AiProviderException>(
                () => source.GetKnowledgeContext());

            Assert.Equal(AiProviderFailureKind.Configuration, exception.Kind);
            Assert.DoesNotContain(root, exception.Message);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static CompositePortfolioKnowledgeSource CreateSource(string root)
    {
        var environment = new TestWebHostEnvironment(root);
        return new CompositePortfolioKnowledgeSource(
            new JsonPortfolioKnowledgeSource(
                environment,
                NullLogger<JsonPortfolioKnowledgeSource>.Instance),
            new MarkdownPortfolioKnowledgeSource(
                environment,
                NullLogger<MarkdownPortfolioKnowledgeSource>.Instance));
    }

    private static void WriteJson(string root, string content)
    {
        var path = Path.Combine(root, JsonRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static void WriteMarkdown(string root, string content)
    {
        var path = Path.Combine(root, MarkdownRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "portfolio-assistant-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
