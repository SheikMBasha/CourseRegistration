using System.ComponentModel.DataAnnotations;

namespace CourseRegistration.Api.Models.Dtos;

public class UpdateLearnerRequest
{
    [Required]
    public List<string> CompletedCourses { get; set; } = new();
}
