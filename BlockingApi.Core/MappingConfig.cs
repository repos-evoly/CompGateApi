using AutoMapper;
using BlockingApi.Data.Models;
using BlockingApi.Core.Dtos;

namespace BlockingApi
{
    public class MappingConfig : Profile
    {
        public MappingConfig()
        {

            // 🔹 Area Mappings
            CreateMap<Area, AreaDto>()
                .ForMember(dest => dest.Branches, opt => opt.MapFrom(src => src.Branches)) // Include branches inside area
                .ReverseMap();

            CreateMap<EditAreaDto, Area>().ReverseMap();

            // 🔹 Branch Mappings
            CreateMap<Branch, BranchDto>()
                .ForMember(dest => dest.AreaId, opt => opt.MapFrom(src => src.Area.Id)) // Include Area Name
                .ReverseMap();

            CreateMap<EditBranchDto, Branch>().ReverseMap();

            // 🔹 Reason Mappings
            CreateMap<Reason, ReasonDto>().ReverseMap();
            CreateMap<EditReasonDto, Reason>().ReverseMap();

            // 🔹 Source Mappings
            CreateMap<Source, SourceDto>().ReverseMap();
            CreateMap<EditSourceDto, Source>().ReverseMap();

            CreateMap<Settings, SettingsDto>()
              .ForMember(dest => dest.TransactionAmount, opt => opt.MapFrom(src => src.TransactionAmount))
              .ForMember(dest => dest.TransactionTimeTo, opt => opt.MapFrom(src => src.TransactionTimeTo))
              .ForMember(dest => dest.TimeToIdle, opt => opt.MapFrom(src => src.TimeToIdle))
              .ReverseMap();
        }
    }

}
