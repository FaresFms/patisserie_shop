using System;
using Volo.Abp.Application.Dtos;

namespace patisserie_shop.Users;

public class GetUsersInput : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }

    public Guid? RoleId { get; set; }
}
