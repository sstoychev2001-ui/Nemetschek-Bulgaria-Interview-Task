using System.Net;
using System.Text;
using Nemetschek.Export.Infrastructure.Archive;

namespace Nemetschek.Export.Infrastructure.Tests.Archive;

/// <summary>
/// The only way the Export service reaches the Archive: over HTTP, against a fake handler.
/// </summary>
public sealed class ArchiveHttpClientTests
{
    [Fact]
    public async Task Documents_are_read_page_by_page_until_the_total_is_accounted_for()
    {
        var handler = new FakeHandler()
            .Answers(Page(["first.txt", "second.txt"], total: 3))
            .Answers(Page(["third.txt"], total: 3));

        var documents = await Read(handler);

        Assert.Equal(["first.txt", "second.txt", "third.txt"], documents.Select(d => d.Title));
        Assert.Contains("page=1", handler.Requests[0].RequestUri!.Query, StringComparison.Ordinal);
        Assert.Contains("page=2", handler.Requests[1].RequestUri!.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task No_request_is_made_for_a_page_that_is_not_owed()
    {
        var handler = new FakeHandler()
            .Answers(Page(["first.txt", "second.txt"], total: 3))
            .Answers(Page(["third.txt"], total: 3));

        await Read(handler);

        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task One_request_is_enough_when_the_first_page_holds_everything()
    {
        var handler = new FakeHandler().Answers(Page(["only.txt"], total: 1));

        var documents = await Read(handler);

        Assert.Equal(["only.txt"], documents.Select(d => d.Title));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task An_empty_cabinet_costs_one_request_and_yields_nothing()
    {
        var handler = new FakeHandler().Answers(Page([], total: 0));

        Assert.Empty(await Read(handler));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task A_total_the_Archive_cannot_deliver_does_not_loop_for_ever()
    {
        var handler = new FakeHandler()
            .Answers(Page(["first.txt"], total: 4))
            .Answers(Page([], total: 4));

        Assert.Equal(["first.txt"], (await Read(handler)).Select(d => d.Title));
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task A_later_page_is_not_asked_for_until_the_caller_needs_it()
    {
        var handler = new FakeHandler()
            .Answers(Page(["first.txt"], total: 2))
            .Answers(Page(["second.txt"], total: 2));

        await foreach (var _ in Client(handler).ReadDocumentsAsync(
                           "Invoices",
                           new DateOnly(2026, 1, 1),
                           new DateOnly(2026, 12, 31),
                           CancellationToken.None))
        {
            break;
        }

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task The_dates_are_sent_as_calendar_dates_and_nothing_else()
    {
        var handler = new FakeHandler().Answers(Page([], 0));

        await Read(handler);

        var query = Uri.UnescapeDataString(handler.Requests[0].RequestUri!.Query);
        Assert.Contains("from=2026-01-01", query, StringComparison.Ordinal);
        Assert.Contains("to=2026-12-31", query, StringComparison.Ordinal);
        Assert.DoesNotContain("timeZone", query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_cabinet_name_is_escaped_into_the_path()
    {
        var handler = new FakeHandler().Answers(Page([], 0));

        await foreach (var _ in Client(handler).ReadDocumentsAsync(
                           "In voices/../etc",
                           new DateOnly(2026, 1, 1),
                           new DateOnly(2026, 12, 31),
                           CancellationToken.None))
        {
        }

        Assert.Contains(
            "In%20voices%2F..%2Fetc",
            handler.Requests[0].RequestUri!.AbsoluteUri,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_part_is_read_as_a_stream()
    {
        var handler = new FakeHandler().Answers(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(Encoding.UTF8.GetBytes("the content")),
        });

        await using var content = await Client(handler).OpenPartAsync(
            "Invoices", Guid.Empty, Guid.Empty, CancellationToken.None);

        Assert.Equal("the content", await new StreamReader(content).ReadToEndAsync());
    }

    [Fact]
    public async Task The_marker_is_written_onto_every_document_in_one_request()
    {
        var handler = new FakeHandler().Answers(new HttpResponseMessage(HttpStatusCode.OK));
        var first = Guid.Parse("01999999-0000-7000-8000-000000000001");
        var second = Guid.Parse("01999999-0000-7000-8000-000000000002");

        await Client(handler).SetTextIndexValueAsync(
            "Invoices", [first, second], "Batch", "Exported on 2026-03-01", CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Patch, request.Method);
        Assert.Equal("/cabinets/Invoices/documents/index-values", request.RequestUri!.AbsolutePath);
        Assert.Equal(
            """{"documentIds":["01999999-0000-7000-8000-000000000001","01999999-0000-7000-8000-000000000002"],"values":{"Batch":{"type":"Text","value":"Exported on 2026-03-01"}}}""",
            handler.Bodies[0]);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task A_status_the_Archive_refused_with_is_carried_into_the_failure(
        HttpStatusCode status)
    {
        var handler = new FakeHandler().Answers(new HttpResponseMessage(status)
        {
            Content = new StringContent("""{"title":"Unknown file cabinet"}"""),
        });

        var failure = await Assert.ThrowsAsync<HttpRequestException>(() => Read(handler));

        Assert.Equal(status, failure.StatusCode);

        Assert.DoesNotContain("Unknown file cabinet", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_answer_that_is_not_a_usable_document_is_refused()
    {
        var handler = new FakeHandler().Answers(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"total":1,"documents":[{"id":"00000000-0000-0000-0000-000000000000"}]}""",
                Encoding.UTF8,
                "application/json"),
        });

        await Assert.ThrowsAsync<InvalidDataException>(() => Read(handler));
    }

    private static async Task<List<Core.Domain.Exports.ArchiveDocument>> Read(FakeHandler handler)
    {
        var documents = new List<Core.Domain.Exports.ArchiveDocument>();

        await foreach (var document in Client(handler).ReadDocumentsAsync(
                           "Invoices",
                           new DateOnly(2026, 1, 1),
                           new DateOnly(2026, 12, 31),
                           CancellationToken.None))
        {
            documents.Add(document);
        }

        return documents;
    }

    private static ArchiveHttpClient Client(FakeHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://archive-api:8080/") });

    private static HttpResponseMessage Page(string[] titles, int total)
    {
        var documents = string.Join(",", titles.Select(title =>
            $$"""{"id":"{{Guid.CreateVersion7()}}","title":"{{title}}","parts":[]}"""));

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $$"""{"total":{{total}},"documents":[{{documents}}]}""",
                Encoding.UTF8,
                "application/json"),
        };
    }

    /// <summary>An <see cref="HttpMessageHandler"/> that answers from a queue and records calls.</summary>
    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> answers = new();

        public List<HttpRequestMessage> Requests { get; } = [];

        public List<string> Bodies { get; } = [];

        public FakeHandler Answers(HttpResponseMessage response)
        {
            answers.Enqueue(response);

            return this;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(request.Content is null
                ? ""
                : await request.Content.ReadAsStringAsync(cancellationToken));

            return answers.Count > 0
                ? answers.Dequeue()
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"total":0,"documents":[]}""", Encoding.UTF8, "application/json"),
                };
        }
    }
}
