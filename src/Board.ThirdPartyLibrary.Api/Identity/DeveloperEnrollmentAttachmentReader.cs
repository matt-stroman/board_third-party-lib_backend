namespace Board.ThirdPartyLibrary.Api.Identity;

internal static class DeveloperEnrollmentAttachmentReader
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
        ".png",
        ".jpg",
        ".jpeg",
        ".zip",
        ".txt"
    };

    private const int MaxAttachmentCount = 5;
    private const long MaxAttachmentBytes = 25L * 1024L * 1024L;

    public static async Task<(IReadOnlyList<ConversationAttachmentDraft> Attachments, Dictionary<string, string[]> Errors)> ReadAsync(
        IReadOnlyList<IFormFile>? files,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (files is null || files.Count == 0)
        {
            return ([], errors);
        }

        if (files.Count > MaxAttachmentCount)
        {
            errors["attachments"] = [$"No more than {MaxAttachmentCount} attachments are allowed."];
            return ([], errors);
        }

        var attachments = new List<ConversationAttachmentDraft>(files.Count);
        foreach (var file in files)
        {
            var extension = Path.GetExtension(file.FileName);
            if (!AllowedExtensions.Contains(extension))
            {
                errors[$"attachments:{file.FileName}"] = ["Only pdf, png, jpg, jpeg, zip, and txt files are allowed."];
                continue;
            }

            if (file.Length <= 0)
            {
                errors[$"attachments:{file.FileName}"] = ["Attachment files must not be empty."];
                continue;
            }

            if (file.Length > MaxAttachmentBytes)
            {
                errors[$"attachments:{file.FileName}"] = [$"Attachment files must be {MaxAttachmentBytes / (1024 * 1024)} MB or smaller."];
                continue;
            }

            await using var stream = file.OpenReadStream();
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream, cancellationToken);

            attachments.Add(new ConversationAttachmentDraft(
                file.FileName,
                string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                file.Length,
                memoryStream.ToArray()));
        }

        return (attachments, errors);
    }
}
