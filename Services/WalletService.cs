using AutoMapper;
using CacheRepository.Service;
using Dto.Proxy.IPG;
using Dto.Proxy.Request.Wallet;
using Dto.Proxy.Response;
using Dto.Proxy.Response.Wallet;
using Dto.Proxy.Wallet;
using Dto.repository;
using Entities;
using Microsoft.Extensions.DependencyInjection;
using PEC.CoreCommon.Attribute;
using Pigi.MDbLogging;
using ProxyService.services;
using Repository.reositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utility;
using PEC.CoreCommon.ExtensionMethods;
using static ProxyService.services.WalletProxy;
using Dto.Request;
using Newtonsoft.Json;
using Dto.Response;
using PEC.CoreCommon.Security.Encryptor;

namespace Application.Services
{
    [ScopedService]
    public class WalletServices : IWalletServices, ILoggable
    {
        #region Private Variable
        private readonly IServiceProvider _serviceProvider;
        private readonly IMapper _mapper;
        private readonly IWalletProxy _proxy;
        private readonly IPecBmsSetting _setting;
        private readonly IIpgServiceProxy _ipgServiceProxy;
        private readonly IWalletRepository _walletRepository;
        private readonly IBillService _billService;
        private readonly IBillRepository _billRepository;
        private readonly IPgwDbRepository _pgwDbRepository;
        private readonly IEncryptor _encryptor;
        private IMdbLogger<WalletServices> _logger;
        #endregion

        #region ctor
        public WalletServices(IServiceProvider serviceProvider, IMdbLogger<WalletServices> logger, IWalletProxy proxy, IIpgServiceProxy ipgServiceProxy, IWalletRepository walletRepository, IBillService billService, IBillRepository billRepository, IPgwDbRepository pgwDbRepository = null, IEncryptor encryptor = null)
        {
            _serviceProvider = serviceProvider;
            _mapper = _serviceProvider.GetRequiredService<IMapper>();
            _setting = _serviceProvider.GetRequiredService<IPecBmsSetting>();
            _proxy = proxy;
            _ipgServiceProxy = ipgServiceProxy;
            _walletRepository = walletRepository;
            _billService = billService;
            _billRepository = billRepository;
            _logger = logger;
            _pgwDbRepository = pgwDbRepository;
            _encryptor = encryptor;
        }
        #endregion

        [LoggingAspect]
        public async Task<List<WalletsInfoResponseDto>> GetUserWallet(long userId)
        {
            List<WalletsInfoResponseDto> WalletsInfos = new List<WalletsInfoResponseDto>();
            try
            {
                List<UserClaim> userClaims = await _pgwDbRepository.GetUserClaimByClaimTypeAsync(userId);
                userClaims.ForEach(c =>
                {
                    var walletsInfoResponseDto = new WalletsInfoResponseDto();
                    string walletinfo = _encryptor.Decrypt(c.ClaimValue);
                    var items = walletinfo.Split(':');
                    walletsInfoResponseDto.WalletCode = items[0];
                    walletsInfoResponseDto.CorporationPIN = items[1];
                    WalletsInfos.Add(walletsInfoResponseDto);
                });
                return WalletsInfos;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        [LoggingAspect]
        public async Task<WalletBalanceResponseDto> GetWalletBalance(WalletBalanceRequestDto walletBalanceRequestDtos)
        {
            var WalletBalance = new WalletBalanceResponseDto();
            WalletBalance = await _proxy.GetBalanceWallet(walletBalanceRequestDtos);
            return WalletBalance;
        }

        [LoggingAspect]
        public async Task<List<MerchantWalletResponseDto>> GetMerchantWallet(List<MerchantWalletRequestDto> inputDto)
        {
            var merchantWallet = new List<MerchantWalletResponseDto>();
            foreach (var item in inputDto)
            {
                var WalletProxyResponse = await _proxy.GetMerchantWallet(item);
                foreach (var walletItem in WalletProxyResponse)
                {
                    walletItem.WalletCode = item.SourceNationalCode;
                }
                merchantWallet.AddRange(WalletProxyResponse);
            }
            return merchantWallet;
        }
        [LoggingAspect]
        public async Task<List<GetCustomerWalletResponseDto>> GetCustomerWallet(List<GetCustomerWalletRequestDto> getCustomer)
        {
            var merchantWallet = new List<GetCustomerWalletResponseDto>();
            foreach (var item in getCustomer)
            {
                var WalletProxyResponse = await _proxy.GetCustomerWallet(item);

                merchantWallet.AddRange(WalletProxyResponse);
            }
            return merchantWallet;
        }

        [LoggingAspect]
        public async Task<ResponseBaseDto<WalletBillPaymentResponseDto>> WalletPayBill(WalletBillPaymentRequestDto walletBill)
        {
            WalletBillPaymentResponseDto resp = new WalletBillPaymentResponseDto();
            try
            {
                resp = await _proxy.BillPayment(walletBill);
                if (resp.ResultId != 0)
                {
                    return new ResponseBaseDto<WalletBillPaymentResponseDto>()
                    {
                        Data = resp,
                        Message = resp.ResultDesc,
                        Status = resp.ResultId
                    };
                }
                else
                {
                    return new ResponseBaseDto<WalletBillPaymentResponseDto>()
                    {
                        Data = resp,
                        Message = "عملیات موفق",
                        Status = 0
                    };

                }
            }
            catch (Exception ex)
            {
                return new ResponseBaseDto<WalletBillPaymentResponseDto>()
                {
                    Data = null,
                    Message = "خطای سرویس",
                    Status = -99
                };


            }

        }
        [LoggingAspect]
        public async Task<ChargeWalletTokenResponseDto> ChargIpgToken(ChargeWalletTokenRequestDto chargeWalletIpgTokenRequest)
        {
            var orderId = Utility.Utility.GenerateRandomOrderID();
            var response = new ChargeWalletTokenResponseDto();
            if (chargeWalletIpgTokenRequest.MerchantId != 0)
            {
                chargeWalletIpgTokenRequest.CallBackUrl = _setting.CallBackUrlCharge + "?mid=" + chargeWalletIpgTokenRequest.MerchantId;
            }
            chargeWalletIpgTokenRequest.CallBackUrl = _setting.CallBackUrlCharge;
            chargeWalletIpgTokenRequest.LoginAccount = _setting.ChargeWalletLoginAccount;
            chargeWalletIpgTokenRequest.OrderId = orderId;
            var req = _mapper.Map<IpgSaleServiceRequestDto>(chargeWalletIpgTokenRequest);
            var chargToken = await _ipgServiceProxy.GetIpgSaleToken(req);
            var resp = _mapper.Map<ChargeWalletTokenResponseDto>(chargToken);
            if (chargToken.Token <= 0 || chargToken.status != 0)
            {
                resp.status = chargToken.status;
                resp.token = -1;
                resp.message = chargToken.Message;
                resp.orderId = orderId;
                return resp;
            }
            else
            {
                Guid obj = Guid.NewGuid();
                ChargeDto charge = new ChargeDto()
                {
                    Amount = chargeWalletIpgTokenRequest.Amount,
                    DestinationWalletId = chargeWalletIpgTokenRequest.WalletCode,
                    WalletType = chargeWalletIpgTokenRequest.WalletType,
                    CreateDate = DateTime.Now,
                    IsSendToSettlement = false,
                    WalletOrderId = orderId,
                    UserId = chargeWalletIpgTokenRequest.UserId,
                    PGWToken = chargToken.Token.ToString(),
                    TrackingCode = obj.ToString(),
                    ApplicationId = chargeWalletIpgTokenRequest.ApplicationId,
                    PaymentStatus = -1


                };
                ////insert inot charge Db
                var result = await AddChargeRow(charge);
                if (result)
                {
                    resp.orderId = orderId;
                    return resp;
                }
                else
                {
                    resp.status = -1;
                    resp.token = -1;
                    resp.message = "خطا در ثبت اطلاعات";
                    resp.orderId = orderId;
                    return resp;
                }
            }


        }
        [LoggingAspect]
        public async Task<ResponseBaseDto<ConfirmChargeResponseDto>> ConfirmCharge(ConfirmChargRequestDto chargRequestDto)
        {
            _logger.Log(11, null, $"📥 walletservice(confrimcahrge {JsonConvert.SerializeObject(chargRequestDto)}'", null);
            ConfirmChargeResponseDto confirmCharge = new ConfirmChargeResponseDto();
            try
            {
                var mid = await _walletRepository.GetChargeRow(Convert.ToString(chargRequestDto.PgwToken));
                confirmCharge.MerchantId = mid.MerchantId;
                ConfirmPaymentRequestDto confirm = new ConfirmPaymentRequestDto()
                {
                    LoginAccount = _setting.ChargeWalletLoginAccount,
                    Token = chargRequestDto.PgwToken
                };
                var confirmResp = await _ipgServiceProxy.ConfirmPayment(confirm);
                Charge charge = new Charge()
                {
                    PaymentStatus = confirmResp.status,
                    RRN = confirmResp.RRN,
                    PGWToken = chargRequestDto.PgwToken.ToString()
                };
                var resp = await _walletRepository.UpdateChargeTable(charge);
                if (confirmResp.status != 0 || !resp)
                {
                    return (new ResponseBaseDto<ConfirmChargeResponseDto>
                    {
                        Message = "خطا در تایید عملیات",
                        Status = confirmResp.status,
                        Data = confirmCharge

                    });
                }
                return (new ResponseBaseDto<ConfirmChargeResponseDto>
                {
                    Message = "عملیات موفق",
                    Status = confirmResp.status,
                    Data = confirmCharge
                });


            }
            catch (Exception)
            {

                return (new ResponseBaseDto<ConfirmChargeResponseDto>
                {
                    Message = "خطا در انجام عملیات تایید تراکنش",
                    Status = -99,
                    Data = confirmCharge
                });
            }
        }
        [LoggingAspect]
        public async Task<ResponseBaseDto<ConfirmChargeResponseDto>> ConfirmSalePayBillWallet(ConfirmChargRequestDto chargRequestDto)
        {
            ConfirmChargeResponseDto confirmCharge = new ConfirmChargeResponseDto();
            string message = "";
            _logger.Log(511, "ConfirmSalePayBillWallet", JsonConvert.SerializeObject(chargRequestDto), null);
            try
            {
                if (chargRequestDto.PgwStatus != 0)
                {
                    var update = _billRepository.UpdateBillTable(chargRequestDto.OrderId, PaymentStatus.UnSuccess, chargRequestDto.PgwToken, chargRequestDto.RRN, message);
                }

                var mid = await _walletRepository.GetChargeRow(Convert.ToString(chargRequestDto.PgwToken));
                confirmCharge.MerchantId = mid.MerchantId;
                ConfirmPaymentRequestDto confirm = new ConfirmPaymentRequestDto()
                {
                    LoginAccount = _setting.LoginAccount,
                    Token = chargRequestDto.PgwToken
                };

                var confirmResp = await _ipgServiceProxy.ConfirmPayment(confirm);
                message = confirmResp.status == 0 ? "عملیات موفق" : "عملیات ناموفق";
                _logger.Log(512, "ConfirmSalePayBillWallet", JsonConvert.SerializeObject(confirmResp), null);
                Charge charge = new Charge()
                {
                    PaymentStatus = confirmResp.status,
                    RRN = chargRequestDto.RRN,
                    PGWToken = chargRequestDto.PgwToken.ToString()
                };
                var resp = await _walletRepository.UpdateChargeTable(charge);
                _logger.Log(513, "ConfirmSalePayBillWallet", JsonConvert.SerializeObject(resp), null);
                if (confirmResp.status != 0 || !resp)
                {
                    var updateBillError = await _billRepository.UpdateBillTable(chargRequestDto.OrderId, PaymentStatus.UnSuccess, chargRequestDto.PgwToken, chargRequestDto.RRN, message);
                    return (new ResponseBaseDto<ConfirmChargeResponseDto>
                    {
                        Message = "خطا در تایید عملیات",
                        Status = confirmResp.status,
                        Data = confirmCharge

                    });
                }
                var updateBill = await _billRepository.UpdateBillTable(chargRequestDto.OrderId, PaymentStatus.PayWithParentWallet, chargRequestDto.PgwToken, chargRequestDto.RRN, message);
                return (new ResponseBaseDto<ConfirmChargeResponseDto>
                {
                    Message = "عملیات موفق",
                    Status = confirmResp.status,
                    Data = confirmCharge
                });


            }
            catch (Exception ex)
            {
                _logger.Log(514, "ConfirmSalePayBillWallet", JsonConvert.SerializeObject(ex), null);
                return (new ResponseBaseDto<ConfirmChargeResponseDto>
                {
                    Message = "خطا در انجام عملیات تایید تراکنش",
                    Status = -99,
                    Data = confirmCharge
                });
            }
        }
        [LoggingAspect]
        public async Task<ResponseBaseDto<WalletTransferResponseDto>> WalletTransfer(WalletTransferResquestDto walletTransfer)
        {
            try
            {
                var resp = await _proxy.WalletTransfer(walletTransfer);
                if (resp.ResultId != 0)
                {
                    return new ResponseBaseDto<WalletTransferResponseDto>()
                    {
                        Data = resp,
                        Message = resp.ResultDesc,
                        Status = resp.ResultId
                    };
                }
                else
                {
                    return new ResponseBaseDto<WalletTransferResponseDto>()
                    {
                        Data = resp,
                        Message = "عملیات موفق",
                        Status = 0
                    };

                }
            }
            catch (Exception ex)
            {

                return (new ResponseBaseDto<WalletTransferResponseDto>
                {
                    Message = "خطا در فراخوانی سرویس",
                    Status = -99,
                    Data = null

                });
            }
        }
        [LoggingAspect]
        public async Task<ResponseBaseDto<WalletPurchaseResponseDto>> WalletPurchase(WalletPurchaseResquestDto walletPurchase)
        {
            try
            {
                var resp = await _proxy.WalletPurchase(walletPurchase);
                if (resp.ResultId != 0)
                {
                    return new ResponseBaseDto<WalletPurchaseResponseDto>()
                    {
                        Data = resp,
                        Message = resp.ResultDesc,
                        Status = resp.ResultId
                    };
                }
                else
                {
                    return new ResponseBaseDto<WalletPurchaseResponseDto>()
                    {
                        Data = resp,
                        Message = "عملیات موفق",
                        Status = 0
                    };

                }
            }
            catch (Exception ex)
            {

                return (new ResponseBaseDto<WalletPurchaseResponseDto>
                {
                    Message = "خطا در فراخوانی سرویس",
                    Status = -99,
                    Data = null

                });
            }
        }

        public async Task<bool> getCharge(string token)
        {
            try
            {

                var result = await _walletRepository.GetChargeRow(token);

                return true;
            }
            catch (Exception)
            {

                return false;
            }
        }


        #region DataBase

        public async Task<bool> AddChargeRow(ChargeDto charge)
        {
            try
            {
                var chargeData = _mapper.Map<Charge>(charge);
                var insertResult = await _walletRepository.InsertChargeData(chargeData);
                return insertResult;
            }
            catch (Exception)
            {

                return false;
            }
        }


        public async Task<ChargeHistoryResponseDto> GetChargeHistory(ChargeHistoryRequestDo chargeHistory)
        {
            ChargeHistoryResponseDto chargeHistoryDataDto = new ChargeHistoryResponseDto();
            try
            {
                var response = await _walletRepository.GetChargeHistory(chargeHistory);
                List<ChargeHistoryDataDto> chargList = new List<ChargeHistoryDataDto>();
                foreach (var item in response.Item2)
                {
                    ChargeHistoryDataDto dataDto = new ChargeHistoryDataDto
                    {
                        Amount = item.Amount,
                        ApplicationId = item.ApplicationId,
                        CreateDate = item.CreateDate,
                        DestinationWalletId = item.DestinationWalletId,
                        IsSendToSettlement = item.IsSendToSettlement,
                        MerchantId = item.MerchantId,
                        PaymentStatus = item.PaymentStatus,
                        PGWToken = item.PGWToken,
                        ResultChargeId = item.ResultChargeId,
                        RRN = item.RRN,
                        TrackingCode = item.TrackingCode,
                        UserId = item.UserId,
                        WalletOrderId = item.WalletOrderId,
                        WalletType = item.WalletType
                    };
                    chargList.Add(dataDto);
                };
                chargeHistoryDataDto.chargeHistory = chargList;
                chargeHistoryDataDto.TotalCount = response.Item1;
                return chargeHistoryDataDto;
            }
            catch (Exception)
            {

                return null;
            }
        }


        #endregion


        public async Task<ResponseBaseDto<CustomerWalletResponseDto>> AddOrUpdateCustomerWalletAsync(CustomerWalletRequestDto requestDto, long userId)
        {
            try
            {
                var resp = await _proxy.AddOrUpdateCustomerWallet(requestDto);
                if (resp.ResultId == 2102 || resp.ResultId == 0)
                {

                    var userclaim = new UserClaim()
                    {
                        UserId = userId,
                        ClaimId = 702,
                        ClaimType = "PecBMSWallet",
                        ClaimValue = _encryptor.Encrypt($"{requestDto.NationalCode}:{requestDto.CorporationPIN}"),
                        CreateDate = DateTime.Now
                    };
                    var resultClaim = await _pgwDbRepository.AddUserClaimAsync(userclaim);
                    if (resultClaim)
                    {
                        return new ResponseBaseDto<CustomerWalletResponseDto>()
                        {
                            Data = resp,
                            Message = resp.ResultDesc,
                            Status = 0
                        };
                    }
                    else
                    {
                        return new ResponseBaseDto<CustomerWalletResponseDto>()
                        {
                            Data = resp,
                            Message = "خطا در فرآیند اعطای دسترسی کیف پول",
                            Status = -1
                        };

                    }
                }
                else
                {
                    return new ResponseBaseDto<CustomerWalletResponseDto>()
                    {
                        Data = resp,
                        Status = (int)ServiceStatus.ErrorInRegisterWallet,
                        Message = DescriptionUtility.GetDescription<ServiceStatus>("ErrorInRegisterWallet")
                    };

                }
            }
            catch (Exception ex)
            {

                return (new ResponseBaseDto<CustomerWalletResponseDto>
                {
                    Message = "خطا در لایه سرویس کیف پول",
                    Status = -99,
                    Data = null

                });
            }
        }

    }

    public interface IWalletServices
    {
        Task<ResponseBaseDto<CustomerWalletResponseDto>> AddOrUpdateCustomerWalletAsync(CustomerWalletRequestDto requestDto, long userId);
        Task<WalletBalanceResponseDto> GetWalletBalance(WalletBalanceRequestDto getWalletBalanceRequestDto);
        Task<List<MerchantWalletResponseDto>> GetMerchantWallet(List<MerchantWalletRequestDto> inputDto);
        Task<List<GetCustomerWalletResponseDto>> GetCustomerWallet(List<GetCustomerWalletRequestDto> getCustomer);
        Task<ChargeWalletTokenResponseDto> ChargIpgToken(ChargeWalletTokenRequestDto chargeWalletIpgTokenRequest);
        Task<ResponseBaseDto<WalletBillPaymentResponseDto>> WalletPayBill(WalletBillPaymentRequestDto walletBill);
        Task<bool> getCharge(string token);
        Task<bool> AddChargeRow(ChargeDto charge);
        Task<ResponseBaseDto<ConfirmChargeResponseDto>> ConfirmCharge(ConfirmChargRequestDto chargRequestDto);
        Task<List<WalletsInfoResponseDto>> GetUserWallet(long userId);
        Task<ResponseBaseDto<WalletPurchaseResponseDto>> WalletPurchase(WalletPurchaseResquestDto walletPurchase);
        Task<ResponseBaseDto<WalletTransferResponseDto>> WalletTransfer(WalletTransferResquestDto walletPurchase);
        Task<ChargeHistoryResponseDto> GetChargeHistory(ChargeHistoryRequestDo chargeHistory);
        Task<ResponseBaseDto<ConfirmChargeResponseDto>> ConfirmSalePayBillWallet(ConfirmChargRequestDto chargRequestDto);
    }
}
