using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.AI.Api.AI;
using Portfolio.AI.Api.Services;
using Portfolio.AI.Api.Tests.Support;

namespace Portfolio.AI.Api.Tests.Services;

public class MarkdownPortfolioKnowledgeSourceTests
{
    private const string RelativePath = "Data/portfolio-profile-public.md";

    [Fact]
    public void GetKnowledgeContext_WithValidFile_ReturnsMarkdownAndCachesIt()
    {
        var root = CreateTempRoot();
        try
        {
            WriteProfile(root, "# Public Profile\n\nDarwin worked on Azure Service Bus workflows.");

            var source = CreateSource(root);

            var first = source.GetKnowledgeContext();
            File.Delete(Path.Combine(root, RelativePath));
            var second = source.GetKnowledgeContext();

            Assert.Contains("Azure Service Bus", first);
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
    public void GetKnowledgeContext_WhenFileIsEmpty_ThrowsSafeConfigurationError()
    {
        var root = CreateTempRoot();
        try
        {
            WriteProfile(root, "   \r\n\t");

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
    public void GetKnowledgeContext_WhenFileContainsInvalidCharacters_ThrowsSafeConfigurationError()
    {
        var root = CreateTempRoot();
        try
        {
            WriteProfile(root, "# Public Profile\0\0");

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
    public void GetKnowledgeContext_WhenPathIsNotAFile_ThrowsSafeConfigurationError()
    {
        var root = CreateTempRoot();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, RelativePath));

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

    private static MarkdownPortfolioKnowledgeSource CreateSource(string root) =>
        new(
            new TestWebHostEnvironment(root),
            NullLogger<MarkdownPortfolioKnowledgeSource>.Instance);

    private static void WriteProfile(string root, string content)
    {
        var path = Path.Combine(root, RelativePath);
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
