using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Production.Formulas;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Production.BranchRequests;

public interface IBranchProductionRequestAppService : IApplicationService
{
    Task<BranchProductionRequestDto> GetAsync(Guid id);
    Task<PagedResultDto<BranchProductionRequestListItemDto>> GetListAsync(GetBranchProductionRequestsInput input);
    Task<BranchProductionRequestDto> GetMyAsync(Guid id);
    Task<PagedResultDto<BranchProductionRequestListItemDto>> GetMyListAsync(GetBranchProductionRequestsInput input);
    Task<BranchProductionRequestDto> GetForReviewAsync(Guid id);
    Task<PagedResultDto<BranchProductionRequestListItemDto>> GetReviewListAsync(GetBranchProductionRequestsInput input);
    Task<BranchProductionRequestDto> CreateAsync(CreateBranchProductionRequestDto input);
    Task<BranchProductionRequestDto> UpdateAsync(Guid id, UpdateBranchProductionRequestDto input);
    Task<BranchProductionRequestDto> SubmitAsync(Guid id);
    Task<BranchProductionRequestDto> CancelAsync(Guid id);
    Task<BranchProductionRequestDto> ApproveAsync(Guid id, ApproveBranchProductionRequestDto input);
    Task<BranchProductionRequestDto> RejectAsync(Guid id, RejectBranchProductionRequestDto input);
    Task<List<RequestableBranchLookupDto>> GetRequestableBranchesLookupAsync();
    Task<List<ProductLookupDto>> GetRequestableProductsLookupAsync(string? filter = null);
}
