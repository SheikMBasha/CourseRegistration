namespace CourseRegistration.Api.Models.Dtos;

public class CourseStatusResponse
{
    public string CourseId { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int EnrolledCount { get; set; }
    public List<string> EnrolledLearnerIds { get; set; } = new();
    public List<string> WaitlistLearnerIds { get; set; } = new();
}
