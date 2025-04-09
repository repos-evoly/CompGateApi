using AutoMapper;
using CardOpsApi.Data.Models;
using CardOpsApi.Core.Dtos;

namespace CardOpsApi
{
    public class MappingConfig : Profile
    {
        public MappingConfig()
        {

            CreateMap<DefinitionCreateDto, Definition>();
            CreateMap<DefinitionUpdateDto, Definition>();
            CreateMap<Definition, DefinitionDto>();

            // Transactions
            CreateMap<Transactions, TransactionDto>()
                .ForMember(dest => dest.ReasonId, opt => opt.MapFrom(src => src.ReasonId))
                .ForMember(dest => dest.ReasonName, opt => opt.MapFrom(src => src.Reason != null ? src.Reason.NameAR : null))
                .ForMember(dest => dest.CurrencyCode, opt => opt.MapFrom(src => src.Currency != null ? src.Currency.Code : string.Empty));
            CreateMap<TransactionCreateDto, Transactions>();
            CreateMap<TransactionUpdateDto, Transactions>();


            CreateMap<Currency, CurrencyDto>();
            CreateMap<CurrencyCreateDto, Currency>();
            CreateMap<CurrencyUpdateDto, Currency>();

            CreateMap<Reason, ReasonDto>();
            CreateMap<ReasonCreateDto, Reason>();
            CreateMap<ReasonUpdateDto, Reason>();


            CreateMap<Settings, SettingsDto>()
              .ForMember(dest => dest.TransactionAmount, opt => opt.MapFrom(src => src.TransactionAmount))
              .ForMember(dest => dest.TransactionTimeTo, opt => opt.MapFrom(src => src.TransactionTimeTo))
              .ForMember(dest => dest.TimeToIdle, opt => opt.MapFrom(src => src.TimeToIdle))
              .ReverseMap();
        }
    }

}
