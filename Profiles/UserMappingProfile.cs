using AutoMapper;
using PollenForYouApi.DTOs;
using PollenForYouApi.Entities;

namespace PollenForYouApi.Profiles;

/// <summary>
/// User account projection. <c>Roles</c> is intentionally not mapped: in .NET 10's
/// ASP.NET Core Identity the <c>IdentityUser</c> no longer exposes a <c>Roles</c>
/// navigation, so role names are resolved via the <c>UserRoles</c>/<c>Roles</c>
/// DbSets by the repository (batched) and the service (single-user flows).
/// </summary>
public class UserMappingProfile : Profile
{
    public UserMappingProfile()
    {
        CreateMap<ApplicationUser, UserResponseDto>()
            .ForMember(d => d.Roles, opt => opt.Ignore());
    }
}
