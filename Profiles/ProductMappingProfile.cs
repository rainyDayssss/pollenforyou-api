using AutoMapper;
using PollenForYouApi.DTOs;
using PollenForYouApi.Entities;

namespace PollenForYouApi.Profiles;

/// <summary>
/// Product projection. The response carries the category display name via the
/// <c>Category</c> navigation so both the public catalog and admin inventory can
/// render it without extra round-trips.
/// </summary>
public class ProductMappingProfile : Profile
{
    public ProductMappingProfile()
    {
        CreateMap<Product, ProductResponseDto>()
            .ForMember(d => d.CategoryName, opt => opt.MapFrom(s => s.Category.Name));

        CreateMap<CreateProductRequestDto, Product>();
    }
}
