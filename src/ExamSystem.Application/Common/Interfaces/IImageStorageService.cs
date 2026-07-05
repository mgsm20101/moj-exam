namespace ExamSystem.Application.Common.Interfaces;

public interface IImageStorageService
{
    /// <returns>A relative URL (e.g. "/question-images/<guid>.jpg") to store on the Question.</returns>
    Task<string> SaveAsync(Stream content, string originalFileName, string contentType, CancellationToken cancellationToken);
}
