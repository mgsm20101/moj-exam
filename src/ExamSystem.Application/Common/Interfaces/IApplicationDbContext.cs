using ExamSystem.Domain.Questions;
using ExamSystem.Domain.Topics;
using Microsoft.EntityFrameworkCore;

namespace ExamSystem.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Topic> Topics { get; }
    DbSet<Question> Questions { get; }
    DbSet<QuestionOption> QuestionOptions { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
