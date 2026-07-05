using ExamSystem.Application.Common.Interfaces;
using Microsoft.AspNetCore.Hosting;

namespace ExamSystem.Infrastructure.Files;

public class LocalImageStorageService : IImageStorageService
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/gif", "image/webp"
    };

    private readonly IWebHostEnvironment _environment;

    public LocalImageStorageService(IWebHostEnvironment environment) => _environment = environment;

    public async Task<string> SaveAsync(Stream content, string originalFileName, string contentType, CancellationToken cancellationToken)
    {
        if (!AllowedContentTypes.Contains(contentType))
        {
            throw new InvalidOperationException($"Unsupported image content type: {contentType}");
        }

        var webRoot = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
        var uploadsDir = Path.Combine(webRoot, "question-images");
        Directory.CreateDirectory(uploadsDir);

        var extension = Path.GetExtension(originalFileName) is { Length: > 0 } ext ? ext : ".jpg";
        var fileName = $"{Guid.NewGuid()}{extension}";
        var fullPath = Path.Combine(uploadsDir, fileName);

        await using (var fileStream = File.Create(fullPath))
        {
            await content.CopyToAsync(fileStream, cancellationToken);
        }

        return $"/question-images/{fileName}";
    }
}
