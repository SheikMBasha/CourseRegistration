using CourseRegistration.Api.Models;
using CourseRegistration.Api.Services;
using CourseRegistration.Api.Store;

namespace CourseRegistration.Tests;

public class RegistrationServiceTests
{
    private readonly IRegistrationService _service;

    public RegistrationServiceTests()
    {
        // Fresh store per test (xUnit creates a new class instance per [Fact]).
        _service = new RegistrationService(new InMemoryCourseStore());
    }

    private void SeedCourse(string courseId, int capacity, string? prerequisiteCourseId = null) =>
        _service.AddCourse(new Course { CourseId = courseId, Capacity = capacity, PrerequisiteCourseId = prerequisiteCourseId });

    private void SeedLearner(string learnerId, params string[] completedCourses) =>
        _service.AddLearner(new Learner { LearnerId = learnerId, CompletedCourses = new HashSet<string>(completedCourses) });

    // Test 1
    [Fact]
    public void Enroll_WithCapacityAvailable_ReturnsEnrolled()
    {
        SeedCourse("CS301", capacity: 2);
        SeedLearner("Alice");

        var result = _service.Enroll("CS301", "Alice");

        Assert.Equal(EnrollmentOutcome.Enrolled, result.Outcome);
        var status = _service.GetStatus("CS301")!;
        Assert.Equal(1, status.EnrolledCount);
        Assert.Contains("Alice", status.EnrolledLearnerIds);
    }

    // Test 2
    [Fact]
    public void Enroll_WhenFullAndEligible_ReturnsWaitlisted()
    {
        SeedCourse("CS301", capacity: 1);
        SeedLearner("Alice");
        SeedLearner("Bob");
        _service.Enroll("CS301", "Alice"); // fills the only seat

        var result = _service.Enroll("CS301", "Bob");

        Assert.Equal(EnrollmentOutcome.Waitlisted, result.Outcome);
        var status = _service.GetStatus("CS301")!;
        Assert.Contains("Bob", status.WaitlistLearnerIds);
    }

    // Test 3
    [Fact]
    public void Enroll_DuplicateWhenAlreadyEnrolled_ReturnsConflict()
    {
        SeedCourse("CS301", capacity: 2);
        SeedLearner("Alice");
        _service.Enroll("CS301", "Alice");

        var result = _service.Enroll("CS301", "Alice");

        Assert.Equal(EnrollmentOutcome.Conflict, result.Outcome);
        var status = _service.GetStatus("CS301")!;
        Assert.Equal(1, status.EnrolledCount); // no duplicate record
    }

    // Test 4
    [Fact]
    public void Enroll_DuplicateWhenAlreadyWaitlisted_ReturnsConflict()
    {
        SeedCourse("CS301", capacity: 1);
        SeedLearner("Alice");
        SeedLearner("Bob");
        _service.Enroll("CS301", "Alice");
        _service.Enroll("CS301", "Bob"); // waitlisted

        var result = _service.Enroll("CS301", "Bob");

        Assert.Equal(EnrollmentOutcome.Conflict, result.Outcome);
        var status = _service.GetStatus("CS301")!;
        Assert.Single(status.WaitlistLearnerIds); // no duplicate record
    }

    // Test 5
    [Fact]
    public void Drop_EnrolledLearner_PromotesNextEligibleWaitlisted()
    {
        SeedCourse("CS301", capacity: 1);
        SeedLearner("Alice");
        SeedLearner("Bob");
        _service.Enroll("CS301", "Alice");
        _service.Enroll("CS301", "Bob"); // waitlisted

        var dropResult = _service.Drop("CS301", "Alice");

        Assert.Equal(EnrollmentOutcome.Dropped, dropResult.Outcome);
        var status = _service.GetStatus("CS301")!;
        Assert.Contains("Bob", status.EnrolledLearnerIds);
        Assert.DoesNotContain("Alice", status.EnrolledLearnerIds);
        Assert.Empty(status.WaitlistLearnerIds);
    }

    // Test 6
    [Fact]
    public void Drop_SkipsIneligibleWaitlisted_PromotesNextEligible_FirstStaysOnWaitlist()
    {
        SeedCourse("CS301", capacity: 2, prerequisiteCourseId: "CS101");
        SeedLearner("Alice", "CS101");
        SeedLearner("Bob", "CS101");
        SeedLearner("Carol"); // no CS101 -> ineligible
        SeedLearner("Dave", "CS101");

        _service.Enroll("CS301", "Alice"); // enrolled
        _service.Enroll("CS301", "Bob");   // enrolled, now full (capacity 2)
        _service.Enroll("CS301", "Carol"); // waitlisted (ineligible, gate-at-the-seat)
        _service.Enroll("CS301", "Dave");  // waitlisted

        var dropResult = _service.Drop("CS301", "Alice");

        Assert.Equal(EnrollmentOutcome.Dropped, dropResult.Outcome);
        var status = _service.GetStatus("CS301")!;
        Assert.Contains("Dave", status.EnrolledLearnerIds);  // Dave promoted (skipped Carol)
        Assert.DoesNotContain("Carol", status.EnrolledLearnerIds);
        Assert.Contains("Carol", status.WaitlistLearnerIds); // Carol retained
        Assert.Equal("Carol", status.WaitlistLearnerIds[0]); // still first in line
    }

    // Test 7
    [Fact]
    public void Enroll_PrerequisiteNotMet_CapacityAvailable_ReturnsRejected()
    {
        SeedCourse("CS302", capacity: 1, prerequisiteCourseId: "MATH101");
        SeedLearner("Erin");

        var result = _service.Enroll("CS302", "Erin");

        Assert.Equal(EnrollmentOutcome.Rejected, result.Outcome);
        var status = _service.GetStatus("CS302")!;
        Assert.Empty(status.EnrolledLearnerIds);
        Assert.Empty(status.WaitlistLearnerIds);
    }

    // Test 8
    [Fact]
    public void Enroll_PrerequisiteNotMet_CourseFull_ReturnsWaitlisted()
    {
        SeedCourse("CS301", capacity: 1, prerequisiteCourseId: "CS101");
        SeedLearner("Alice", "CS101");
        SeedLearner("Carol"); // ineligible
        _service.Enroll("CS301", "Alice"); // fills seat

        var result = _service.Enroll("CS301", "Carol");

        Assert.Equal(EnrollmentOutcome.Waitlisted, result.Outcome);
        var status = _service.GetStatus("CS301")!;
        Assert.Contains("Carol", status.WaitlistLearnerIds);
    }

    // Test 9
    [Fact]
    public void Drop_PromotesWaitlistedLearner_WhoBecameEligibleAfterPatch()
    {
        SeedCourse("CS301", capacity: 1, prerequisiteCourseId: "CS101");
        SeedLearner("Alice", "CS101");
        SeedLearner("Carol"); // ineligible initially
        _service.Enroll("CS301", "Alice");
        _service.Enroll("CS301", "Carol"); // waitlisted while ineligible

        var updated = _service.UpdateLearnerCompletedCourses("Carol", new[] { "CS101" });
        Assert.True(updated);

        var dropResult = _service.Drop("CS301", "Alice");

        Assert.Equal(EnrollmentOutcome.Dropped, dropResult.Outcome);
        var status = _service.GetStatus("CS301")!;
        Assert.Contains("Carol", status.EnrolledLearnerIds);
        Assert.Empty(status.WaitlistLearnerIds);
    }

    // Test 10
    [Fact]
    public void Drop_NeverEnrolledOrWaitlisted_ReturnsNotFound()
    {
        SeedCourse("CS301", capacity: 1);
        SeedLearner("Frank");

        var result = _service.Drop("CS301", "Frank");

        Assert.Equal(EnrollmentOutcome.NotFound, result.Outcome);
    }

    // Test 11
    [Fact]
    public void Drop_Repeated_ReturnsNotFound()
    {
        SeedCourse("CS301", capacity: 1);
        SeedLearner("Alice");
        _service.Enroll("CS301", "Alice");
        _service.Drop("CS301", "Alice");

        var secondDrop = _service.Drop("CS301", "Alice");

        Assert.Equal(EnrollmentOutcome.NotFound, secondDrop.Outcome);
    }

    // Test 12
    [Fact]
    public async Task Enroll_ConcurrentRequestsForLastSeat_ExactlyOneEnrolledOneWaitlisted()
    {
        SeedCourse("CS301", capacity: 1);
        SeedLearner("Alice");
        SeedLearner("Bob");

        // Both threads block on the same signal and are released together, to actually
        // contend for the lock rather than just happening to run one after another.
        using var startSignal = new ManualResetEventSlim(false);
        EnrollmentResult? resultA = null;
        EnrollmentResult? resultB = null;

        var taskA = Task.Run(() =>
        {
            startSignal.Wait();
            resultA = _service.Enroll("CS301", "Alice");
        });
        var taskB = Task.Run(() =>
        {
            startSignal.Wait();
            resultB = _service.Enroll("CS301", "Bob");
        });

        startSignal.Set();
        await Task.WhenAll(taskA, taskB);

        var outcomes = new[] { resultA!.Outcome, resultB!.Outcome };
        Assert.Single(outcomes, o => o == EnrollmentOutcome.Enrolled);
        Assert.Single(outcomes, o => o == EnrollmentOutcome.Waitlisted);

        var status = _service.GetStatus("CS301")!;
        Assert.Equal(1, status.EnrolledCount);
        Assert.Single(status.WaitlistLearnerIds);
    }

    // Test 13
    [Fact]
    public void Enroll_NonexistentCourseOrLearner_ReturnsNotFound()
    {
        SeedCourse("CS301", capacity: 1);
        SeedLearner("Alice");

        var missingCourse = _service.Enroll("CS999", "Alice");
        var missingLearner = _service.Enroll("CS301", "Grace");

        Assert.Equal(EnrollmentOutcome.NotFound, missingCourse.Outcome);
        Assert.Equal(EnrollmentOutcome.NotFound, missingLearner.Outcome);
    }

    // Test 14 — Invariant #5, exercised against the trickier state where a seat is open
    // AND the waitlist is non-empty (an earlier drop had no eligible learner to promote,
    // so the skipped learner is still waitlisted while a seat sits open).
    [Fact]
    public void Enroll_CapacityAvailableAndIneligible_RejectsEvenWithNonEmptyWaitlist()
    {
        SeedCourse("CS301", capacity: 2, prerequisiteCourseId: "CS101");
        SeedLearner("Alice", "CS101");
        SeedLearner("Bob", "CS101");
        SeedLearner("Carol");  // ineligible
        SeedLearner("Dave");   // ineligible

        _service.Enroll("CS301", "Alice"); // enrolled (1/2)
        _service.Enroll("CS301", "Bob");   // enrolled (2/2, full)
        _service.Enroll("CS301", "Carol"); // waitlisted (full, ineligible, gate-at-the-seat)

        var dropResult = _service.Drop("CS301", "Bob"); // seat opens, but Carol (only
                                                          // waitlisted learner) is still
                                                          // ineligible -> skipped, seat
                                                          // stays open, Carol stays waitlisted
        Assert.Equal(EnrollmentOutcome.Dropped, dropResult.Outcome);
        var midStatus = _service.GetStatus("CS301")!;
        Assert.Equal(1, midStatus.EnrolledCount); // seat genuinely open (1 < capacity 2)
        Assert.Contains("Carol", midStatus.WaitlistLearnerIds); // and waitlist non-empty

        var result = _service.Enroll("CS301", "Dave"); // new ineligible request, capacity available

        Assert.Equal(EnrollmentOutcome.Rejected, result.Outcome);
        var finalStatus = _service.GetStatus("CS301")!;
        Assert.DoesNotContain("Dave", finalStatus.WaitlistLearnerIds); // never added to waitlist
        Assert.DoesNotContain("Dave", finalStatus.EnrolledLearnerIds);
        Assert.Equal(1, finalStatus.EnrolledCount);
    }
}
