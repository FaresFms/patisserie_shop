using System;
using Volo.Abp;

namespace Production.Orders;

public static class ProductionExpiryPolicy
{
    public static DateTime Resolve(
        DateTime? requestedExpiryDate,
        int? shelfLifeDays,
        DateTime productionDate,
        Guid productId)
    {
        var productionDay = productionDate.Date;
        var maximumExpiryDate = shelfLifeDays.HasValue
            ? productionDay.AddDays(shelfLifeDays.Value)
            : (DateTime?)null;

        if (requestedExpiryDate.HasValue)
        {
            var requestedDate = requestedExpiryDate.Value.Date;
            if (maximumExpiryDate.HasValue && requestedDate > maximumExpiryDate.Value)
            {
                throw new BusinessException(ProductionErrorCodes.ProductionExpiryDateExceedsShelfLife)
                    .WithData("ProductId", productId)
                    .WithData("MaximumExpiryDate", maximumExpiryDate.Value.ToString("yyyy-MM-dd"));
            }

            return requestedDate;
        }

        if (maximumExpiryDate.HasValue)
        {
            return maximumExpiryDate.Value;
        }

        throw new BusinessException(ProductionErrorCodes.ProductionExpiryDateRequired)
            .WithData("ProductId", productId);
    }
}
