using CourseRegistration.Api.Models;
using CourseRegistration.Api.Models.Dtos;
using CourseRegistration.Api.Store;

namespace CourseRegistration.Api.Services;

// Thin delegation layer over ICourseStore. The atomicity-sensitive algorithm (decision
// table, skip-and-retain promotion) lives in InMemoryCourseStore because the lock must
// own it — see course-registration-plan.md, "Clarification on where the algorithm
// lives." This class exists so Controllers depend on an abstraction (IRegistrationService)
// rather than the concrete store, preserving Dependency Inversion and testability.
public class RegistrationService : IRegistrationService
{
    private readonly ICourseStore _store;

    public RegistrationService(ICourseStore store)
    {
        _store = store;
    }

    public void AddCourse(Course course) => _store.AddCourse(course);

    public void AddLearner(Learner learner) => _store.AddLearner(learner);

    public bool UpdateLearnerCompletedCourses(string learnerId, IEnumerable<string> completedCourses) =>
        _store.UpdateLearnerCompletedCourses(learnerId, completedCourses);

    public EnrollmentResult Enroll(string courseId, string learnerId) =>
        _store.Enroll(courseId, learnerId);

    public EnrollmentResult Drop(string courseId, string learnerId) =>
        _store.Drop(courseId, learnerId);

    public CourseStatusResponse? GetStatus(string courseId) =>
        _store.GetStatus(courseId);
}
