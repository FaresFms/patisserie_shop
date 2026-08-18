namespace Production;

public static class ProductionErrorCodes
{
    // ── Formula aggregate (Wave 2) ──

    /// <summary>Formula output quantity must be a positive integer.</summary>
    public const string InvalidOutputQuantity = "Production:001";

    /// <summary>Formula name is required.</summary>
    public const string FormulaNameRequired = "Production:002";

    /// <summary>Expected waste percent must be at least 0 and less than 100.</summary>
    public const string InvalidWastePercent = "Production:003";

    /// <summary>Labor / overhead cost per batch must be zero or positive.</summary>
    public const string InvalidCost = "Production:004";

    /// <summary>Estimated production minutes must be zero or positive.</summary>
    public const string InvalidProductionMinutes = "Production:005";

    /// <summary>Formula version must be a positive integer.</summary>
    public const string InvalidVersion = "Production:006";

    /// <summary>A formula item quantity must be a positive integer (base units).</summary>
    public const string InvalidItemQuantity = "Production:007";

    /// <summary>A formula item loss percent must be between 0 and 100.</summary>
    public const string InvalidItemLossPercent = "Production:008";

    /// <summary>The same ingredient cannot appear twice in one formula.</summary>
    public const string DuplicateIngredient = "Production:009";

    /// <summary>The referenced formula item does not exist on this formula.</summary>
    public const string FormulaItemNotFound = "Production:010";

    /// <summary>The finished product of a formula must be marked IsProducible.</summary>
    public const string FinishedProductNotProducible = "Production:011";

    /// <summary>An ingredient must be a RawMaterial, Packaging or SemiFinished product (not a FinishedGood).</summary>
    public const string InvalidIngredientProductType = "Production:012";

    /// <summary>Planned output quantity passed to the cost calculator must be positive.</summary>
    public const string InvalidPlannedOutputQuantity = "Production:013";
    public const string FormulaIngredientsRequired = "Production:014";
    public const string ApprovedFormulaIsImmutable = "Production:015";
    public const string FormulaVersionAlreadyExists = "Production:016";
    public const string FormulaMustBeApproved = "Production:017";
    public const string FormulaCannotBeDeletedAfterApproval = "Production:018";

    // ── Branch requests + production planning (Wave 3) ──

    public const string InvalidRequestQuantity = "Production:101";
    public const string DuplicateRequestProduct = "Production:102";
    public const string RequestItemNotFound = "Production:103";
    public const string CannotSubmitEmptyRequest = "Production:104";
    public const string InvalidRequestStatusTransition = "Production:105";
    public const string ApprovalAdjustmentReasonRequired = "Production:106";
    public const string RejectionReasonRequired = "Production:107";
    public const string RequestedProductNotProducible = "Production:108";
    public const string BranchMustBeSalesBranch = "Production:109";
    public const string KitchenBranchRequired = "Production:110";
    public const string KitchenBranchMustBeMainKitchen = "Production:111";
    public const string InvalidPlannedQuantity = "Production:112";
    public const string PlanLineNotFound = "Production:113";
    public const string PlanOverrideReasonRequired = "Production:114";
    public const string CannotConfirmEmptyPlan = "Production:115";
    public const string InvalidPlanStatusTransition = "Production:116";
    public const string BranchRequestAccessDenied = "Production:117";
    public const string CannotCancelPlanWithOrders = "Production:118";
    public const string RequestPlannedQuantityExceeded = "Production:119";
    public const string RequestFulfilledQuantityExceeded = "Production:120";
    public const string KitchenAccessDenied = "Production:121";

    // ── Production orders + cook workflow (Wave 4) ──

    public const string InvalidOrderStatusTransition = "Production:201";
    public const string InvalidOrderQuantity = "Production:202";
    public const string ProductionOrderIngredientNotFound = "Production:203";
    public const string FormulaRequiredForProductionOrder = "Production:204";
    public const string IngredientShortage = "Production:205";
    public const string CannotCompleteWithoutAcceptedQuantity = "Production:206";
    public const string WasteReasonRequired = "Production:207";
    public const string ProductionOrderAlreadyExistsForPlanLine = "Production:208";
    public const string NoPlannedQuantityForProductionOrder = "Production:209";
    public const string ProductionExpiryDateRequired = "Production:210";
    public const string IngredientDefaultSupplierRequired = "Production:211";
    public const string ProductionExpiryDateInPast = "Production:212";
    public const string DispatchQuantityExceedsRemaining = "Production:213";
    public const string DispatchDestinationHasNoAllocation = "Production:214";
    public const string ProductionDispatchAlreadyExists = "Production:215";
    public const string ProductionDispatchNotFound = "Production:216";
    public const string ProductionDispatchAllocationNotFound = "Production:217";
    public const string ProductionExpiryDateExceedsShelfLife = "Production:218";
    public const string StockDispatchTargetNotAvailable = "Production:219";
    public const string StockDispatchQuantityExceedsAvailable = "Production:220";
    public const string StockDispatchReconciliationMismatch = "Production:221";
    public const string IngredientCostRequired = "Production:222";
    public const string ProductionScheduleRequired = "Production:223";
    public const string ProductionOperatorRequired = "Production:224";
    public const string InvalidProductionSchedule = "Production:225";
    public const string QualityReleaseRequiredForDispatch = "Production:226";
    public const string InvalidQualityTransition = "Production:227";
    public const string QualityReasonRequired = "Production:228";
    public const string NoSemiFinishedShortage = "Production:229";
    public const string SubProductionAlreadyExists = "Production:230";
    public const string ProductionCapacityExceeded = "Production:231";

    // ── Waste, dashboard, analytics, decisions (Wave 6) ──

    public const string InvalidWasteQuantity = "Production:301";
    public const string InvalidWasteCost = "Production:302";
    public const string InvalidWasteType = "Production:303";
    public const string InvalidWasteReason = "Production:304";
    public const string ProductionWasteReasonRequired = "Production:305";
    public const string WasteInventoryNotFound = "Production:306";
    public const string WasteQuantityExceedsStock = "Production:307";
}
