using Tria.Models;

namespace Tria.Services;

public interface IProgressService
{
    // Lesson progress
    Task<UserLessonProgress?> GetLessonProgressAsync(string userId, int lessonId);
    Task<List<UserLessonProgress>> GetAllProgressAsync(string userId);
    Task CompleteMaterialsAsync(string userId, int lessonId, int moduleId, int courseId, int xp);

    // Test attempts
    Task<List<UserTestAttempt>> GetTestAttemptsAsync(string userId, int lessonId);
    Task<UserTestAttempt?> GetLatestAttemptAsync(string userId, int lessonId);
    Task<UserTestAttempt> SubmitTestAsync(string userId, int lessonId, List<UserAnswer> answers, LessonTest test);

    // Aggregate progress
    Task<int> GetCourseProgressPercentAsync(string userId, int courseId, List<Lesson> allLessons);
    Task<int> GetModuleProgressPercentAsync(string userId, int moduleId, List<Lesson> moduleLessons);

    // XP
    Task<int> GetTotalXpAsync(string userId);
}
