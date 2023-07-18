using AutoMapper;
using Dto.Pagination;
using Dto.Proxy.IPG;
using Dto.Proxy.Request.Naja;
using Dto.Proxy.Request.Wallet;
using Dto.Proxy.Response;
using Dto.Proxy.Response.Naja;
using Dto.Proxy.Wallet;
using Dto.repository;
using Entities;
using PEC.CoreCommon.Attribute;
using ProxyService.services;
using Repository.reositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Utility;
using static ProxyService.services.WalletProxy;
using Pigi.MDbLogging;
using Newtonsoft.Json;
using Dto.Proxy.Response.Tosan;
using Dto.Proxy.Request.Tosan;
using Microsoft.Extensions.Configuration;
using Entities.Transportation;
using Pigi.MDbLogging.Models;
using Microsoft.Extensions.Caching.Memory;

namespace Application.Services
{

    [ScopedService]
    public class TosanSohaServices : ITosanSohaServices, ILoggable
    {
        #region Private Variable
        private readonly IServiceProvider _serviceProvider;
        private readonly IMapper _mapper;
        private readonly IConfiguration _config;
        private readonly IWalletProxy _proxy;
        private readonly IPecBmsSetting _setting;
        private readonly IIpgServiceProxy _ipgServiceProxy;
        private readonly ITosanSohaServiceProxy _tosanSohaServiceProxy;
        private readonly IWalletRepository _walletRepository;
        private readonly ITosanSohaRepository _tosanSohaRepository;
        private readonly IPaymentDbRepository _paymentDbRepository;
        private readonly IWalletServices _walletServices;
        private readonly IMdbLogger<TosanSohaServices> _logger;
        #endregion
        private readonly IMemoryCache _Cache;


        #region ctor
        public TosanSohaServices(IServiceProvider serviceProvider, IConfiguration config,
            IWalletServices walletServices,
            IPecBmsSetting pecBmsSetting, IMapper mapper,
            ITosanSohaRepository tosanSohaRepository,
            ITosanSohaServiceProxy tosanSohaServiceProxy,
            IWalletProxy proxy,
            IIpgServiceProxy ipgServiceProxy,
            IMdbLogger<TosanSohaServices> logger,
            IWalletRepository walletRepository,
            IPaymentDbRepository paymentDbRepository, IMemoryCache cache)
        {
            _serviceProvider = serviceProvider;
            _setting = pecBmsSetting;
            _config = config;
            _mapper = mapper;
            _tosanSohaRepository = tosanSohaRepository;
            _walletRepository = walletRepository;
            _logger = logger;
            _ipgServiceProxy = ipgServiceProxy;
            _walletServices = walletServices;
            _tosanSohaServiceProxy = tosanSohaServiceProxy;
            _proxy = proxy;
            _paymentDbRepository = paymentDbRepository;
            _Cache = cache;
        }
        #endregion

        #region ClientCards
        public async Task<bool> UpdateTicketCard(TicketCard myCard)
        {
            return await _tosanSohaRepository.UpdateTicketCard(myCard);
        }
        public async Task<List<TicketCard>> GetTicketCardsAsync(long userId)
        {
            return await _tosanSohaRepository.GetTicketCardsAsync(userId);
        }
        public async Task<ResponseBaseDto> AddTicketCard(TicketCard myCard)
        {
            var result = await _tosanSohaRepository.GetTicketCardByCardSerialAsync(myCard.CardSerial);
            if (result != null)
            {
                return new ResponseBaseDto { Message = "این کارت قبلا افزوده شده است", Status = -5 };
            }
            else
            {
                var resultadd = _tosanSohaRepository.AddTicketCard(myCard);
                if (resultadd)
                {
                    return new ResponseBaseDto { Status = 0, Message = "کارت شما با موفقیت افزوده شد" };
                }
                else
                {
                    return new ResponseBaseDto { Status = -1, Message = "در روند افزودن کارت خطایی رخ داد" };
                }
            }
        }
        public async Task<TicketCard> GetTicketCardByIdAsync(long CardId)
        {
            return await _tosanSohaRepository.GetTicketCardByIdAsync(CardId);
        }
        public async Task<bool> DeleteTicketCardAsync(long CardId)
        {
            return await _tosanSohaRepository.DeleteTicketCardAsync(CardId);
        }
        #endregion

        #region ChargeTicketCard

        #region [- InquiryChargeCardByOrderId -]
        [LoggingAspect]
        public async Task<ResponseBaseDto<TicketCardInfo>> InquiryChargeCardByOrderIdAsync(long orderId, long userId)
        {
            var GetOrder = await _tosanSohaRepository.GetTicketCardInfoByExperssionAsync(n => n.OrderId == orderId &&
                                                                            n.UserId == userId);
            var haveOrder = GetOrder.FirstOrDefault();
            if (haveOrder == null/* && haveOrder.MobileEnabled.HasValue*/)
            {

                return new ResponseBaseDto<TicketCardInfo>()
                {
                    Message = DescriptionUtility.GetDescription<ServiceStatus>("OrderIdNotValid"),
                    Status = (int)ServiceStatus.OrderIdNotValid,
                    Data = null
                };
            }

            if (haveOrder != null && haveOrder.OrderId == orderId && haveOrder.PaymentStatus.Value == PaymentStatus.Success && haveOrder.TransactionType == (int)TransactionTypeStatus.Shetab)
            {
                return new ResponseBaseDto<TicketCardInfo>()
                {
                    Message = DescriptionUtility.GetDescription<ServiceStatus>("GetwaySuccess"),
                    Status = (int)ServiceStatus.GetwaySuccess,
                    Data = haveOrder
                };
            }
            else if (haveOrder != null && haveOrder.OrderId == orderId && haveOrder.Status == 0 && haveOrder.WalletReturnId.Value != 0 && haveOrder.TransactionType == (int)TransactionTypeStatus.Wallet)
            {
                return new ResponseBaseDto<TicketCardInfo>()
                {
                    Message = DescriptionUtility.GetDescription<ServiceStatus>("WalletSuccess"),
                    Status = (int)(int)ServiceStatus.WalletSuccess,
                    Data = haveOrder
                };
            }
            else if (haveOrder != null && haveOrder.OrderId == orderId && !haveOrder.WalletReturnId.HasValue)
            {
                return new ResponseBaseDto<TicketCardInfo>()
                {
                    Message = haveOrder.ResultMessage,
                    Status = haveOrder.Status.HasValue ? haveOrder.Status.Value : -10,
                    Data = haveOrder
                };
            }
            else
            {
                return new ResponseBaseDto<TicketCardInfo>()
                {
                    Message = haveOrder.ResultMessage,
                    Status = haveOrder.PaymentStatus.Value,
                    Data = haveOrder
                };
            }
        }
        #endregion

        #region ConfirmTicketCardCharge
        public async Task<bool> ConfirmChargeCardAsync(TicketCardInfo ticketCardInfo)
        {
            _logger.Log(ticketCardInfo.Token, null, $"📥Step1 tosanServices(ConfirmChargeCardAsync {JsonConvert.SerializeObject(ticketCardInfo)}'", null);
            try
            {

                var Model = await _tosanSohaRepository.GetTicketCardInfoByIdAsync(ticketCardInfo.OrderId.Value);
                if (ticketCardInfo.PaymentStatus == 0)
                {
                    //Confirm GetWay
                    ConfirmPaymentRequestDto confirm = new ConfirmPaymentRequestDto()
                    {
                        LoginAccount = _config.GetSection("loginAccount").Value,
                        Token = ticketCardInfo.Token.Value
                    };
                    var confirmResp = await _ipgServiceProxy.ConfirmPayment(confirm);
                    _logger.Log(ticketCardInfo.Token, "📥Step2 tosanServices(ConfirmChargeCardAsync(ConfirmPayment)", JsonConvert.SerializeObject(confirmResp), null);

                    if (confirmResp != null && confirmResp.status == 0)
                    {
                        Charge charge = new Charge()
                        {
                            PaymentStatus = confirmResp.status,
                            RRN = ticketCardInfo.RRN.Value,
                            PGWToken = ticketCardInfo.Token.ToString()
                        };
                        var respUpdateCharge = await _walletRepository.UpdateChargeTable(charge);
                        if (!respUpdateCharge)
                        {
                            var try2UpdateCharge = await _walletRepository.UpdateChargeTable(charge);
                            _logger.Log(ticketCardInfo.Token, $"📥Step3 tosanServices(ConfirmChargeCardAsync(UpdateChargeTableUse(try2UpdateCharge={try2UpdateCharge}))", JsonConvert.SerializeObject(charge), null);
                        }
                        Model.Status = 0;
                        Model.PaymentStatus = confirmResp.status;
                        Model.BussinessDate = DateTime.Now;
                        Model.OrderId = ticketCardInfo.OrderId;
                        Model.Token = ticketCardInfo.Token;
                        Model.RRN = confirmResp.RRN;
                        Model.Amount = ticketCardInfo.Amount;
                        Model.MerchantId = ticketCardInfo.MerchantId;

                        Model.MaskCardNumber = confirmResp.CardNumberMasked;
                        Model.ResultMessage = $"سرویس درگاه : پرداخت با موفقیت انجام شد ";
                        var resultRepo = await _tosanSohaRepository.UpdateTicketCardInfoAsync(Model);
                        if (!resultRepo)
                        {
                            var res = await _tosanSohaRepository.UpdateTicketCardInfoAsync(Model);
                            return res;
                        }
                        else
                        {
                            return true;
                        }
                    }
                    else
                    {
                        Model.Status = confirmResp.status;
                        Model.PaymentStatus = confirmResp.status;
                        //Model.BussinessDate = DateTime.Now;
                        Model.OrderId = ticketCardInfo.OrderId;
                        Model.Token = confirmResp.Token;
                        Model.RRN = confirmResp.RRN;
                        Model.Amount = ticketCardInfo.Amount;
                        Model.MaskCardNumber = confirmResp.CardNumberMasked;
                        Model.ResultMessage = $"سرویس درگاه : {confirmResp.status} ";
                        var resultRepo = await _tosanSohaRepository.UpdateTicketCardInfoAsync(Model);
                        if (!resultRepo)
                        {
                            var res = await _tosanSohaRepository.UpdateTicketCardInfoAsync(Model);
                            return res;
                        }
                        else
                        {
                            return true;
                        }
                    }
                }
                else
                {
                    Model.Status = -1;
                    Model.PaymentStatus = ticketCardInfo.PaymentStatus;
                    Model.BussinessDate = DateTime.Now;
                    Model.OrderId = ticketCardInfo.OrderId;
                    Model.Token = ticketCardInfo.Token;
                    Model.ResultMessage = $"سرویس درگاه : پرداخت ناموفق";
                    var result = await _tosanSohaRepository.UpdateTicketCardInfoAsync(Model);
                    if (result)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Log(ticketCardInfo.Token, $"📥 tosanServices (when got exception ConfirmCardCharge)", JsonConvert.SerializeObject(ex.Message), ex);
                return false;
            }
        }



        #endregion

        #region Proxy
        [LoggingAspect]
        public Task<TicketGetBalanceDto> GetBalanceCompanyCard()
        {
            return _tosanSohaServiceProxy.GetBalanceCompanyCard();
        }
        [LoggingAspect]
        public Task<IsValidCardTypeDto> IsValidCardType(long? cardserial)
        {
            return _tosanSohaServiceProxy.IsValidCardType(cardserial);
        }
        [LoggingAspect]
        public Task<CreateVoucherResponseDto> CreateVoucher(CreateVoucherRequestDto request)
        {
            return _tosanSohaServiceProxy.CreateVoucher(request);
        }

        #region [- ShetabPayWage -]
        [LoggingAspect]
        public async Task<ResponseBaseDto<PayWageResponseDto>> ShetabPayTicketCardAsync(PayTosanSohaRequestDto payTosanSohaRequestDto)
        {
            try
            {
                var callbackUrl = "";

                var GetOrder = await _tosanSohaRepository.GetTicketCardInfoByExperssionAsync(n => n.OrderId == payTosanSohaRequestDto.OrderId &&
                                                                            n.UserId == payTosanSohaRequestDto.UserId);
                var haveOrder = GetOrder.FirstOrDefault();
                if (haveOrder == null)
                {

                    return new ResponseBaseDto<PayWageResponseDto>()
                    {
                        Message = DescriptionUtility.GetDescription<ServiceStatus>("InvalidOperationNotConfirmMobileNo"),
                        Status = (int)ServiceStatus.InvalidOperationNotConfirmMobileNo,
                        Data = null
                    };
                }

                if (haveOrder != null && haveOrder.OrderId == payTosanSohaRequestDto.OrderId && haveOrder.PaymentStatus.Value != 0)
                {
                    haveOrder.MerchantId = payTosanSohaRequestDto.MerchantId;
                    haveOrder.Amount = payTosanSohaRequestDto.Amount;
                    haveOrder.UserId = payTosanSohaRequestDto.UserId;
                    haveOrder.WalletId = payTosanSohaRequestDto.WalletId;
                    haveOrder.TransactionType = payTosanSohaRequestDto.TransactionType;
                    haveOrder.CreateDate = DateTime.Now;
                    haveOrder.OrderId = payTosanSohaRequestDto.OrderId;
                    haveOrder.Status = PaymentStatus.ReadyToPay;
                    haveOrder.CardSerial = payTosanSohaRequestDto.CardSerial;

                    var ticketCardInfo = await _tosanSohaRepository.UpdateTicketCardInfoAsync(haveOrder);
                    if (!ticketCardInfo)
                    {
                        return new ResponseBaseDto<PayWageResponseDto>()
                        {
                            Message = DescriptionUtility.GetDescription<ServiceStatus>("InsertFaildData"),
                            Status = (int)ServiceStatus.InsertFaildData,
                            Data = null
                        };
                    }
                    if (haveOrder.MerchantId != 0)
                    {
                        callbackUrl = _config.GetValue<string>("Proxy:Url:TosanSoha:CallbackUrlTicketCardInfo") + "?mid=" + haveOrder.MerchantId;
                    }
                    else
                    {
                        callbackUrl = _config.GetValue<string>("Proxy:Url:TosanSoha:CallbackUrlTicketCardInfo");
                    }

                    IpgSaleServiceRequestDto req = new IpgSaleServiceRequestDto()
                    {
                        AdditionalData = "شارژکارت بلیت_pecbms",
                        Amount = haveOrder.Amount.Value,
                        CallBackUrl = callbackUrl,
                        LoginAccount = _config.GetValue<string>("Proxy:Url:TosanSoha:LoginAccount"),
                        OrderId = haveOrder.OrderId.Value,
                        Originator = haveOrder.MobileNo
                    };

                    //get ipg token
                    var saleToken = await _ipgServiceProxy.GetIpgSaleToken(req);

                    if (saleToken.status == 0)
                    {
                        //insert to charge for storage wallet
                        Guid obj = Guid.NewGuid();
                        ChargeDto charge = new ChargeDto()
                        {
                            Amount = req.Amount,
                            DestinationWalletId = _config.GetValue<string>("Proxy:Url:TosanSoha:MotherWalletTicketCardInfo"),
                            WalletType = _setting.ParrentWalletType,
                            CreateDate = DateTime.Now,
                            IsSendToSettlement = false,
                            WalletOrderId = haveOrder.OrderId.Value,
                            UserId = haveOrder.UserId.Value,
                            PGWToken = saleToken.Token.ToString(),
                            TrackingCode = obj.ToString(),
                            ApplicationId = 1,//تغییر یابد,

                            PaymentStatus = PaymentStatus.UnSuccess
                        };
                        var insertIntoChargeDb = await _walletServices.AddChargeRow(charge);
                        if (insertIntoChargeDb)
                        {
                            haveOrder.Token = saleToken.Token;
                            haveOrder.ResultMessage = DescriptionUtility.GetDescription<PaymentStatus>("GetTokenSuccess");
                            haveOrder.OrderId = haveOrder.OrderId;
                            haveOrder.PaymentStatus = (int)PaymentStatus.GetTokenSuccess;
                            haveOrder.BussinessDate = DateTime.Now;

                            await _tosanSohaRepository.UpdateTicketCardInfoAsync(haveOrder);

                            PayWageResponseDto OkGetWay = new PayWageResponseDto()
                            {
                                OrderId = haveOrder.OrderId.Value.ToString(),
                                Message = saleToken.Message,
                                Token = saleToken.Token
                            };

                            return new ResponseBaseDto<PayWageResponseDto>()
                            {
                                Status = (int)PaymentStatus.GetTokenSuccess,
                                Message = DescriptionUtility.GetDescription<PaymentStatus>("GetTokenSuccess"),
                                Data = OkGetWay
                            };
                        }
                        else
                        {
                            return new ResponseBaseDto<PayWageResponseDto>()
                            {
                                Message = DescriptionUtility.GetDescription<PaymentStatus>("NotInsertToCharge"),
                                Status = (int)PaymentStatus.NotInsertToCharge,
                                Data = null
                            };
                        }


                    }
                    else
                    {
                        return new ResponseBaseDto<PayWageResponseDto>()
                        {
                            Status = saleToken.status,
                            Message = saleToken.Message,
                            Data = null
                        };

                    }
                }
                else
                {
                    return new ResponseBaseDto<PayWageResponseDto>()
                    {
                        Status = (int)ServiceStatus.OrderIdNotValid,
                        Message = DescriptionUtility.GetDescription<ServiceStatus>("OrderIdNotValid"),
                        Data = null
                    };
                }
            }
            catch (Exception)
            {
                return new ResponseBaseDto<PayWageResponseDto>()
                {
                    Status = (int)ServiceStatus.OperationFaild,
                    Message = DescriptionUtility.GetDescription<ServiceStatus>("OperationFaild"),
                    Data = null
                };
            }
        }
        #endregion

        #region [- WalletPayWage -]
        [LoggingAspect]
        public async Task<ResponseBaseDto<PayWageResponseDto>> WalletPayTicketCardAsync(PayTosanSohaRequestDto payTosanSohaRequestDto)
        {
            #region CheckDuplicateOrder
            var GetDuplicateOrderId = await _tosanSohaRepository.GetTicketCardInfoByExperssionAsync(
                                           n => n.OrderId == payTosanSohaRequestDto.OrderId
                                        && n.WalletReturnId != null
                                        && n.WalletReturnId != 0);
            var CheckDuplicateOrderId = GetDuplicateOrderId.FirstOrDefault();
            if (CheckDuplicateOrderId != null && CheckDuplicateOrderId.Status == 0)
            {
                return new ResponseBaseDto<PayWageResponseDto>()
                {
                    Status = (int)ServiceStatus.OrderIdPaied,
                    Message = DescriptionUtility.GetDescription<ServiceStatus>("OrderIdPaied"),
                    Data = null
                };
            }

            #endregion

            var GetOrderID = await _tosanSohaRepository.GetTicketCardInfoByExperssionAsync(n => n.OrderId == payTosanSohaRequestDto.OrderId);
            var haveOrderID = GetOrderID.FirstOrDefault();

            if (haveOrderID == null)
            {
                return new ResponseBaseDto<PayWageResponseDto>()
                {
                    Status = (int)ServiceStatus.OrderIdNotValid,
                    Message = DescriptionUtility.GetDescription<ServiceStatus>("OrderIdNotValid"),
                    Data = null
                };
            }
            PayWageResponseDto response = new PayWageResponseDto();
            WalletBalanceRequestDto walletBalance = new WalletBalanceRequestDto()
            {
                CorporationPIN = _setting.CorporationPIN,
                WalletId = payTosanSohaRequestDto.WalletId,
            };
            var walletBalanceResponse = await _walletServices.GetWalletBalance(walletBalance);
            if (walletBalanceResponse.ResultId != 0)
            {
                return new ResponseBaseDto<PayWageResponseDto>()
                {
                    Message = walletBalanceResponse.ResultDesc,
                    Status = walletBalanceResponse.ResultId,
                    Data = null
                };
            }
            else
            {
                var walletAmount = Convert.ToInt64(walletBalanceResponse.Amount);
                if (!(walletAmount > payTosanSohaRequestDto.Amount))
                {
                    haveOrderID.MerchantId = payTosanSohaRequestDto.MerchantId;
                    haveOrderID.Amount = payTosanSohaRequestDto.Amount;
                    haveOrderID.UserId = payTosanSohaRequestDto.UserId;
                    haveOrderID.WalletId = payTosanSohaRequestDto.WalletId;
                    haveOrderID.TransactionType = payTosanSohaRequestDto.TransactionType;
                    haveOrderID.BussinessDate = DateTime.Now;
                    haveOrderID.OrderId = payTosanSohaRequestDto.OrderId;
                    haveOrderID.Status = PaymentStatus.NotEnoughWalletBalance;

                    var updateNajaWageRequest = await _tosanSohaRepository.UpdateTicketCardInfoAsync(haveOrderID);

                    if (!updateNajaWageRequest)
                    {
                        return new ResponseBaseDto<PayWageResponseDto>()
                        {
                            Status = (int)ServiceStatus.InsertFaildData,
                            Message = DescriptionUtility.GetDescription<ServiceStatus>("InsertFaildData"),
                            Data = null
                        };
                    }

                    return new ResponseBaseDto<PayWageResponseDto>()
                    {
                        Status = (int)ServiceStatus.NotEnoughWalletBalance,
                        Message = DescriptionUtility.GetDescription<ServiceStatus>("NotEnoughWalletBalance"),
                        Data = null
                    };

                }
                if (payTosanSohaRequestDto.OrderId <= 0)
                {
                    return new ResponseBaseDto<PayWageResponseDto>()
                    {
                        Status = (int)ServiceStatus.OrderIdNotValid,
                        Message = DescriptionUtility.GetDescription<ServiceStatus>("OrderIdNotValid"),
                        Data = null
                    };
                }
                if (haveOrderID != null)
                {
                    haveOrderID.MerchantId = payTosanSohaRequestDto.MerchantId;
                    haveOrderID.Amount = payTosanSohaRequestDto.Amount;
                    haveOrderID.UserId = payTosanSohaRequestDto.UserId;
                    haveOrderID.WalletId = payTosanSohaRequestDto.WalletId;
                    haveOrderID.TransactionType = payTosanSohaRequestDto.TransactionType;
                    haveOrderID.BussinessDate = DateTime.Now;
                    haveOrderID.OrderId = payTosanSohaRequestDto.OrderId;
                    haveOrderID.Status = PaymentStatus.ReadyToPayWithWallet;

                    var updateTicketCardRequest = await _tosanSohaRepository.UpdateTicketCardInfoAsync(haveOrderID);
                    if (!updateTicketCardRequest)
                    {
                        return new ResponseBaseDto<PayWageResponseDto>()
                        {
                            Status = (int)ServiceStatus.InsertFaildData,
                            Message = DescriptionUtility.GetDescription<ServiceStatus>("InsertFaildData"),
                            Data = null
                        };
                    }

                    response.OrderId = haveOrderID.OrderId.Value.ToString();
                    response.Message = "پرداخت شارژ کارت بلیت با موفقیت از کیف پول شما کسر گردید";

                    WalletTransferResquestDto transferRequestDto = new WalletTransferResquestDto()
                    {
                        CallBackURL = _setting.CallBackUrlParentWallet,
                        IpAddress = "172.30.2.155",
                        MediaTypeId = _setting.WalletMediaType,
                        AdditionalData = "پرداخت شارژ کارت بلیت از کیف پول",
                        Amount = haveOrderID.Amount.Value,
                        //call back pay bill
                        CorporationPIN = _setting.CorporationPIN,
                        DestinationWalletId = Convert.ToInt64(_config.GetSection("Proxy:Url:TosanSoha:TosanSohaMotherWallet").Value),
                        OrderId = haveOrderID.OrderId.Value,
                        PIN = "234",
                        SourceWalletId = haveOrderID.WalletId.Value,
                        TransactionDateTime = DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss")
                    };
                    var paymentProcess = await _walletServices.WalletTransfer(transferRequestDto);
                    if (paymentProcess != null && paymentProcess.Status == 0)
                    {
                        haveOrderID.Status = PaymentStatus.Success;
                        haveOrderID.PaymentStatus = PaymentStatus.Success;
                        haveOrderID.BussinessDate = DateTime.Now;
                        haveOrderID.WalletReturnId = paymentProcess.Data.ReturnId;
                        haveOrderID.ResultMessage = $"{paymentProcess.Message} : سرویس کیف پول";
                        var updateDb = await _tosanSohaRepository.UpdateTicketCardInfoAsync(haveOrderID);
                        if (updateDb)
                        {
                            response.Token = paymentProcess.Data.ReturnId;
                            return new ResponseBaseDto<PayWageResponseDto>()
                            {
                                Message = paymentProcess.Message,
                                Status = 0,
                                Data = response
                            };
                        }
                        else
                        {
                            return new ResponseBaseDto<PayWageResponseDto>()
                            {
                                Status = (int)ServiceStatus.PaymentSuccessButOperationFaild,
                                Message = DescriptionUtility.GetDescription<ServiceStatus>("PaymentSuccessButOperationFaild"),
                                Data = null
                            };
                        }
                    }
                    else
                    {
                        return new ResponseBaseDto<PayWageResponseDto>()
                        {
                            Message = paymentProcess.Message,
                            Status = (int)ServiceStatus.ErrorInWalletService,
                            Data = null
                        };
                    }
                }
                else
                {
                    return new ResponseBaseDto<PayWageResponseDto>()
                    {
                        Status = (int)ServiceStatus.OrderIdNotValid,
                        Message = DescriptionUtility.GetDescription<ServiceStatus>("OrderIdNotValid"),
                        Data = null
                    };
                }
            }
        }
        #endregion

        [LoggingAspect]
        public Task<VoucherInfoListResponseDto> VoucherInfoList(VoucherListRequestDto request, string cardSerial)
        {
            return _tosanSohaServiceProxy.VoucherInfoList(request, cardSerial);
        }

        #endregion

        #region Repository
        public async Task<List<TicketCardInfo>> GetTicketCardsInfoAsync(long userId)
        {
            return await _tosanSohaRepository.GetTicketCardsInfoAsync(userId);
        }
        public async Task<bool> AddTicketCardInfo(TicketCardInfo ticketCardInfo)
        {
            return await _tosanSohaRepository.AddTicketCardInfo(ticketCardInfo);
        }
        public async Task<TicketCardInfo> GetTicketCardInfoByIdAsync(long CardId)
        {
            return await _tosanSohaRepository.GetTicketCardInfoByIdAsync(CardId);
        }
        public async Task<bool> UpdateTicketCardInfoAsync(TicketCardInfo ticketCardInfo)
        {
            return await _tosanSohaRepository.UpdateTicketCardInfoAsync(ticketCardInfo);
        }
        public async Task<bool> UpdateTicketCardInfoRequestAsync(long orderId)
        {
            return await _tosanSohaRepository.UpdateTicketCardInfoRequestAsync(orderId);
        }
        public async Task<List<TicketCardInfo>> GetTicketCardInfoByExperssionAsync(Expression<Func<TicketCardInfo, bool>> predicate)
        {
            return await _tosanSohaRepository.GetTicketCardInfoByExperssionAsync(predicate);
        }
        #endregion

        #endregion


        #region SingleDirectionTicket

        #region [- InquirySingleDirectionTicketByOrderId -]
        [LoggingAspect]
        public async Task<PagedResponse<List<SingleTicketResponsePaginated>>> GetPaginatedSingleTicketAsync(PaginationFilterDto paginationFilter)
        {
            if (paginationFilter.ToDate.HasValue && paginationFilter.FromDate.HasValue)
            {
                var validdate = paginationFilter.ToDate < paginationFilter.FromDate;
                if (validdate)
                {
                    return null;
                }
                var validFilter = new PaginationFilterDto(paginationFilter.PageNumber, paginationFilter.PageSize, paginationFilter.FromDate.Value, paginationFilter.ToDate.Value);
                var pagedData = await _tosanSohaRepository.GetPaginatedSingleTicketAsync(paginationFilter.UserId);
                pagedData = pagedData.Where(d => d.BussinessDate >= validFilter.FromDate.Value.AddHours(-12) && 
                                                 d.BussinessDate <= validFilter.ToDate.Value.AddHours(12)).ToList();
                var totalRecords = pagedData.Count();
                var Divide = decimal.Divide(totalRecords, validFilter.PageSize);
                var totalPages = (int)Math.Ceiling(Divide);
                pagedData = pagedData.Skip((validFilter.PageNumber - 1) * validFilter.PageSize)
                    .Take(validFilter.PageSize).ToList();
                return new PagedResponse<List<SingleTicketResponsePaginated>>(pagedData,
                    validFilter.PageNumber,
                    validFilter.PageSize,
                    totalPages,
                    totalRecords);
            }
            else
            {
                var validFilter = new PaginationFilterDto(paginationFilter.PageNumber, paginationFilter.PageSize);
                var pagedData = await _tosanSohaRepository.GetPaginatedSingleTicketAsync(paginationFilter.UserId);
                var totalRecords = pagedData.Count;
                var Divide = decimal.Divide(totalRecords, validFilter.PageSize);
                var totalPages = (int)Math.Ceiling(Divide);
                pagedData = pagedData.Skip((validFilter.PageNumber - 1) * validFilter.PageSize)
                    .Take(validFilter.PageSize).ToList();
                return new PagedResponse<List<SingleTicketResponsePaginated>>(pagedData.ToList(),
                    validFilter.PageNumber,
                    validFilter.PageSize,
                    totalPages,
                    totalRecords);
            }
        }

        [LoggingAspect]
        public async Task<ResponseBaseDto<SingleDirectionTicket>> InquirySingleDirectionTicketByOrderIdAsync(long orderId, long userId)
        {
            var GetOrder = await _tosanSohaRepository.GetSingleDirectionTicketByExperssionAsync(n => n.OrderId == orderId &&
                                                                            n.UserId == userId);
            var haveOrder = GetOrder.FirstOrDefault();
            if (haveOrder == null/* && haveOrder.MobileEnabled.HasValue*/)
            {

                return new ResponseBaseDto<SingleDirectionTicket>()
                {
                    Message = DescriptionUtility.GetDescription<ServiceStatus>("OrderIdNotValid"),
                    Status = (int)ServiceStatus.OrderIdNotValid,
                    Data = null
                };
            }

            if (haveOrder != null && haveOrder.OrderId == orderId && haveOrder.PaymentStatus.Value == 0 && haveOrder.TransactionType == 1)
            {
                return new ResponseBaseDto<SingleDirectionTicket>()
                {
                    Message = DescriptionUtility.GetDescription<ServiceStatus>("GetwaySuccess"),
                    Status = (int)ServiceStatus.GetwaySuccess,
                    Data = haveOrder
                };
            }
            else if (haveOrder != null && haveOrder.OrderId == orderId && haveOrder.Status == 0 && haveOrder.WalletReturnId.Value != 0 && haveOrder.TransactionType == 2)
            {
                return new ResponseBaseDto<SingleDirectionTicket>()
                {
                    Message = DescriptionUtility.GetDescription<ServiceStatus>("WalletSuccess"),
                    Status = (int)(int)ServiceStatus.WalletSuccess,
                    Data = haveOrder
                };
            }
            else if (haveOrder != null && haveOrder.OrderId == orderId && !haveOrder.WalletReturnId.HasValue)
            {
                return new ResponseBaseDto<SingleDirectionTicket>()
                {
                    Message = haveOrder.ResultMessage,
                    Status = haveOrder.Status.HasValue ? haveOrder.Status.Value : -10,
                    Data = haveOrder
                };
            }
            else
            {
                return new ResponseBaseDto<SingleDirectionTicket>()
                {
                    Message = haveOrder.ResultMessage,
                    Status = haveOrder.PaymentStatus.Value,
                    Data = haveOrder
                };
            }
        }
        #endregion

        #region ConfirmSingleDirectionTicket
        public async Task<bool> ConfirmSingleDirectionTicketAsync(SingleDirectionTicket singleDirectionTicket)
        {
            _logger.Log(singleDirectionTicket.Token, null, $"📥Step1 tosanServices(ConfirmsingleDirectionTicketAsync {JsonConvert.SerializeObject(singleDirectionTicket)}'", null);
            try
            {
                var Model = await _tosanSohaRepository.GetSingleDirectionTicketByIdAsync(singleDirectionTicket.OrderId.Value);
                if (singleDirectionTicket.PaymentStatus == 0)
                {
                    //Confirm GetWay
                    ConfirmPaymentRequestDto confirm = new ConfirmPaymentRequestDto()
                    {
                        LoginAccount = _config.GetSection("loginAccount").Value,
                        Token = singleDirectionTicket.Token.Value
                    };
                    var confirmResp = await _ipgServiceProxy.ConfirmPayment(confirm);
                    _logger.Log(singleDirectionTicket.Token, "📥Step2 tosanServices(ConfirmCsingleDirectionTicketAsync(ConfirmPayment)", JsonConvert.SerializeObject(confirmResp), null);

                    if (confirmResp != null && confirmResp.status == 0)
                    {
                        Charge charge = new Charge()
                        {
                            PaymentStatus = confirmResp.status,
                            RRN = singleDirectionTicket.RRN.Value,
                            PGWToken = singleDirectionTicket.Token.ToString()
                        };
                        var respUpdateCharge = await _walletRepository.UpdateChargeTable(charge);
                        if (!respUpdateCharge)
                        {
                            var try2UpdateCharge = await _walletRepository.UpdateChargeTable(charge);
                            _logger.Log(singleDirectionTicket.Token, $"📥Step3 tosanServices(ConfirmsingleDirectionTicketAsync(UpdateChargeTableUse(try2UpdateCharge={try2UpdateCharge}))", JsonConvert.SerializeObject(charge), null);
                        }
                        Model.Status = 0;
                        Model.PaymentStatus = confirmResp.status;
                        Model.BussinessDate = DateTime.Now;
                        Model.OrderId = singleDirectionTicket.OrderId;
                        Model.Token = singleDirectionTicket.Token;
                        Model.RRN = confirmResp.RRN;
                        Model.Amount = singleDirectionTicket.Amount;
                        Model.MaskCardNumber = confirmResp.CardNumberMasked;
                        Model.ResultMessage = $"سرویس درگاه : پرداخت با موفقیت انجام شد ";
                        var resultRepo = await _tosanSohaRepository.UpdateSingleDirectionTicketAsync(Model);
                        if (!resultRepo)
                        {
                            var res = await _tosanSohaRepository.UpdateSingleDirectionTicketAsync(Model);
                            return res;
                        }
                        else
                        {
                            return true;
                        }
                    }
                    else
                    {
                        Model.Status = confirmResp.status;
                        Model.PaymentStatus = confirmResp.status;
                        Model.BussinessDate = DateTime.Now;
                        Model.OrderId = singleDirectionTicket.OrderId;
                        Model.Token = confirmResp.Token;
                        Model.RRN = confirmResp.RRN;
                        Model.Amount = singleDirectionTicket.Amount;
                        Model.MaskCardNumber = confirmResp.CardNumberMasked;
                        Model.ResultMessage = $"سرویس درگاه : {confirmResp.status} ";
                        var resultRepo = await _tosanSohaRepository.UpdateSingleDirectionTicketAsync(Model);
                        if (!resultRepo)
                        {
                            var res = await _tosanSohaRepository.UpdateSingleDirectionTicketAsync(Model);
                            return res;
                        }
                        else
                        {
                            return true;
                        }
                    }
                }
                else
                {
                    Model.Status = -1;
                    Model.PaymentStatus = singleDirectionTicket.PaymentStatus;
                    Model.BussinessDate = DateTime.Now;
                    Model.OrderId = singleDirectionTicket.OrderId;
                    Model.Token = singleDirectionTicket.Token;
                    Model.ResultMessage = $"سرویس درگاه : پرداخت ناموفق";
                    var result = await _tosanSohaRepository.UpdateSingleDirectionTicketAsync(Model);
                    if (result)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Log(singleDirectionTicket.Token, $"📥 tosanServices (when got exception singleDirectionTicket)", JsonConvert.SerializeObject(ex.Message), ex);
                return false;
            }
        }

        #region [- ShetabSingleDirectionTicket -]
        [LoggingAspect]
        public async Task<ResponseBaseDto<GetIpgTokenResponseDto>> ShetabSingleDirectionTicket(SingleDirectionTicket payTosanSohaRequestDto)
        {
            try
            {
                var callbackUrl = "";

                var GetOrder = await _tosanSohaRepository.GetSingleDirectionTicketByExperssionAsync(n => n.OrderId == payTosanSohaRequestDto.OrderId &&
                                                                            n.UserId == payTosanSohaRequestDto.UserId);
                var haveOrder = GetOrder.FirstOrDefault();
                if (haveOrder == null)
                {

                    return new ResponseBaseDto<GetIpgTokenResponseDto>()
                    {
                        Message = DescriptionUtility.GetDescription<ServiceStatus>("InvalidOperationNotConfirmMobileNo"),
                        Status = (int)ServiceStatus.InvalidOperationNotConfirmMobileNo,
                        Data = null
                    };
                }

                if (haveOrder != null && haveOrder.OrderId == payTosanSohaRequestDto.OrderId && haveOrder.PaymentStatus.Value != 0)
                {
                    haveOrder.MerchantId = payTosanSohaRequestDto.MerchantId;
                    haveOrder.Amount = payTosanSohaRequestDto.Amount;
                    haveOrder.UserId = payTosanSohaRequestDto.UserId;
                    haveOrder.WalletId = payTosanSohaRequestDto.WalletId;
                    // haveOrder.TransactionType = payTosanSohaRequestDto.TransactionType;
                    //  haveOrder.CreateDate = DateTime.Now;
                    //  haveOrder.OrderId = payTosanSohaRequestDto.OrderId;
                    haveOrder.Status = PaymentStatus.ReadyToPay;
                    //  haveOrder.serialNo = payTosanSohaRequestDto.serialNo;

                    var ticketCardInfo = await _tosanSohaRepository.UpdateSingleDirectionTicketAsync(haveOrder);
                    if (!ticketCardInfo)
                    {
                        return new ResponseBaseDto<GetIpgTokenResponseDto>()
                        {
                            Message = DescriptionUtility.GetDescription<ServiceStatus>("InsertFaildData"),
                            Status = (int)ServiceStatus.InsertFaildData,
                            Data = null
                        };
                    }

                    if (haveOrder.MerchantId != 0)
                    {
                        callbackUrl = _setting.CallbackUrlSingleDirectionTicket + "?mid=" + haveOrder.MerchantId;
                    }
                    else
                    {
                        callbackUrl = _setting.CallbackUrlSingleDirectionTicket;
                    }


                    IpgSaleServiceRequestDto req = new IpgSaleServiceRequestDto()
                    {
                        AdditionalData = "خرید بلیت تک سفره_pecbms",
                        Amount = haveOrder.Amount.Value,
                        CallBackUrl = callbackUrl,
                        LoginAccount = _setting.TosanSohaLoginAccount,
                        OrderId = haveOrder.OrderId.Value,
                        Originator = haveOrder.MobileNo
                    };

                    //get ipg token
                    var saleToken = await _ipgServiceProxy.GetIpgSaleToken(req);

                    if (saleToken.status == 0)
                    {
                        //insert to charge for storage wallet
                        Guid obj = Guid.NewGuid();
                        ChargeDto charge = new ChargeDto()
                        {
                            Amount = req.Amount,
                            DestinationWalletId = _config.GetValue<string>("Proxy:Url:TosanSoha:MotherWalletSingleDirectionTicket"),
                            WalletType = _setting.ParrentWalletType,
                            CreateDate = DateTime.Now,
                            IsSendToSettlement = false,
                            WalletOrderId = haveOrder.OrderId.Value,
                            UserId = haveOrder.UserId.Value,
                            PGWToken = saleToken.Token.ToString(),
                            TrackingCode = obj.ToString(),
                            ApplicationId = 1,//تغییر یابد,

                            PaymentStatus = PaymentStatus.UnSuccess
                        };
                        var insertIntoChargeDb = await _walletServices.AddChargeRow(charge);
                        if (insertIntoChargeDb)
                        {
                            haveOrder.Token = saleToken.Token;
                            haveOrder.ResultMessage = DescriptionUtility.GetDescription<PaymentStatus>("GetTokenSuccess");
                            haveOrder.OrderId = haveOrder.OrderId;
                            haveOrder.PaymentStatus = (int)PaymentStatus.GetTokenSuccess;
                            haveOrder.BussinessDate = DateTime.Now;

                            await _tosanSohaRepository.UpdateSingleDirectionTicketAsync(haveOrder);

                            GetIpgTokenResponseDto OkGetWay = new GetIpgTokenResponseDto()
                            {
                                OrderId = haveOrder.OrderId.Value.ToString(),
                                Message = saleToken.Message,
                                Token = saleToken.Token
                            };

                            return new ResponseBaseDto<GetIpgTokenResponseDto>()
                            {
                                Status = (int)PaymentStatus.GetTokenSuccess,
                                Message = DescriptionUtility.GetDescription<PaymentStatus>("GetTokenSuccess"),
                                Data = OkGetWay
                            };
                        }
                        else
                        {
                            return new ResponseBaseDto<GetIpgTokenResponseDto>()
                            {
                                Message = DescriptionUtility.GetDescription<PaymentStatus>("NotInsertToCharge"),
                                Status = (int)PaymentStatus.NotInsertToCharge,
                                Data = null
                            };
                        }
                    }
                    else
                    {
                        return new ResponseBaseDto<GetIpgTokenResponseDto>()
                        {
                            Status = saleToken.status,
                            Message = saleToken.Message,
                            Data = null
                        };
                    }
                }
                else
                {
                    return new ResponseBaseDto<GetIpgTokenResponseDto>()
                    {
                        Status = (int)ServiceStatus.OrderIdNotValid,
                        Message = DescriptionUtility.GetDescription<ServiceStatus>("OrderIdNotValid"),
                        Data = null
                    };
                }
            }
            catch (Exception)
            {
                return new ResponseBaseDto<GetIpgTokenResponseDto>()
                {
                    Status = (int)ServiceStatus.OperationFaild,
                    Message = DescriptionUtility.GetDescription<ServiceStatus>("OperationFaild"),
                    Data = null
                };
            }
        }
        #endregion

        #region [- WalletSingleDirectionTicket -]
        [LoggingAspect]
        public async Task<ResponseBaseDto<GetIpgTokenResponseDto>> WalletSingleDirectionTicket(SingleDirectionTicket payTosanSohaRequestDto)
        {
            #region CheckDuplicateOrder
            var GetDuplicateOrderId = await _tosanSohaRepository.GetSingleDirectionTicketByExperssionAsync(
                                           n => n.OrderId == payTosanSohaRequestDto.OrderId
                                        && n.WalletReturnId != null
                                        && n.WalletReturnId != 0);
            var CheckDuplicateOrderId = GetDuplicateOrderId.FirstOrDefault();
            if (CheckDuplicateOrderId != null && CheckDuplicateOrderId.Status == 0)
            {
                return new ResponseBaseDto<GetIpgTokenResponseDto>()
                {
                    Status = (int)ServiceStatus.OrderIdPaied,
                    Message = DescriptionUtility.GetDescription<ServiceStatus>("OrderIdPaied"),
                    Data = null
                };
            }

            #endregion

            var GetOrderID = await _tosanSohaRepository.GetSingleDirectionTicketByExperssionAsync(n => n.OrderId == payTosanSohaRequestDto.OrderId);
            var haveOrderID = GetOrderID.FirstOrDefault();

            if (haveOrderID == null)
            {
                return new ResponseBaseDto<GetIpgTokenResponseDto>()
                {
                    Status = (int)ServiceStatus.OrderIdNotValid,
                    Message = DescriptionUtility.GetDescription<ServiceStatus>("OrderIdNotValid"),
                    Data = null
                };
            }
            GetIpgTokenResponseDto response = new GetIpgTokenResponseDto();
            WalletBalanceRequestDto walletBalance = new WalletBalanceRequestDto()
            {
                CorporationPIN = _setting.CorporationPIN,
                WalletId = payTosanSohaRequestDto.WalletId.Value,
            };
            var walletBalanceResponse = await _walletServices.GetWalletBalance(walletBalance);
            if (walletBalanceResponse.ResultId != 0)
            {
                return new ResponseBaseDto<GetIpgTokenResponseDto>()
                {
                    Message = walletBalanceResponse.ResultDesc,
                    Status = walletBalanceResponse.ResultId,
                    Data = null
                };
            }
            else
            {
                var walletAmount = Convert.ToInt64(walletBalanceResponse.Amount);
                if (!(walletAmount > payTosanSohaRequestDto.Amount))
                {
                    haveOrderID.MerchantId = payTosanSohaRequestDto.MerchantId;
                    haveOrderID.Amount = payTosanSohaRequestDto.Amount;
                    haveOrderID.UserId = payTosanSohaRequestDto.UserId;
                    haveOrderID.WalletId = payTosanSohaRequestDto.WalletId;
                    haveOrderID.TransactionType = payTosanSohaRequestDto.TransactionType;
                    haveOrderID.BussinessDate = DateTime.Now;
                    haveOrderID.OrderId = payTosanSohaRequestDto.OrderId;
                    haveOrderID.Status = PaymentStatus.NotEnoughWalletBalance;

                    var updateNajaWageRequest = await _tosanSohaRepository.UpdateSingleDirectionTicketAsync(haveOrderID);

                    if (!updateNajaWageRequest)
                    {
                        return new ResponseBaseDto<GetIpgTokenResponseDto>()
                        {
                            Status = (int)ServiceStatus.InsertFaildData,
                            Message = DescriptionUtility.GetDescription<ServiceStatus>("InsertFaildData"),
                            Data = null
                        };
                    }

                    return new ResponseBaseDto<GetIpgTokenResponseDto>()
                    {
                        Status = (int)ServiceStatus.NotEnoughWalletBalance,
                        Message = DescriptionUtility.GetDescription<ServiceStatus>("NotEnoughWalletBalance"),
                        Data = null
                    };

                }
                if (payTosanSohaRequestDto.OrderId <= 0)
                {
                    return new ResponseBaseDto<GetIpgTokenResponseDto>()
                    {
                        Status = (int)ServiceStatus.OrderIdNotValid,
                        Message = DescriptionUtility.GetDescription<ServiceStatus>("OrderIdNotValid"),
                        Data = null
                    };
                }
                if (haveOrderID != null)
                {
                    haveOrderID.MerchantId = payTosanSohaRequestDto.MerchantId;
                    haveOrderID.Amount = payTosanSohaRequestDto.Amount;
                    haveOrderID.UserId = payTosanSohaRequestDto.UserId;
                    haveOrderID.WalletId = payTosanSohaRequestDto.WalletId;
                    haveOrderID.TransactionType = payTosanSohaRequestDto.TransactionType;
                    haveOrderID.BussinessDate = DateTime.Now;
                    haveOrderID.OrderId = payTosanSohaRequestDto.OrderId;
                    haveOrderID.Status = PaymentStatus.ReadyToPayWithWallet;

                    var updateTicketCardRequest = await _tosanSohaRepository.UpdateSingleDirectionTicketAsync(haveOrderID);
                    if (!updateTicketCardRequest)
                    {
                        return new ResponseBaseDto<GetIpgTokenResponseDto>()
                        {
                            Status = (int)ServiceStatus.InsertFaildData,
                            Message = DescriptionUtility.GetDescription<ServiceStatus>("InsertFaildData"),
                            Data = null
                        };
                    }

                    response.OrderId = haveOrderID.OrderId.Value.ToString();
                    response.Message = "پرداخت بلیت تک سفره با موفقیت از کیف پول شما کسر گردید";

                    WalletTransferResquestDto transferRequestDto = new WalletTransferResquestDto()
                    {
                        AdditionalData = "پرداخت بلیت تک سفره از کیف پول",
                        Amount = haveOrderID.Amount.Value,
                        //call back pay bill
                        CallBackURL = _setting.CallBackUrlParentWallet,
                        CorporationPIN = _setting.CorporationPIN,
                        DestinationWalletId = Convert.ToInt64(_config.GetSection("Proxy:Url:TosanSoha:TosanSohaMotherWalletId").Value),
                        IpAddress = "172.30.2.155",
                        MediaTypeId = _setting.WalletMediaType,
                        OrderId = haveOrderID.OrderId.Value,
                        PIN = "234",
                        SourceWalletId = haveOrderID.WalletId.Value,
                        TransactionDateTime = DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss")
                    };
                    var paymentProcess = await _walletServices.WalletTransfer(transferRequestDto);
                    if (paymentProcess != null && paymentProcess.Status == 0)
                    {
                        haveOrderID.Status = PaymentStatus.Success;
                        haveOrderID.PaymentStatus = PaymentStatus.Success;
                        haveOrderID.BussinessDate = DateTime.Now;
                        haveOrderID.WalletReturnId = paymentProcess.Data.ReturnId;
                        haveOrderID.Token = paymentProcess.Data.ReturnId;
                        haveOrderID.ResultMessage = $"{paymentProcess.Message} : سرویس کیف پول";
                        var updateDb = await _tosanSohaRepository.UpdateSingleDirectionTicketAsync(haveOrderID);
                        if (updateDb)
                        {
                            response.Token = paymentProcess.Data.ReturnId;
                            return new ResponseBaseDto<GetIpgTokenResponseDto>()
                            {
                                Message = paymentProcess.Message,
                                Status = 0,
                                Data = response
                            };
                        }
                        else
                        {
                            return new ResponseBaseDto<GetIpgTokenResponseDto>()
                            {
                                Status = (int)ServiceStatus.PaymentSuccessButOperationFaild,
                                Message = DescriptionUtility.GetDescription<ServiceStatus>("PaymentSuccessButOperationFaild"),
                                Data = null
                            };
                        }
                    }
                    else
                    {
                        return new ResponseBaseDto<GetIpgTokenResponseDto>()
                        {
                            Message = paymentProcess.Message,
                            Status = (int)ServiceStatus.ErrorInWalletService,
                            Data = null
                        };
                    }

                }
                else
                {
                    return new ResponseBaseDto<GetIpgTokenResponseDto>()
                    {
                        Status = (int)ServiceStatus.OrderIdNotValid,
                        Message = DescriptionUtility.GetDescription<ServiceStatus>("OrderIdNotValid"),
                        Data = null
                    };
                }
            }
        }
        #endregion

        #endregion

        #region Proxy
        [LoggingAspect]
        public Task<ClientBalance> ClientBalance(string clientUniqueId)
        {
            return _tosanSohaServiceProxy.ClientBalance(clientUniqueId);
        }
        [LoggingAspect]
        public async Task<List<SingleVoucherPriceResponseDto>> VoucherPriceInquiry()
        {
            var cacheKey = "VoucherPriceInquiry";
            //checks if cache entries exists
            if (!_Cache.TryGetValue(cacheKey, out List<SingleVoucherPriceResponseDto> VoucherPriceList))
            {

                var result = new List<SingleVoucherPriceResponseDto>();

                foreach (var item in VoucherMunicipalCode.VoucherMunicipalCodeMembers)
                {
                    var resProxy = await _tosanSohaServiceProxy.VoucherPriceInquiry(item.Value);
                    result.Add(new SingleVoucherPriceResponseDto()
                    {
                        IsActive = resProxy.price > 0,
                        VoucherMunicipalCode = item.Value,
                        Price = resProxy?.price.ToString("#,#"),
                        Title = item.Key
                    });
                }

                var cacheExpiryOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpiration = DateTime.Now.AddMinutes(5),
                    Priority = CacheItemPriority.High,
                    SlidingExpiration = TimeSpan.FromMinutes(20)
                };
                //setting cache entries
                _Cache.Set(cacheKey, result, cacheExpiryOptions);

                return result;
            }
            return VoucherPriceList;
        }

        [LoggingAspect]
        public Task<SingleDirectVoucherSellResponseDto> VoucherSell(SingleDirectVoucherSellRequestDto request)
        {
            return _tosanSohaServiceProxy.VoucherSell(request);
        }
        [LoggingAspect]
        public Task<SingleDirectVoucherSellResponseDto> VoucherSellInquiry(InquirySellTicketRequestDto request)
        {
            return _tosanSohaServiceProxy.VoucherSellInquiry(request);
        }
        [LoggingAspect]
        public Task<VoucherHistoryResponseDto> VoucherHistory(VoucherHistoryRequestDto request)
        {
            return _tosanSohaServiceProxy.VoucherHistory(request);
        }
        #endregion

        #region Repository
        public async Task<List<SingleDirectionTicket>> GetSingleDirectionTicketsAsync(long userId)
        {
            return await _tosanSohaRepository.GetSingleDirectionTicketsAsync(userId);
        }
        public async Task<bool> AddSingleDirectionTicket(SingleDirectionTicket singleDirectionTicket)
        {
            return await _tosanSohaRepository.AddSingleDirectionTicket(singleDirectionTicket);
        }
        public async Task<SingleDirectionTicket> GetSingleDirectionTicketByIdAsync(long CardId)
        {
            return await _tosanSohaRepository.GetSingleDirectionTicketByIdAsync(CardId);
        }

        public async Task<bool> UpdateSingleDirectionTicketAsync(SingleDirectionTicket singleDirectionTicket)
        {
            return await _tosanSohaRepository.UpdateSingleDirectionTicketAsync(singleDirectionTicket);
        }
        public async Task<bool> UpdateSingleDirectionTicketRequestAsync(long orderId)
        {
            return await _tosanSohaRepository.UpdateSingleDirectionTicketRequestAsync(orderId);
        }
        public async Task<List<SingleDirectionTicket>> GetSingleDirectionTicketByExperssionAsync(Expression<Func<SingleDirectionTicket, bool>> predicate)
        {
            return await _tosanSohaRepository.GetSingleDirectionTicketByExperssionAsync(predicate);
        }
        #endregion

        #endregion

    }

    public interface ITosanSohaServices
    {
        #region ClientCards
        Task<bool> UpdateTicketCard(TicketCard myCard);
        Task<List<TicketCard>> GetTicketCardsAsync(long userId);
        Task<ResponseBaseDto> AddTicketCard(TicketCard myCard);
        Task<TicketCard> GetTicketCardByIdAsync(long CardId);

        Task<bool> DeleteTicketCardAsync(long CardId);
        #endregion

        #region GateChargeCard

        #region InquiryChargeCard
        Task<ResponseBaseDto<TicketCardInfo>> InquiryChargeCardByOrderIdAsync(long orderId, long userId);
        #endregion

        #region Confirm
        Task<bool> ConfirmChargeCardAsync(TicketCardInfo ticketCardInfo);
        #endregion

        #region Proxy
        Task<TicketGetBalanceDto> GetBalanceCompanyCard();
        Task<IsValidCardTypeDto> IsValidCardType(long? cardserial);
        Task<CreateVoucherResponseDto> CreateVoucher(CreateVoucherRequestDto request);
        Task<VoucherInfoListResponseDto> VoucherInfoList(VoucherListRequestDto request, string cardSerial);
        Task<ResponseBaseDto<PayWageResponseDto>> ShetabPayTicketCardAsync(PayTosanSohaRequestDto payTosanSohaRequestDto);
        Task<ResponseBaseDto<PayWageResponseDto>> WalletPayTicketCardAsync(PayTosanSohaRequestDto payTosanSohaRequestDto);
        #endregion

        #region Repository
        Task<List<TicketCardInfo>> GetTicketCardsInfoAsync(long userId);
        Task<bool> AddTicketCardInfo(TicketCardInfo ticketCardInfo);
        Task<TicketCardInfo> GetTicketCardInfoByIdAsync(long CardId);
        Task<bool> UpdateTicketCardInfoAsync(TicketCardInfo ticketCardInfo);
        Task<bool> UpdateTicketCardInfoRequestAsync(long orderId);
        Task<List<TicketCardInfo>> GetTicketCardInfoByExperssionAsync(Expression<Func<TicketCardInfo, bool>> predicate);
        #endregion

        #endregion

        #region SingleDirectTicket
        #region Proxy
        Task<ResponseBaseDto<GetIpgTokenResponseDto>> ShetabSingleDirectionTicket(SingleDirectionTicket payTosanSohaRequestDto);
        Task<ResponseBaseDto<GetIpgTokenResponseDto>> WalletSingleDirectionTicket(SingleDirectionTicket payTosanSohaRequestDto);
        Task<ClientBalance> ClientBalance(string clientUniqueId);
        Task<List<SingleVoucherPriceResponseDto>> VoucherPriceInquiry();
        Task<SingleDirectVoucherSellResponseDto> VoucherSell(SingleDirectVoucherSellRequestDto request);
        Task<SingleDirectVoucherSellResponseDto> VoucherSellInquiry(InquirySellTicketRequestDto request);
        Task<VoucherHistoryResponseDto> VoucherHistory(VoucherHistoryRequestDto request);
        #endregion

        #region Repository
        Task<PagedResponse<List<SingleTicketResponsePaginated>>> GetPaginatedSingleTicketAsync(PaginationFilterDto paginationFilter);

        Task<List<SingleDirectionTicket>> GetSingleDirectionTicketsAsync(long userId);
        Task<bool> AddSingleDirectionTicket(SingleDirectionTicket singleDirectionTicket);
        Task<SingleDirectionTicket> GetSingleDirectionTicketByIdAsync(long CardId);

        Task<bool> UpdateSingleDirectionTicketAsync(SingleDirectionTicket singleDirectionTicket);
        Task<bool> UpdateSingleDirectionTicketRequestAsync(long orderId);
        Task<List<SingleDirectionTicket>> GetSingleDirectionTicketByExperssionAsync(Expression<Func<SingleDirectionTicket, bool>> predicate);
        #endregion

        #region InquiryChargeCard
        Task<ResponseBaseDto<SingleDirectionTicket>> InquirySingleDirectionTicketByOrderIdAsync(long orderId, long userId);
        #region Confirm
        Task<bool> ConfirmSingleDirectionTicketAsync(SingleDirectionTicket singleDirectionTicket);
        #endregion
        #endregion

        #endregion

    }
}
