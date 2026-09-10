using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using ResourcePulse.Common.Domain;
using ResourcePulse.Hosting;

namespace ResourcePulse.Application.Tests;

// The fallback for anything a service did not translate itself.
public class GlobalExceptionHandlerTests
{
    private sealed class Env(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static async Task<(int Status, JsonElement Body)> HandleAsync(
        Exception exception, string environment = "Production")
    {
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance, new Env(environment));
        var context = new DefaultHttpContext { TraceIdentifier = "trace-1" };
        context.Response.Body = new MemoryStream();

        (await handler.TryHandleAsync(context, exception, CancellationToken.None)).Should().BeTrue();

        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        return (context.Response.StatusCode, document.RootElement.Clone());
    }

    [Fact]
    public async Task AnUncaughtDomainException_IsA409_LikeOneTheServiceCaught()
    {
        // Services translate DomainException into ServiceResult.Conflict, which
        // ControllerFoundation renders as 409. Falling through to here must not
        // change the answer: whether a try/catch was written is not something the
        // caller can see or act on. (It used to be 422.)
        var (status, body) = await HandleAsync(new DomainException("Work windows must not overlap."));

        status.Should().Be(StatusCodes.Status409Conflict);
        body.GetProperty("status").GetInt32().Should().Be(StatusCodes.Status409Conflict);
        body.GetProperty("title").GetString().Should().Be("Conflict");
        body.GetProperty("detail").GetString().Should().Be("Work windows must not overlap.");
        body.GetProperty("correlationId").GetString().Should().Be("trace-1");
    }

    [Fact]
    public async Task AnyOtherException_IsA500ThatDoesNotLeakItsMessage()
    {
        var (status, body) = await HandleAsync(new InvalidOperationException("connection string: hunter2"));

        status.Should().Be(StatusCodes.Status500InternalServerError);
        body.GetProperty("detail").GetString().Should().Be("An unexpected error occurred.");
        body.GetProperty("correlationId").GetString().Should().Be("trace-1");
    }

    [Fact]
    public async Task InDevelopment_The500CarriesTheMessage()
    {
        var (_, body) = await HandleAsync(
            new InvalidOperationException("something specific"), Environments.Development);

        body.GetProperty("detail").GetString().Should().Be("something specific");
    }
}
