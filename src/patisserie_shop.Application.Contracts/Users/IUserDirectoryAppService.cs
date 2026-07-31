using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Identity;

namespace patisserie_shop.Users;

public interface IUserDirectoryAppService : IApplicationService
{
    Task<PagedResultDto<IdentityUserDto>> GetListAsync(GetUsersInput input);

    Task<UserDetailsDto> GetDetailsAsync(Guid id);
}
