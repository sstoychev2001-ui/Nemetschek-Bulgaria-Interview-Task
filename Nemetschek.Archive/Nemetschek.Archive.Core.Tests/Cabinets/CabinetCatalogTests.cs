using Nemetschek.Archive.Core.Services.Cabinets;

namespace Nemetschek.Archive.Core.Tests.Cabinets;

/// <summary>
/// The catalog: a fixed set of cabinets, looked up by technical name.
/// </summary>
public sealed class CabinetCatalogTests
{
    private static CabinetCatalog Catalog() =>
        new([TestCabinets.Invoices(), TestCabinets.Contracts()]);

    [Theory]
    [InlineData("Invoices", "Invoices")]
    [InlineData("invoices", "Invoices")]
    [InlineData("INVOICES", "Invoices")]
    [InlineData("Contracts", "Contracts")]
    public void A_cabinet_is_found_by_name_whatever_its_case(string asked, string expected)
    {
        Assert.Equal(expected, Catalog().Find(asked)?.Name);
    }

    [Theory]
    [InlineData("Nonsense")]
    [InlineData("")]
    [InlineData(" Invoices")]
    public void A_name_that_is_not_a_cabinet_is_not_found(string asked)
    {
        // Null rather than an exception: the caller decides what a miss means, and for an upload
        // that is UnknownCabinetException with the name the caller used.
        Assert.Null(Catalog().Find(asked));
    }

    [Fact]
    public void Every_cabinet_is_listed_in_the_order_it_was_given()
    {
        Assert.Equal(["Invoices", "Contracts"], Catalog().All.Select(cabinet => cabinet.Name));
    }

    [Fact]
    public void An_empty_catalog_finds_nothing_and_lists_nothing()
    {
        // The readiness check reads All.Count, so an empty catalog has to be observable rather than
        // throwing on construction.
        var empty = new CabinetCatalog([]);

        Assert.Empty(empty.All);
        Assert.Null(empty.Find("Invoices"));
    }
}
