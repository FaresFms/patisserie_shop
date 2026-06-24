using System;
using System.Collections.Generic;

namespace patisserie_shop.Planning;

/// <summary>
/// A forward-looking month of planning events: predicted stockouts, batches
/// expiring and expected purchase-order deliveries, all composed deterministically
/// (plain arithmetic + the weekday-indexed <c>ForecastWalker</c>) in the host app.
/// </summary>
public class ReorderCalendarDto
{
    /// <summary>First day (UTC, date-only) of the rendered month.</summary>
    public DateTime MonthStart { get; set; }

    /// <summary>Last day (UTC, date-only) of the rendered month.</summary>
    public DateTime MonthEnd { get; set; }

    /// <summary>First visible cell of the grid — the Sunday on/before <see cref="MonthStart"/>.</summary>
    public DateTime GridStart { get; set; }

    /// <summary>Last visible cell of the grid — the Saturday on/after <see cref="MonthEnd"/>.</summary>
    public DateTime GridEnd { get; set; }

    /// <summary>"Today" as the service saw it (UTC date), so the page can highlight it consistently.</summary>
    public DateTime TodayUtc { get; set; }

    /// <summary>All events falling inside the visible grid window, chronologically ordered.</summary>
    public List<CalendarEventDto> Events { get; set; } = new();

    // ── Headline counts for the legend / summary row ──
    public int StockoutCount { get; set; }
    public int ExpiryCount { get; set; }
    public int DeliveryCount { get; set; }

    /// <summary>True when the event list was capped server-side (see service cap).</summary>
    public bool Truncated { get; set; }
}

/// <summary>One dated event on the reorder calendar.</summary>
public class CalendarEventDto
{
    /// <summary>The calendar day (UTC, date-only) the event lands on.</summary>
    public DateTime Date { get; set; }

    /// <summary>One of <see cref="CalendarEventTypes"/>: Stockout | Expiry | Delivery.</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>Short headline — typically the product or PO number.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Optional explanatory line (branch, quantity, reasoning).</summary>
    public string? Detail { get; set; }

    public Guid? ProductId { get; set; }
    public Guid BranchId { get; set; }
    public string? BranchName { get; set; }

    /// <summary>Theme accent token suggestion: "danger" | "warn" | "success".</summary>
    public string? Accent { get; set; }
}

/// <summary>String constants for <see cref="CalendarEventDto.EventType"/> — keeps the page and service in sync.</summary>
public static class CalendarEventTypes
{
    public const string Stockout = "Stockout";
    public const string Expiry = "Expiry";
    public const string Delivery = "Delivery";
}
