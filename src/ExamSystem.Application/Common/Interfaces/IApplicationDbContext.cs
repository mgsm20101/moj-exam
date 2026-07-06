using ExamSystem.Domain.Attempts;
using ExamSystem.Domain.Candidates;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Questions;
using ExamSystem.Domain.Queue;
using ExamSystem.Domain.Topics;
using Microsoft.EntityFrameworkCore;

namespace ExamSystem.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Topic> Topics { get; }
    DbSet<Question> Questions { get; }
    DbSet<QuestionOption> QuestionOptions { get; }
    DbSet<Exam> Exams { get; }
    DbSet<ExamTopicSelection> ExamTopicSelections { get; }

    DbSet<Candidate> Candidates { get; }
    DbSet<CandidateExamAttemptGrant> CandidateExamAttemptGrants { get; }
    DbSet<ExamAttempt> ExamAttempts { get; }
    DbSet<AttemptQuestion> AttemptQuestions { get; }
    DbSet<AttemptQuestionOption> AttemptQuestionOptions { get; }
    DbSet<AttemptAnswer> AttemptAnswers { get; }
    DbSet<WaitingQueueEntry> WaitingQueueEntries { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
