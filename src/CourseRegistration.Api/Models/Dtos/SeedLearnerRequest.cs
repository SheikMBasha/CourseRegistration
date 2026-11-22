using System.ComponentModel.DataAnnotations;

namespace CourseRegistration.Api.Models.Dtos;

public class SeedLearnerRequest
{
    [Required(AllowEmptyStrings = false)]
    public string LearnerId { get; set; } = string.Empty;

    public List<string> CompletedCourses { get; set; } = new();
}
