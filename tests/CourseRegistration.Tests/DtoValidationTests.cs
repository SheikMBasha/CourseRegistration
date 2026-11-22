using System.ComponentModel.DataAnnotations;
using CourseRegistration.Api.Models.Dtos;

namespace CourseRegistration.Tests;

// Test 15: a genuinely malformed request body should fail validation (-> 400 once bound
// through the real ASP.NET Core model-binding pipeline via [ApiController]). Verified here
// directly against the DTOs' data annotations rather than through a full HTTP pipeline, to
// avoid adding a new Microsoft.AspNetCore.Mvc.Testing dependency for this one check.
public class DtoValidationTests
{
    private static bool TryValidate(object dto, out List<ValidationResult> results)
    {
        results = new List<ValidationResult>();
        var context = new ValidationContext(dto);
        return Validator.TryValidateObject(dto, context, results, validateAllProperties: true);
    }

    [Fact]
    public void EnrollRequest_MissingCourseId_FailsValidation()
    {
        var request = new EnrollRequest { CourseId = "", LearnerId = "Alice" };

        var isValid = TryValidate(request, out var results);

        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(EnrollRequest.CourseId)));
    }

    [Fact]
    public void EnrollRequest_MissingLearnerId_FailsValidation()
    {
        var request = new EnrollRequest { CourseId = "CS301", LearnerId = "" };

        var isValid = TryValidate(request, out var results);

        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(EnrollRequest.LearnerId)));
    }

    [Fact]
    public void SeedCourseRequest_NonPositiveCapacity_FailsValidation()
    {
        var request = new SeedCourseRequest { CourseId = "CS301", Capacity = 0 };

        var isValid = TryValidate(request, out var results);

        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(SeedCourseRequest.Capacity)));
    }

    [Fact]
    public void SeedCourseRequest_ValidBody_PassesValidation()
    {
        var request = new SeedCourseRequest { CourseId = "CS301", Capacity = 2, PrerequisiteCourseId = "CS101" };

        var isValid = TryValidate(request, out var results);

        Assert.True(isValid);
        Assert.Empty(results);
    }
}
