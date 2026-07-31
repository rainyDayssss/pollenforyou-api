using AutoMapper;
using PollenForYouApi.DTOs;
using PollenForYouApi.Entities;

namespace PollenForYouApi.Profiles;

/// <summary>
/// Order projection for the queue and detail responses. Claimer emails come from
/// the optional <c>ClaimedBy</c> navigation (null when unclaimed or the claiming
/// admin is soft-deleted); audit FKs like <c>VerifiedByAdminId</c> are projected
/// as raw ints per AGENT.md (the principal may be filtered out).
/// </summary>
public class OrderMappingProfile : Profile
{
    public OrderMappingProfile()
    {
        CreateMap<Order, OrderQueueDto>()
            .ForMember(d => d.ClaimedByEmail, opt => opt.MapFrom(s =>
                s.ClaimedBy != null ? s.ClaimedBy.Email : null));

        CreateMap<Order, OrderDetailDto>()
            .ForMember(d => d.ClaimedByEmail, opt => opt.MapFrom(s =>
                s.ClaimedBy != null ? s.ClaimedBy.Email : null));

        CreateMap<OrderItem, OrderItemDto>();
        CreateMap<Payment, PaymentDto>();
    }
}
