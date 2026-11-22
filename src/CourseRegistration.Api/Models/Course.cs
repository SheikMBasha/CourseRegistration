namespace CourseRegistration.Api.Models;

public class Course
{
    public required string CourseId { get; set; }
    public required int Capacity { get; set; }
    public string? PrerequisiteCourseId { get; set; }
}
