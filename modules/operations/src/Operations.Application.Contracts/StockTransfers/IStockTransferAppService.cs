using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Operations.StockTransfers;

public interface IStockTransferAppService : IApplicationService
{
    Task<StockTransferDto> GetAsync(Guid id);
    Task<PagedResultDto<StockTransferDto>> GetListAsync(GetStockTransfersInput input);

    Task<StockTransferDto> CreateAsync(CreateStockTransferDto input);

    Task<StockTransferItemDto> AddItemAsync(Guid id, AddStockTransferItemDto input);
    Task RemoveItemAsync(Guid id, Guid itemId);
    Task<StockTransferItemDto> UpdateApprovedQuantityAsync(Guid id, Guid itemId, UpdateApprovedQuantityDto input);

    Task<StockTransferDto> SubmitAsync(Guid id);
    Task<StockTransferDto> AssignSourceAsync(Guid id, AssignStockTransferSourceDto input);
    Task<StockTransferDto> ApproveAsync(Guid id);
    Task<StockTransferDto> RejectAsync(Guid id, RejectStockTransferDto input);
    Task<StockTransferDto> ShipAsync(Guid id);
    Task<StockTransferDto> CompleteAsync(Guid id, CompleteStockTransferDto input);
    Task<StockTransferDto> CancelAsync(Guid id, CancelStockTransferDto input);

    /// <summary>Counts of transfers currently waiting for the calling user, per workflow step.</summary>
    Task<StockTransferActionSummaryDto> GetActionSummaryAsync();

    /// <summary>Products with stock at the transfer's source branch, for the add-item picker.</summary>
    Task<List<StockTransferProductLookupDto>> GetSourceProductsAsync(Guid id);
}
