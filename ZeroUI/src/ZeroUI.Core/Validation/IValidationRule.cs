namespace ZeroUI.Core.Validation
{
    /// <summary>
    /// Contract for evaluating a value and producing a validation result.
    /// </summary>
    public interface IValidationRule<in T>
    {
        ValidationResult Validate(T value);
    }
}
