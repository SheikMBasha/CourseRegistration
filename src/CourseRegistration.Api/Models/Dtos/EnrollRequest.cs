using System.ComponentModel.DataAnnotations;

namespace CourseRegistration.Api.Models.Dtos;

public class EnrollRequest
{
    [Required(AllowEmptyStrings = false)]
    public string CourseId { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string LearnerId { get; set; } = string.Empty;
}
