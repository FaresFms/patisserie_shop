using System;
using System.Threading.Tasks;
using Production.BranchRequests;
using Production.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace Production.Dispatch;

public class ProductionStockDispatchManager : DomainService
{
    private readonly IBranchProductionRequestRepository _requestRepository;

    public ProductionStockDispatchManager(IBranchProductionRequestRepository requestRepository)
    {
        _requestRepository = requestRepository;
    }

    public async Task<ProductionStockDispatchReservation> ReserveAsync(
        AppBranchProductionRequest request,
        Guid requestItemId,
        Guid kitchenBranchId,
        int quantity,
        DateTime usableOnDate)
    {
        Check.NotNull(request, nameof(request));
        if (quantity <= 0)
        {
            throw new BusinessException(ProductionErrorCodes.InvalidOrderQuantity)
                .WithData("Quantity", quantity);
        }

        var target = await _requestRepository.FindStockDispatchTargetAsync(
            kitchenBranchId,
            request.Id,
            requestItemId,
            usableOnDate);
        if (target == null)
        {
            throw new BusinessException(ProductionErrorCodes.StockDispatchTargetNotAvailable)
                .WithData("RequestId", request.Id)
                .WithData("RequestItemId", requestItemId);
        }
        if (quantity > target.DispatchableQuantity)
        {
            throw new BusinessException(ProductionErrorCodes.StockDispatchQuantityExceedsAvailable)
                .WithData("Quantity", quantity)
                .WithData("Available", target.DispatchableQuantity)
                .WithData("UsableKitchenStock", target.UsableKitchenStock)
                .WithData("CommittedKitchenStock", target.CommittedKitchenStock);
        }

        request.ReservePlannedQuantity(requestItemId, quantity);
        return new ProductionStockDispatchReservation(
            request.Id,
            requestItemId,
            request.RequestNumber,
            request.BranchId,
            target.ProductId,
            quantity);
    }
}

public sealed record ProductionStockDispatchReservation(
    Guid RequestId,
    Guid RequestItemId,
    string RequestNumber,
    Guid DestinationBranchId,
    Guid ProductId,
    int Quantity);
