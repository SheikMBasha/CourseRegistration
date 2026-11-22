namespace CourseRegistration.Api.Models;

public class Learner
{
    public required string LearnerId { get; set; }
    public HashSet<string> CompletedCourses { get; set; } = new();
}
