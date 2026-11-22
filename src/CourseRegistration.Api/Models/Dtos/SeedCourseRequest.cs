using System.ComponentModel.DataAnnotations;

namespace CourseRegistration.Api.Models.Dtos;

public class SeedCourseRequest
{
    [Required(AllowEmptyStrings = false)]
    public string CourseId { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Capacity must be a positive integer.")]
    public int Capacity { get; set; }

    public string? PrerequisiteCourseId { get; set; }
}
