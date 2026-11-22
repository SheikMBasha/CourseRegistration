using CourseRegistration.Api.Models;
using CourseRegistration.Api.Models.Dtos;

namespace CourseRegistration.Api.Services;

public interface IRegistrationService
{
    void AddCourse(Course course);
    void AddLearner(Learner learner);
    bool UpdateLearnerCompletedCourses(string learnerId, IEnumerable<string> completedCourses);
    EnrollmentResult Enroll(string courseId, string learnerId);
    EnrollmentResult Drop(string courseId, string learnerId);
    CourseStatusResponse? GetStatus(string courseId);
}
