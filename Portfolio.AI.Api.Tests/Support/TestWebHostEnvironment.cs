using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace Portfolio.AI.Api.Tests.Support;

public sealed class TestWebHostEnvironment(string contentRootPath) : IWebHostEnvironment
{
    public string ApplicationName { get; set; } = "Portfolio.AI.Api.Tests";

    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

    public string WebRootPath { get; set; } = contentRootPath;

    public string EnvironmentName { get; set; } = "Development";

    public string ContentRootPath { get; set; } = contentRootPath;

    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
