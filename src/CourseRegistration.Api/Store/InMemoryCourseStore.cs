using CourseRegistration.Api.Models;
using CourseRegistration.Api.Models.Dtos;

namespace CourseRegistration.Api.Store;

public class InMemoryCourseStore : ICourseStore
{
    // `lock` (Monitor), not SemaphoreSlim: every critical section below is purely
    // synchronous in-memory dictionary/list work with no `await` inside it, so there's
    // nothing to gain from SemaphoreSlim's async WaitAsync() — it would add overhead and
    // manual Release()/Dispose() discipline for no benefit. If the store ever needed an
    // await inside a locked section (e.g. a real I/O-backed persistence layer), `lock`
    // would stop compiling there and SemaphoreSlim would become necessary — not the case
    // here by design.
    private readonly object _lock = new();
    private readonly Dictionary<string, Course> _courses = new();
    private readonly Dictionary<string, Learner> _learners = new();
    private readonly Dictionary<string, HashSet<string>> _enrolledByCourse = new();
    private readonly Dictionary<string, List<string>> _waitlistByCourse = new();

    public void AddCourse(Course course)
    {
        lock (_lock)
        {
            _courses[course.CourseId] = course;
            if (!_enrolledByCourse.ContainsKey(course.CourseId))
                _enrolledByCourse[course.CourseId] = new HashSet<string>();
            if (!_waitlistByCourse.ContainsKey(course.CourseId))
                _waitlistByCourse[course.CourseId] = new List<string>();
        }
    }

    public void AddLearner(Learner learner)
    {
        lock (_lock)
        {
            _learners[learner.LearnerId] = learner;
        }
    }

    public bool UpdateLearnerCompletedCourses(string learnerId, IEnumerable<string> completedCourses)
    {
        lock (_lock)
        {
            if (!_learners.TryGetValue(learnerId, out var learner))
                return false;

            learner.CompletedCourses = new HashSet<string>(completedCourses);
            return true;
        }
    }

    public EnrollmentResult Enroll(string courseId, string learnerId)
    {
        lock (_lock)
        {
            if (!_courses.TryGetValue(courseId, out var course))
                return new EnrollmentResult(EnrollmentOutcome.NotFound, $"Course '{courseId}' does not exist.");

            if (!_learners.TryGetValue(learnerId, out var learner))
                return new EnrollmentResult(EnrollmentOutcome.NotFound, $"Learner '{learnerId}' does not exist.");

            var enrolled = _enrolledByCourse[courseId];
            var waitlist = _waitlistByCourse[courseId];

            if (enrolled.Contains(learnerId))
                return new EnrollmentResult(EnrollmentOutcome.Conflict, $"Learner '{learnerId}' is already enrolled in '{courseId}'.");

            if (waitlist.Contains(learnerId))
                return new EnrollmentResult(EnrollmentOutcome.Conflict, $"Learner '{learnerId}' is already waitlisted for '{courseId}'.");

            var hasCapacity = enrolled.Count < course.Capacity;
            var eligible = IsEligible(course, learner);

            if (hasCapacity)
            {
                if (!eligible)
                    return new EnrollmentResult(EnrollmentOutcome.Rejected, $"Learner '{learnerId}' does not meet the prerequisite for '{courseId}'.");

                enrolled.Add(learnerId);
                return new EnrollmentResult(EnrollmentOutcome.Enrolled, $"Learner '{learnerId}' enrolled in '{courseId}'.");
            }

            // Course is full. Gate-at-the-seat: eligibility is not required to join the
            // waitlist; it's only enforced at promotion time.
            waitlist.Add(learnerId);
            return new EnrollmentResult(EnrollmentOutcome.Waitlisted, $"Learner '{learnerId}' waitlisted for '{courseId}'.");
        }
    }

    public EnrollmentResult Drop(string courseId, string learnerId)
    {
        lock (_lock)
        {
            if (!_courses.TryGetValue(courseId, out var course))
                return new EnrollmentResult(EnrollmentOutcome.NotFound, $"Course '{courseId}' does not exist.");

            var enrolled = _enrolledByCourse[courseId];
            var waitlist = _waitlistByCourse[courseId];

            if (enrolled.Remove(learnerId))
            {
                PromoteFirstEligible(course, enrolled, waitlist);
                return new EnrollmentResult(EnrollmentOutcome.Dropped, $"Learner '{learnerId}' dropped from '{courseId}'; seat released.");
            }

            if (waitlist.Remove(learnerId))
                return new EnrollmentResult(EnrollmentOutcome.Dropped, $"Learner '{learnerId}' removed from the waitlist for '{courseId}'.");

            return new EnrollmentResult(EnrollmentOutcome.NotFound, $"Learner '{learnerId}' has no enrollment or waitlist entry for '{courseId}'.");
        }
    }

    public CourseStatusResponse? GetStatus(string courseId)
    {
        lock (_lock)
        {
            if (!_courses.TryGetValue(courseId, out var course))
                return null;

            var enrolled = _enrolledByCourse[courseId];
            var waitlist = _waitlistByCourse[courseId];

            return new CourseStatusResponse
            {
                CourseId = courseId,
                Capacity = course.Capacity,
                EnrolledCount = enrolled.Count,
                EnrolledLearnerIds = enrolled.ToList(),
                WaitlistLearnerIds = waitlist.ToList()
            };
        }
    }

    // Skip-and-retain: scan front-to-back, promote the first eligible learner found,
    // leave everyone skipped in their original waitlist position. Only one seat opened
    // per Drop, so at most one promotion happens per call.
    private void PromoteFirstEligible(Course course, HashSet<string> enrolled, List<string> waitlist)
    {
        for (var i = 0; i < waitlist.Count; i++)
        {
            var learner = _learners[waitlist[i]];
            if (IsEligible(course, learner))
            {
                enrolled.Add(waitlist[i]);
                waitlist.RemoveAt(i);
                return;
            }
        }
    }

    private static bool IsEligible(Course course, Learner learner) =>
        course.PrerequisiteCourseId is null || learner.CompletedCourses.Contains(course.PrerequisiteCourseId);
}
