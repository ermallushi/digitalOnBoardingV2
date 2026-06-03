using System.ComponentModel.DataAnnotations;

namespace DigitalOnBoardingV2.Api;

internal static class Validation
{
    internal static IResult? Validate<T>(T request) where T : class
    {
        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();
        if (Validator.TryValidateObject(request, context, results, validateAllProperties: true))
            return null;

        return Results.ValidationProblem(results
            .GroupBy(x => x.MemberNames.FirstOrDefault() ?? "request")
            .ToDictionary(x => x.Key, x => x.Select(r => r.ErrorMessage ?? "Invalid").ToArray()));
    }
}
