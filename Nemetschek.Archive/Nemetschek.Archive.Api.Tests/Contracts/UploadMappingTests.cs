using System.Text;
using Microsoft.AspNetCore.Http;
using Nemetschek.Archive.Api.Contracts.Uploads.Mappings;
using Nemetschek.Archive.Api.Contracts.Uploads.Requests;
using Nemetschek.Archive.Core.Domain.Uploads;

namespace Nemetschek.Archive.Api.Tests.Contracts;

/// <summary>
/// The multipart body on the way in, and the upload report on the way out.
/// </summary>
public sealed class UploadMappingTests
{
    // --- the body on the way in ----------------------------------------------------------------

    [Fact]
    public void The_route_cabinet_and_the_form_field_reach_the_rules()
    {
        var request = Form("Batch", "sample-material").ToUploadRequest("Invoices");

        Assert.Equal("Invoices", request.CabinetName);
        Assert.Equal("Batch", request.FieldName);
        Assert.Equal("sample-material", request.FolderName);
    }

    [Fact]
    public void A_parts_filename_becomes_the_relative_path_the_rules_read()
    {
        // This is the whole transport decision: the structure of the folder travels in `filename`.
        var form = Form("Batch", "sample-material", files:
            ["invoice-001.txt", "delivery-2026-03/page1.txt"]);

        Assert.Equal(
            ["invoice-001.txt", "delivery-2026-03/page1.txt"],
            form.ToUploadRequest("Invoices").Files.Select(file => file.RelativePath));
    }

    [Fact]
    public void A_filename_is_carried_across_untouched_because_the_rules_judge_it()
    {
        // Nothing here sanitises or rejects: RelativePath is the one place that decides, and it
        // reports rather than silently rewriting. A mapping that "helped" would hide an attack.
        var form = Form("Batch", "sample-material", files: ["../../etc/passwd"]);

        Assert.Equal(
            "../../etc/passwd",
            Assert.Single(form.ToUploadRequest("Invoices").Files).RelativePath);
    }

    [Fact]
    public async Task A_parts_content_is_reachable_without_being_read_during_mapping()
    {
        var form = Form("Batch", "sample-material", files: ["a.txt"]);

        var file = Assert.Single(form.ToUploadRequest("Invoices").Files);

        await using var content = file.OpenContent();
        Assert.Equal("content of a.txt", await new StreamReader(content).ReadToEndAsync());
    }

    [Fact]
    public void The_named_folders_are_passed_through()
    {
        var form = Form("Batch", "sample-material", folders: ["empty-folder", "delivery/archive"]);

        Assert.Equal(
            ["empty-folder", "delivery/archive"],
            form.ToUploadRequest("Invoices").FolderPaths);
    }

    [Fact]
    public void An_upload_with_neither_files_nor_folders_still_maps()
    {
        var request = Form("Batch", "sample-material").ToUploadRequest("Invoices");

        Assert.Empty(request.Files);
        Assert.Empty(request.FolderPaths);
    }

    // --- the report on the way out -------------------------------------------------------------

    [Fact]
    public void The_report_maps_its_cabinet_field_and_folder_name()
    {
        var response = new UploadReport("Invoices", "Batch", "sample-material", []).ToResponse();

        Assert.Equal("Invoices", response.Cabinet);
        Assert.Equal("Batch", response.Field);
        Assert.Equal("sample-material", response.FolderName);
        Assert.Empty(response.Results);
    }

    [Fact]
    public void A_stored_entry_maps_its_id_and_part_names()
    {
        var id = Guid.CreateVersion7();
        var entry = new UploadEntry(
            "delivery", UploadEntryOutcome.Stored, null, id, ["page1.txt", "page2.txt"], null);

        var result = entry.ToResponse();

        Assert.Equal("delivery", result.Entry);
        Assert.Equal(UploadEntryOutcome.Stored, result.Outcome);
        Assert.Equal(id, result.DocumentId);
        Assert.Equal(["page1.txt", "page2.txt"], result.Parts);
        Assert.Null(result.Reason);
    }

    [Theory]
    [InlineData(UploadEntryOutcome.Skipped)]
    [InlineData(UploadEntryOutcome.Failed)]
    public void An_entry_that_was_not_stored_maps_its_reason_and_no_id(UploadEntryOutcome outcome)
    {
        var entry = new UploadEntry("a.txt", outcome, null, null, [], "the reason");

        var result = entry.ToResponse();

        Assert.Equal(outcome, result.Outcome);
        Assert.Null(result.DocumentId);
        Assert.Equal("the reason", result.Reason);
        Assert.Empty(result.Parts);
    }

    [Fact]
    public void The_draft_never_reaches_the_wire()
    {
        // The draft holds open-content callbacks and the whole domain document; the response shape
        // deliberately has no field for it.
        Assert.DoesNotContain(
            "Draft",
            typeof(Api.Contracts.Uploads.Responses.UploadEntryResult)
                .GetProperties().Select(property => property.Name));
    }

    [Fact]
    public void Every_row_of_the_report_is_mapped_in_order()
    {
        var report = new UploadReport("Invoices", "Batch", "sample-material",
        [
            new UploadEntry("a.txt", UploadEntryOutcome.Stored, null, Guid.CreateVersion7(), [], null),
            new UploadEntry("b.txt", UploadEntryOutcome.Skipped, null, null, [], "nested too deep"),
            new UploadEntry("c.txt", UploadEntryOutcome.Failed, null, null, [], "the volume is full"),
        ]);

        Assert.Equal(
            ["a.txt", "b.txt", "c.txt"],
            report.ToResponse().Results.Select(result => result.Entry));
    }

    private static UploadFormRequest Form(
        string field,
        string folderName,
        string[]? files = null,
        string[]? folders = null) =>
        new()
        {
            Field = field,
            FolderName = folderName,
            Files = [.. (files ?? []).Select(FormFileNamed)],
            Folders = [.. folders ?? []],
        };

    private static IFormFile FormFileNamed(string relativePath)
    {
        var content = Encoding.UTF8.GetBytes($"content of {Path.GetFileName(relativePath)}");

        return new FormFile(new MemoryStream(content), 0, content.Length, "files", relativePath);
    }
}
