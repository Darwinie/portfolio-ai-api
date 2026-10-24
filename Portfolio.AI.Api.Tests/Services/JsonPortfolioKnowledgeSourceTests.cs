using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.AI.Api.AI;
using Portfolio.AI.Api.Services;
using Portfolio.AI.Api.Tests.Support;

namespace Portfolio.AI.Api.Tests.Services;

public class JsonPortfolioKnowledgeSourceTests
{
    [Fact]
    public void GetKnowledgeContext_WithValidFile_ReturnsJsonAndCachesIt()
    {
        var root = CreateTempRoot();
        try
        {
            var dataDirectory = Path.Combine(root, "Data");
            Directory.CreateDirectory(dataDirectory);
            var path = Path.Combine(dataDirectory, "portfolio-knowledge.json");
            File.WriteAllText(path, """{"profile":{"name":"Darwin M. Lubugan"}}""");

            var source = CreateSource(root);

            var first = source.GetKnowledgeContext();
            File.Delete(path);
            var second = source.GetKnowledgeContext();

            Assert.Contains("Darwin M. Lubugan", first);
            Assert.Equal(first, second);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void GetKnowledgeContext_WhenFileIsMissing_ThrowsSafeConfigurationError()
    {
        var root = CreateTempRoot();
        try
        {
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

    [Fact]
    public void GetKnowledgeContext_WithInvalidJson_ThrowsSafeConfigurationError()
    {
        var root = CreateTempRoot();
        try
        {
            var dataDirectory = Path.Combine(root, "Data");
            Directory.CreateDirectory(dataDirectory);
            File.WriteAllText(
                Path.Combine(dataDirectory, "portfolio-knowledge.json"),
                "{not valid json");

            var source = CreateSource(root);

            var exception = Assert.Throws<AiProviderException>(
                () => source.GetKnowledgeContext());

            Assert.Equal(AiProviderFailureKind.Configuration, exception.Kind);
            Assert.DoesNotContain("not valid json", exception.Message);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static JsonPortfolioKnowledgeSource CreateSource(string root) =>
        new(
            new TestWebHostEnvironment(root),
            NullLogger<JsonPortfolioKnowledgeSource>.Instance);

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
