using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Intelligence.Decisions;

public interface IDecisionLogAppService : IApplicationService
{
    Task<DecisionLogDto> GetAsync(Guid id);

    Task<PagedResultDto<DecisionLogDto>> GetListAsync(GetDecisionLogsInput input);

    Task<DecisionLogSummaryDto> GetSummaryAsync();

    Task<DecisionLogDto> AcknowledgeAsync(Guid id);

    Task<DecisionLogDto> DismissAsync(Guid id);

    Task<DecisionLogDto> ExecuteAsync(Guid id, ExecuteDecisionLogInput? input = null);
}
