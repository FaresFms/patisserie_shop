using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace patisserie_shop.Decisions;

/// <summary>
/// Host-level orchestrator that closes the decision→action loop: executing a
/// decision log entry creates the corrective document as a DRAFT (a human still
/// approves it) and records the link on the decision log row.
/// </summary>
public interface IDecisionActionAppService : IApplicationService
{
    Task<DecisionActionResultDto> ExecuteDecisionAsync(Guid decisionLogId);
}
