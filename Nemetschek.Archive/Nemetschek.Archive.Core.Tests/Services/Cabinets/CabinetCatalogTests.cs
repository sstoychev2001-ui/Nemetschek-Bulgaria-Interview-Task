using Nemetschek.Archive.Core.Services.Cabinets;
using Nemetschek.Archive.Core.Tests.Utils;

namespace Nemetschek.Archive.Core.Tests.Services.Cabinets;

/// <summary>
/// The catalog: a fixed set of cabinets, looked up by technical name.
/// </summary>
[TestClass]
public sealed class CabinetCatalogTests
{
    private CabinetCatalog catalog = null!;

    [TestInitialize]
    public void BeforeEach() =>
        catalog = new CabinetCatalog([TestCabinets.Invoices(), TestCabinets.Contracts()]);

    [TestMethod]
    public void Every_cabinet_is_listed_in_the_order_it_was_given()
    {
        CollectionAssert.AreEqual(
            new[] { "Invoices", "Contracts" },
            catalog.All.Select(cabinet => cabinet.Name).ToArray());
    }

    [TestMethod]
    public void An_empty_catalog_finds_nothing_and_lists_nothing()
    {
        var empty = new CabinetCatalog([]);

        Assert.IsEmpty(empty.All);
        Assert.IsNull(empty.Find("Invoices"));
    }
}
