namespace FastLabel.Backend.Extensions;

public static class ValidationExtensions
{
    public static bool TryValidateModel<T>(T model, out IResult? errorResult) where T : class
    {
        var validationResults = new List<ValidationResult>();
        var context = new ValidationContext(model);

        if (!Validator.TryValidateObject(model, context, validationResults, true))
        {
            var errorMessage = validationResults.FirstOrDefault()?.ErrorMessage ?? "Validation error";
            errorResult = Results.BadRequest(new ErrorResponseDto { Message = errorMessage });
            return false;
        }

        errorResult = null;
        return true;
    }
}