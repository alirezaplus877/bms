using AutoMapper;
using Dto;
using Dto.Proxy.IPG;
using Dto.Proxy.Request;
using Dto.Proxy.Request.IPG;
using Dto.Proxy.Request.Wallet;
using Dto.Proxy.Response;
using Dto.Proxy.Wallet;
using Dto.repository;
using Dto.Response;
using Entities;
using Microsoft.Extensions.DependencyInjection;
using PEC.CoreCommon.Attribute;
using Pigi.MDbLogging;
using ProxyService.services;
using Repository.reositories;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utility;
using static ProxyService.services.WalletProxy;

namespace Application.Services
{
    [ScopedService]
    public class PayServices : IPayServices
    {
        #region Private Variable
        private readonly IServiceProvider _serviceProvider;
        private readonly IMapper _mapper;
        private readonly IWalletProxy _proxy;
        private readonly IPecBmsSetting _setting;
        private readonly IIpgServiceProxy _ipgServiceProxy;
        private readonly IPaymentDbRepository _merhcantProviderRepository;
        private readonly IWalletRepository _walletRepository;
        private readonly IBillRepository _billRepository;
        private readonly IWalletServices _walletServices;
        #endregion
        #region ctor
        public PayServices(IServiceProvider serviceProvider, IWalletServices walletServices, IPecBmsSetting pecBmsSetting, IMapper mapper, IBillRepository billRepository, IWalletProxy proxy, IIpgServiceProxy ipgServiceProxy, IWalletRepository walletRepository, IPaymentDbRepository merhcantProviderRepository = null)
        {
            _serviceProvider = serviceProvider;
            _setting = pecBmsSetting;
            _mapper = mapper;
            _billRepository = billRepository;
            _walletRepository = walletRepository;
            _ipgServiceProxy = ipgServiceProxy;
            _walletServices = walletServices;
            _proxy = proxy;
            _merhcantProviderRepository = merhcantProviderRepository;
        }
        #endregion

        [LoggingAspect]
        public async Task<ResponseBaseDto<PayBillResposneDto>> ShetabPayBill(PayBillRequestDto payBillRequest)
        {
            PayBillResposneDto response = new PayBillResposneDto();
            var callbackUrl = "";
            #region insert into BillRequest

            var orderID = Utility.Utility.GenerateRandomOrderID();
            var billRequest = new BillRequest()
            {
                MerchatnID = payBillRequest.MerchantId,
                TotalAmount = payBillRequest.TotalAmount,
                UserID = payBillRequest.UserId,
                WalletId = payBillRequest.WalletId,
                TransType = payBillRequest.TransactionType,
                CreateDate = DateTime.Now,
                OrderID = orderID,
                Status = PaymentStatus.ReadyToPay
            };
            foreach (var item in payBillRequest.Bills)
            {
                int _MIncome = 0;
                if (payBillRequest.MerchantId > 0)
                {
                    var MerchantProvider = await _merhcantProviderRepository.GetMerchantProvider(m => m.MerchantId == payBillRequest.MerchantId && m.OrgId == item.OrganizationId); //Get merhcant wage
                    if (MerchantProvider != null)
                    {
                        _MIncome = MerchantProvider.Payment.HasValue ? MerchantProvider.Payment.Value : 0;
                    }
                }
                var detailOrderID = Utility.Utility.GenerateRandomOrderID();
                billRequest.BillRequestDetails.Add(new BillRequestDetail()
                {
                    Amount = item.Amount,
                    BillRequestID = billRequest.Id,
                    OrganizationID = item.OrganizationId,
                    PayId = item.PayId,
                    BillID = item.BillId,
                    CreateDate = DateTime.Now,
                    OrderId = detailOrderID,
                    Status = PaymentStatus.ReadyToPay,
                    IsSendToSettelment = false,
                    MIncome = _MIncome
                });
            }
            var addBillRequest = _billRepository.InsertBillrequest(billRequest);
            #endregion
            if (!addBillRequest)
            {
                return new ResponseBaseDto<PayBillResposneDto>()
                {
                    Message = "خطا در فرایند ثبت اطلاعات",
                    Status = 0,
                    Data = null
                };
            }
            if (payBillRequest.MerchantId != 0)
            {
                callbackUrl = _setting.CallBackUrlShetab + "?mid=" + payBillRequest.MerchantId;
            }
            else
            {
                callbackUrl = _setting.CallBackUrlShetab;
            }
            BillPaymentRequestDto billPayment = new BillPaymentRequestDto()
            {
                AdditionalData = "",
                BillId = payBillRequest.Bills[0].BillId,
                PayId = payBillRequest.Bills[0].PayId,
                CallBackUrl = callbackUrl,
                LoginAccount = _setting.BillAloneLoginAccount,
                OrderId = orderID,
                Originator = payBillRequest.MobileNumber
            };
            var ipgTokenResponse = await _ipgServiceProxy.BillPayment(billPayment);
            response.orderId = orderID;
            response.PaiadBills = new List<BillsDto>();
            foreach (var item in payBillRequest.Bills)
            {
                BillsDto bill = new BillsDto()
                {
                    Amount = item.Amount,
                    BillId = item.BillId,
                    PayId = item.PayId,
                    OrganizationId = item.OrganizationId
                };
                response.PaiadBills.Add(bill);
            }
            response.token = ipgTokenResponse.Token;
            response.message = "ثبت موفق قبض";
            BillRequest billRequestData = new BillRequest()
            {
                OrderID = orderID,
                Token = ipgTokenResponse.Token,
                ServiceMessage = ""
            };

            var updateToken = await _billRepository.UpdateBillRequest(billRequestData);
            return new ResponseBaseDto<PayBillResposneDto>()
            {
                Message = ipgTokenResponse.Message,
                Status = ipgTokenResponse.Status,
                Data = response
            };


        }
        [LoggingAspect]
        public async Task<ResponseBaseDto<TollBillPaymentResponseDto>> TollBillPayProcess(TollBillPaymentRequestDto tollBillPayment)
        {
            ///عوارض آزادراهی فقط پرداخت شتابی دارد
            try
            {
                var tollBillOrderId = Utility.Utility.GenerateRandomIDTollBill();
                #region insert to db

                var tollPlateData = new TollPlateData()
                {

                    CreateDate = DateTime.Now,
                    Mobile = tollBillPayment.MobileNumber,
                    OrderID = tollBillOrderId,
                    PlateNumber = tollBillPayment.PlateNumber,
                    TabanToken = tollBillPayment.Token,


                };
                foreach (var item in tollBillPayment.TollBills)
                {
                    tollPlateData.TollPlateBill.Add(new TollPlateBill()
                    {
                        Amount = item.Amount,
                        BillID = item.BillId,
                        PlateDataId = Convert.ToInt32(tollPlateData.Id),
                        TraversDate = item.TraversDate

                    });
                }
                var addBillRequest = _billRepository.InsertTollBillData(tollPlateData);
                #endregion
                if (addBillRequest)
                {

                    IpgSaleServiceRequestDto req = new IpgSaleServiceRequestDto()
                    {
                        AdditionalData = "پرداخت قبض عوارض آزاد راهی_pecbms",
                        Amount = tollBillPayment.TotalAmount,
                        CallBackUrl = _setting.CallBackUrlTollBill,
                        LoginAccount = _setting.TollBillLoginAccount,
                        OrderId = tollBillOrderId,
                        Originator = tollBillPayment.MobileNumber
                    };
                    //get ipg token
                    var saleToken = await _ipgServiceProxy.GetIpgSaleToken(req);
                    TollPlateData request = new TollPlateData()
                    {
                        PGWToken = saleToken.Token.ToString(),
                        OrderID = tollBillOrderId
                    };
                    var updateAfterGetToken = await _billRepository.UpdateTollBill(request);

                    TollBillPaymentResponseDto response = new TollBillPaymentResponseDto()
                    {
                        OrderId = tollBillOrderId,
                        Token = saleToken.Token
                    };
                    return new ResponseBaseDto<TollBillPaymentResponseDto>()
                    {
                        Status = saleToken.status,
                        Message = saleToken.Message,
                        Data = response
                    };
                }
                else
                {
                    return new ResponseBaseDto<TollBillPaymentResponseDto>()
                    {
                        Status = -1,
                        Message = "خطا در ثبت اطلاعات ",
                        Data = null
                    };
                }

            }
            catch (Exception ex)
            {
                return new ResponseBaseDto<TollBillPaymentResponseDto>()
                {
                    Status = -99,
                    Message = "خطای سیستمی ",
                    Data = null
                };
            }

        }

        [LoggingAspect]
        public async Task<ResponseBaseDto<InquiryByOrderIdResponseDto>> InquirybyOrderId(long OrderId)
        {
            try
            {
                InquiryByOrderIdResponseDto resp = new InquiryByOrderIdResponseDto();
                var inquiryResponse = await _billRepository.GetBillRequestByOrderId(OrderId);
                var charge = await _walletRepository.GetChargeViaOrderId(OrderId);
                if (inquiryResponse != null)
                {
                    var respoosne = _mapper.Map<BillRequestData>(inquiryResponse);
                    resp.bills = respoosne;
                    return new ResponseBaseDto<InquiryByOrderIdResponseDto>
                    {
                        Status = 0,
                        Message = "عملیات موفق",
                        Data = resp

                    };
                }
                else if (charge != null)
                {
                    var respoosne = _mapper.Map<ChargeData>(charge);
                    resp.charge = respoosne;
                    return new ResponseBaseDto<InquiryByOrderIdResponseDto>
                    {
                        Status = 0,
                        Message = "عملیات موفق",
                        Data = resp

                    };
                }
                else
                {
                    return new ResponseBaseDto<InquiryByOrderIdResponseDto>
                    {
                        Status = -1,
                        Message = "اطلاعاتی یافت نشد",
                        Data = null

                    };
                }

            }
            catch (Exception ex)
            {

                return new ResponseBaseDto<InquiryByOrderIdResponseDto>
                {
                    Status = -99,
                    Message = "خطای سیستمی "

                };
            }
        }
        [LoggingAspect]
        public async Task<ResponseBaseDto<PayBillResposneDto>> WalletPayBill(PayBillRequestDto payBillRequest)
        {

            PayBillResposneDto response = new PayBillResposneDto();
            WalletBalanceRequestDto walletBalance = new WalletBalanceRequestDto()
            {
                CorporationPIN = _setting.CorporationPIN,
                WalletId = payBillRequest.WalletId,
            };
            var walletBalanceResponse = await _walletServices.GetWalletBalance(walletBalance);
            if (walletBalanceResponse.ResultId != 0)
            {
                return new ResponseBaseDto<PayBillResposneDto>()
                {
                    Message = walletBalanceResponse.ResultDesc,
                    Status = walletBalanceResponse.ResultId,
                    Data = null
                };
            }
            else
            {
                var amount = Convert.ToInt64(walletBalanceResponse.Amount);
                var message = "قبوض جهت پرداخت ثبت شد جهت مشاهده وضعیت پرداخت لطحظاتی دیگر استعلام نمایید";
                long detailOrderID = 0;
                if (!(amount > payBillRequest.TotalAmount))
                {
                    return new ResponseBaseDto<PayBillResposneDto>()
                    {
                        Message = "موجودی  کیف پول جهت انجام تراکنش کافی نیست",
                        Status = -1,
                        Data = null
                    };
                }
                var orderID = Utility.Utility.GenerateRandomOrderID();
                var billRequest = new BillRequest()
                {
                    MerchatnID = payBillRequest.MerchantId,
                    TotalAmount = payBillRequest.TotalAmount,
                    UserID = payBillRequest.UserId,
                    WalletId = payBillRequest.WalletId,
                    TransType = payBillRequest.TransactionType,
                    CreateDate = DateTime.Now,
                    OrderID = orderID,
                    Status = PaymentStatus.ReadyToPayWithWallet,
                    IsSendAutomation = payBillRequest.IsSendAutomation
                };
                foreach (var item in payBillRequest.Bills)
                {
                    int _MIncome = 0;
                    if (payBillRequest.MerchantId > 0)
                    {
                        var MerchantProvider = await _merhcantProviderRepository.GetMerchantProvider(m => m.MerchantId == payBillRequest.MerchantId && m.OrgId == item.OrganizationId); //Get merhcant wage
                        if (MerchantProvider != null)
                        {
                            _MIncome = MerchantProvider.Payment.HasValue ? MerchantProvider.Payment.Value : 0;
                        }
                    }
                    detailOrderID = Utility.Utility.GenerateRandomOrderID();

                    billRequest.BillRequestDetails.Add(new BillRequestDetail()
                    {
                        Amount = item.Amount,
                        BillRequestID = billRequest.Id,
                        OrganizationID = item.OrganizationId,
                        PayId = item.PayId,
                        BillID = item.BillId,
                        CreateDate = DateTime.Now,
                        OrderId = detailOrderID,
                        Status = PaymentStatus.ReadyToPayWithWallet,
                        IsSendToSettelment = false,
                        MIncome = _MIncome
                    });
                }
                var addBillRequest = _billRepository.InsertBillrequest(billRequest);
                if (!addBillRequest)
                {
                    return new ResponseBaseDto<PayBillResposneDto>()
                    {
                        Message = "خطا در فرایند ثبت اطلاعات لطفا مجددا تلاش نمایید",
                        Status = -1,
                        Data = null
                    };
                }
                response.PaiadBills = new List<BillsDto>();
                foreach (var item in payBillRequest.Bills)
                {
                    BillsDto bill = new BillsDto()
                    {
                        Amount = item.Amount,
                        BillId = item.BillId,
                        PayId = item.PayId,
                        OrganizationId = item.OrganizationId
                    };
                    response.PaiadBills.Add(bill);
                }

                response.orderId = orderID;
                response.message = "ثبت موفق";

                #region پرداخت قبض کیف پولی هنگامی که یه عدد هست
                //if (payBillRequest.Bills.Count == 1)
                //{
                //    foreach (var item in payBillRequest.Bills)
                //    {
                //        WalletBillPaymentRequestDto paymentRequestDto = new WalletBillPaymentRequestDto()
                //        {
                //            AdditionalData = item.BillId.PadLeft(13, '0') + "=" + item.PayId.PadLeft(13, '0'),
                //            Amount = item.Amount,
                //            //call back pay bill
                //            CallBackURL = _setting.CallBackUrlParentWallet,
                //            CorporationPIN = _setting.CorporationPIN,
                //            DestinationNationalCode = _setting.MotherWallet.ToString(),//// ؟؟؟؟
                //            IpAddress = "172.30.2.155",
                //            MediaTypeId = _setting.WalletMediaType,
                //            OrderId = orderID,
                //            PIN = "234",
                //            SourceWalletId = payBillRequest.WalletId,
                //            TransactionDateTime = DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss")

                //        };
                //        var paymentProcess = await _walletServices.WalletPayBill(paymentRequestDto);
                //        var updateDb = await _billRepository.UpdateBillTableForWallet(orderID, paymentProcess.Status, paymentProcess.Message, paymentProcess.Data.ReturnId, detailOrderID);
                //        message = paymentProcess.Message;
                //  }
                //}
                #endregion

                return new ResponseBaseDto<PayBillResposneDto>()
                {
                    Message = response.message,
                    Status = 0,
                    Data = response
                };
            }


        }
        [LoggingAspect]
        public async Task<ResponseBaseDto<PayBillResposneDto>> ParrentWalletPayBill(PayBillRequestDto payBillRequest)
        {
            PayBillResposneDto response = new PayBillResposneDto();
            var callbackUrl = "";
            #region insert into Db
            var orderID = Utility.Utility.GenerateRandomOrderID();
            var billRequest = new BillRequest()
            {
                MerchatnID = payBillRequest.MerchantId,
                TotalAmount = payBillRequest.TotalAmount,
                UserID = payBillRequest.UserId,
                WalletId = _setting.MotherWalletId,
                TransType = payBillRequest.TransactionType,
                CreateDate = DateTime.Now,
                OrderID = orderID,
                Status = PaymentStatus.InitialRegister
            };
            foreach (var item in payBillRequest.Bills)
            {
                int _MIncome = 0;
                if (payBillRequest.MerchantId > 0)
                {
                    var MerchantProvider = await _merhcantProviderRepository.GetMerchantProvider(m => m.MerchantId == payBillRequest.MerchantId && m.OrgId == item.OrganizationId); //Get merhcant wage
                    if (MerchantProvider != null)
                    {
                        _MIncome = MerchantProvider.Payment.HasValue ? MerchantProvider.Payment.Value : 0;
                    }
                }
                var detailOrderID = Utility.Utility.GenerateRandomOrderID();
                billRequest.BillRequestDetails.Add(new BillRequestDetail()
                {
                    Amount = item.Amount,
                    BillRequestID = billRequest.Id,
                    OrganizationID = item.OrganizationId,
                    PayId = item.PayId,
                    BillID = item.BillId,
                    CreateDate = DateTime.Now,
                    OrderId = detailOrderID,
                    Status = PaymentStatus.InitialRegister,
                    IsSendToSettelment = false,
                    MIncome = _MIncome
                });
            }
            var addBillRequest = _billRepository.InsertBillrequest(billRequest);
            #endregion
            if (!addBillRequest)
            {
                return new ResponseBaseDto<PayBillResposneDto>()
                {
                    Message = "خطا در ثبت اطلاعات اولیه",
                    Status = -1,
                    Data = null
                };

            }
            if (payBillRequest.MerchantId != 0)
            {
                callbackUrl = _setting.CallBackUrlParentWallet + "?mid=" + payBillRequest.MerchantId;
            }
            else
            {
                callbackUrl = _setting.CallBackUrlParentWallet;
            }
            IpgSaleServiceRequestDto ipgTokenReq = new IpgSaleServiceRequestDto()
            {
                AdditionalData = "",
                Amount = billRequest.TotalAmount,
                //Amount = 10000,
                ///call back pay bill  + check mid
                CallBackUrl = callbackUrl,
                LoginAccount = _setting.LoginAccount,
                OrderId = orderID,
                Originator = payBillRequest.MobileNumber
            };
            var getSaleToken = await _ipgServiceProxy.GetIpgSaleToken(ipgTokenReq);
            if (getSaleToken.status == 0)
            {
                BillRequest UpdateData = new BillRequest()
                {
                    Token = getSaleToken.Token,
                    OrderID = orderID,
                    ServiceMessage = ""
                };
                var resp = await _billRepository.UpdateBillRequest(UpdateData);
                Guid obj = Guid.NewGuid();
                ChargeDto charge = new ChargeDto()
                {
                    Amount = payBillRequest.TotalAmount,
                    DestinationWalletId = _setting.MotherWalletCode.ToString(),
                    WalletType = _setting.ParrentWalletType,
                    CreateDate = DateTime.Now,
                    IsSendToSettlement = false,
                    WalletOrderId = orderID,
                    UserId = payBillRequest.UserId,
                    PGWToken = getSaleToken.Token.ToString(),
                    TrackingCode = obj.ToString(),
                    ApplicationId = payBillRequest.ApplicationId,
                    PaymentStatus = PaymentStatus.UnSuccess
                };
                var insertIntoChargeDb = await _walletServices.AddChargeRow(charge);
                if (insertIntoChargeDb)
                {
                    response.message = getSaleToken.Message;
                    response.orderId = orderID;
                    response.token = getSaleToken.Token;
                    response.PaiadBills = new List<BillsDto>();
                    foreach (var item in payBillRequest.Bills)
                    {
                        BillsDto bill = new BillsDto()
                        {
                            Amount = item.Amount,
                            BillId = item.BillId,
                            PayId = item.PayId,
                            OrganizationId = item.OrganizationId
                        };
                        response.PaiadBills.Add(bill);
                    }
                    return new ResponseBaseDto<PayBillResposneDto>()
                    {
                        Message = "موفق",
                        Status = 0,
                        Data = response
                    };
                }
                else
                {
                    return new ResponseBaseDto<PayBillResposneDto>()
                    {
                        Message = "خطا در عملیات شارژ کیف پول",
                        Status = 0,
                        Data = null
                    };
                }

            }
            else
            {
                return new ResponseBaseDto<PayBillResposneDto>()
                {
                    Message = getSaleToken.Message,
                    Status = -1,
                    Data = null
                };
            }
        }
    }

    public interface IPayServices
    {
        Task<ResponseBaseDto<PayBillResposneDto>> ShetabPayBill(PayBillRequestDto payBillRequest);
        Task<ResponseBaseDto<PayBillResposneDto>> WalletPayBill(PayBillRequestDto payBillRequest);
        Task<ResponseBaseDto<PayBillResposneDto>> ParrentWalletPayBill(PayBillRequestDto payBillRequest);
        Task<ResponseBaseDto<TollBillPaymentResponseDto>> TollBillPayProcess(TollBillPaymentRequestDto tollBillPayment);
        Task<ResponseBaseDto<InquiryByOrderIdResponseDto>> InquirybyOrderId(long OrderId);
    }
}
