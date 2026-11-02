using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Nemetschek.Archive.Api.Middleware;
using Nemetschek.Archive.Core.Exceptions.Cabinets;
using Nemetschek.Archive.Core.Exceptions.Uploads;

namespace Nemetschek.Archive.Api.Tests.Middleware;

/// <summary>
/// The one place in the service that turns a refusal into a status code. Both of the middleware's
/// dependencies are mocked, and what each was handed is captured for the assertions to read.
/// </summary>
[TestClass]
public sealed class ArchiveExceptionMiddlewareTests
{
    private readonly List<ProblemDetails> written = [];
    private readonly List<(LogLevel Level, string Message)> logged = [];

    private Mock<IProblemDetailsService> problems = null!;
    private Mock<ILogger<ArchiveExceptionMiddleware>> logger = null!;

    /// <summary>The ProblemDetails the middleware wrote, or <c>null</c> if it wrote none.</summary>
    private ProblemDetails? Written => written.LastOrDefault();

    [TestInitialize]
    public void BeforeEach()
    {
        problems = new Mock<IProblemDetailsService>();
        problems
            .Setup(service => service.TryWriteAsync(It.IsAny<ProblemDetailsContext>()))
            .Callback<ProblemDetailsContext>(context => written.Add(context.ProblemDetails))
            .Returns(ValueTask.FromResult(true));
        problems
            .Setup(service => service.WriteAsync(It.IsAny<ProblemDetailsContext>()))
            .Callback<ProblemDetailsContext>(context => written.Add(context.ProblemDetails))
            .Returns(ValueTask.CompletedTask);

        logger = new Mock<ILogger<ArchiveExceptionMiddleware>>();

        // Without this the LogInformation extension returns before it reaches Log, and nothing
        // is recorded — Moq's default for a bool is false.
        logger.Setup(log => log.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        logger
            .Setup(log => log.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            // The state is the framework's FormattedLogValues, whose ToString() is the rendered
            // message — the same string the formatter argument would produce, without reflecting
            // over a generic delegate to call it.
            .Callback(new InvocationAction(invocation => logged.Add((
                (LogLevel)invocation.Arguments[0],
                invocation.Arguments[2]?.ToString() ?? string.Empty))));
    }

    [TestMethod]
    public async Task An_unknown_cabinet_is_answered_404()
    {
        var context = await Invoke(new UnknownCabinetException("Nonsense"));

        Assert.AreEqual(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.AreEqual("Unknown file cabinet", Written!.Title);
        Assert.AreEqual(StatusCodes.Status404NotFound, Written.Status);
        Assert.AreEqual("There is no file cabinet named 'Nonsense'.", Written.Detail);
    }

    [TestMethod]
    public async Task A_refused_upload_is_answered_400()
    {
        var refusal = new UploadRefusedException(
            "Status", "Index field 'Status' accepts at most 12 characters, but the value has 15.");

        var context = await Invoke(refusal);

        Assert.AreEqual(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.AreEqual("The upload was refused", Written!.Title);
        Assert.AreEqual(StatusCodes.Status400BadRequest, Written.Status);
    }

    [TestMethod]
    public async Task The_detail_is_the_rules_own_sentence_so_a_client_can_show_it()
    {
        var refusal = new UploadRefusedException(
            "Amount", "Index field 'Amount' holds a number value; a text field is required.");

        await Invoke(refusal);

        Assert.AreEqual(
            "Index field 'Amount' holds a number value; a text field is required.",
            Written!.Detail);
    }

    [TestMethod]
    public async Task An_unexpected_failure_is_rethrown_rather_than_dressed_as_a_refusal()
    {
        var bug = new InvalidOperationException("something the Archive did not expect");

        var thrown = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => Middleware(_ => throw bug).InvokeAsync(new DefaultHttpContext()));

        Assert.AreSame(bug, thrown);
        Assert.IsNull(Written);
        problems.Verify(
            service => service.TryWriteAsync(It.IsAny<ProblemDetailsContext>()), Times.Never);
    }

    [TestMethod]
    public async Task A_request_that_does_not_fail_is_left_alone()
    {
        var context = new DefaultHttpContext();

        await Middleware(_ => Task.CompletedTask).InvokeAsync(context);

        Assert.AreEqual(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.IsNull(Written);
        problems.Verify(
            service => service.TryWriteAsync(It.IsAny<ProblemDetailsContext>()), Times.Never);
    }

    [TestMethod]
    public async Task A_refusal_is_logged_with_its_status_and_reason()
    {
        await Invoke(new UnknownCabinetException("Nonsense"));

        var entry = Assert.ContainsSingle(logged);
        Assert.AreEqual(LogLevel.Information, entry.Level);
        Assert.Contains("404", entry.Message, StringComparison.Ordinal);
        Assert.Contains("Unknown file cabinet", entry.Message, StringComparison.Ordinal);
        Assert.Contains("Nonsense", entry.Message, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task A_refusal_is_not_logged_as_an_error()
    {
        await Invoke(new UploadRefusedException("Status", "too long"));

        Assert.DoesNotContain(entry => entry.Level >= LogLevel.Warning, logged);
    }

    /// <summary>Runs the middleware over a request that throws, and answers with the context.</summary>
    private async Task<HttpContext> Invoke(Exception failure)
    {
        var context = new DefaultHttpContext();

        await Middleware(_ => throw failure).InvokeAsync(context);

        return context;
    }

    private ArchiveExceptionMiddleware Middleware(RequestDelegate next) =>
        new(next, problems.Object, logger.Object);
}
