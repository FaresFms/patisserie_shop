namespace Operations;

/// <summary>
/// Persistent ABP user-claim types used by the cashier POS. These live in
/// Domain.Shared so both the Operations module and the host application can
/// reference the same constant without a circular dependency.
/// </summary>
public static class CashierClaimTypes
{
    /// <summary>
    /// The Guid (string) of the single branch a cashier is assigned to operate.
    /// Stored as a persistent claim on the IdentityUser (table AbpUserClaims); ABP's
    /// claims-principal factory includes it in the principal at login, so
    /// <c>CurrentUser.FindClaimValue(AssignedBranchId)</c> returns it for a logged-in
    /// cashier. Changing the assignment takes effect on the cashier's NEXT login.
    /// </summary>
    public const string AssignedBranchId = "AssignedBranchId";
}
