using Nemetschek.Archive.Core.Domain.Documents;
using Nemetschek.Archive.Core.Domain.IndexFields;
using Nemetschek.Archive.Core.Exceptions.Cabinets;
using Nemetschek.Archive.Core.Services.Cabinets;
using Nemetschek.Archive.Core.Services.Documents;
using Nemetschek.Archive.Core.Services.Interfaces.Documents;
using Nemetschek.Archive.Core.Tests.Cabinets;

namespace Nemetschek.Archive.Core.Tests.Documents;

/// <summary>
/// Reading a page: the cabinet is proved to exist before the store is asked.
/// </summary>
public sealed class DocumentsServiceTests
{
    [Fact]
    public async Task An_unknown_cabinet_is_refused_before_the_store_is_touched()
    {
        var store = new RecordingStore();

        var refusal = await Assert.ThrowsAsync<UnknownCabinetException>(
            () => Service(store).ListAsync("Nonsense", StoredRange.All, 0, 10, CancellationToken.None));

        Assert.Equal("Nonsense", refusal.CabinetName);
        Assert.Null(store.AskedFor);
    }

    [Theory]
    [InlineData("Invoices")]
    [InlineData("invoices")]
    [InlineData("INVOICES")]
    public async Task A_cabinet_is_found_case_insensitively_and_read_under_its_own_name(string asked)
    {
        var store = new RecordingStore();

        await Service(store).ListAsync(asked, StoredRange.All, 0, 10, CancellationToken.None);

        Assert.Equal("Invoices", store.AskedFor);
    }

    [Fact]
    public async Task The_page_window_is_passed_through_untouched()
    {
        var store = new RecordingStore();

        await Service(store).ListAsync("Invoices", StoredRange.All, skip: 40, take: 5, CancellationToken.None);

        Assert.Equal((40, 5), (store.Skip, store.Take));
    }

    [Fact]
    public async Task The_store_page_is_returned_as_it_is()
    {
        var page = new DocumentPage(
            [
                new Document
                {
                    CabinetName = "Invoices",
                    Title = "a",
                    IndexValues = new Dictionary<string, IndexValue>(),
                    Parts = [],
                },
            ],
            7);
        var store = new RecordingStore { Page = page };

        Assert.Same(page, await Service(store).ListAsync("Invoices", StoredRange.All, 0, 10, CancellationToken.None));
    }

    private static DocumentsService Service(IDocumentStore store) =>
        new(new CabinetCatalog([TestCabinets.Invoices(), TestCabinets.Contracts()]), store);

    /// <summary>An <see cref="IDocumentStore"/> that records the read it was asked for.</summary>
    private sealed class RecordingStore : IDocumentStore
    {
        public string? AskedFor { get; private set; }

        public int Skip { get; private set; }

        public int Take { get; private set; }

        public DocumentPage Page { get; init; } = new([], 0);

        public StoredRange? AskedOver { get; private set; }

        public Document Found { get; init; } = new()
        {
            CabinetName = "Invoices",
            Title = "a.txt",
            IndexValues = new Dictionary<string, IndexValue>(),
            Parts = [],
        };

        public Stream Content { get; init; } = new MemoryStream();

        public IReadOnlyDictionary<string, IndexValue>? Written { get; private set; }

        public Task<Document> AddAsync(Document document, CancellationToken cancellationToken) =>
            throw new NotSupportedException("A read must not store anything.");

        public Task<DocumentPage> ListAsync(
            string cabinetName,
            StoredRange storedAt,
            int skip,
            int take,
            CancellationToken cancellationToken)
        {
            AskedFor = cabinetName;
            AskedOver = storedAt;
            Skip = skip;
            Take = take;

            return Task.FromResult(Page);
        }

        public Task<Document> FindAsync(
            string cabinetName,
            Guid documentId,
            CancellationToken cancellationToken)
        {
            AskedFor = cabinetName;

            return Task.FromResult(Found);
        }

        public Task<Stream> OpenPartAsync(
            string cabinetName,
            Guid documentId,
            Guid partId,
            CancellationToken cancellationToken)
        {
            AskedFor = cabinetName;

            return Task.FromResult(Content);
        }

        public Task SetIndexValuesAsync(
            string cabinetName,
            Guid documentId,
            IReadOnlyDictionary<string, IndexValue> values,
            CancellationToken cancellationToken)
        {
            AskedFor = cabinetName;
            Written = values;

            return Task.CompletedTask;
        }
    }
}
