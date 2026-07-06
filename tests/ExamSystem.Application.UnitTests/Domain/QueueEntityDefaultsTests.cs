using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Queue;
using Xunit;

namespace ExamSystem.Application.UnitTests.Domain;

public class QueueEntityDefaultsTests
{
    [Fact]
    public void Exam_Defaults_HaveCapacityAndGrace()
    {
        var exam = new Exam();
        Assert.Equal(20, exam.MaxConcurrentAttempts);
        Assert.Equal(3, exam.GraceWindowMinutes);
    }

    [Fact]
    public void WaitingQueueEntry_Defaults_AreWaiting()
    {
        var entry = new WaitingQueueEntry();
        Assert.Equal(WaitingQueueStatus.Waiting, entry.Status);
        Assert.NotEqual(System.Guid.Empty, entry.Id);
    }
}
