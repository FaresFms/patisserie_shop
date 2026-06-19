using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace patisserie_shop.Cashier;

/// <summary>
/// Admin/manager service for tying each cashier to ONE branch. The assignment is stored
/// as a persistent <c>AssignedBranchId</c> claim on the cashier's IdentityUser — no new
/// table or migration. Lives in the host (which references ABP Identity) because the
/// Operations module does not. Gated by <c>Operations.Cashier.ViewAllShifts</c> — the
/// same cashier-oversight permission managers and admins already hold.
///
/// IMPORTANT: a claim change takes effect on the cashier's NEXT login, because the
/// principal (and its claims) is built at login time.
/// </summary>
public interface ICashierAssignmentAppService : IApplicationService
{
    /// <summary>
    /// Cashiers the caller may manage with their current branch assignment. An admin
    /// (Sales.ManageAll) sees every cashier; a manager sees only cashiers assigned to a
    /// branch they manage.
    /// </summary>
    Task<List<CashierAssignmentDto>> GetCashiersAsync();

    /// <summary>
    /// Active branches (Id + Name) for the assignment / create dropdowns, scoped to the
    /// branches the caller may assign (all for an admin, only the manager's branches
    /// otherwise).
    /// </summary>
    Task<List<CashierBranchOptionDto>> GetBranchOptionsAsync();

    /// <summary>
    /// Assigns the given (active) branch to the given cashier, replacing any previous
    /// assignment. Validates the branch exists and is active, that it is one the caller
    /// may assign, and that the user is in the Cashier role. Takes effect on the
    /// cashier's next login.
    /// </summary>
    Task AssignBranchAsync(AssignCashierBranchInput input);

    /// <summary>
    /// Creates a new cashier user (full identity details), places it in the Cashier role
    /// and assigns it to the given branch via a persistent <c>AssignedBranchId</c> claim.
    /// The target branch must be one the caller may assign (a manager can only create
    /// cashiers for branches they manage). Returns the new user's Id. Identity failures
    /// (duplicate username/email, weak password) surface as localized validation errors.
    /// </summary>
    Task<Guid> CreateCashierAsync(CreateCashierInput input);
}
