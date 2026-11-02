using Nemetschek.Archive.Api.Contracts.Documents.Requests;

namespace Nemetschek.Archive.Api.Tests.Contracts.Documents.Requests;

/// <summary>
/// Turning a page number and size into the window the store reads, within the server's maximum.
/// </summary>
[TestClass]
public sealed class DocumentPageRequestTests
{
    [TestMethod]
    public void The_defaults_are_the_first_page_of_twenty()
    {
        var request = new DocumentPageRequest();

        Assert.AreEqual(1, request.Page);
        Assert.AreEqual(20, request.Size);
    }

    [TestMethod]
    public void The_skip_follows_the_capped_size_not_the_requested_one()
    {
        Assert.AreEqual(10, new DocumentPageRequest { Page = 3, Size = 100 }.Skip(5));
    }
}
