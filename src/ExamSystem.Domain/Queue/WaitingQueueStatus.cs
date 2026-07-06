namespace ExamSystem.Domain.Queue;

public enum WaitingQueueStatus
{
    Waiting = 0,
    Called = 1,
    Started = 2,
    Expired = 3,
    Cancelled = 4
}
