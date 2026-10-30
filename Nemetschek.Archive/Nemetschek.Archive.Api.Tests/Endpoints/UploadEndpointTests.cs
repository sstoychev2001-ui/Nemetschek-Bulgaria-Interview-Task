using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Nemetschek.Archive.Api.Contracts.Uploads.Responses;
using Nemetschek.Archive.Core.Domain.Uploads;

namespace Nemetschek.Archive.Api.Tests.Endpoints;

/// <summary>
/// <c>POST /cabinets/{cabinet}/uploads</c>, against the real service in process.
/// </summary>
// The statuses here were verified by hand against the container for several sessions; this is the
// same walk-through as a test, so a change to the middleware or the contract fails the build rather
// than the demo.
public sealed class UploadEndpointTests : IDisposable
{
    private readonly ArchiveApplication application = new();

    private readonly HttpClient client;

    public UploadEndpointTests() => client = application.CreateClient();

    [Fact]
    public async Task A_file_and_a_folder_are_stored_and_reported()
    {
        var response = await client.PostAsync(
            "/cabinets/Invoices/uploads",
            Body(
                field: "Batch",
                folderName: "sample-material",
                files: ["invoice-001.txt", "delivery/page1.txt", "delivery/page2.txt"]),
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var report = await Read(response);
        Assert.Equal("Invoices", report.Cabinet);
        Assert.Equal("Batch", report.Field);
        Assert.Equal("sample-material", report.FolderName);
        Assert.Equal(["delivery", "invoice-001.txt"], report.Results.Select(row => row.Entry));
        Assert.All(report.Results, row => Assert.Equal(UploadEntryOutcome.Stored, row.Outcome));
        Assert.Equal(
            ["page1.txt", "page2.txt"],
            report.Results.Single(row => row.Entry == "delivery").Parts);
    }

    [Fact]
    public async Task An_empty_folder_the_client_names_becomes_a_document_with_no_parts()
    {
        var response = await client.PostAsync(
            "/cabinets/Invoices/uploads",
            Body("Batch", "sample-material", folders: ["empty-folder"]),
            Token);

        var row = Assert.Single((await Read(response)).Results);
        Assert.Equal("empty-folder", row.Entry);
        Assert.Equal(UploadEntryOutcome.Stored, row.Outcome);
        Assert.Empty(row.Parts);
    }

    [Fact]
    public async Task An_entry_nested_too_deep_comes_back_skipped_inside_a_200()
    {
        var response = await client.PostAsync(
            "/cabinets/Invoices/uploads",
            Body("Batch", "sample-material", files: ["delivery/archive/old.txt"]),
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var report = await Read(response);
        var skipped = Assert.Single(report.Results, row => row.Outcome == UploadEntryOutcome.Skipped);
        Assert.Equal("delivery/archive/old.txt", skipped.Entry);
        Assert.Contains("nested more than one folder deep", skipped.Reason!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_zero_byte_file_and_an_empty_folder_are_told_apart()
    {
        var response = await client.PostAsync(
            "/cabinets/Invoices/uploads",
            Body("Batch", "sample-material", files: ["empty-note.txt"], folders: ["empty-folder"],
                content: ""),
            Token);

        var report = await Read(response);
        Assert.Equal(["empty-note.txt"], report.Results.Single(row => row.Entry == "empty-note.txt").Parts);
        Assert.Empty(report.Results.Single(row => row.Entry == "empty-folder").Parts);
    }

    [Theory]
    [InlineData("Status", "accepts at most 12 characters")]
    [InlineData("ApprovedBy", "is read-only and cannot be chosen")]
    [InlineData("Amount", "holds a number value; a text field is required")]
    [InlineData("Nonsense", "has no index field 'Nonsense'")]
    public async Task A_field_that_may_not_be_chosen_is_answered_400_with_the_rules_sentence(
        string field,
        string reason)
    {
        var response = await client.PostAsync(
            "/cabinets/Invoices/uploads",
            Body(field, "sample-material", files: ["a.txt"]),
            Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Token);
        Assert.Equal("The upload was refused", problem!.Title);
        Assert.Contains(reason, problem.Detail!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unknown_cabinet_is_answered_404()
    {
        var response = await client.PostAsync(
            "/cabinets/Nonsense/uploads",
            Body("Batch", "sample-material", files: ["a.txt"]),
            Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Token);
        Assert.Equal("Unknown file cabinet", problem!.Title);
        Assert.Contains("There is no file cabinet named 'Nonsense'", problem.Detail!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_missing_field_is_answered_400_in_the_frameworks_validation_shape()
    {
        // Documented in README.md as the one place the error shape differs: model validation is not
        // suppressed, so a missing form field answers ValidationProblemDetails rather than the
        // middleware's ProblemDetails.
        var response = await client.PostAsync(
            "/cabinets/Invoices/uploads",
            Body(field: null, folderName: "sample-material", files: ["a.txt"]),
            Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(Token);
        Assert.Contains("Field", problem!.Errors.Keys);
    }

    [Fact]
    public async Task A_folder_name_that_is_a_path_is_refused()
    {
        var response = await client.PostAsync(
            "/cabinets/Invoices/uploads",
            Body("Batch", "../escape", files: ["a.txt"]),
            Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_unicode_name_survives_the_round_trip()
    {
        var response = await client.PostAsync(
            "/cabinets/Invoices/uploads",
            Body("Batch", "sample-material", files: ["Договор София.txt"]),
            Token);

        var row = Assert.Single((await Read(response)).Results);
        Assert.Equal("Договор София.txt", row.Entry);
        Assert.Equal(["Договор София.txt"], row.Parts);
    }

    [Fact]
    public async Task The_response_is_json()
    {
        var response = await client.PostAsync(
            "/cabinets/Invoices/uploads",
            Body("Batch", "sample-material", files: ["a.txt"]),
            Token);

        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    public void Dispose()
    {
        client.Dispose();
        application.Dispose();
    }

    private static CancellationToken Token => CancellationToken.None;

    private static async Task<UploadReportResponse> Read(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<UploadReportResponse>(Token))!;

    private static MultipartFormDataContent Body(
        string? field,
        string folderName,
        string[]? files = null,
        string[]? folders = null,
        string content = "content")
    {
        var body = new MultipartFormDataContent();

        if (field is not null)
        {
            body.Add(new StringContent(field), "field");
        }

        body.Add(new StringContent(folderName), "folderName");

        foreach (var path in files ?? [])
        {
            var part = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
            part.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
            {
                Name = "files",
                FileName = path,
            };

            body.Add(part);
        }

        foreach (var path in folders ?? [])
        {
            body.Add(new StringContent(path), "folders");
        }

        return body;
    }
}
