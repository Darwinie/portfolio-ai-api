using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.AI.Api.Services;
using Portfolio.AI.Api.Tests.Support;

namespace Portfolio.AI.Api.Tests.Services;

public class PublishedKnowledgeFilesTests
{
    [Fact]
    public void PublicKnowledgeSources_LoadTogetherAndExcludePrivateProfileDetails()
    {
        var environment = new TestWebHostEnvironment(AppContext.BaseDirectory);
        var source = new CompositePortfolioKnowledgeSource(
            new JsonPortfolioKnowledgeSource(
                environment,
                NullLogger<JsonPortfolioKnowledgeSource>.Instance),
            new MarkdownPortfolioKnowledgeSource(
                environment,
                NullLogger<MarkdownPortfolioKnowledgeSource>.Instance));

        var context = source.GetKnowledgeContext();

        Assert.Contains("NOUS AV IT Solutions", context);
        Assert.Contains("Czarina Forex Exchange", context);
        Assert.Contains("Azure Service Bus", context);

        string[] privateMarkers =
        [
            "₱",
            "25,000",
            "girlfriend",
            "resignation",
            "notice period",
            "compensation",
            "salary",
            "C:\\Dev",
            "C:\\Apache24",
            "NOUS-AV-IT-S",
            "Lian",
            "Parañaque",
            "lubugandarwin05",
            "Adidas Philippines",
            "4 GB RAM"
        ];

        foreach (var marker in privateMarkers)
        {
            Assert.DoesNotContain(marker, context, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void PrivateWorkingProfile_IsNotCopiedToTheRuntimeOutput()
    {
        var privateProfilePath = Path.Combine(
            AppContext.BaseDirectory,
            "Data",
            "personal_profile_darwin_lubugan.md");

        Assert.False(File.Exists(privateProfilePath));
    }

    [Fact]
    public void PublicProfile_FlagsUnconfirmedTimekeepingTechnology()
    {
        var environment = new TestWebHostEnvironment(AppContext.BaseDirectory);
        var source = new MarkdownPortfolioKnowledgeSource(
            environment,
            NullLogger<MarkdownPortfolioKnowledgeSource>.Instance);

        var profile = source.GetKnowledgeContext();

        Assert.Contains("PHP/MySQL", profile, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("C#/ASP.NET MVC", profile, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("unconfirmed", profile, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("conflict", profile, StringComparison.OrdinalIgnoreCase);
    }
}
