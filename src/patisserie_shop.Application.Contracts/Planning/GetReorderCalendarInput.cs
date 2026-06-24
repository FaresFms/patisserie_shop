using System;

namespace patisserie_shop.Planning;

/// <summary>
/// Input for the forward-looking reorder calendar. The client supplies the month
/// to render (any day inside it is fine — only year+month are used) because
/// <c>DateTime.Now</c> is unreliable server-side; the service validates it and
/// computes the visible 6-week grid window from there.
/// </summary>
public class GetReorderCalendarInput
{
    /// <summary>
    /// Any date inside the month to render. When unset the service falls back to
    /// the current UTC month. Only Year and Month are read; the day is ignored.
    /// </summary>
    public DateTime? Month { get; set; }

    /// <summary>
    /// Optional narrowing to a single branch. Always intersected with the caller's
    /// accessible-branch scope — a branch manager asking for a branch they don't
    /// manage simply gets an empty result.
    /// </summary>
    public Guid? BranchId { get; set; }
}
