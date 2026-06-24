namespace patisserie_shop.Suppliers;

public class GetSupplierScorecardsInput
{
    /// <summary>
    /// Trailing window length in days for the delivery-history analysis. Valid values
    /// are 30, 90 and 180; anything else is clamped server-side to the nearest valid
    /// value. Defaults to 90.
    /// </summary>
    public int Days { get; set; } = 90;
}
