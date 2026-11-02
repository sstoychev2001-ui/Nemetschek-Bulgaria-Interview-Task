using System.Text;
using Microsoft.AspNetCore.Http;
using Nemetschek.Archive.Api.Contracts.Uploads.Mappings;
using Nemetschek.Archive.Api.Contracts.Uploads.Requests;
using Nemetschek.Archive.Core.Domain.Uploads;

namespace Nemetschek.Archive.Api.Tests.Contracts.Uploads.Mappings;

/// <summary>
/// The multipart body on the way in, and the upload report on the way out.
/// </summary>
[TestClass]
public sealed class UploadMappingTests
{
    [TestMethod]
    public void The_route_cabinet_and_the_form_field_reach_the_rules()
    {
        var request = Form("Batch", "sample-material").ToUploadRequest("Invoices");

        Assert.AreEqual("Invoices", request.CabinetName);
        Assert.AreEqual("Batch", request.FieldName);
        Assert.AreEqual("sample-material", request.FolderName);
    }

    [TestMethod]
    public void A_parts_filename_becomes_the_relative_path_the_rules_read()
    {
        var form = Form("Batch", "sample-material", files:
            ["invoice-001.txt", "delivery-2026-03/page1.txt"]);

        CollectionAssert.AreEqual(
            new[] { "invoice-001.txt", "delivery-2026-03/page1.txt" },
            form.ToUploadRequest("Invoices").Files.Select(file => file.RelativePath).ToArray());
    }

    [TestMethod]
    public void A_filename_is_carried_across_untouched_because_the_rules_judge_it()
    {
        var form = Form("Batch", "sample-material", files: ["../../etc/passwd"]);

        Assert.AreEqual(
            "../../etc/passwd",
            Assert.ContainsSingle(form.ToUploadRequest("Invoices").Files).RelativePath);
    }

    [TestMethod]
    public async Task A_parts_content_is_reachable_without_being_read_during_mapping()
    {
        var form = Form("Batch", "sample-material", files: ["a.txt"]);

        var file = Assert.ContainsSingle(form.ToUploadRequest("Invoices").Files);

        await using var content = file.OpenContent();
        Assert.AreEqual("content of a.txt", await new StreamReader(content).ReadToEndAsync());
    }

    [TestMethod]
    public void The_named_folders_are_passed_through()
    {
        var form = Form("Batch", "sample-material", folders: ["empty-folder", "delivery/archive"]);

        CollectionAssert.AreEqual(
            new[] { "empty-folder", "delivery/archive" },
            form.ToUploadRequest("Invoices").FolderPaths.ToArray());
    }

    [TestMethod]
    public void An_upload_with_neither_files_nor_folders_still_maps()
    {
        var request = Form("Batch", "sample-material").ToUploadRequest("Invoices");

        Assert.IsEmpty(request.Files);
        Assert.IsEmpty(request.FolderPaths);
    }

    [TestMethod]
    public void The_report_maps_its_cabinet_field_and_folder_name()
    {
        var response = new UploadReport("Invoices", "Batch", "sample-material", []).ToResponse();

        Assert.AreEqual("Invoices", response.Cabinet);
        Assert.AreEqual("Batch", response.Field);
        Assert.AreEqual("sample-material", response.FolderName);
        Assert.IsEmpty(response.Results);
    }

    [TestMethod]
    public void A_stored_entry_maps_its_id_and_part_names()
    {
        var id = Guid.CreateVersion7();
        var entry = new UploadEntry(
            "delivery", UploadEntryOutcome.Stored, null, id, ["page1.txt", "page2.txt"], null);

        var result = entry.ToResponse();

        Assert.AreEqual("delivery", result.Entry);
        Assert.AreEqual(UploadEntryOutcome.Stored, result.Outcome);
        Assert.AreEqual(id, result.DocumentId);
        CollectionAssert.AreEqual(new[] { "page1.txt", "page2.txt" }, result.Parts.ToArray());
        Assert.IsNull(result.Reason);
    }

    [TestMethod]
    public void The_draft_never_reaches_the_wire()
    {
        Assert.DoesNotContain(
            "Draft",
            typeof(Api.Contracts.Uploads.Responses.UploadEntryResult)
                .GetProperties().Select(property => property.Name));
    }

    [TestMethod]
    public void Every_row_of_the_report_is_mapped_in_order()
    {
        var report = new UploadReport("Invoices", "Batch", "sample-material",
        [
            new UploadEntry("a.txt", UploadEntryOutcome.Stored, null, Guid.CreateVersion7(), [], null),
            new UploadEntry("b.txt", UploadEntryOutcome.Skipped, null, null, [], "nested too deep"),
            new UploadEntry("c.txt", UploadEntryOutcome.Failed, null, null, [], "the volume is full"),
        ]);

        CollectionAssert.AreEqual(
            new[] { "a.txt", "b.txt", "c.txt" },
            report.ToResponse().Results.Select(result => result.Entry).ToArray());
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
