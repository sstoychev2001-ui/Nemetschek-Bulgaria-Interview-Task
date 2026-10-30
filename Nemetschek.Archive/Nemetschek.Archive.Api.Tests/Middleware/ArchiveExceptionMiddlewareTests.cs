using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Nemetschek.Archive.Api.Middleware;
using Nemetschek.Archive.Core.Exceptions.Cabinets;
using Nemetschek.Archive.Core.Exceptions.Uploads;

namespace Nemetschek.Archive.Api.Tests.Middleware;

/// <summary>
/// The one place in the service that turns a refusal into a status code.
/// </summary>
public sealed class ArchiveExceptionMiddlewareTests
{
    [Fact]
    public async Task An_unknown_cabinet_is_answered_404()
    {
        var (context, problems, _) = await Invoke(new UnknownCabinetException("Nonsense"));

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.Equal("Unknown file cabinet", problems.Written!.Title);
        Assert.Equal(StatusCodes.Status404NotFound, problems.Written.Status);
        Assert.Equal("There is no file cabinet named 'Nonsense'.", problems.Written.Detail);
    }

    [Fact]
    public async Task A_refused_upload_is_answered_400()
    {
        var refusal = new UploadRefusedException(
            "Status", "Index field 'Status' accepts at most 12 characters, but the value has 15.");

        var (context, problems, _) = await Invoke(refusal);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal("The upload was refused", problems.Written!.Title);
        Assert.Equal(StatusCodes.Status400BadRequest, problems.Written.Status);
    }

    [Fact]
    public async Task The_detail_is_the_rules_own_sentence_so_a_client_can_show_it()
    {
        var refusal = new UploadRefusedException("Amount", "Index field 'Amount' holds a number value; a text field is required.");

        var (_, problems, _) = await Invoke(refusal);

        Assert.Equal(
            "Index field 'Amount' holds a number value; a text field is required.",
            problems.Written!.Detail);
    }

    [Fact]
    public async Task An_unexpected_failure_is_rethrown_rather_than_dressed_as_a_refusal()
    {
        var bug = new InvalidOperationException("something the Archive did not expect");
        var problems = new CapturingProblemDetailsService();

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new ArchiveExceptionMiddleware(_ => throw bug, problems, Logger())
                .InvokeAsync(new DefaultHttpContext()));

        Assert.Same(bug, thrown);
        Assert.Null(problems.Written);
    }

    [Theory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(ArgumentException))]
    [InlineData(typeof(NotSupportedException))]
    public async Task Only_the_refusals_the_Archive_expects_are_mapped(Type exceptionType)
    {
        var problems = new CapturingProblemDetailsService();
        var exception = (Exception)Activator.CreateInstance(exceptionType)!;

        await Assert.ThrowsAsync(
            exceptionType,
            () => new ArchiveExceptionMiddleware(_ => throw exception, problems, Logger())
                .InvokeAsync(new DefaultHttpContext()));

        Assert.Null(problems.Written);
    }

    [Fact]
    public async Task A_request_that_does_not_fail_is_left_alone()
    {
        var problems = new CapturingProblemDetailsService();
        var context = new DefaultHttpContext();

        await new ArchiveExceptionMiddleware(_ => Task.CompletedTask, problems, Logger())
            .InvokeAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Null(problems.Written);
    }

    [Fact]
    public async Task A_refusal_is_logged_with_its_status_and_reason()
    {
        var (_, _, logger) = await Invoke(new UnknownCabinetException("Nonsense"));

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("404", entry.Message, StringComparison.Ordinal);
        Assert.Contains("Unknown file cabinet", entry.Message, StringComparison.Ordinal);
        Assert.Contains("Nonsense", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_refusal_is_not_logged_as_an_error()
    {
        var (_, _, logger) = await Invoke(new UploadRefusedException("Status", "too long"));

        Assert.DoesNotContain(logger.Entries, entry => entry.Level >= LogLevel.Warning);
    }

    private static async Task<(HttpContext Context, CapturingProblemDetailsService Problems,
        RecordingLogger Logger)> Invoke(Exception failure)
    {
        var context = new DefaultHttpContext();
        var problems = new CapturingProblemDetailsService();
        var logger = new RecordingLogger();

        await new ArchiveExceptionMiddleware(_ => throw failure, problems, logger)
            .InvokeAsync(context);

        return (context, problems, logger);
    }

    private static RecordingLogger Logger() => new();

    /// <summary>An <see cref="IProblemDetailsService"/> that keeps what the middleware wrote.</summary>
    private sealed class CapturingProblemDetailsService : IProblemDetailsService
    {
        public ProblemDetails? Written { get; private set; }

        public ValueTask WriteAsync(ProblemDetailsContext context)
        {
            Written = context.ProblemDetails;

            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> TryWriteAsync(ProblemDetailsContext context)
        {
            Written = context.ProblemDetails;

            return ValueTask.FromResult(true);
        }
    }

    /// <summary>An <see cref="ILogger{T}"/> that keeps every entry written to it.</summary>
    private sealed class RecordingLogger : ILogger<ArchiveExceptionMiddleware>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}
