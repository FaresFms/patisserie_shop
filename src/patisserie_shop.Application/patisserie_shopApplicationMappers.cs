using patisserie_shop.Users;
using Riok.Mapperly.Abstractions;
using Volo.Abp.Identity;
using Volo.Abp.Mapperly;

namespace patisserie_shop;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class UserDetailsMapper : MapperBase<IdentityUser, UserDetailsDto>
{
    [MapperIgnoreTarget(nameof(UserDetailsDto.RoleNames))]
    public override partial UserDetailsDto Map(IdentityUser source);

    [MapperIgnoreTarget(nameof(UserDetailsDto.RoleNames))]
    public override partial void Map(IdentityUser source, UserDetailsDto destination);
}
