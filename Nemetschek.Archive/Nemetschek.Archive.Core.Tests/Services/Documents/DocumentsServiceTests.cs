using Moq;
using Nemetschek.Archive.Core.Domain.Documents;
using Nemetschek.Archive.Core.Domain.IndexFields;
using Nemetschek.Archive.Core.Exceptions.Cabinets;
using Nemetschek.Archive.Core.Services.Documents;
using Nemetschek.Archive.Core.Services.Interfaces.Cabinets;
using Nemetschek.Archive.Core.Services.Interfaces.Documents;
using Nemetschek.Archive.Core.Tests.Utils;

namespace Nemetschek.Archive.Core.Tests.Services.Documents;

/// <summary>
/// Reading a page: the cabinet is proved to exist before the store is asked.
/// </summary>
[TestClass]
public sealed class DocumentsServiceTests
{
    private readonly Mock<ICabinetCatalog> cabinets = new();
    private readonly Mock<IDocumentStore> store = new();

    private DocumentsService service = null!;

    [TestInitialize]
    public void BeforeEach()
    {
        cabinets.Setup(catalog => catalog.Find("Invoices")).Returns(TestCabinets.Invoices());
        service = new DocumentsService(cabinets.Object, store.Object);
    }

    [TestMethod]
    public async Task An_unknown_cabinet_is_refused_before_the_store_is_touched()
    {
        var refusal = await Assert.ThrowsExactlyAsync<UnknownCabinetException>(
            () => service.ListAsync("Nonsense", StoredRange.All, 0, 10, CancellationToken.None));

        Assert.AreEqual("Nonsense", refusal.CabinetName);
        store.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task The_page_window_is_passed_through_untouched()
    {
        await service.ListAsync("Invoices", StoredRange.All, skip: 40, take: 5, CancellationToken.None);

        store.Verify(
            documents => documents.ListAsync(
                "Invoices", StoredRange.All, 40, 5, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
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

        store
            .Setup(documents => documents.ListAsync(
                It.IsAny<string>(),
                It.IsAny<StoredRange>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(page);

        Assert.AreSame(
            page,
            await service.ListAsync("Invoices", StoredRange.All, 0, 10, CancellationToken.None));
    }
}
