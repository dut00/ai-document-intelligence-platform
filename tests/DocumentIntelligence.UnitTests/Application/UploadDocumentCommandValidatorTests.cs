using System.Text;
using DocumentIntelligence.Application.Documents;
using DocumentIntelligence.Domain.Documents;
using DocumentIntelligence.Domain.Users;

namespace DocumentIntelligence.UnitTests.Application;

public sealed class UploadDocumentCommandValidatorTests
{
    private static readonly byte[] _pdf = Encoding.ASCII.GetBytes("%PDF-1.7\n...");
    private static readonly byte[] _text = Encoding.UTF8.GetBytes("Zażółć gęślą jaźń: payment due 11.11.2026");

    private readonly UploadDocumentCommandValidator _validator = new();

    [Theory]
    [InlineData("contract.pdf", "application/pdf")]
    [InlineData("CONTRACT.PDF", "application/pdf")]
    public async Task Pdf_with_pdf_signature_is_valid(string fileName, string contentType)
    {
        var result = await ValidateAsync(Command(fileName, contentType, _pdf));

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Utf8_text_file_is_valid()
    {
        var result = await ValidateAsync(Command("notes.txt", "text/plain; charset=utf-8", _text));

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Empty_file_is_rejected()
    {
        var result = await ValidateAsync(Command("empty.txt", "text/plain", []));

        result.Errors.ShouldHaveSingleItem().PropertyName.ShouldBe(nameof(UploadDocumentCommand.Length));
    }

    [Fact]
    public async Task File_over_the_size_limit_is_rejected()
    {
        var command = Command("big.txt", "text/plain", _text) with { Length = FileSize.MaxBytes + 1 };

        var result = await ValidateAsync(command);

        result.Errors.ShouldHaveSingleItem().PropertyName.ShouldBe(nameof(UploadDocumentCommand.Length));
    }

    [Theory]
    [InlineData("image.png", "image/png")]
    [InlineData("notes.txt", null)]
    [InlineData("notes.txt", "application/octet-stream")]
    public async Task Unsupported_content_type_is_rejected(string fileName, string? contentType)
    {
        var result = await ValidateAsync(Command(fileName, contentType, _text));

        result.Errors.ShouldHaveSingleItem().PropertyName.ShouldBe(nameof(UploadDocumentCommand.ContentType));
    }

    [Theory]
    [InlineData("notes.pdf", "text/plain")]
    [InlineData("contract.txt", "application/pdf")]
    [InlineData("contract", "application/pdf")]
    public async Task Extension_that_does_not_match_content_type_is_rejected(string fileName, string contentType)
    {
        var result = await ValidateAsync(Command(fileName, contentType, _pdf));

        result.Errors.ShouldHaveSingleItem().PropertyName.ShouldBe(nameof(UploadDocumentCommand.FileName));
    }

    [Fact]
    public async Task Pdf_without_pdf_signature_is_rejected()
    {
        var result = await ValidateAsync(Command("fake.pdf", "application/pdf", _text));

        result.Errors.ShouldHaveSingleItem().PropertyName.ShouldBe(nameof(UploadDocumentCommand.Content));
    }

    [Fact]
    public async Task Binary_content_declared_as_text_is_rejected()
    {
        var result = await ValidateAsync(Command("fake.txt", "text/plain", [0x50, 0x4B, 0x03, 0x04, 0x00, 0x00]));

        result.Errors.ShouldHaveSingleItem().PropertyName.ShouldBe(nameof(UploadDocumentCommand.Content));
    }

    [Fact]
    public async Task Validation_rewinds_the_content_stream()
    {
        var command = Command("contract.pdf", "application/pdf", _pdf);

        await ValidateAsync(command);

        command.Content.Position.ShouldBe(0);
    }

    private static UploadDocumentCommand Command(string fileName, string? contentType, byte[] content) =>
        new(UserId.New(), fileName, contentType, content.Length, new MemoryStream(content));

    private Task<FluentValidation.Results.ValidationResult> ValidateAsync(UploadDocumentCommand command) =>
        _validator.ValidateAsync(command, TestContext.Current.CancellationToken);
}
