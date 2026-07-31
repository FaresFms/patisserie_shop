using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace patisserie_shop.Users;

public class UserDetailsDto : EntityDto<Guid>
{
    public string UserName { get; set; } = string.Empty;

    public string? Name { get; set; }

    public string? Surname { get; set; }

    public string? Email { get; set; }

    public bool EmailConfirmed { get; set; }

    public string? PhoneNumber { get; set; }

    public bool PhoneNumberConfirmed { get; set; }

    public bool IsActive { get; set; }

    public bool LockoutEnabled { get; set; }

    public DateTimeOffset? LockoutEnd { get; set; }

    public DateTimeOffset? LastSignInTime { get; set; }

    public DateTimeOffset? LastPasswordChangeTime { get; set; }

    public DateTime CreationTime { get; set; }

    public DateTime? LastModificationTime { get; set; }

    public List<string> RoleNames { get; set; } = [];
}
