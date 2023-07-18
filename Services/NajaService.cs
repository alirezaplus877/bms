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
using Microsoft.EntityFrameworkCore;
using static ProxyService.services.WalletProxy;
using Pigi.MDbLogging;
using Newtonsoft.Json;

namespace Application.Services
{
    [ScopedService]
    public class NajaServices : INajaServices, ILoggable
    {
        #region Private Variable
        private readonly IServiceProvider _serviceProvider;
        private readonly IMapper _mapper;
        private readonly IWalletProxy _proxy;
        private readonly IPecBmsSetting _setting;
        private readonly IIpgServiceProxy _ipgServiceProxy;
        private readonly INajaServiceProxy _najaServiceProxy;
        private readonly IWalletRepository _walletRepository;
        private readonly INajaRepository _najaRepository;
        private readonly IPaymentDbRepository _paymentDbRepository;
        private readonly IWalletServices _walletServices;
        private readonly IMdbLogger<NajaServices> _logger;
        #endregion

        #region ctor
        public NajaServices(IServiceProvider serviceProvider,
            IWalletServices walletServices,
            INajaServiceProxy najaServiceProxy,
            IPecBmsSetting pecBmsSetting, IMapper mapper,
            INajaRepository najaRepository,
            IWalletProxy proxy,
            IIpgServiceProxy ipgServiceProxy,
            IMdbLogger<NajaServices> logger,
            IWalletRepository walletRepository,
            IPaymentDbRepository paymentDbRepository)
        {
            _serviceProvider = serviceProvider;
            _setting = pecBmsSetting;
            _mapper = mapper;
            _najaRepository = najaRepository;
            _walletRepository = walletRepository;
            _logger = logger;
            _ipgServiceProxy = ipgServiceProxy;
            _walletServices = walletServices;
            _najaServiceProxy = najaServiceProxy;
            _proxy = proxy;
            _paymentDbRepository = paymentDbRepository;
        }
        #endregion

        #region [- Repository -]
        [LoggingAspect]
        public async Task<ResponseBaseDto<NajaWageDto>> GetNajaWageViolationImageAsync(long orderId, long userId)
        {
            var najaWage = await _najaRepository.GetNajaWageForViolationImageAsync(orderId, userId);
            if (najaWage != null)
            {
                var data = _mapper.Map<NajaWageDto>(najaWage);
                if (data.Status == 0 || data.PaymentStatus == 0)
                {
                    return new ResponseBaseDto<NajaWageDto>()
                    {
                        Message = DescriptionUtility.GetDescription<ServiceStatus>("Success"),
                        Status = (int)ServiceStatus.Success,
                        Data = data
                    };
                }
                else
                {
                    return new ResponseBaseDto<NajaWageDto>()
                    {
                        Message = DescriptionUtility.GetDescription<ServiceStatus>("NotValidPayment"),
                        Status = (int)ServiceStatus.NotValidPayment,
                        Data = null
                    };
                }

            }
            else
            {
                return new ResponseBaseDto<NajaWageDto>()
                {
                    Message = DescriptionUtility.GetDescription<ServiceStatus>("OrderIdNotValid"),
                    Status = (int)ServiceStatus.OrderIdNotValid,
                    Data = null
                };
            }
        }

        public async Task<NajaWageDto> GetNajaWageByOrderIdAsync(long orderId)
        {
            var najaWage = await _najaRepository.GetNajaWageByOrderIdAsync(orderId);
            return _mapper.Map<NajaWageDto>(najaWage);
        }

        public async Task<List<NajaWageDto>> GetAllNajaPaysAsync()
        {
            var najaWage = await _najaRepository.GetAllNajaWagesAsync();
            return _mapper.Map<List<NajaWageDto>>(najaWage);
        }

        public async Task<bool> AddNajaWageAsync(NajaWageDto najaPay)
        {
            var najaWage = _mapper.Map<NajaWage>(najaPay);
            if (najaWage != null && najaWage.MerchantId.HasValue && najaWage.MerchantId.Value > 0)
            {
                //organisationId must read web config
                if (najaWage.MerchantId > 0)
                {
                    var merchantProvider = await _paymentDbRepository.GetMerchantProvider(p => p.MerchantId == najaWage.MerchantId && p.OrgId == _setting.NajaMerchantProvider);
                    if (merchantProvider != null)
                    {
                        najaWage.MIncome = merchantProvider.Payment;
                        najaWage.IsSendToSettelment = false;
                    }
                }
            }
            return await _najaRepository.AddNajaWageAsync(najaWage);
        }

        public async Task<bool> UpdateNajaWageAsync(NajaWageDto najaPay)
        {
            var najaWage = _mapper.Map<NajaWage>(najaPay);

            return await _najaRepository.UpdateNajaWageAsync(najaWage);
        }

        public async Task<List<NajaWageDto>> GetNajaWageByExperssionAsync(Expression<Func<NajaWage, bool>> predicate)
        {

            var listNaja = await _najaRepository.GetNajaWageByExperssionAsync(predicate);

            return _mapper.Map<List<NajaWageDto>>(listNaja);
        }

        [LoggingAspect]
        public async Task<PagedResponse<List<NajaWageDtoResponsePaginated>>> GetPaginatedNajaWageAsync(PaginationFilterDto paginationFilter)
        {
            if (paginationFilter.ToDate.HasValue && paginationFilter.FromDate.HasValue)
            {
                var validdate = paginationFilter.ToDate < paginationFilter.FromDate;
                if (validdate)
                {
                    return null;
                }
                var validFilter = new PaginationFilterDto(paginationFilter.PageNumber, paginationFilter.PageSize, paginationFilter.FromDate.Value, paginationFilter.ToDate.Value);
                var pagedData = await _najaRepository.GetPaginatedNajaWageAsync(paginationFilter.UserId);
                pagedData = pagedData.Where(d => d.BussinessDate >= validFilter.FromDate && d.BussinessDate <= validFilter.ToDate).ToList();
                var totalRecords = pagedData.Count();
                var Divide = decimal.Divide(totalRecords, validFilter.PageSize);
                var totalPages = (int)Math.Ceiling(Divide);
                pagedData = pagedData.Skip((validFilter.PageNumber - 1) * validFilter.PageSize)
                    .Take(validFilter.PageSize).ToList();
                return new PagedResponse<List<NajaWageDtoResponsePaginated>>(pagedData,
                    validFilter.PageNumber,
                    validFilter.PageSize,
                    totalPages,
                    totalRecords);
            }
            else
            {
                var validFilter = new PaginationFilterDto(paginationFilter.PageNumber, paginationFilter.PageSize);
                var pagedData = await _najaRepository.GetPaginatedNajaWageAsync(paginationFilter.UserId);
                var totalRecords = pagedData.Count;
                var Divide = decimal.Divide(totalRecords, validFilter.PageSize);
                var totalPages = (int)Math.Ceiling(Divide);
                pagedData = pagedData.Skip((validFilter.PageNumber - 1) * validFilter.PageSize)
                    .Take(validFilter.PageSize).ToList();
                return new PagedResponse<List<NajaWageDtoResponsePaginated>>(pagedData.ToList(),
                    validFilter.PageNumber,
                    validFilter.PageSize,
                    totalPages,
                    totalRecords);
            }
        }
        #endregion

        #region [- InquiryWageByOrderId -]
        [LoggingAspect]
        public async Task<ResponseBaseDto<NajaWageDto>> InquiryWageByOrderIdAsync(long orderId, long userId)
        {
            var GetOrder = await _najaRepository.GetNajaWageByExperssionAsync(n => n.OrderId == orderId &&
                                                                            n.UserId == userId);
            var haveOrder = GetOrder.FirstOrDefault();
            if (haveOrder == null/* && haveOrder.MobileEnabled.HasValue*/)
            {
                //    if (!haveOrder.MobileEnabled.Value)
                //    {
                //        return new ResponseBaseDto<NajaWageDto>()
                //        {
                //            Message = DescriptionUtility.GetDescription<ServiceStatus>("InvalidOperationNotConfirmMobileNo"),
                //            Status = (int)ServiceStatus.InvalidOperationNotConfirmMobileNo,
                //            Data = null
                //        };
                //    }
                //}
                //else
                //{
                return new ResponseBaseDto<NajaWageDto>()
                {
                    Message = DescriptionUtility.GetDescription<ServiceStatus>("OrderIdNotValid"),
                    Status = (int)ServiceStatus.OrderIdNotValid,
                    Data = null
                };
            }
            var najawage = _mapper.Map<NajaWageDto>(haveOrder);

            if (haveOrder != null && haveOrder.OrderId == orderId && haveOrder.PaymentStatus.Value == 0 && haveOrder.TransactionType == 1)
            {
                return new ResponseBaseDto<NajaWageDto>()
                {
                    Message = DescriptionUtility.GetDescription<ServiceStatus>("GetwaySuccess"),
                    Status = (int)ServiceStatus.GetwaySuccess,
                    Data = najawage
                };
            }
            else if (haveOrder != null && haveOrder.OrderId == orderId && haveOrder.Status == 0 && haveOrder.WalletReturnId.Value != 0 && haveOrder.TransactionType == 2)
            {
                return new ResponseBaseDto<NajaWageDto>()
                {
                    Message = DescriptionUtility.GetDescription<ServiceStatus>("WalletSuccess"),
                    Status = (int)(int)ServiceStatus.WalletSuccess,
                    Data = najawage
                };
            }
            else if (haveOrder != null && haveOrder.OrderId == orderId && !haveOrder.WalletReturnId.HasValue)
            {
                return new ResponseBaseDto<NajaWageDto>()
                {
                    Message = haveOrder.ServiceMessage,
                    Status = haveOrder.Status.HasValue ? haveOrder.Status.Value : -10,
                    Data = najawage
                };
            }
            else
            {
                return new ResponseBaseDto<NajaWageDto>()
                {
                    Message = haveOrder.ServiceMessage,
                    Status = haveOrder.PaymentStatus.Value,
                    Data = najawage
                };
            }
        }
        #endregion

        #region [- ShetabPayWage -]
        [LoggingAspect]
        public async Task<ResponseBaseDto<PayWageResponseDto>> ShetabPayWageAsync(PayWageRequestDto payWageRequest)
        {
            try
            {
                PayWageResponseDto response = new PayWageResponseDto();
                var callbackUrl = "";

                var GetOrder = await _najaRepository.GetNajaWageByExperssionAsync(n => n.OrderId == payWageRequest.OrderId &&
                                                                            n.UserId == payWageRequest.UserId);
                var haveOrder = GetOrder.FirstOrDefault();
                if (haveOrder == null /*&& haveOrder.MobileEnabled.HasValue*/)
                {
                    //    if (!haveOrder.MobileEnabled.Value)
                    //    {
                    //        return new ResponseBaseDto<PayWageResponseDto>()
                    //        {

                    //            Message = DescriptionUtility.GetDescription<ServiceStatus>("InvalidOperationNotConfirmMobileNo"),
                    //            Status = (int)ServiceStatus.InvalidOperationNotConfirmMobileNo,
                    //            Data = null
                    //        };
                    //    }
                    //}
                    //else
                    //{
                    return new ResponseBaseDto<PayWageResponseDto>()
                    {
                        Message = DescriptionUtility.GetDescription<ServiceStatus>("InvalidOperationNotConfirmMobileNo"),
                        Status = (int)ServiceStatus.InvalidOperationNotConfirmMobileNo,
                        Data = null
                    };
                }

                if (haveOrder != null && haveOrder.OrderId == payWageRequest.OrderId && haveOrder.PaymentStatus.Value != 0)
                {
                    haveOrder.MerchantId = payWageRequest.MerchantId;
                    haveOrder.Amount = payWageRequest.Amount;
                    haveOrder.UserId = payWageRequest.UserId;
                    haveOrder.WalletId = payWageRequest.WalletId;
                    haveOrder.TransactionType = payWageRequest.TransactionType;
                    haveOrder.CreateDate = DateTime.Now;
                    haveOrder.BussinessDate = DateTime.Now;
                    haveOrder.OrderId = payWageRequest.OrderId;
                    haveOrder.Status = PaymentStatus.ReadyToPay;
                    haveOrder.NajaType = payWageRequest.NajaType;
                    haveOrder.NajaServiceData = payWageRequest.NajaServiceData;

                    var updateNajaPay = await _najaRepository.UpdateNajaWageAsync(haveOrder);
                    if (!updateNajaPay)
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
                        callbackUrl = _setting.CallBackUrlShetabNajaWage + "?mid=" + haveOrder.MerchantId;
                    }
                    else
                    {
                        callbackUrl = _setting.CallBackUrlShetabNajaWage;
                    }

                    IpgSaleServiceRequestDto req = new IpgSaleServiceRequestDto()
                    {
                        AdditionalData = "پرداخت کارمزد ناجا_pecbms",
                        Amount = (haveOrder.Amount ?? 52000),
                        CallBackUrl = callbackUrl,
                        LoginAccount = _setting.NajaLoginAccount,
                        OrderId = haveOrder.OrderId.Value,
                        Originator = haveOrder.Mobile
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
                            DestinationWalletId = _setting.NajaMotherWallet.ToString(),
                            WalletType = _setting.ParrentWalletType,
                            CreateDate = DateTime.Now,
                            IsSendToSettlement = false,
                            WalletOrderId = haveOrder.OrderId.Value,
                            UserId = haveOrder.UserId.Value,
                            PGWToken = saleToken.Token.ToString(),
                            TrackingCode = obj.ToString(),
                            ApplicationId = payWageRequest.ApplicationId,
                            PaymentStatus = PaymentStatus.UnSuccess
                        };
                        var insertIntoChargeDb = await _walletServices.AddChargeRow(charge);
                        if (insertIntoChargeDb)
                        {
                            haveOrder.PgwToken = saleToken.Token;
                            haveOrder.ServiceMessage = DescriptionUtility.GetDescription<PaymentStatus>("GetTokenSuccess");
                            haveOrder.OrderId = haveOrder.OrderId;
                            haveOrder.PaymentStatus = (int)PaymentStatus.GetTokenSuccess;
                            haveOrder.BussinessDate = DateTime.Now;

                            await _najaRepository.UpdateNajaWageAsync(haveOrder);

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
        public async Task<ResponseBaseDto<PayWageResponseDto>> WalletPayWageAsync(PayWageRequestDto payWageRequest)
        {
            #region CheckDuplicateOrder
            var GetDuplicateOrderId = await _najaRepository.GetNajaWageByExperssionAsync(
                                           n => n.OrderId == payWageRequest.OrderId
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

            //if (CheckDuplicateOrderId != null && !CheckDuplicateOrderId.MobileEnabled.Value)
            //{
            //    return new ResponseBaseDto<PayWageResponseDto>()
            //    {
            //        Status = (int)ServiceStatus.InvalidOperationNotConfirmMobileNo,
            //        Message = DescriptionUtility.GetDescription<ServiceStatus>("InvalidOperationNotConfirmMobileNo"),
            //        Data = null
            //    };
            //}
            #endregion

            var GetOrderID = await _najaRepository.GetNajaWageByExperssionAsync(n => n.OrderId == payWageRequest.OrderId);
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
                WalletId = payWageRequest.WalletId,
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
                if (!(walletAmount > payWageRequest.Amount))
                {
                    haveOrderID.MerchantId = payWageRequest.MerchantId;
                    haveOrderID.Amount = payWageRequest.Amount;
                    haveOrderID.UserId = payWageRequest.UserId;
                    haveOrderID.WalletId = payWageRequest.WalletId;
                    haveOrderID.TransactionType = payWageRequest.TransactionType;
                    haveOrderID.BussinessDate = DateTime.Now;
                    haveOrderID.OrderId = payWageRequest.OrderId;
                    haveOrderID.Status = PaymentStatus.NotEnoughWalletBalance;
                    haveOrderID.NajaType = payWageRequest.NajaType;
                    haveOrderID.NajaServiceData = payWageRequest.NajaServiceData;

                    var updateNajaWageRequest = await _najaRepository.UpdateNajaWageAsync(haveOrderID);

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
                if (payWageRequest.OrderId <= 0)
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
                    haveOrderID.MerchantId = payWageRequest.MerchantId;
                    haveOrderID.Amount = payWageRequest.Amount;
                    haveOrderID.UserId = payWageRequest.UserId;
                    haveOrderID.WalletId = payWageRequest.WalletId;
                    haveOrderID.TransactionType = payWageRequest.TransactionType;
                    haveOrderID.BussinessDate = DateTime.Now;
                    haveOrderID.OrderId = payWageRequest.OrderId;
                    haveOrderID.Status = PaymentStatus.ReadyToPayWithWallet;
                    haveOrderID.NajaType = payWageRequest.NajaType;
                    haveOrderID.NajaServiceData = payWageRequest.NajaServiceData;

                    var updateNajaWageRequest = await _najaRepository.UpdateNajaWageAsync(haveOrderID);
                    if (!updateNajaWageRequest)
                    {
                        return new ResponseBaseDto<PayWageResponseDto>()
                        {
                            Status = (int)ServiceStatus.InsertFaildData,
                            Message = DescriptionUtility.GetDescription<ServiceStatus>("InsertFaildData"),
                            Data = null
                        };
                    }

                    response.OrderId = haveOrderID.OrderId.Value.ToString();
                    response.Message = "پرداخت کارمزد با موفقیت از کیف پول شما کسر گردید";

                    WalletTransferResquestDto transferRequestDto = new WalletTransferResquestDto()
                    {
                        AdditionalData = "پرداخت کارمزد ناجا از کیف پول",
                        Amount = haveOrderID.Amount.Value,
                        //call back pay bill
                        CallBackURL = _setting.CallBackUrlParentWallet,
                        CorporationPIN = _setting.CorporationPIN,
                        DestinationWalletId = _setting.NajaMotherWalletId,//// این باید شناسه کیف پول مخزن ناجا باشه
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
                        haveOrderID.ServiceMessage = $"{paymentProcess.Message} : سرویس کیف پول";
                        var updateDb = await _najaRepository.UpdateNajaWageAsync(haveOrderID);
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

        #region [- ViolationInquiry -]
        [LoggingAspect]
        public async Task<NajaResponseDtoGeneric<ViolationInquiryResponseDto>> ViolationInquiryAsync(ViolationInquiryRequestDto violationInquiryDto)
        {
            var checkInfo =
               await _najaServiceProxy.CheckInfo(
                    new CheckInfoRequestDto
                    {
                        MobileNumber = violationInquiryDto.MobileNumber,
                        NationalCode = violationInquiryDto.NationalCode
                    });
            if (checkInfo.Status == 0)
            {
                _logger.Log(663, null, $"📥 Before NajaServices(ViolationInquiry {JsonConvert.SerializeObject(violationInquiryDto)}'", null);

                var violationinquiry = await _najaServiceProxy.ViolationInquiry(violationInquiryDto);
                _logger.Log(663, null, $"📥 After NajaServices(ViolationInquiry {JsonConvert.SerializeObject(violationinquiry)}'", null);

                if (violationinquiry.Status == 0)
                {
                    return new NajaResponseDtoGeneric<ViolationInquiryResponseDto>(
                        (ViolationInquiryResponseDto)violationinquiry.Data,
                                                    violationinquiry.Message,
                                                    violationinquiry.Status,
                                                    violationinquiry.TraceId,
                                                    violationinquiry.RefId
                        );
                }
                else if (violationinquiry.Status == -51)
                {
                    //استعلام بهزاد
                    var getInquiry = await _najaServiceProxy.GetInquiry(new GetInquiryRequestDto() { TrackingNo = violationInquiryDto.TrackingNo, TraceId = 0 });
                    if (getInquiry.Status == 0)
                    {
                        var CommingOnInquiry = (ViolationInquiryResponseDto)getInquiry.Data;
                        return new NajaResponseDtoGeneric<ViolationInquiryResponseDto>()
                        {
                            Data = CommingOnInquiry,
                            Message = getInquiry.Message,
                            TraceId = getInquiry.TraceId,
                            RefId = getInquiry.RefId,
                            Status = getInquiry.Status
                        };
                    }
                    else if (getInquiry.Status == 502)
                    {
                        return new NajaResponseDtoGeneric<ViolationInquiryResponseDto>()
                        {
                            Data = null,
                            Message = "شماره پلاک با کد ملی تطابق ندارد",
                            TraceId = getInquiry.TraceId,
                            RefId = getInquiry.RefId,
                            Status = getInquiry.Status
                        };
                    }
                    else
                    {
                        return new NajaResponseDtoGeneric<ViolationInquiryResponseDto>()
                        {
                            Data = null,
                            Message = getInquiry.Message,
                            TraceId = getInquiry.TraceId,
                            RefId = getInquiry.RefId,
                            Status = getInquiry.Status
                        };
                    }

                }
                else if (violationinquiry.Status == 502)
                {
                    return new NajaResponseDtoGeneric<ViolationInquiryResponseDto>()
                    {
                        Data = null,
                        Message = "شماره پلاک با کد ملی تطابق ندارد",
                        TraceId = violationinquiry.TraceId,
                        RefId = violationinquiry.RefId,
                        Status = violationinquiry.Status
                    };
                }
                else
                {
                    return new NajaResponseDtoGeneric<ViolationInquiryResponseDto>()
                    { Status = violationinquiry.Status, Message = violationinquiry.Message };
                }
            }
            else
            {
                return new NajaResponseDtoGeneric<ViolationInquiryResponseDto>()
                {
                    Status = checkInfo.Status,
                    Data = null,
                    Message = checkInfo.Message,
                    RefId = checkInfo.RefId,
                    TraceId = checkInfo.TraceId
                };
            }

        }
        #endregion

        #region [- GetInquiry -]
        [LoggingAspect]
        public async Task<NajaResponseDtoGeneric<object>> GetInquiry(GetInquiryRequestDto violationInquiryDto)
        {
            //استعلام بهزاد
            var getInquiry = await _najaServiceProxy.GetInquiry(new GetInquiryRequestDto() { TrackingNo = violationInquiryDto.TrackingNo, TraceId = 0 });
            if (getInquiry.Status == 0)
            {
                var CommingOnInquiry = getInquiry.Data;
                return new NajaResponseDtoGeneric<object>()
                {
                    Data = CommingOnInquiry,
                    Message = getInquiry.Message,
                    TraceId = getInquiry.TraceId,
                    RefId = getInquiry.RefId,
                    Status = getInquiry.Status
                };
            }
            else if (getInquiry.Status == 502)
            {
                return new NajaResponseDtoGeneric<object>()
                {
                    Data = null,
                    Message = "شماره پلاک با کد ملی تطابق ندارد",
                    TraceId = getInquiry.TraceId,
                    RefId = getInquiry.RefId,
                    Status = getInquiry.Status
                };
            }
            else
            {
                return new NajaResponseDtoGeneric<object>()
                {
                    Data = null,
                    Message = getInquiry.Message,
                    TraceId = getInquiry.TraceId,
                    RefId = getInquiry.RefId,
                    Status = getInquiry.Status
                };
            }
        }
        #endregion

        #region [- AccumulationViolationsInquiry -]
        [LoggingAspect]
        public async Task<NajaResponseDtoGeneric<AccumulationViolationsInquiryResponseDto>> AccumulationViolationsInquiryAsync(AccumulationViolationsInquiryRequestDto reqDto)
        {
            _logger.Log(13681370, "service AccumulationViolationsInquiryAsync => " + JsonConvert.SerializeObject(reqDto), null);
            var checkInfo = await _najaServiceProxy.CheckInfo(
                                                    new CheckInfoRequestDto
                                                    {
                                                        MobileNumber = reqDto.MobileNumber,
                                                        NationalCode = reqDto.NationalCode
                                                    });
            if (checkInfo.Status == 0)
            {
                _logger.Log(13681370, "service checkinfo 0 => " + JsonConvert.SerializeObject(reqDto), null);
                var AccumulationViolationsInquiry = await _najaServiceProxy.AccumulationViolationsInquiry(reqDto);

                _logger.Log(13681370, "service AccumulationViolationsInquiry resp => " + JsonConvert.SerializeObject(AccumulationViolationsInquiry), null);
                if (AccumulationViolationsInquiry.Status == 0)
                {
                    return AccumulationViolationsInquiry;
                }
                else if (AccumulationViolationsInquiry.Status == -51)
                {
                    //استعلام بهزاد
                    var getInquiry = await _najaServiceProxy.GetInquiry(new GetInquiryRequestDto() { TrackingNo = reqDto.TrackingNo, TraceId = 0 });
                    if (getInquiry.Status == 0)
                    {
                        var CommingOnInquiry = (AccumulationViolationsInquiryResponseDto)getInquiry.Data;
                        return new NajaResponseDtoGeneric<AccumulationViolationsInquiryResponseDto>()
                        {
                            Data = CommingOnInquiry,
                            Message = getInquiry.Message,
                            TraceId = getInquiry.TraceId,
                            RefId = getInquiry.RefId,
                            Status = getInquiry.Status
                        };
                    }
                    else
                    {
                        return new NajaResponseDtoGeneric<AccumulationViolationsInquiryResponseDto>()
                        {
                            Data = null,
                            Message = getInquiry.Message,
                            TraceId = getInquiry.TraceId,
                            RefId = getInquiry.RefId,
                            Status = getInquiry.Status
                        };
                    }

                }
                else if (AccumulationViolationsInquiry.Status == 502)
                {
                    return new NajaResponseDtoGeneric<AccumulationViolationsInquiryResponseDto>()
                    {
                        Data = null,
                        Message = "شماره پلاک با کد ملی تطابق ندارد",
                        TraceId = AccumulationViolationsInquiry.TraceId,
                        RefId = AccumulationViolationsInquiry.RefId,
                        Status = AccumulationViolationsInquiry.Status
                    };
                }
                else
                {
                    return new NajaResponseDtoGeneric<AccumulationViolationsInquiryResponseDto>()
                    { Status = AccumulationViolationsInquiry.Status, Message = AccumulationViolationsInquiry.Message };
                }
            }
            else
            {
                return new NajaResponseDtoGeneric<AccumulationViolationsInquiryResponseDto>()
                {
                    Data = null,
                    Status = checkInfo.Status,
                    Message = checkInfo.Message,
                    RefId = checkInfo.RefId,
                    TraceId = checkInfo.TraceId
                };
            }
        }
        #endregion

        #region [- ActivePlakInquiry -]
        [LoggingAspect]
        public async Task<NajaResponseDtoGeneric<List<ActivePlakInquiryResponseDto>>> ActivePlakInquiryAsync(ActivePlakInquiryRequestDto reqDto)
        {
            _logger.Log(13681370, "service ActivePlakInquiryAsync => " + JsonConvert.SerializeObject(reqDto), null);
            var checkInfo = await _najaServiceProxy.CheckInfo(
                                                 new CheckInfoRequestDto
                                                 {
                                                     MobileNumber = reqDto.MobileNumber,
                                                     NationalCode = reqDto.NationalCode
                                                 });
            if (checkInfo.Status == 0)
            {
                var ActivePlakInquiry = await _najaServiceProxy.ActivePlakInquiry(reqDto);
                _logger.Log(13681370, "service ActivePlakInquiry resp => " + JsonConvert.SerializeObject(ActivePlakInquiry), null);
                if (ActivePlakInquiry.Status == 0)
                {
                    return ActivePlakInquiry;
                }
                else if (ActivePlakInquiry.Status == -51)
                {
                    //استعلام بهزاد
                    var getInquiry = await _najaServiceProxy.GetInquiry(new GetInquiryRequestDto() { TrackingNo = reqDto.TrackingNo, TraceId = 0 });
                    if (getInquiry.Status == 0)
                    {
                        var CommingOnInquiry = (List<ActivePlakInquiryResponseDto>)getInquiry.Data;
                        return new NajaResponseDtoGeneric<List<ActivePlakInquiryResponseDto>>()
                        {
                            Data = CommingOnInquiry,
                            Message = getInquiry.Message,
                            TraceId = getInquiry.TraceId,
                            RefId = getInquiry.RefId,
                            Status = getInquiry.Status
                        };
                    }
                    else
                    {
                        return new NajaResponseDtoGeneric<List<ActivePlakInquiryResponseDto>>()
                        {
                            Data = null,
                            Message = getInquiry.Message,
                            TraceId = getInquiry.TraceId,
                            RefId = getInquiry.RefId,
                            Status = getInquiry.Status
                        };
                    }

                }
                else
                {
                    return new NajaResponseDtoGeneric<List<ActivePlakInquiryResponseDto>>()
                    { Status = ActivePlakInquiry.Status, Message = ActivePlakInquiry.Message };
                }
            }
            else
            {
                return new NajaResponseDtoGeneric<List<ActivePlakInquiryResponseDto>>()
                {
                    Data = null,
                    Status = checkInfo.Status,
                    Message = checkInfo.Message,
                    RefId = checkInfo.RefId,
                    TraceId = checkInfo.TraceId
                };
            }
        }
        #endregion

        #region [- CardSanadInquiry -]
        [LoggingAspect]
        public async Task<NajaResponseDtoGeneric<CardSanadInquiryResponseDto>> CardSanadInquiryAsync(CardSanadInquiryRequestDto reqDto)
        {
            var checkInfo = await _najaServiceProxy.CheckInfo(
                                              new CheckInfoRequestDto
                                              {
                                                  MobileNumber = reqDto.MobileNumber,
                                                  NationalCode = reqDto.NationalCode
                                              });
            if (checkInfo.Status == 0)
            {

                var CardSanadInquiry = await _najaServiceProxy.CardSanadInquiry(reqDto);
                if (CardSanadInquiry.Status == 0)
                {
                    return CardSanadInquiry;
                }
                else if (CardSanadInquiry.Status == -51)
                {
                    //استعلام بهزاد
                    var getInquiry = await _najaServiceProxy.GetInquiry(new GetInquiryRequestDto() { TrackingNo = reqDto.TrackingNo, TraceId = 0 });
                    if (getInquiry.Status == 0)
                    {
                        var CommingOnInquiry = (CardSanadInquiryResponseDto)getInquiry.Data;
                        return new NajaResponseDtoGeneric<CardSanadInquiryResponseDto>()
                        {
                            Data = CommingOnInquiry,
                            Message = getInquiry.Message,
                            TraceId = getInquiry.TraceId,
                            RefId = getInquiry.RefId,
                            Status = getInquiry.Status
                        };
                    }
                    else
                    {
                        return new NajaResponseDtoGeneric<CardSanadInquiryResponseDto>()
                        {
                            Data = null,
                            Message = getInquiry.Message,
                            TraceId = getInquiry.TraceId,
                            RefId = getInquiry.RefId,
                            Status = getInquiry.Status
                        };
                    }

                }
                else
                {
                    return new NajaResponseDtoGeneric<CardSanadInquiryResponseDto>()
                    { Status = CardSanadInquiry.Status, Message = CardSanadInquiry.Message };
                }
            }
            else
            {
                return new NajaResponseDtoGeneric<CardSanadInquiryResponseDto>()
                {
                    Status = checkInfo.Status,
                    Data = null,
                    Message = checkInfo.Message,
                    RefId = checkInfo.RefId,
                    TraceId = checkInfo.TraceId
                };
            }
        }

        #endregion

        #region [- CheckInfo -]
        [LoggingAspect]
        public Task<NajaResponseDtoGeneric<CheckInfoResponseDto>> CheckInfoAsync(CheckInfoRequestDto dto)
        {
            return _najaServiceProxy.CheckInfo(dto);
        }

        #endregion

        #region [- ExitTaxesReceipt -]
        [LoggingAspect]
        public async Task<NajaResponseDtoGeneric<ExitTaxesReceiptResponseDto>> ExitTaxesReceiptAsync(ExitTaxesReceiptRequestDto reqDto)
        {
            var checkInfo = await _najaServiceProxy.CheckInfo(
                                          new CheckInfoRequestDto
                                          {
                                              MobileNumber = reqDto.MobileNumber,
                                              NationalCode = reqDto.NationalCode
                                          });
            //_logger.Log(836, null, $"📥 NajaServices(ExitTaxesReceipt {JsonConvert.SerializeObject(reqDto)}'", null);

            if (checkInfo.Status == 0)
            {

                var ExitTaxesReceipt = await _najaServiceProxy.ExitTaxesReceipt(reqDto);
                if (ExitTaxesReceipt.Status == 0)
                {
                    return ExitTaxesReceipt;
                }
                else if (ExitTaxesReceipt.Status == -51)
                {
                    //استعلام بهزاد
                    var getInquiry = await _najaServiceProxy.GetInquiry(new GetInquiryRequestDto() { TrackingNo = reqDto.TrackingNo, TraceId = 0 });
                    if (getInquiry.Status == 0)
                    {
                        var CommingOnInquiry = (ExitTaxesReceiptResponseDto)getInquiry.Data;
                        return new NajaResponseDtoGeneric<ExitTaxesReceiptResponseDto>()
                        {
                            Data = CommingOnInquiry,
                            Message = getInquiry.Message,
                            TraceId = getInquiry.TraceId,
                            RefId = getInquiry.RefId,
                            Status = getInquiry.Status
                        };
                    }
                    else
                    {
                        return new NajaResponseDtoGeneric<ExitTaxesReceiptResponseDto>()
                        {
                            Data = null,
                            Message = getInquiry.Message,
                            TraceId = getInquiry.TraceId,
                            RefId = getInquiry.RefId,
                            Status = getInquiry.Status
                        };
                    }

                }
                else
                {
                    return new NajaResponseDtoGeneric<ExitTaxesReceiptResponseDto>()
                    { Status = ExitTaxesReceipt.Status, Message = ExitTaxesReceipt.Message };
                }
            }
            else
            {
                return new NajaResponseDtoGeneric<ExitTaxesReceiptResponseDto>()
                {
                    Status = checkInfo.Status,
                    Data = null,
                    Message = checkInfo.Message,
                    RefId = checkInfo.RefId,
                    TraceId = checkInfo.TraceId
                };
            }
        }
        #endregion

        #region [- LicenseNegativePointInquiry -]
        [LoggingAspect]
        public async Task<NajaResponseDtoGeneric<LicenseNegativePointInquiryResponseDto>> LicenseNegativePointInquiryAsync(LicenseNegativePointInquiryRequestDto reqDto)
        {
            _logger.Log(13681370, "service LicenseNegativePointInquiryAsync => " + JsonConvert.SerializeObject(reqDto), null);
            var checkInfo =
                  await _najaServiceProxy.CheckInfo(
                       new CheckInfoRequestDto
                       {
                           MobileNumber = reqDto.MobileNumber,
                           NationalCode = reqDto.NationalCode
                       });
            //_logger.Log(875, null, $"📥 NajaServices(LicenseNegativePointInquiry {JsonConvert.SerializeObject(checkInfo)}'", null);

            if (checkInfo.Status == 0)
            {
                _logger.Log(13681370, "service LicenseNegativePointInquiryAsync ok => " + JsonConvert.SerializeObject(reqDto), null);
                var LicenseNegativePointInquiry = await _najaServiceProxy.LicenseNegativePointInquiry(reqDto);
                _logger.Log(13681370, "service LicenseNegativePointInquiry => " + JsonConvert.SerializeObject(LicenseNegativePointInquiry), null);
                if (LicenseNegativePointInquiry.Status == 0)
                {
                    return LicenseNegativePointInquiry;
                }
                else if (LicenseNegativePointInquiry.Status == -51)
                {
                    //استعلام بهزاد
                    var getInquiry = await _najaServiceProxy.GetInquiry(new GetInquiryRequestDto() { TrackingNo = reqDto.TrackingNo, TraceId = 0 });
                    if (getInquiry.Status == 0)
                    {
                        var CommingOnInquiry = (LicenseNegativePointInquiryResponseDto)getInquiry.Data;
                        return new NajaResponseDtoGeneric<LicenseNegativePointInquiryResponseDto>()
                        {
                            Data = CommingOnInquiry,
                            Message = getInquiry.Message,
                            TraceId = getInquiry.TraceId,
                            RefId = getInquiry.RefId,
                            Status = getInquiry.Status
                        };
                    }
                    else
                    {
                        return new NajaResponseDtoGeneric<LicenseNegativePointInquiryResponseDto>()
                        {
                            Data = null,
                            Message = getInquiry.Message,
                            TraceId = getInquiry.TraceId,
                            RefId = getInquiry.RefId,
                            Status = getInquiry.Status
                        };
                    }

                }
                else
                {
                    return new NajaResponseDtoGeneric<LicenseNegativePointInquiryResponseDto>()
                    { Status = LicenseNegativePointInquiry.Status, Message = LicenseNegativePointInquiry.Message };
                }
            }
            else
            {
                return new NajaResponseDtoGeneric<LicenseNegativePointInquiryResponseDto>()
                {
                    Status = checkInfo.Status,
                    Data = null,
                    Message = checkInfo.Message,
                    RefId = checkInfo.RefId,
                    TraceId = checkInfo.TraceId
                };
            }
        }
        #endregion

        #region [- LicenseStatusInquiry -]
        [LoggingAspect]
        public async Task<NajaResponseDtoGeneric<List<LicenseStatusInquiryResponseDto>>> LicenseStatusInquiryAsync(LicenseStatusInquiryRequestDto reqDto)
        {
            _logger.Log(13681370, "service LicenseStatusInquiryAsync => " + JsonConvert.SerializeObject(reqDto), null);
            var checkInfo = await _najaServiceProxy.CheckInfo(
                                       new CheckInfoRequestDto
                                       {
                                           MobileNumber = reqDto.MobileNumber,
                                           NationalCode = reqDto.NationalCode
                                       });
            // _logger.Log(920, null, $"📥 NajaServices(LicenseStatusInquiry {JsonConvert.SerializeObject(checkInfo)}'", null);

            if (checkInfo.Status == 0)
            {
                var LicenseStatusInquiry = await _najaServiceProxy.LicenseStatusInquiry(reqDto);
                _logger.Log(13681370, "service LicenseStatusInquiry resp => " + JsonConvert.SerializeObject(reqDto), null);
                if (LicenseStatusInquiry.Status == 0)
                {
                    return LicenseStatusInquiry;
                }
                else if (LicenseStatusInquiry.Status == -51)
                {
                    //استعلام بهزاد
                    var getInquiry = await _najaServiceProxy.GetInquiry(new GetInquiryRequestDto() { TrackingNo = reqDto.TrackingNo, TraceId = 0 });
                    if (getInquiry.Status == 0)
                    {
                        var CommingOnInquiry = (List<LicenseStatusInquiryResponseDto>)getInquiry.Data;
                        return new NajaResponseDtoGeneric<List<LicenseStatusInquiryResponseDto>>()
                        {
                            Data = CommingOnInquiry,
                            Message = getInquiry.Message,
                            TraceId = getInquiry.TraceId,
                            RefId = getInquiry.RefId,
                            Status = getInquiry.Status
                        };
                    }
                    else
                    {
                        return new NajaResponseDtoGeneric<List<LicenseStatusInquiryResponseDto>>()
                        {
                            Data = null,
                            Message = getInquiry.Message,
                            TraceId = getInquiry.TraceId,
                            RefId = getInquiry.RefId,
                            Status = getInquiry.Status
                        };
                    }

                }
                else
                {
                    return new NajaResponseDtoGeneric<List<LicenseStatusInquiryResponseDto>>()
                    { Status = LicenseStatusInquiry.Status, Message = LicenseStatusInquiry.Message };
                }
            }
            else
            {
                return new NajaResponseDtoGeneric<List<LicenseStatusInquiryResponseDto>>()
                {
                    Status = checkInfo.Status,
                    Data = null,
                    Message = checkInfo.Message,
                    RefId = checkInfo.RefId,
                    TraceId = checkInfo.TraceId
                };
            }

        }
        #endregion

        #region [- MotorViolationInquiry -]
        [LoggingAspect]
        public async Task<NajaResponseDtoGeneric<MotorViolationInquiryResponseDto>> MotorViolationInquiryAsync(MotorViolationInquiryRequestDto reqDto)
        {
            _logger.Log(1371, "motor service enter => " + JsonConvert.SerializeObject(reqDto), null);
            var checkInfo =
              await _najaServiceProxy.CheckInfo(
                   new CheckInfoRequestDto
                   {
                       MobileNumber = reqDto.MobileNumber,
                       NationalCode = reqDto.NationalCode
                   });

            _logger.Log(1371, "motor service info => " + JsonConvert.SerializeObject(checkInfo), null);
            if (checkInfo.Status == 0)
            {
                var MotorViolationInquiry = await _najaServiceProxy.MotorViolationInquiry(reqDto);
                _logger.Log(1371, "motor service resp => " + JsonConvert.SerializeObject(MotorViolationInquiry), null);
                if (MotorViolationInquiry.Status == 0)
                {
                    return MotorViolationInquiry;
                }
                else if (MotorViolationInquiry.Status == -51)
                {
                    //استعلام بهزاد
                    var getInquiry = await _najaServiceProxy.GetInquiry(new GetInquiryRequestDto() { TrackingNo = reqDto.TrackingNo, TraceId = 0 });
                    if (getInquiry.Status == 0)
                    {
                        var CommingOnInquiry = (MotorViolationInquiryResponseDto)getInquiry.Data;
                        return new NajaResponseDtoGeneric<MotorViolationInquiryResponseDto>()
                        {
                            Data = CommingOnInquiry,
                            Message = getInquiry.Message,
                            TraceId = getInquiry.TraceId,
                            RefId = getInquiry.RefId,
                            Status = getInquiry.Status
                        };
                    }
                    else
                    {
                        return new NajaResponseDtoGeneric<MotorViolationInquiryResponseDto>()
                        {
                            Data = null,
                            Message = getInquiry.Message,
                            TraceId = getInquiry.TraceId,
                            RefId = getInquiry.RefId,
                            Status = getInquiry.Status
                        };
                    }

                }
                else if (MotorViolationInquiry.Status == 502)
                {
                    return new NajaResponseDtoGeneric<MotorViolationInquiryResponseDto>()
                    {
                        Data = null,
                        Message = "شماره پلاک با کد ملی تطابق ندارد",
                        TraceId = MotorViolationInquiry.TraceId,
                        RefId = MotorViolationInquiry.RefId,
                        Status = MotorViolationInquiry.Status
                    };
                }
                else
                {
                    return new NajaResponseDtoGeneric<MotorViolationInquiryResponseDto>()
                    { Status = MotorViolationInquiry.Status, Message = MotorViolationInquiry.Message };
                }
            }
            else
            {
                return new NajaResponseDtoGeneric<MotorViolationInquiryResponseDto>()
                {
                    Status = checkInfo.Status,
                    Data = null,
                    Message = checkInfo.Message,
                    RefId = checkInfo.RefId,
                    TraceId = checkInfo.TraceId
                };
            }
        }
        #endregion

        #region [- NoExitInquiry -]
        [LoggingAspect]
        public async Task<NajaResponseDtoGeneric<NoExitInquiryResponseDto>> NoExitInquiryAsync(NoExitInquiryRequestDto reqDto)
        {
            var checkInfo =
                      await _najaServiceProxy.CheckInfo(
                           new CheckInfoRequestDto
                           {
                               MobileNumber = reqDto.MobileNumber,
                               NationalCode = reqDto.NationalCode
                           });
            if (checkInfo.Status == 0)
            {

                var NoExitInquiry = await _najaServiceProxy.NoExitInquiry(reqDto);
                if (NoExitInquiry.Status == 0)
                {
                    return NoExitInquiry;
                }
                else if (NoExitInquiry.Status == -51)
                {
                    //استعلام بهزاد
                    var getInquiry = await _najaServiceProxy.GetInquiry(new GetInquiryRequestDto() { TrackingNo = reqDto.TrackingNo, TraceId = 0 });
                    if (getInquiry.Status == 0)
                    {
                        var CommingOnInquiry = (NoExitInquiryResponseDto)getInquiry.Data;
                        return new NajaResponseDtoGeneric<NoExitInquiryResponseDto>()
                        {
                            Data = CommingOnInquiry,
                            Message = getInquiry.Message,
                            TraceId = getInquiry.TraceId,
                            RefId = getInquiry.RefId,
                            Status = getInquiry.Status
                        };
                    }
                    else
                    {
                        return new NajaResponseDtoGeneric<NoExitInquiryResponseDto>()
                        {
                            Data = null,
                            Message = getInquiry.Message,
                            TraceId = getInquiry.TraceId,
                            RefId = getInquiry.RefId,
                            Status = getInquiry.Status
                        };
                    }

                }
                else
                {
                    return new NajaResponseDtoGeneric<NoExitInquiryResponseDto>()
                    { Status = NoExitInquiry.Status, Message = NoExitInquiry.Message };
                }
            }
            else
            {
                return new NajaResponseDtoGeneric<NoExitInquiryResponseDto>()
                {
                    Status = checkInfo.Status,
                    Data = null,
                    Message = checkInfo.Message,
                    RefId = checkInfo.RefId,
                    TraceId = checkInfo.TraceId
                };
            }
        }
        #endregion

        #region [- PassportStatusInquiry -]
        [LoggingAspect]
        public async Task<NajaResponseDtoGeneric<PassportStatusInquiryResponseDto>> PassportStatusInquiryAsync(PassportStatusInquiryRequestDto reqDto)
        {
            var checkInfo =
                          await _najaServiceProxy.CheckInfo(
                               new CheckInfoRequestDto
                               {
                                   MobileNumber = reqDto.MobileNumber,
                                   NationalCode = reqDto.NationalCode
                               });
            if (checkInfo.Status == 0)
            {
                var PassportStatusInquiry = await _najaServiceProxy.PassportStatusInquiry(reqDto);
                if (PassportStatusInquiry.Status == 0)
                {
                    return PassportStatusInquiry;
                }
                else if (PassportStatusInquiry.Status == -51)
                {
                    //استعلام بهزاد
                    var getInquiry = await _najaServiceProxy.GetInquiry(new GetInquiryRequestDto() { TrackingNo = reqDto.TrackingNo, TraceId = 0 });
                    if (getInquiry.Status == 0)
                    {
                        var CommingOnInquiry = (PassportStatusInquiryResponseDto)getInquiry.Data;
                        return new NajaResponseDtoGeneric<PassportStatusInquiryResponseDto>()
                        {
                            Data = CommingOnInquiry,
                            Message = getInquiry.Message,
                            TraceId = getInquiry.TraceId,
                            RefId = getInquiry.RefId,
                            Status = getInquiry.Status
                        };
                    }
                    else
                    {
                        return new NajaResponseDtoGeneric<PassportStatusInquiryResponseDto>()
                        {
                            Data = null,
                            Message = getInquiry.Message,
                            TraceId = getInquiry.TraceId,
                            RefId = getInquiry.RefId,
                            Status = getInquiry.Status
                        };
                    }

                }
                else
                {
                    return new NajaResponseDtoGeneric<PassportStatusInquiryResponseDto>()
                    { Status = PassportStatusInquiry.Status, Message = PassportStatusInquiry.Message };
                }
            }
            else
            {
                return new NajaResponseDtoGeneric<PassportStatusInquiryResponseDto>()
                {
                    Status = checkInfo.Status,
                    Data = null,
                    Message = checkInfo.Message,
                    RefId = checkInfo.RefId,
                    TraceId = checkInfo.TraceId
                };
            }
        }
        #endregion

        #region [- PaymentExitInquiry -]
        [LoggingAspect]
        public Task<NajaResponseDtoGeneric<PaymentExitInquiryResponseDto>> PaymentExitInquiryAsync(PaymentExitInquiryRequestDto reqDto)
            => _najaServiceProxy.PaymentExitInquiry(reqDto);
        #endregion

        #region [- SetPayment -]
        [LoggingAspect]
        public Task<NajaResponseDtoGeneric<object>> SetPaymentAsync(SetPaymentRequestDto reqDto)
            => _najaServiceProxy.SetPayment(reqDto);
        #endregion

        #region [- ViolationImageInquiry -]
        [LoggingAspect]
        public async Task<NajaResponseDtoGeneric<ViolationImageInquiryResponseDto>> ViolationImageInquiryAsync(ViolationImageInquiryRequestDto reqDto)
        {

            var checkInfo =
                                  await _najaServiceProxy.CheckInfo(
                                       new CheckInfoRequestDto
                                       {
                                           MobileNumber = reqDto.MobileNumber,
                                           NationalCode = reqDto.NationalCode
                                       });
            //_logger.Log(1140, null, $"📥 NajaServices(ViolationImageInquiry {JsonConvert.SerializeObject(checkInfo)}'", null);
            if (checkInfo.Status == 0)
            {

                var ViolationImageInquiry = await _najaServiceProxy.ViolationImageInquiry(reqDto);
                if (ViolationImageInquiry.Status == 0)
                {
                    return ViolationImageInquiry;
                }
                else if (ViolationImageInquiry.Status == -51)
                {
                    //استعلام بهزاد
                    var getInquiry = await _najaServiceProxy.GetInquiry(new GetInquiryRequestDto() { TrackingNo = reqDto.TrackingNo, TraceId = 0 });
                    if (getInquiry.Status == 0)
                    {
                        var CommingOnInquiry = (ViolationImageInquiryResponseDto)getInquiry.Data;
                        return new NajaResponseDtoGeneric<ViolationImageInquiryResponseDto>()
                        {
                            Data = CommingOnInquiry,
                            Message = getInquiry.Message,
                            TraceId = getInquiry.TraceId,
                            RefId = getInquiry.RefId,
                            Status = getInquiry.Status
                        };
                    }
                    else
                    {
                        return new NajaResponseDtoGeneric<ViolationImageInquiryResponseDto>()
                        {
                            Data = null,
                            Message = getInquiry.Message,
                            TraceId = getInquiry.TraceId,
                            RefId = getInquiry.RefId,
                            Status = getInquiry.Status
                        };
                    }

                }
                else if (ViolationImageInquiry.Status == 502)
                {
                    return new NajaResponseDtoGeneric<ViolationImageInquiryResponseDto>()
                    {
                        Data = null,
                        Message = "شماره پلاک با کد ملی تطابق ندارد",
                        TraceId = ViolationImageInquiry.TraceId,
                        RefId = ViolationImageInquiry.RefId,
                        Status = ViolationImageInquiry.Status
                    };
                }
                else
                {
                    return new NajaResponseDtoGeneric<ViolationImageInquiryResponseDto>()
                    { Status = ViolationImageInquiry.Status, Message = ViolationImageInquiry.Message };
                }
            }
            else
            {
                return new NajaResponseDtoGeneric<ViolationImageInquiryResponseDto>()
                {
                    Status = checkInfo.Status,
                    Data = null,
                    Message = checkInfo.Message,
                    RefId = checkInfo.RefId,
                    TraceId = checkInfo.TraceId
                };
            }

        }



        #endregion

        #region [- ConfirmPayWage -]
        [LoggingAspect]
        public async Task<bool> ConfirmPayWageAsync(NajaWageDto najaWage)
        {
            _logger.Log(najaWage.PgwToken, null, $"📥Step1 NajaServices(ConfirmPayWage {JsonConvert.SerializeObject(najaWage)}'", null);
            try
            {

                var najaModel = await _najaRepository.GetNajaWageByOrderIdAsync(najaWage.OrderId.Value);
                if (najaWage.PaymentStatus == 0)
                {
                    //Confirm GetWay
                    ConfirmPaymentRequestDto confirm = new ConfirmPaymentRequestDto()
                    {
                        LoginAccount = _setting.NajaLoginAccount,
                        Token = najaWage.PgwToken.Value
                    };
                    var confirmResp = await _ipgServiceProxy.ConfirmPayment(confirm);
                    _logger.Log(najaWage.PgwToken, "📥Step2 NajaServices(ConfirmPayWage(ConfirmPayment)", JsonConvert.SerializeObject(confirmResp), null);

                    if (confirmResp != null && confirmResp.status == 0)
                    {
                        Charge charge = new Charge()
                        {
                            PaymentStatus = confirmResp.status,
                            RRN = najaWage.RRN.Value,
                            PGWToken = najaWage.PgwToken.ToString()
                        };
                        var respUpdateCharge = await _walletRepository.UpdateChargeTable(charge);
                        if (!respUpdateCharge)
                        {
                            var try2UpdateCharge = await _walletRepository.UpdateChargeTable(charge);
                            _logger.Log(najaWage.PgwToken, $"📥Step3 NajaServices(ConfirmPayWage(UpdateChargeTableUse(try2UpdateCharge={try2UpdateCharge}))", JsonConvert.SerializeObject(charge), null);
                        }
                        najaModel.Status = 0;
                        najaModel.PaymentStatus = confirmResp.status;
                        najaModel.BussinessDate = DateTime.Now;
                        najaModel.OrderId = najaWage.OrderId;
                        najaModel.PgwToken = najaWage.PgwToken;
                        najaModel.RRN = confirmResp.RRN;
                        najaModel.Amount = najaWage.Amount;
                        najaModel.MaskCardNumber = confirmResp.CardNumberMasked;
                        najaModel.ServiceMessage = $"سرویس درگاه : پرداخت با موفقیت انجام شد ";
                        var resultRepo = await _najaRepository.UpdateNajaWageAsync(najaModel);
                        if (!resultRepo)
                        {
                            var res = await _najaRepository.UpdateNajaWageAsync(najaModel);
                            return res;
                        }
                        else
                        {
                            return true;
                        }
                    }
                    else
                    {
                        najaModel.Status = confirmResp.status;
                        najaModel.PaymentStatus = confirmResp.status;
                        najaModel.BussinessDate = DateTime.Now;
                        najaModel.OrderId = najaWage.OrderId;
                        najaModel.PgwToken = confirmResp.Token;
                        najaModel.RRN = confirmResp.RRN;
                        najaModel.Amount = najaWage.Amount;
                        najaModel.MaskCardNumber = confirmResp.CardNumberMasked;
                        najaModel.ServiceMessage = $"سرویس درگاه : {confirmResp.status} ";
                        var resultRepo = await _najaRepository.UpdateNajaWageAsync(najaModel);
                        if (!resultRepo)
                        {
                            var res = await _najaRepository.UpdateNajaWageAsync(najaModel);
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
                    najaModel.Status = -1;
                    najaModel.PaymentStatus = najaWage.PaymentStatus;
                    najaModel.BussinessDate = DateTime.Now;
                    najaModel.OrderId = najaWage.OrderId;
                    najaModel.PgwToken = najaWage.PgwToken;
                    najaModel.ServiceMessage = $"سرویس درگاه : پرداخت ناموفق";
                    var result = await _najaRepository.UpdateNajaWageAsync(najaModel);
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
                _logger.Log(najaWage.PgwToken, $"📥 NajaServices (when got exception ConfirmPayWage)", JsonConvert.SerializeObject(ex.Message), ex);
                return false;
            }
        }

        public async Task<bool> UpdateNajaWageRequestIsSentAsync(long orderId)
        {
            return await _najaRepository.UpdateNajaWageSentRequestAsync(orderId);
        }
        #endregion
    }

    #region [- Interface -]
    public interface INajaServices
    {
        Task<NajaWageDto> GetNajaWageByOrderIdAsync(long orderId);
        Task<ResponseBaseDto<NajaWageDto>> GetNajaWageViolationImageAsync(long orderId, long userId);
        Task<List<NajaWageDto>> GetNajaWageByExperssionAsync(Expression<Func<NajaWage, bool>> predicate);
        Task<List<NajaWageDto>> GetAllNajaPaysAsync();
        Task<bool> AddNajaWageAsync(NajaWageDto najaPay);
        Task<bool> UpdateNajaWageAsync(NajaWageDto najaPay);
        Task<bool> UpdateNajaWageRequestIsSentAsync(long orderId);
        Task<PagedResponse<List<NajaWageDtoResponsePaginated>>> GetPaginatedNajaWageAsync(PaginationFilterDto paginationFilter);
        Task<bool> ConfirmPayWageAsync(NajaWageDto najaWage);
        Task<ResponseBaseDto<NajaWageDto>> InquiryWageByOrderIdAsync(long orderId, long userId);
        Task<ResponseBaseDto<PayWageResponseDto>> ShetabPayWageAsync(PayWageRequestDto payWageRequest);
        Task<ResponseBaseDto<PayWageResponseDto>> WalletPayWageAsync(PayWageRequestDto payWageRequest);
        Task<NajaResponseDtoGeneric<AccumulationViolationsInquiryResponseDto>> AccumulationViolationsInquiryAsync(AccumulationViolationsInquiryRequestDto reqDto);
        Task<NajaResponseDtoGeneric<List<ActivePlakInquiryResponseDto>>> ActivePlakInquiryAsync(ActivePlakInquiryRequestDto reqDto);
        Task<NajaResponseDtoGeneric<CardSanadInquiryResponseDto>> CardSanadInquiryAsync(CardSanadInquiryRequestDto reqDto);
        Task<NajaResponseDtoGeneric<CheckInfoResponseDto>> CheckInfoAsync(CheckInfoRequestDto dto);
        Task<NajaResponseDtoGeneric<ExitTaxesReceiptResponseDto>> ExitTaxesReceiptAsync(ExitTaxesReceiptRequestDto reqDto);
        Task<NajaResponseDtoGeneric<LicenseNegativePointInquiryResponseDto>> LicenseNegativePointInquiryAsync(LicenseNegativePointInquiryRequestDto reqDto);
        Task<NajaResponseDtoGeneric<List<LicenseStatusInquiryResponseDto>>> LicenseStatusInquiryAsync(LicenseStatusInquiryRequestDto reqDto);
        Task<NajaResponseDtoGeneric<MotorViolationInquiryResponseDto>> MotorViolationInquiryAsync(MotorViolationInquiryRequestDto reqDto);
        Task<NajaResponseDtoGeneric<NoExitInquiryResponseDto>> NoExitInquiryAsync(NoExitInquiryRequestDto reqDto);
        Task<NajaResponseDtoGeneric<PassportStatusInquiryResponseDto>> PassportStatusInquiryAsync(PassportStatusInquiryRequestDto reqDto);
        Task<NajaResponseDtoGeneric<PaymentExitInquiryResponseDto>> PaymentExitInquiryAsync(PaymentExitInquiryRequestDto reqDto);
        Task<NajaResponseDtoGeneric<object>> SetPaymentAsync(SetPaymentRequestDto reqDto);
        Task<NajaResponseDtoGeneric<ViolationImageInquiryResponseDto>> ViolationImageInquiryAsync(ViolationImageInquiryRequestDto reqDto);
        Task<NajaResponseDtoGeneric<ViolationInquiryResponseDto>> ViolationInquiryAsync(ViolationInquiryRequestDto reqDto);
        Task<NajaResponseDtoGeneric<object>> GetInquiry(GetInquiryRequestDto violationInquiryDto);
    }
    #endregion

}
