using AutoMapper;
using CompGateApi.Data.Models;
using CompGateApi.Core.Dtos;

namespace CompGateApi
{
    public class MappingConfig : Profile
    {
        public MappingConfig()
        {
            CreateMap<TransferRequest, TransferRequestDto>()
                       .ForMember(dest => dest.CategoryName,
                                  opt => opt.MapFrom(src => src.TransactionCategory.Name))
                       .ForMember(dest => dest.CurrencyCode,
                                  opt => opt.MapFrom(src => src.Currency.Code))
                       .ForMember(dest => dest.PackageName,
                                  opt => opt.MapFrom(src => src.ServicePackage.Name))
                       .ForMember(dest => dest.CommissionAmount,
                                  opt => opt.MapFrom(src => src.CommissionAmount))
                       .ForMember(dest => dest.CommissionOnRecipient,
                                  opt => opt.MapFrom(src => src.CommissionOnRecipient))
                        .ForMember(dest => dest.EconomicSectorName, opt => opt.MapFrom(src => src.EconomicSector.Name));

            // everything else (Id, UserId, FromAccount, ToAccount, Amount, Status, Description, RequestedAt, ServicePackageId)
            // will be auto-mapped by convention
            ;

            // ── CreateDto → Domain ────────────────────────────────────────────────
            CreateMap<TransferRequestCreateDto, TransferRequest>()
                .ForMember(dest => dest.TransactionCategoryId,
                           opt => opt.MapFrom(src => src.TransactionCategoryId))
                .ForMember(dest => dest.FromAccount,
                           opt => opt.MapFrom(src => src.FromAccount))
                .ForMember(dest => dest.ToAccount,
                           opt => opt.MapFrom(src => src.ToAccount))
                .ForMember(dest => dest.Amount,
                           opt => opt.MapFrom(src => src.Amount))
                .ForMember(dest => dest.CurrencyId,
                           opt => opt.MapFrom(src => src.CurrencyId))
                .ForMember(dest => dest.Description,
                           opt => opt.MapFrom(src => src.Description))
                    .ForAllMembers(opt => opt.Ignore());
            CreateMap<Currency, CurrencyDto>();
            CreateMap<CurrencyCreateDto, Currency>();
            CreateMap<CurrencyUpdateDto, Currency>();

            CreateMap<Reason, ReasonDto>();
            CreateMap<ReasonCreateDto, Reason>();
            CreateMap<ReasonUpdateDto, Reason>();

            //Economic Sectors
            CreateMap<EconomicSector, EconomicSectorDto>();
            CreateMap<EconomicSectorCreateDto, EconomicSector>();
            CreateMap<EconomicSectorUpdateDto, EconomicSector>();

            //attachments
            CreateMap<Attachment, AttachmentDto>()
                .ForMember(dest => dest.AttUrl, opt => opt.MapFrom(src => src.AttUrl))
                .ForMember(dest => dest.AttFileName, opt => opt.MapFrom(src => src.AttFileName))
                .ForMember(dest => dest.AttMime, opt => opt.MapFrom(src => src.AttMime))
                .ForMember(dest => dest.AttSize, opt => opt.MapFrom(src => src.AttSize))
                .ForMember(dest => dest.CreatedBy, opt => opt.MapFrom(src => src.CreatedBy))
                .ReverseMap();


            CreateMap<Settings, SettingsDto>()
              .ForMember(dest => dest.CommissionAccount, opt => opt.MapFrom(src => src.CommissionAccount))
              .ForMember(dest => dest.CommissionAccountUSD, opt => opt.MapFrom(src => src.CommissionAccountUSD))

              .ReverseMap();
        }
    }

}
