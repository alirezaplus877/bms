using AutoMapper;
using Dto;
using Dto.Pagination;
using Dto.Proxy.Request;
using Dto.Proxy.Request.IPG;
using Dto.Proxy.Request.Naja;
using Dto.Proxy.Request.PecIs;
using Dto.Proxy.Request.Tosan;
using Dto.Proxy.Request.Wallet;
using Dto.Proxy.Response;
using Dto.Proxy.Response.IPG;
using Dto.Proxy.Response.Naja;
using Dto.Proxy.Response.PecIs;
using Dto.Proxy.Response.Wallet;
using Dto.Proxy.Wallet;
using Dto.repository;
using Dto.Request;
using Dto.Response;
using Entities;
using Entities.Transportation;
using Microsoft.Extensions.DependencyInjection;
using PEC.CoreCommon.Security.Encryptor;
using PEC.CoreCommon.ServiceActivator;
using PecBMS.ViewModel;
using PecBMS.ViewModel.Request;
using PecBMS.ViewModel.Request.Naja;
using PecBMS.ViewModel.Request.PecISInquiry;
using PecBMS.ViewModel.Response;
using PecBMS.ViewModel.Response.Naja;
using PecBMS.ViewModel.Response.PecISInquiry;
using PecBMS.ViewModel.Transportation.Request;
using PecBMS.ViewModel.Transportation.Response;
using ProxyService.ProxyModel.Request.Naja;
using System.Collections.Generic;
using Utility;

namespace PecBMS.Helper
{
    public class AutoMapperProfile : Profile
    {
        private readonly IPecBmsSetting _Setting;
        private readonly IEncryptor _encryptor;

        public AutoMapperProfile()
        {

            _Setting = ServiceActivator.GetScope().ServiceProvider.GetRequiredService<IPecBmsSetting>();
            _encryptor = ServiceActivator.GetScope().ServiceProvider.GetRequiredService<IEncryptor>();


            #region [- CheckInfo -]
            CreateMap<CheckInfoRequestDto, CheckInfoRequestViewModel>().ReverseMap();
            CreateMap<CheckInfoRequestViewModel, CheckInfoRequestDto>().ReverseMap();
            #endregion

            #region [- MotorViolationInquiry -]
            CreateMap<MotorViolationInquiryResponseDto, MotorViolationInquiryResponseViewModel>().ReverseMap();
            CreateMap<MotorViolationInquiryResponseViewModel, MotorViolationInquiryResponseDto>().ReverseMap();

            CreateMap<MotorViolationInquiryObjectModel, MotorViolationInquiryRequestDto>()
                    .AfterMap((s, d) =>
                    {
                        if (s.MotorPlateNo.Length == 8)
                        {
                            d.FirstSection = s.MotorPlateNo[..3];
                            d.SecondSection = s.MotorPlateNo.Substring(3, 5);
                        }
                    });
            #endregion

            #region [- CarViolationInquiry -]
            CreateMap<GetViolationImageInquiryRequestViewModel, ViolationImageInquiryRequestDto>()
                .ForMember("NationalCode", opt => opt.Ignore())
                .ForMember("MobileNumber", opt => opt.Ignore())
                .ForMember(dest => dest.SerialNo, opt => opt.MapFrom(src => src.SerialNo))
                .ForMember(dest => dest.TrackingNo, opt => opt.MapFrom(src => long.Parse(src.OrderId)));

            CreateMap<CarViolationInquiryObjectModel, ViolationInquiryRequestDto>()
               .AfterMap((s, d) =>
               {
                   if (s.CarPlateNo.Length == 9)
                   {
                       d.FirstSection = s.CarPlateNo[..2];
                       d.CharacterSection = s.CarPlateNo.Substring(2, 2);
                       d.ThirdSection = s.CarPlateNo.Substring(4, 3);
                       d.FourthSection = s.CarPlateNo.Substring(7, 2);
                   }
               });

            CreateMap<CarViolationInquiryObjectModel, ViolationImageInquiryRequestDto>()
                .ForMember("NationalCode", opt => opt.Ignore())
                .ForMember("MobileNumber", opt => opt.Ignore())
                .ForMember("SerialNo", opt => opt.Ignore())
                .ForMember("TrackingNo", opt => opt.Ignore())
                .AfterMap((s, d) =>
                {
                    if (s.CarPlateNo.Length == 9)
                    {
                        d.FirstSection = s.CarPlateNo[..2];
                        d.CharacterSection = s.CarPlateNo.Substring(2, 2);
                        d.ThirdSection = s.CarPlateNo.Substring(4, 3);
                        d.FourthSection = s.CarPlateNo.Substring(7, 2);
                    }
                });
            #endregion

            #region [- AccumulationViolationsInquiry -]
            CreateMap<AccumulationViolationsInquiryRequestDto, AccumulationViolationsInquiryRequestViewModel>().ReverseMap();
            CreateMap<AccumulationViolationsInquiryRequestViewModel, AccumulationViolationsInquiryRequestDto>().ReverseMap();

            CreateMap<AccumulationViolationsInquiryResponseDto, AccumulationViolationsInquiryResponseViewModel>().ReverseMap();
            CreateMap<AccumulationViolationsInquiryResponseViewModel, AccumulationViolationsInquiryResponseDto>().ReverseMap();

            CreateMap<CarViolationInquiryObjectModel, AccumulationViolationsInquiryRequestViewModel>()
               .AfterMap((s, d) =>
               {
                   if (s.CarPlateNo.Length == 9)
                   {
                       d.FirstSection = s.CarPlateNo[..2];
                       d.CharacterSection = s.CarPlateNo.Substring(2, 2);
                       d.ThirdSection = s.CarPlateNo.Substring(4, 3);
                       d.FourthSection = s.CarPlateNo.Substring(7, 2);
                   }
               });

            CreateMap(typeof(GetInquiryRequestModel), typeof(GetInquiryRequestDto)).ReverseMap();
            CreateMap(typeof(PagedResponse<List<NajaWageDto>>), typeof(PagedResponse<List<NajaWage>>)).ReverseMap();
            CreateMap<PaginationFilterDto, PaginationFilterViewModel>()
                            .AfterMap((s, d) =>
                                {
                                    if (s.PageNumber > 0)
                                    {
                                        d.PageNumber = s.PageNumber;
                                    }
                                    if (s.PageSize > 0)
                                    {
                                        d.PageSize = s.PageSize;
                                    }
                                });
            CreateMap<PaginationFilterViewModel, PaginationFilterDto>()
                                //.ForMember(dest => dest.FromDate, opt => opt.MapFrom(src => src._FromDate))
                                //.ForMember(dest => dest.ToDate, opt => opt.MapFrom(src => src._ToDate))
                                .AfterMap((s, d) =>
                                {
                                    if (s.PageNumber > 0)
                                    {
                                        d.PageNumber = s.PageNumber;
                                    }
                                    if (s.PageSize > 0)
                                    {
                                        d.PageSize = s.PageSize;
                                    }
                                    d.FromDate = !string.IsNullOrEmpty(s.FromDate) ? s._FromDate : null;
                                    d.ToDate = !string.IsNullOrEmpty(s.ToDate) ? s._ToDate : null;
                                });

            #endregion

            #region [- LicenseStatusInquiry -]
            CreateMap<LicenseStatusInquiryRequestDto, LicenseStatusInquiryRequestViewModel>().ReverseMap();
            CreateMap<LicenseStatusInquiryRequestViewModel, LicenseStatusInquiryRequestDto>().ReverseMap();

            CreateMap<LicenseStatusInquiryResponseDto, LicenseStatusInquiryResponseViewModel>().ReverseMap();
            CreateMap<LicenseStatusInquiryResponseViewModel, LicenseStatusInquiryResponseDto>().ReverseMap();
            #endregion

            #region [- ActivePlakInquiry -]
            CreateMap<ActivePlakInquiryRequestDto, ActivePlakInquiryRequestViewModel>().ReverseMap();
            CreateMap<ActivePlakInquiryRequestViewModel, ActivePlakInquiryRequestDto>().ReverseMap();

            CreateMap<ActivePlakInquiryResponseDto, ActivePlakInquiryResponseViewModel>().ReverseMap();
            CreateMap<ActivePlakInquiryResponseViewModel, ActivePlakInquiryResponseDto>().ReverseMap();
            #endregion

            #region [- NoExitInquiry -]
            CreateMap<NoExitInquiryRequestDto, NoExitInquiryRequestViewModel>().ReverseMap();
            CreateMap<NoExitInquiryRequestViewModel, NoExitInquiryRequestDto>().ReverseMap();

            CreateMap<NoExitInquiryResponseDto, NoExitInquiryResponseViewModel>().ReverseMap();
            CreateMap<NoExitInquiryResponseViewModel, NoExitInquiryResponseDto>().ReverseMap();
            #endregion

            #region [- ViolationImageInquiry -]
            CreateMap<ViolationImageInquiryRequestDto, ViolationImageInquiryRequestViewModel>().ReverseMap();
            CreateMap<ViolationImageInquiryRequestViewModel, ViolationImageInquiryRequestDto>().ReverseMap();

            CreateMap<ViolationImageInquiryResponseDto, ViolationImageInquiryResponseViewModel>().ReverseMap();
            CreateMap<ViolationImageInquiryResponseViewModel, ViolationImageInquiryResponseDto>().ReverseMap();
            #endregion

            #region [- Public Violation -]
            CreateMap<ViolationInquiryResponseDto, ViolationInquiryResponseViewModel>().ReverseMap();
            CreateMap<ViolationInquiryResponseViewModel, ViolationInquiryResponseDto>().ReverseMap();

            CreateMap<WarningDTO, WarningDTOViewModel>().ReverseMap();
            CreateMap<WarningDTOViewModel, WarningDTO>().ReverseMap();
            #endregion

            #region [- LicenseNegativePointInquiry -]

            CreateMap<LicenseNegativePointInquiryRequestDto, LicenseNegativePointInquiryRequestViewModel>().ReverseMap();
            CreateMap<LicenseNegativePointInquiryRequestViewModel, LicenseNegativePointInquiryRequestDto>().ReverseMap();

            CreateMap<LicenseNegativePointInquiryResponseDto, LicenseNegativePointInquiryResponseViewModel>().ReverseMap();
            CreateMap<LicenseNegativePointInquiryResponseViewModel, LicenseNegativePointInquiryResponseDto>().ReverseMap();


            CreateMap<LicenseNegativePointInquiryObjectModel, LicenseNegativePointInquiryRequestViewModel>()
                   .AfterMap((s, d) =>
                   {
                       if (!string.IsNullOrEmpty(s.LicenseNo))
                       {
                           d.LicenseNo = s.LicenseNo;
                       }
                   });
            #endregion

            #region [- PassportStatusInquiry -]

            CreateMap<PassportStatusInquiryRequestDto, PassportStatusInquiryRequestViewModel>().ReverseMap();
            CreateMap<PassportStatusInquiryRequestViewModel, PassportStatusInquiryRequestDto>().ReverseMap();

            CreateMap<PassportStatusInquiryResponseDto, PassportStatusInquiryResponseViewModel>().ReverseMap();
            CreateMap<PassportStatusInquiryResponseViewModel, PassportStatusInquiryResponseDto>().ReverseMap();
            #endregion

            #region [- PayWage -]
            CreateMap(typeof(PayWageResponseDto), typeof(PayWageResponseViewModel<>))
                  .ForMember("NajaData", opt => opt.Ignore())
                  .ForMember("NajaType", opt => opt.Ignore())
                  .ForMember("AmountWage", opt => opt.Ignore())
                  .ForMember("TransactionDate", opt => opt.Ignore());

            CreateMap(typeof(PayWageResponseViewModel<>), typeof(PayWageResponseDto));

            CreateMap<PayWageRequestViewModel, PayWageRequestDto>()
               .ForMember(dest => dest.NajaType, opt => opt.MapFrom(src => src.Service.NajaType))
               .ForMember(dest => dest.NationalCode, opt => opt.MapFrom(src => src.Service.NationalCode))
               .ForMember(dest => dest.MobileNumber, opt => opt.MapFrom(src => src.Service.MobileNumber))
               .ForMember(dest => dest.NajaServiceData, opt => opt.MapFrom(src => src.Service.ServiceData.Data));
            #endregion

            #region [- Requestmap -]
            CreateMap<IrancellInquiryRequestDto, IrancellInquiryViewModel>();
            CreateMap<IrancellInquiryViewModel, IrancellInquiryRequestDto>();

            CreateMap<ConfirmationAutoBillRequestViewModel, ConfirmationAutoBillPaymentDto>();
            CreateMap<ConfirmationAutoBillPaymentDto, ConfirmationAutoBillRequestViewModel>();

            CreateMap<SendSmsAutoBillRequestViewModel, SendSmsAutoBillDto>();
            CreateMap<SendSmsAutoBillDto, SendSmsAutoBillRequestViewModel>();
            
            CreateMap<WalletsInfo, GetCustomerWalletRequestDto>()
                        .ForMember(dest => dest.CorporationUserId, opt => opt.MapFrom(src => _Setting.CorporationUserId))
                        .ForMember(dest => dest.NationalCode, opt => opt.MapFrom(src => src.WalletCode))
                        .ForMember(dest => dest.GroupWalletId, opt => opt.MapFrom(src => _Setting.GroupWalletId));



            CreateMap<ChargeHistoryRequestViewModel, ChargeHistoryRequestDo>()
            .ForMember(dest => dest.WalletCode, opt => opt.MapFrom(src => src.WalletCode))
            .ForMember(dest => dest.WalletType, opt => opt.MapFrom(src => src.WalletType));



            CreateMap<AddUserBillRequestViewModel, UserBillDto>()
                .ForMember(dest => dest.BillId, opt => opt.MapFrom(src => src.BillID))
                .ForMember(dest => dest.CustomerId, opt => opt.MapFrom(src => src.CustomerID))
                .ForMember(dest => dest.BillType, opt => opt.MapFrom(src => src.BillType))
                .ForMember(dest => dest.OrganizationId, opt => opt.MapFrom(src => src.OrganizationID))
                .ForMember(dest => dest.ClientId, opt => opt.MapFrom(src => src.ClientId))
                .ForMember(dest => dest.AutoPaymentActiveated, opt => opt.MapFrom(src => src.AutoPaymentActiveated))
                .ForMember(dest => dest.MaxAmountPayment, opt => opt.MapFrom(src => src.MaxAmountPayment))
                .ForMember(dest => dest.ExpireDatePayment, opt => opt.MapFrom(src => src.ExpireDatePayment))
                .ForMember(dest => dest.DailyPayment, opt => opt.MapFrom(src => src.DailyPayment));

            CreateMap<DeleteUsersBillRequestViewModel, UserBillDto>()
               .ForMember(dest => dest.BillId, opt => opt.MapFrom(src => src.BillId))
               .ForMember(dest => dest.BillType, opt => opt.MapFrom(src => src.BillType));
            CreateMap<TollBillsViewModel, TollBillsDto>()
            .ForMember(dest => dest.Amount, opt => opt.MapFrom(src => src.Amount))
            .ForMember(dest => dest.BillId, opt => opt.MapFrom(src => src.BillId))
            .ForMember(dest => dest.TraversDate, opt => opt.MapFrom(src => src.TraversDate));

            CreateMap<TollBillPaymentRequestViewModel, TollBillPaymentRequestDto>()
            .ForMember(dest => dest.MerchantId, opt => opt.MapFrom(src => src.MerchantId))
            .ForMember(dest => dest.MobileNumber, opt => opt.MapFrom(src => src.MobileNumber))
            .ForMember(dest => dest.PlateNumber, opt => opt.MapFrom(src => src.PlateNumber))
            .ForMember(dest => dest.Token, opt => opt.MapFrom(src => src.Token))
            .ForMember(dest => dest.TollBills, opt => opt.MapFrom(src => src.TollBills))
            .ForMember(dest => dest.TotalAmount, opt => opt.MapFrom(src => src.TotalAmount))
            .ForMember(dest => dest.TransactionType, opt => opt.MapFrom(src => src.TransactionType))
            ;

            CreateMap<MciBillInquiryRequestViewModel, MciBillInquiryRequestDto>()
               .ForMember(dest => dest.MobileNumber, opt => opt.MapFrom(src => src.MobileNumber));


            CreateMap<NigcBillInquiryRequestViewModel, NigcBillInquiryRequestDto>()
                   .ForMember(dest => dest.SubscriptionId, opt => opt.MapFrom(src => src.SubscriptionId));
            CreateMap<PaymentDetailRequestViewModel, PaymentDetailRequestDto>()
                 .ForMember(dest => dest.pgwToken, opt => opt.MapFrom(src => src.Token));



            CreateMap<TciInquiryRequestViewModel, TciInquiryRequestDto>()
                 .ForMember(dest => dest.TelNo, opt => opt.MapFrom(src => src.TelNo));

            CreateMap<BillInfoInquiryRequestViewModel, BillInfoRequestDto>()
             .ForMember(dest => dest.BillId, opt => opt.MapFrom(src => src.BillId))
             .ForMember(dest => dest.PayId, opt => opt.MapFrom(src => src.PayID));

            CreateMap<TollBillInquiryRequestViewModel, TollBillInquiryRequestDto>()
                .ForMember(dest => dest.MobileNo, opt => opt.MapFrom(src => src.MobileNo))
                .ForMember(dest => dest.PlateNumber, opt => opt.MapFrom(src => src.PlateNumber))
                .ForMember(dest => dest.TermNo, opt => opt.MapFrom(src => src.TermNo));

            CreateMap<BillTypeViewModel, BillTypeDto>()
                  .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
                  .ForMember(dest => dest.BillTypeTitle, opt => opt.MapFrom(src => src.BillTypeTitle));

            CreateMap<BarghBillInquiryRequestViewModel, BarghBillInquiryRequestDto>()
                  .ForMember(dest => dest.BillId, opt => opt.MapFrom(src => src.BillId));



            CreateMap<BillsViewModel, BillsDto>()
                 .ForMember(dest => dest.Amount, opt => opt.MapFrom(src => src.Amount))
                 .ForMember(dest => dest.BillId, opt => opt.MapFrom(src => src.BillId))
                 .ForMember(dest => dest.OrganizationId, opt => opt.MapFrom(src => src.OrganizationId))
                 .ForMember(dest => dest.PayId, opt => opt.MapFrom(src => src.PayId))
                ;
            CreateMap<BillsDto, BillsViewModel>()
                .ForMember(dest => dest.Amount, opt => opt.MapFrom(src => src.Amount))
                 .ForMember(dest => dest.BillId, opt => opt.MapFrom(src => src.BillId))
                 .ForMember(dest => dest.OrganizationId, opt => opt.MapFrom(src => src.OrganizationId))
                 .ForMember(dest => dest.PayId, opt => opt.MapFrom(src => src.PayId))
                ;


            CreateMap<Bills, BillsValidDto>()
                .ForMember(dest => dest.PayId, opt => opt.MapFrom(src => src.PayId))
                .ForMember(dest => dest.BillId, opt => opt.MapFrom(src => src.BillId))
                ;

            CreateMap<BillValidRequestViewModel, BillValidRequestDto>()
                .ForMember(dest => dest.Bills, opt => opt.MapFrom(src => src.Bills))
                ;

            CreateMap<BillValidRequestViewModel, BatchBillPaymentRequestDto>()
                   .ForMember(dest => dest.Bills, opt => opt.MapFrom(src => src.Bills));
            CreateMap<PayBillRequestViewModel, PayBillRequestDto>()
                 .ForMember(dest => dest.Bills, opt => opt.MapFrom(src => src.Bills))
                 .ForMember(dest => dest.WalletId, opt => opt.MapFrom(src => src.WalletId))
                 .ForMember(dest => dest.TotalAmount, opt => opt.MapFrom(src => src.TotalAmount))
                 .ForMember(dest => dest.ApplicationId, opt => opt.MapFrom(src => src.ApplicationId))
                 .ForMember(dest => dest.LoginAccount, opt => opt.MapFrom(src => _Setting.LoginAccount))
                 .ForMember(dest => dest.CallBackUrl, opt => opt.MapFrom(src => _Setting.CallBackUrlCharge))
                 .ForMember(dest => dest.WalletCode, opt => opt.MapFrom(src => src.WalletCode))
                 .ForMember(dest => dest.WalletType, opt => opt.MapFrom(src => src.WalletType))
                 .ForMember(dest => dest.MobileNumber, opt => opt.MapFrom(src => src.MobileNumber))
                 .ForMember(dest => dest.MerchantId, opt => opt.MapFrom(src => src.MerchantId))
                 .ForMember(dest => dest.TransactionType, opt => opt.MapFrom(src => src.TransactionType))

                 ;

            CreateMap<GetBillPaymentHistoryRequestViewModel, GetBillPaymentHistoryRequestDto>()
                  .ForMember(dest => dest.BillId, opt => opt.MapFrom(src => src.BillId))
                  .ForMember(dest => dest.OrganizationId, opt => opt.MapFrom(src => src.OrganizationId));



            CreateMap<GetChargeIpgTokenRequestViewModel, ChargeWalletTokenRequestDto>()
                .ForMember(dest => dest.Amount, opt => opt.MapFrom(src => src.Amount))
                .ForMember(dest => dest.WalletCode, opt => opt.MapFrom(src => src.WalletCode))
                .ForMember(dest => dest.ApplicationId, opt => opt.MapFrom(src => src.ApplicationId))
                .ForMember(dest => dest.WalletCode, opt => opt.MapFrom(src => src.WalletCode))
                .ForMember(dest => dest.WalletType, opt => opt.MapFrom(src => src.WalletType))
                .ForMember(dest => dest.MerchantId, opt => opt.MapFrom(src => src.MerchantId))

                ;

            CreateMap<ConfirmChargeRequestViewModel, ConfirmChargRequestDto>()
                  .ForMember(dest => dest.Amount, opt => opt.MapFrom(src => src.Amount))
                  .ForMember(dest => dest.MiladiTransDate, opt => opt.MapFrom(src => src.MiladiTransDate))
                  .ForMember(dest => dest.OrderId, opt => opt.MapFrom(src => src.OrderId))
                  .ForMember(dest => dest.PgwStatus, opt => opt.MapFrom(src => src.PgwStatus))
                  .ForMember(dest => dest.PgwToken, opt => opt.MapFrom(src => src.PgwToken))
                  .ForMember(dest => dest.RRN, opt => opt.MapFrom(src => src.RRN))
                  .ForMember(dest => dest.TerminalNumber, opt => opt.MapFrom(src => src.TerminalNumber))
                  .ForMember(dest => dest.TraceNo, opt => opt.MapFrom(src => src.TraceNo))
                  .ForMember(dest => dest.TransDate, opt => opt.MapFrom(src => src.TransDate))

                ;
            CreateMap<UserBillPaymentHistoryRequestViewModel, UserBillPaymentHistoryRequestDto>()
                 .ForMember(dest => dest.Count, opt => opt.MapFrom(src => src.Count))
                 .ForMember(dest => dest.FromDate, opt => opt.MapFrom(src => src.FromDate))
                 .ForMember(dest => dest.PageSize, opt => opt.MapFrom(src => src.PageSize))
                 .ForMember(dest => dest.ToDate, opt => opt.MapFrom(src => src.ToDate))
                 .AfterMap((s, d) =>
                 {
                     if (s.FromDate == null)
                     {
                         d.FromDate = System.DateTime.Now.AddHours(-5);
                     }
                     if (s.ToDate == null)
                     {
                         d.ToDate = System.DateTime.Now.AddHours(5);
                     }
                 });


            CreateMap<GetMerchantBanerRequestViewModel, GetMerchantBanerRequestDto>()
                  .ForMember(dest => dest.MerchantId, opt => opt.MapFrom(src => src.MerchantId))
                ;
            CreateMap<MerchantInfoRequestViewModel, GetMerchantInformationRequestDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.MerchantId))
                ;
            #endregion


            CreateMap<OrganizationViewModel, OrganizationDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.UtilityCode, opt => opt.MapFrom(src => src.UtilityCode))
                .ForMember(dest => dest.OrganizationName, opt => opt.MapFrom(src => src.OrganizationName));
            CreateMap<OrganizationDto, OrganizationViewModel>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.UtilityCode, opt => opt.MapFrom(src => src.UtilityCode))
                .ForMember(dest => dest.OrganizationName, opt => opt.MapFrom(src => src.OrganizationName));

            #region [- ResponseMap -]
            CreateMap<IrancellInquiryResponseDto, IrancellInquiryResponseViewModel>();
            CreateMap<CustomerWalletRequestDto, CustomerWalletRequestViewModel>();
            CreateMap<CustomerWalletRequestViewModel, CustomerWalletRequestDto>();

            CreateMap<MerchantWalletResponseDto, MerchantWalletResponseViewModel>()
                   .ForMember(dest => dest.Amount, opt => opt.MapFrom(src => src.Amount))
                   .ForMember(dest => dest.FirstName, opt => opt.MapFrom(src => src.CustomerFirstName))
                   .ForMember(dest => dest.LastName, opt => opt.MapFrom(src => src.CustomerLastName))
                   .ForMember(dest => dest.WalletId, opt => opt.MapFrom(src => src.CustomerWalletId))
                   .ForMember(dest => dest.WalletType, opt => opt.MapFrom(src => src.GroupWalletId))
                   .ForMember(dest => dest.WalletCode, opt => opt.MapFrom(src => src.WalletCode))
                   .ForMember(dest => dest.WalletTitle, opt => opt.MapFrom(src => src.GroupWalletTitle));

            CreateMap<GetCustomerWalletResponseDto, GetCustomerWalletResponseViewModel>()
                .ForMember(dest => dest.AdditionalData, opt => opt.MapFrom(src => src.AdditionalData))
                .ForMember(dest => dest.Amount, opt => opt.MapFrom(src => src.Amount))
                .ForMember(dest => dest.CorporationUserId, opt => opt.MapFrom(src => src.CorporationUserId))
                .ForMember(dest => dest.CorporationUserTitle, opt => opt.MapFrom(src => src.CorporationUserTitle))
                .ForMember(dest => dest.CustomerAgencyCode, opt => opt.MapFrom(src => src.CustomerAgencyCode))
                .ForMember(dest => dest.CustomerFirstName, opt => opt.MapFrom(src => src.CustomerFirstName))
                .ForMember(dest => dest.CustomerId, opt => opt.MapFrom(src => src.CustomerId))
                .ForMember(dest => dest.CustomerLastName, opt => opt.MapFrom(src => src.CustomerLastName))
                .ForMember(dest => dest.CustomerMerchantName, opt => opt.MapFrom(src => src.CustomerMerchantName))
                .ForMember(dest => dest.CustomerSettlementIBAN, opt => opt.MapFrom(src => src.CustomerSettlementIBAN))
                .ForMember(dest => dest.CustomerWalletId, opt => opt.MapFrom(src => src.CustomerWalletId))
                .ForMember(dest => dest.GroupWalletId, opt => opt.MapFrom(src => src.GroupWalletId))
                .ForMember(dest => dest.GroupWalletTitle, opt => opt.MapFrom(src => src.GroupWalletTitle))
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => src.IsActive))
                .ForMember(dest => dest.IsChargeable, opt => opt.MapFrom(src => src.IsChargeable))
                .ForMember(dest => dest.IsChargeableDeposit, opt => opt.MapFrom(src => src.IsChargeableDeposit))
                .ForMember(dest => dest.IsDestinationTransfer, opt => opt.MapFrom(src => src.IsDestinationTransfer))
                .ForMember(dest => dest.IsMain, opt => opt.MapFrom(src => src.IsMain))
                .ForMember(dest => dest.IsPurchase, opt => opt.MapFrom(src => src.IsPurchase))
                .ForMember(dest => dest.IsSettlement, opt => opt.MapFrom(src => src.IsSettlement))
                .ForMember(dest => dest.IsSupportMain, opt => opt.MapFrom(src => src.IsSupportMain))
                .ForMember(dest => dest.IsTransfer, opt => opt.MapFrom(src => src.IsTransfer))
                .ForMember(dest => dest.WalletCode, opt => opt.MapFrom(src => src.WalletCode))
                ;
            CreateMap<ValidBillResponseDto, ValidBillResponseViewModel>()
                .ForMember(dest => dest.Amount, opt => opt.MapFrom(src => src.Amount))
                .ForMember(dest => dest.BillId, opt => opt.MapFrom(src => src.BillId))
                .ForMember(dest => dest.BillType, opt => opt.MapFrom(src => src.BillType))
                .ForMember(dest => dest.CompanyName, opt => opt.MapFrom(src => src.CompanyName))
                .ForMember(dest => dest.IsValid, opt => opt.MapFrom(src => src.IsValid))
                .ForMember(dest => dest.OrganizationId, opt => opt.MapFrom(src => src.OrganizationId))
                .ForMember(dest => dest.PayId, opt => opt.MapFrom(src => src.PayId))
                .ForMember(dest => dest.StatusDescription, opt => opt.MapFrom(src => src.StatusDescription))
                ;

            CreateMap<UserBillDto, UserBillViewResponseModel>()
                 .ForMember(dest => dest.BillID, opt => opt.MapFrom(src => src.BillId))
                 .ForMember(dest => dest.BillType, opt => opt.MapFrom(src => src.BillType))
                 .ForMember(dest => dest.CustomerID, opt => opt.MapFrom(src => src.CustomerId))
                 .ForMember(dest => dest.OrganizationID, opt => opt.MapFrom(src => src.OrganizationId))
                 .ForMember(dest => dest.Title, opt => opt.MapFrom(src => src.Title))
                 .ForMember(dest => dest.UserID, opt => opt.MapFrom(src => src.UserID))
                ;
            CreateMap<NigcBillInquiryResponseDto, NigcBillInquiryResponseViewModel>()
                .ForMember(dest => dest.AbonnmanValue, opt => opt.MapFrom(src => src.AbonnmanValue))
                .ForMember(dest => dest.BankBillId, opt => opt.MapFrom(src => src.BankBillId))
                .ForMember(dest => dest.BankPayId, opt => opt.MapFrom(src => src.BankPayId))
                .ForMember(dest => dest.BillDesc, opt => opt.MapFrom(src => src.BillDesc))
                .ForMember(dest => dest.BuildingId, opt => opt.MapFrom(src => src.BuildingId))
                .ForMember(dest => dest.CityName, opt => opt.MapFrom(src => src.CityName))
                .ForMember(dest => dest.CurrentDate, opt => opt.MapFrom(src => src.CurrentDate))
                .ForMember(dest => dest.CurrentRounding, opt => opt.MapFrom(src => src.CurrentRounding))
                .ForMember(dest => dest.DebitCount, opt => opt.MapFrom(src => src.DebitCount))
                .ForMember(dest => dest.DebtBalance, opt => opt.MapFrom(src => src.DebtBalance))
                .ForMember(dest => dest.GasAmount, opt => opt.MapFrom(src => src.GasAmount))
                .ForMember(dest => dest.Inssurance, opt => opt.MapFrom(src => src.Inssurance))
                .ForMember(dest => dest.Kind, opt => opt.MapFrom(src => src.Kind))
                .ForMember(dest => dest.MiscCostValue, opt => opt.MapFrom(src => src.MiscCostValue))
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
                .ForMember(dest => dest.PayAmount, opt => opt.MapFrom(src => src.PayAmount))
                .ForMember(dest => dest.PayTimeoutDate, opt => opt.MapFrom(src => src.PayTimeoutDate))
                .ForMember(dest => dest.PreviousDate, opt => opt.MapFrom(src => src.PreviousDate))
                .ForMember(dest => dest.PreviousInvoiceBalance, opt => opt.MapFrom(src => src.PreviousInvoiceBalance))
                .ForMember(dest => dest.PreviousRounding, opt => opt.MapFrom(src => src.PreviousRounding))
                .ForMember(dest => dest.SerialNo, opt => opt.MapFrom(src => src.SerialNo))
                .ForMember(dest => dest.StandardConsuption, opt => opt.MapFrom(src => src.StandardConsuption))
                .ForMember(dest => dest.Tax, opt => opt.MapFrom(src => src.Tax))
                .ForMember(dest => dest.Unit, opt => opt.MapFrom(src => src.Unit));

            CreateMap<MciBillInquiryResponseDto, MciBillInquiryResponseViewModel>()
                 .ForMember(dest => dest.Amount, opt => opt.MapFrom(src => src.Amount))
                 .ForMember(dest => dest.BillDesc, opt => opt.MapFrom(src => src.BillDesc))
                 .ForMember(dest => dest.BillId, opt => opt.MapFrom(src => src.BillId))
                 .ForMember(dest => dest.MobileNumber, opt => opt.MapFrom(src => src.MobileNumber))
                 .ForMember(dest => dest.PaymentId, opt => opt.MapFrom(src => src.PaymentId))
                 .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.Type))
                 .ForMember(dest => dest.TypeDesc, opt => opt.MapFrom(src => src.TypeDesc))
;

            CreateMap<TciInquiryResponseDto, TciInquiryResponseViewModel>()
              .ForMember(dest => dest.Amount, opt => opt.MapFrom(src => src.Amount))
              .ForMember(dest => dest.BillDesc, opt => opt.MapFrom(src => src.BillDesc))
              .ForMember(dest => dest.BillId, opt => opt.MapFrom(src => src.BillId))
              .ForMember(dest => dest.CycleId, opt => opt.MapFrom(src => src.CycleId))
              .ForMember(dest => dest.PayId, opt => opt.MapFrom(src => src.PayId))
              .ForMember(dest => dest.TelephoneNum, opt => opt.MapFrom(src => src.TelephoneNum))
              .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.Type))
              .ForMember(dest => dest.TypeDesc, opt => opt.MapFrom(src => src.TypeDesc));

            CreateMap<TollBillPaymentResponseDto, TollBillPaymentResponseViewModel>()
                 .ForMember(dest => dest.OrderId, opt => opt.MapFrom(src => src.OrderId))
                 .ForMember(dest => dest.Token, opt => opt.MapFrom(src => src.Token))
                ;

            CreateMap<BillInfoResponseDto, BillInfoInquiryResponseViewModel>()
                 .ForMember(dest => dest.Amount, opt => opt.MapFrom(src => src.Amount))
                 .ForMember(dest => dest.BillType, opt => opt.MapFrom(src => src.BillType))
                 .ForMember(dest => dest.CompanyName, opt => opt.MapFrom(src => src.CompanyName))
                 .ForMember(dest => dest.OrganizationId, opt => opt.MapFrom(src => src.OrganizationId))
                 .ForMember(dest => dest.RequestDateTime, opt => opt.MapFrom(src => src.RequestDateTime))
                 .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status))
                 .ForMember(dest => dest.StatusDescription, opt => opt.MapFrom(src => src.StatusDescription))
                 .ForMember(dest => dest.SubUtilityCode, opt => opt.MapFrom(src => src.SubUtilityCode))
                 .ForMember(dest => dest.UtilityCode, opt => opt.MapFrom(src => src.UtilityCode))
                ;

            CreateMap<BillTypeDto, BillTypeViewModel>()
                  .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
                  .ForMember(dest => dest.BillTypeTitle, opt => opt.MapFrom(src => src.BillTypeTitle));


            CreateMap<BarghBillInquiryResponseDto, BarghBillInquiryResponseViewModel>()
                .ForMember(dest => dest.Amount, opt => opt.MapFrom(src => src.Amount))
                .ForMember(dest => dest.BillDesc, opt => opt.MapFrom(src => src.BillDesc))
                .ForMember(dest => dest.BillId, opt => opt.MapFrom(src => src.BillId))
                .ForMember(dest => dest.CustomerAddress, opt => opt.MapFrom(src => src.CustomerAddress))
                .ForMember(dest => dest.CustomerMobileNumber, opt => opt.MapFrom(src => src.CustomerMobileNumber))
                .ForMember(dest => dest.CustomerName, opt => opt.MapFrom(src => src.CustomerName))
                .ForMember(dest => dest.CustomerPostCode, opt => opt.MapFrom(src => src.CustomerPostCode))
                .ForMember(dest => dest.DistributionCompany, opt => opt.MapFrom(src => src.DistributionCompany))
                .ForMember(dest => dest.LastReadDate, opt => opt.MapFrom(src => src.LastReadDate))
                .ForMember(dest => dest.PayId, opt => opt.MapFrom(src => src.PayId))
                .ForMember(dest => dest.PaymentDeadLine, opt => opt.MapFrom(src => src.PaymentDeadLine))

                ;
            CreateMap<WalletBalanceResponseDto, WalletBalanceResponseViewModel>()
                 .ForMember(dest => dest.Amount, opt => opt.MapFrom(src => src.Amount))
                 .ForMember(dest => dest.ResultDesc, opt => opt.MapFrom(src => src.ResultDesc))
                 .ForMember(dest => dest.ResultId, opt => opt.MapFrom(src => src.ResultId));

            CreateMap<TollBillInquiryResponseDto, TollBillInquiryResponseViewModel>()
                .ForMember(dest => dest.Token, opt => opt.MapFrom(src => src.Token))
                .ForMember(dest => dest.TollGetBillsDatas, opt => opt.MapFrom(src => src.TollGetBillsDatas));
            CreateMap<WalletBalanceRequestViewModel, WalletBalanceRequestDto>()
                .ForMember(dest => dest.WalletId, opt => opt.MapFrom(src => src.WalletId))
                ;

            CreateMap<PaidWalletBillsDto, PaidWalletBillsViewModel>()
           .ForMember(dest => dest.Amount, opt => opt.MapFrom(src => src.Amount))
           .ForMember(dest => dest.BillId, opt => opt.MapFrom(src => src.BillId))
           .ForMember(dest => dest.OrderId, opt => opt.MapFrom(src => src.OrderId))
           .ForMember(dest => dest.PayId, opt => opt.MapFrom(src => src.PayId))
           .ForMember(dest => dest.PayStatus, opt => opt.MapFrom(src => src.PayStatus))
           .ForMember(dest => dest.ReturnId, opt => opt.MapFrom(src => src.ReturnId))
            ;
            CreateMap<PayBillResposneDto, PayBillResponseViewModel>()
                .ForMember(dest => dest.orderId, opt => opt.MapFrom(src => src.orderId.ToString()))
                .ForMember(dest => dest.message, opt => opt.MapFrom(src => src.message))
                .ForMember(dest => dest.PaiadBills, opt => opt.MapFrom(src => src.PaiadBills))
                .ForMember(dest => dest.token, opt => opt.MapFrom(src => src.token));

            CreateMap<ChargeWalletTokenResponseDto, GetChargeIpgTokenResponseViewModel>()
                .ForMember(dest => dest.message, opt => opt.MapFrom(src => src.message))
                .ForMember(dest => dest.orderId, opt => opt.MapFrom(src => src.orderId))
                .ForMember(dest => dest.token, opt => opt.MapFrom(src => src.token));



            CreateMap<PaymentDetailResponseDto, PaymentDetailResponseViewModel>()
                 .ForMember(dest => dest.bills, opt => opt.MapFrom(src => src.bills))
                 .ForMember(dest => dest.MerchatnID, opt => opt.MapFrom(src => src.MerchatnID))
                 .ForMember(dest => dest.RRN, opt => opt.MapFrom(src => src.RRN))
                 .ForMember(dest => dest.OrderId, opt => opt.MapFrom(src => src.OrderId))
                 .ForMember(dest => dest.ServiceMessage, opt => opt.MapFrom(src => src.ServiceMessage))
                 .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status))
                 .ForMember(dest => dest.Token, opt => opt.MapFrom(src => src.Token))
                 .ForMember(dest => dest.TotalAmount, opt => opt.MapFrom(src => src.TotalAmount))
                 .ForMember(dest => dest.TransType, opt => opt.MapFrom(src => src.TransType))
                 .ForMember(dest => dest.UserID, opt => opt.MapFrom(src => src.UserID))
                 .ForMember(dest => dest.WalletCode, opt => opt.MapFrom(src => src.WalletCode));


            CreateMap<BillRequestDetailDto, BillRequestDetailViewModel>()
                .ForMember(dest => dest.Amount, opt => opt.MapFrom(src => src.Amount))
                .ForMember(dest => dest.BillID, opt => opt.MapFrom(src => src.BillID))
                .ForMember(dest => dest.OrderId, opt => opt.MapFrom(src => src.OrderId))
                .ForMember(dest => dest.OrganizationID, opt => opt.MapFrom(src => src.OrganizationID))
                .ForMember(dest => dest.PayDate, opt => opt.MapFrom(src => src.PayDate))
                .ForMember(dest => dest.PayId, opt => opt.MapFrom(src => src.PayId))
                .ForMember(dest => dest.ReturnID, opt => opt.MapFrom(src => src.ReturnID))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status));

            CreateMap<ConfirmBatchBillPaymentResponseDto, ConfirmBatchBillPaymentResponseViewModel>()
                .ForMember(dest => dest.bills, opt => opt.MapFrom(src => src.bills))
                .ForMember(dest => dest.OrderId, opt => opt.MapFrom(src => src.OrderId))
                .ForMember(dest => dest.RRN, opt => opt.MapFrom(src => src.RRN))
                .ForMember(dest => dest.Token, opt => opt.MapFrom(src => src.Token))
                .ForMember(dest => dest.TotalAmount, opt => opt.MapFrom(src => src.TotalAmount))
                ;
            CreateMap<ValidBillDataDto, ValidBillDataViewModel>()
                .ForMember(dest => dest.Amount, opt => opt.MapFrom(src => src.Amount))
                .ForMember(dest => dest.BillId, opt => opt.MapFrom(src => src.BillId))
                .ForMember(dest => dest.BillType, opt => opt.MapFrom(src => src.BillType))
                .ForMember(dest => dest.CompanyName, opt => opt.MapFrom(src => src.CompanyName))
                .ForMember(dest => dest.IsValid, opt => opt.MapFrom(src => src.IsValid))
                .ForMember(dest => dest.OrganizationId, opt => opt.MapFrom(src => src.OrganizationId))
                .ForMember(dest => dest.PayId, opt => opt.MapFrom(src => src.PayId))
                .ForMember(dest => dest.StatusDescription, opt => opt.MapFrom(src => src.StatusDescription))

                ;
            CreateMap<BatchBillPaymentResponseDto, BatchBillPaymentResponseViewModel>()
                 .ForMember(dest => dest.Bills, opt => opt.MapFrom(src => src.Bills))
                 .ForMember(dest => dest.OrderId, opt => opt.MapFrom(src => src.OrderId))
                 .ForMember(dest => dest.Token, opt => opt.MapFrom(src => src.Token))
                 .ForMember(dest => dest.ValidBillCount, opt => opt.MapFrom(src => src.ValidBillCount))
                ;
            CreateMap<BillRequestDto, BillsHistoryViewModel>()
                .ForMember(dest => dest.BillRequestDetails, opt => opt.MapFrom(src => src.BillRequestDetails))
                .ForMember(dest => dest.BillRequestUniqID, opt => opt.MapFrom(src => src.BillRequestUniqID))
                .ForMember(dest => dest.BussinessDate, opt => opt.MapFrom(src => src.BussinessDate))
                .ForMember(dest => dest.CreateDate, opt => opt.MapFrom(src => src.CreateDate))
                .ForMember(dest => dest.MerchatnID, opt => opt.MapFrom(src => src.MerchatnID))
                .ForMember(dest => dest.OrderID, opt => opt.MapFrom(src => src.OrderID))
                .ForMember(dest => dest.RRN, opt => opt.MapFrom(src => src.RRN))
                .ForMember(dest => dest.ServiceMessage, opt => opt.MapFrom(src => src.ServiceMessage))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status))
                .ForMember(dest => dest.Token, opt => opt.MapFrom(src => src.Token))
                .ForMember(dest => dest.TotalAmount, opt => opt.MapFrom(src => src.TotalAmount))
                .ForMember(dest => dest.TransType, opt => opt.MapFrom(src => src.TransType))
                .ForMember(dest => dest.UserID, opt => opt.MapFrom(src => src.UserID))
                .ForMember(dest => dest.WalletCode, opt => opt.MapFrom(src => src.WalletId))
                ;
            CreateMap<GetBillPaymentHistoryResponseDto, GetBillPaymentHistoryResponseViewModel>()
                .ForMember(dest => dest.bills, opt => opt.MapFrom(src => src.bills))
                .ForMember(dest => dest.TotalCount, opt => opt.MapFrom(src => src.TotalCount));

            CreateMap<UserBillPaymentHistoryResponseDto, GetBillPaymentHistoryResponseViewModel>()
                .ForMember(dest => dest.bills, opt => opt.MapFrom(src => src.bills))
                .ForMember(dest => dest.TotalCount, opt => opt.MapFrom(src => src.TotalCount));
            CreateMap<MerchantTopUpDto, MerchatnInfoResponseViewModel>()
                  .ForMember(dest => dest.Address, opt => opt.MapFrom(src => src.Address))
                  .ForMember(dest => dest.AboutUs, opt => opt.MapFrom(src => src.AboutUs))
                  .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email))
                  .ForMember(dest => dest.Family, opt => opt.MapFrom(src => src.Family))
                  .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
                  .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => src.IsActive))
                  .ForMember(dest => dest.IsBillPayment, opt => opt.MapFrom(src => src.IsBillPayment))
                  .ForMember(dest => dest.IsCharity, opt => opt.MapFrom(src => src.IsCharity))
                  .ForMember(dest => dest.IsSaleCharge, opt => opt.MapFrom(src => src.IsSaleCharge))
                  .ForMember(dest => dest.IsSaleInternetPackage, opt => opt.MapFrom(src => src.IsSaleInternetPackage))
                  .ForMember(dest => dest.IsSalePin, opt => opt.MapFrom(src => src.IsSalePin))
                  .ForMember(dest => dest.IsWebsite, opt => opt.MapFrom(src => src.IsWebsite))
                  .ForMember(dest => dest.MerchantName, opt => opt.MapFrom(src => src.MerchantName))
                  .ForMember(dest => dest.MobileNo, opt => opt.MapFrom(src => src.MobileNo))
                  .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
                  .ForMember(dest => dest.Tel, opt => opt.MapFrom(src => src.Tel))
                  .ForMember(dest => dest.WalletId, opt => opt.MapFrom(src => src.WalletId));

            CreateMap<TollBillsDto, TollBillsViewModel>()
                .ForMember(dest => dest.Amount, opt => opt.MapFrom(src => src.Amount))
                .ForMember(dest => dest.BillId, opt => opt.MapFrom(src => src.BillId))
                .ForMember(dest => dest.TraversDate, opt => opt.MapFrom(src => src.TraversDate));

            CreateMap<GetTollBillResponseDto, GetTollBillResponseViewModel>()
                .ForMember(dest => dest.Amount, opt => opt.MapFrom(src => src.Amount))
                .ForMember(dest => dest.BillPayMessage, opt => opt.MapFrom(src => src.BillPayMessage))
                .ForMember(dest => dest.CreateDate, opt => opt.MapFrom(src => src.CreateDate))
                .ForMember(dest => dest.OrderId, opt => opt.MapFrom(src => src.OrderId))
                .ForMember(dest => dest.RRN, opt => opt.MapFrom(src => src.RRN))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status))
                .ForMember(dest => dest.token, opt => opt.MapFrom(src => src.token))
                .ForMember(dest => dest.TollBills, opt => opt.MapFrom(src => src.TollBills));

            CreateMap<MerchantTopUpBanerDto, GetMerchantBanerResponseViewModel>()
                 .ForMember(dest => dest.ImagePath, opt => opt.MapFrom(src => src.ImagePath));

            CreateMap<ChargeHistoryDataDto, ChargeHistoryData>()
                .ForMember(dest => dest.Amount, opt => opt.MapFrom(src => src.Amount))
                .ForMember(dest => dest.ApplicationId, opt => opt.MapFrom(src => src.ApplicationId))
                .ForMember(dest => dest.CreateDate, opt => opt.MapFrom(src => src.CreateDate))
                .ForMember(dest => dest.PaymentStatus, opt => opt.MapFrom(src => src.PaymentStatus))
                .ForMember(dest => dest.PGWToken, opt => opt.MapFrom(src => src.PGWToken))
                .ForMember(dest => dest.ResultChargeId, opt => opt.MapFrom(src => src.ResultChargeId))
                .ForMember(dest => dest.RRN, opt => opt.MapFrom(src => src.RRN))
                .ForMember(dest => dest.UserId, opt => opt.MapFrom(src => src.UserId))
                .ForMember(dest => dest.WalletOrderId, opt => opt.MapFrom(src => src.WalletOrderId));

            CreateMap<ChargeHistoryResponseDto, ChargeHistoryReponseViewModel>()
                .ForMember(dest => dest.chargeHistory, opt => opt.MapFrom(src => src.chargeHistory))
                .ForMember(dest => dest.TotalCount, opt => opt.MapFrom(src => src.TotalCount));
            CreateMap<ClientCardTicketResponseVM, TicketCard>().ReverseMap();
            #endregion

            #region TosanSoha
            CreateMap<ClientCardTicketReqVM, TicketCard>().ReverseMap();
            CreateMap<PayTosanSohaRequestDto, PayCardChargeReqVM>().ReverseMap();
            
            
            CreateMap<TosanSohaResponseVM<object>,GetIpgTokenResponseDto>().ReverseMap();
            CreateMap<GetIpgTokenResponseDto, TosanSohaResponseVM<object>>().ReverseMap();

            

            #endregion
        }
    }
}
