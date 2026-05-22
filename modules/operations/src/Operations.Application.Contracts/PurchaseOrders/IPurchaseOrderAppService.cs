using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Operations.PurchaseOrders;

public interface IPurchaseOrderAppService : IApplicationService
{
    Task<PurchaseOrderDto> GetAsync(Guid id);
    Task<PagedResultDto<PurchaseOrderDto>> GetListAsync(GetPurchaseOrdersInput input);

    Task<PurchaseOrderDto> CreateAsync(CreatePurchaseOrderDto input);
    Task<PurchaseOrderDto> UpdateHeaderAsync(Guid id, UpdatePurchaseOrderHeaderDto input);
    Task DeleteAsync(Guid id);

    Task<PurchaseOrderItemDto> AddItemAsync(Guid id, AddPurchaseOrderItemDto input);
    Task<PurchaseOrderItemDto> UpdateItemAsync(Guid id, Guid itemId, UpdatePurchaseOrderItemDto input);
    Task RemoveItemAsync(Guid id, Guid itemId);

    Task<PurchaseOrderDto> SubmitAsync(Guid id);
    Task<PurchaseOrderDto> ApproveAsync(Guid id);
    Task<PurchaseOrderDto> CancelAsync(Guid id);
    Task<PurchaseOrderDto> ReceiveAsync(Guid id, ReceiveItemsDto input);
}
