namespace CourseRegistration.Api.Models;

public enum EnrollmentOutcome
{
    Enrolled,
    Waitlisted,
    Rejected,
    Conflict,
    NotFound,
    Dropped
}

public record EnrollmentResult(EnrollmentOutcome Outcome, string Message);
