namespace ZeroAgent.Dialog.Learning
{
    /// <summary>
    /// Domain Invariant and Safety Boundary Validator.
    /// Acts as Gate 1 against data poisoning and hallucinated knowledge ingestion.
    /// </summary>
    public interface IKnowledgeValidator
    {
        /// <summary>
        /// Validates whether the target entity code actually exists in the ground-truth master catalog (ERP/MES/WMS).
        /// Rejects learning aliases for hallucinated or fabricated entities.
        /// </summary>
        bool ValidateEntityExists(string entityCategory, string entityCode);

        /// <summary>
        /// Validates that a proposed heuristic rule or property value complies with domain safety bounds
        /// (e.g. price > 0, discount <= max allowable threshold, valid operational warehouse).
        /// </summary>
        bool ValidateSafetyBounds(string category, string ruleKey, string proposedValue, out string? violationReason);

        /// <summary>
        /// Determines whether the operator role has authority to commit verified knowledge directly
        /// without going through multi-observation consensus staging.
        /// </summary>
        bool CanCommitDirectly(string operatorRole);
    }
}
