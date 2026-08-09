using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Production.Control;

public class ProductionControlProfileDto
{
    public bool RequireScheduleBeforeStart { get; set; } = true;
    public bool RequireOperatorBeforeStart { get; set; } = true;
    public bool RequireQualityReleaseBeforeDispatch { get; set; } = true;

    [Range(0, 100)]
    public decimal ForecastSafetyPercent { get; set; } = 10m;

    [Range(1, 365)]
    public int ForecastAccuracyWindowDays { get; set; } = 30;

    public List<ProductionWorkCenterDto> WorkCenters { get; set; } = new();
    public List<ProductionShiftDto> Shifts { get; set; } = new();
}

public class ProductionWorkCenterDto
{
    [Required, StringLength(64)]
    public string Code { get; set; } = null!;

    [Required, StringLength(128)]
    public string Name { get; set; } = null!;

    public Guid? KitchenBranchId { get; set; }

    [Range(1, 100000)]
    public int CapacityUnitsPerHour { get; set; } = 100;

    [Range(1, 20)]
    public int ParallelSlots { get; set; } = 1;

    public bool IsActive { get; set; } = true;
}

public class ProductionShiftDto
{
    [Required, StringLength(64)]
    public string Code { get; set; } = null!;

    [Required, StringLength(128)]
    public string Name { get; set; } = null!;

    public TimeSpan StartTime { get; set; } = new(6, 0, 0);
    public TimeSpan EndTime { get; set; } = new(14, 0, 0);
    public bool IsActive { get; set; } = true;
}

public class ProductionOperatorLookupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string UserName { get; set; } = null!;
}
