using Nemetschek.Archive.Api.Contracts.Documents.Requests;

namespace Nemetschek.Archive.Api.Tests.Contracts;

/// <summary>
/// Turning a page number and size into the window the store reads, within the server's maximum.
/// </summary>
// The maximum is the brief's configurable page size, and the review demo runs with it set to 5, so
// the clamping is demo-visible behaviour rather than a detail.
public sealed class DocumentPageRequestTests
{
    [Fact]
    public void The_defaults_are_the_first_page_of_twenty()
    {
        var request = new DocumentPageRequest();

        Assert.Equal(1, request.Page);
        Assert.Equal(20, request.Size);
    }

    [Theory]
    [InlineData(20, 50, 20)]
    [InlineData(100, 50, 50)]
    [InlineData(6, 5, 5)]
    [InlineData(5, 5, 5)]
    public void A_page_never_holds_more_than_the_server_allows(int size, int max, int expected)
    {
        Assert.Equal(expected, new DocumentPageRequest { Size = size }.Take(max));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void A_size_below_one_still_asks_for_one_document(int size)
    {
        // Model validation also rejects it, but the clamp is what stops a zero-size page becoming an
        // endless client loop if validation is ever relaxed.
        Assert.Equal(1, new DocumentPageRequest { Size = size }.Take(50));
    }

    [Theory]
    [InlineData(1, 5, 0)]
    [InlineData(2, 5, 5)]
    [InlineData(3, 5, 10)]
    [InlineData(4, 20, 60)]
    public void The_skip_is_the_pages_before_this_one(int page, int size, int expected)
    {
        Assert.Equal(expected, new DocumentPageRequest { Page = page, Size = size }.Skip(50));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void A_page_below_one_reads_from_the_beginning(int page)
    {
        Assert.Equal(0, new DocumentPageRequest { Page = page, Size = 10 }.Skip(50));
    }

    [Fact]
    public void The_skip_follows_the_capped_size_not_the_requested_one()
    {
        // Page 3 of a 100-size request under a maximum of 5 is documents 10-14, not 200-299.
        Assert.Equal(10, new DocumentPageRequest { Page = 3, Size = 100 }.Skip(5));
    }
}
