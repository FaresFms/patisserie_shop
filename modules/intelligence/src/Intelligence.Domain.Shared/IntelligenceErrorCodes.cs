namespace Intelligence;

public static class IntelligenceErrorCodes
{
    public const string InvalidRuleType = "Intelligence:Rules:InvalidRuleType";
    public const string ThresholdValueRequired = "Intelligence:Rules:ThresholdValueRequired";
    public const string ThresholdDaysRequired = "Intelligence:Rules:ThresholdDaysRequired";
    public const string InvalidRuleActionMode = "Intelligence:Rules:InvalidActionMode";

    public const string DecisionLogNotPending = "Intelligence:DecisionLogs:NotPending";
    public const string InvalidDecisionActionType = "Intelligence:DecisionLogs:InvalidActionType";
    public const string DecisionBranchRequired = "Intelligence:DecisionLogs:BranchRequired";
    public const string DecisionTransferBranchesRequired = "Intelligence:DecisionLogs:TransferBranchesRequired";
    public const string DecisionProductHasNoDefaultSupplier = "Intelligence:DecisionLogs:NoDefaultSupplier";
    public const string DecisionOutcomeAlreadyRecorded = "Intelligence:DecisionLogs:OutcomeAlreadyRecorded";
    public const string InvalidDecisionOutcome = "Intelligence:DecisionLogs:InvalidOutcome";
}
