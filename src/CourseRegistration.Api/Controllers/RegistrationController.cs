using CourseRegistration.Api.Models;
using CourseRegistration.Api.Models.Dtos;
using CourseRegistration.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace CourseRegistration.Api.Controllers;

// Thin HTTP layer only: translates requests to IRegistrationService calls and maps
// EnrollmentOutcome -> HTTP status codes. No business logic lives here (see
// course-registration-plan.md, Error/Status Code Scheme and Acceptance Criteria
// Traceability sections for what each code means and why).
[ApiController]
public class RegistrationController : ControllerBase
{
    private readonly IRegistrationService _service;

    public RegistrationController(IRegistrationService service)
    {
        _service = service;
    }

    [HttpPost("/courses")]
    public IActionResult SeedCourse([FromBody] SeedCourseRequest request)
    {
        _service.AddCourse(new Course
        {
            CourseId = request.CourseId,
            Capacity = request.Capacity,
            PrerequisiteCourseId = request.PrerequisiteCourseId
        });
        return StatusCode(StatusCodes.Status201Created);
    }

    [HttpPost("/learners")]
    public IActionResult SeedLearner([FromBody] SeedLearnerRequest request)
    {
        _service.AddLearner(new Learner
        {
            LearnerId = request.LearnerId,
            CompletedCourses = new HashSet<string>(request.CompletedCourses)
        });
        return StatusCode(StatusCodes.Status201Created);
    }

    [HttpPatch("/learners/{learnerId}")]
    public IActionResult UpdateLearner(string learnerId, [FromBody] UpdateLearnerRequest request)
    {
        var updated = _service.UpdateLearnerCompletedCourses(learnerId, request.CompletedCourses);
        if (!updated)
            return NotFound(new { error = $"Learner '{learnerId}' does not exist." });

        return Ok();
    }

    [HttpPost("/enrollments")]
    public IActionResult Enroll([FromBody] EnrollRequest request)
    {
        var result = _service.Enroll(request.CourseId, request.LearnerId);
        return MapResult(result);
    }

    [HttpDelete("/enrollments/{courseId}/{learnerId}")]
    public IActionResult Drop(string courseId, string learnerId)
    {
        var result = _service.Drop(courseId, learnerId);
        return MapResult(result);
    }

    [HttpGet("/courses/{courseId}/status")]
    public IActionResult GetStatus(string courseId)
    {
        var status = _service.GetStatus(courseId);
        if (status is null)
            return NotFound(new { error = $"Course '{courseId}' does not exist." });

        return Ok(status);
    }

    private IActionResult MapResult(EnrollmentResult result) => result.Outcome switch
    {
        EnrollmentOutcome.Enrolled => StatusCode(StatusCodes.Status201Created, result),
        EnrollmentOutcome.Waitlisted => StatusCode(StatusCodes.Status201Created, result),
        EnrollmentOutcome.Dropped => Ok(result),
        EnrollmentOutcome.Conflict => Conflict(result),
        EnrollmentOutcome.Rejected => StatusCode(StatusCodes.Status422UnprocessableEntity, result),
        EnrollmentOutcome.NotFound => NotFound(result),
        _ => StatusCode(StatusCodes.Status500InternalServerError, result)
    };
}
