using System;

namespace patisserie_shop.Analytics;

public class GetStocktakeReconciliationInput
{
    public int Days { get; set; } = 30;
    public Guid? BranchId { get; set; }
}
