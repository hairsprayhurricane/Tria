using Tria.Models;

namespace Tria.Services;

public interface ILearningService
{
    List<Course> GetAllCourses();
    Course? GetCourseById(int courseId);
    CourseModule? GetModuleById(int moduleId);
    Lesson? GetLessonById(int lessonId);
    List<Lesson> GetLessonsByModuleId(int moduleId);
}
