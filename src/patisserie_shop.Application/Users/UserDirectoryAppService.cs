using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Identity;

namespace patisserie_shop.Users;

[Authorize(IdentityPermissions.Users.Default)]
public class UserDirectoryAppService : patisserie_shopAppService, IUserDirectoryAppService
{
    private readonly IIdentityUserRepository _userRepository;

    public UserDirectoryAppService(IIdentityUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<PagedResultDto<IdentityUserDto>> GetListAsync(GetUsersInput input)
    {
        var totalCount = await _userRepository.GetCountAsync(
            filter: input.Filter,
            roleId: input.RoleId);

        var users = await _userRepository.GetListAsync(
            sorting: input.Sorting ?? nameof(IdentityUser.UserName),
            maxResultCount: input.MaxResultCount,
            skipCount: input.SkipCount,
            filter: input.Filter,
            roleId: input.RoleId);

        return new PagedResultDto<IdentityUserDto>(
            totalCount,
            users.ConvertAll(user => ObjectMapper.Map<IdentityUser, IdentityUserDto>(user)));
    }

    public async Task<UserDetailsDto> GetDetailsAsync(Guid id)
    {
        var user = await _userRepository.GetAsync(id);
        var details = ObjectMapper.Map<IdentityUser, UserDetailsDto>(user);
        details.RoleNames = await _userRepository.GetRoleNamesAsync(id);
        return details;
    }
}
