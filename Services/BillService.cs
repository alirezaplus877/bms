using AutoMapper;
using Dto.Proxy.Request.IPG;
using Dto.Proxy.Request.PecIs;
using Dto.Proxy.Response;
using Dto.Proxy.Response.IPG;
using Dto.Proxy.Response.PecIs;
using Dto.repository;
using Entities;
using Microsoft.Extensions.DependencyInjection;
using PEC.CoreCommon.Attribute;
using Pigi.MDbLogging;
using ProxyService.services;
using Repository.reositories;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Utility;
using Dto;
using Dto.Proxy.IPG;
using Dto.Proxy.Request;
using Dto.Request;
using Newtonsoft.Json;
using Dto.Response;
using System.Linq;
using Dto.Proxy.Request.SMS;

namespace Application.Services
{
    [ScopedService]
    public class BillService : IBillService, ILoggable
    {
        #region Private Variable
        private readonly IServiceProvider _serviceProvider;
        private readonly IMapper _mapper;
        private readonly IBillRepository _billRepository;
        private readonly IPaymentDbRepository _merhcantProviderRepository;
        private readonly IBillInquiryServiceProxy _billProxy;
        private readonly IIpgServiceProxy _ipgServiceProxy;
        private readonly IPecBmsSetting _setting;
        private readonly IMdbLogger<BillService> _logger;
        private readonly IWalletRepository _walletRepository;
        private readonly ISMSProxy _SMSProxy;
        #endregion

        #region ctor
        public BillService(IServiceProvider serviceProvider, IWalletRepository walletRepository, IMdbLogger<BillService> logger, IBillRepository billRepository, IBillInquiryServiceProxy proxy, IIpgServiceProxy ipgServiceProxy, IPecBmsSetting setting, ISMSProxy sMSProxy, IPaymentDbRepository merhcantProviderRepository = null)
        {
            _serviceProvider = serviceProvider;
            _mapper = _serviceProvider.GetRequiredService<IMapper>();
            _billRepository = billRepository;
            _billProxy = proxy;
            _ipgServiceProxy = ipgServiceProxy;
            _setting = setting;
            _logger = logger;
            _walletRepository = walletRepository;
            _merhcantProviderRepository = merhcantProviderRepository;
            _SMSProxy = sMSProxy;
        }
        #endregion

        #region Db

        #region old code comment
        //public async Task<bool> ActiveAutoBillPayment(long UsersBillId)
        //{
        //    try
        //    {
        //        var activeatedResult = await _billRepository.ActiveAutoBillPayment(UsersBillId);
        //        return activeatedResult;
        //    }
        //    catch (Exception ex)
        //    {
        //        return false;
        //    }
        //}

        //public async Task<bool> DeleteAutoBillPayment(long UsersBillId)
        //{
        //    try
        //    {
        //        var deletedResult = await _billRepository.DeleteAutoBillPayment(UsersBillId);
        //        return deletedResult;
        //    }
        //    catch (Exception ex)
        //    {
        //        return false;
        //    }
        //} 
        #endregion


        public async Task<ResponseBaseDto> SendSmsAutoBillPayment(SendSmsAutoBillDto dto)
        {
            var UserBill = await _billRepository.GetUserBillBy(u => u.UserID == dto.UserId && u.BillID == dto.BillID);
            if (UserBill.Item1 != null && UserBill.Item2)
            {
                var confirmCode = new Random().Next(1000, 9999);
                var GetAutoBill = await _billRepository.GetAutoBillBy(a => a.UsersBillId == UserBill.Item1.Id);
                if (GetAutoBill.Item1 != null && GetAutoBill.Item2)
                {
                    var resultsms = await _SMSProxy.SendSMS(new SMSRequestDto
                    {
                        MobileNo = dto.MobileNo,
                        Content = $"کد تایید قبض خودکار <#>{confirmCode}"
                    });

                    if (resultsms > 0)
                    {
                        GetAutoBill.Item1.ConfirmCode = confirmCode;
                        var resultUpdateAutoBill = await _billRepository.UpdateAutoBillPayment(GetAutoBill.Item1);
                        switch (resultUpdateAutoBill)
                        {
                            case 0:
                                return new ResponseBaseDto
                                {
                                    Status = 0,
                                    Message = "کد تایید افزودن قبض با موفقیت ارسال شد"
                                };
                            case -1:
                                return new ResponseBaseDto
                                {
                                    Status = -1,
                                    Message = "پرداخت خودکار برای قبض یافت نشد"
                                };
                            case -3:
                                return new ResponseBaseDto
                                {
                                    Status = -3,
                                    Message = "خطا در بروزرسانی قبض خودکار"
                                };
                            case -100:
                                return new ResponseBaseDto
                                {
                                    Status = -100,
                                    Message = "خطای ناشناخته برای قبض خودکار"
                                };
                            default:
                                return new ResponseBaseDto
                                {
                                    Status = -100,
                                    Message = "خطای ناشناخته برای قبض خودکار"
                                };
                        }
                    }
                    else
                    {
                        return new ResponseBaseDto
                        {
                            Status = 1,
                            Message = "خطا در ارسال پیامک مجددا تلاش کنید"
                        };
                    }
                }
                else
                {
                    return new ResponseBaseDto
                    {
                        Status = 0,
                        Message = "قبض خودکار یافت نشد"
                    };
                }
            }
            else
            {
                return new ResponseBaseDto
                {
                    Status = -4,
                    Message = "قبض یافت نشد"
                };
            }
        }

        public async Task<ResponseBaseDto> UpdateUserBillByAutoPayment(UserBillDto userBillDto)
        {
            var result = await _billRepository.UpdateUserBillAutoPayment(userBillDto);
            switch (result)
            {
                case -1:
                    return new ResponseBaseDto { Status = result, Message = "قبض با این مشخصات یافت نشد" };
                case 0:
                    return new ResponseBaseDto { Status = result, Message = "قبض با موفقیت بروزرسانی گردید" };
                case -2:
                    return new ResponseBaseDto { Status = result, Message = "پرداخت خودکار برای این قبض یافت نشد" };
                case -3:
                    return new ResponseBaseDto { Status = result, Message = "خطا در بروزرسانی قبض خودکار" };
                case -4:
                    return new ResponseBaseDto { Status = result, Message = "خطا در بروزرسانی قبض" };
                case -100:
                    return new ResponseBaseDto { Status = result, Message = "خطای ناشناخته" };
                default:
                    return new ResponseBaseDto { Status = result, Message = "خطای ناشناخته" };
            }
        }

        public async Task<bool> UpdateBussunessDateAutoBillPayment(long userId, string BillId, DateTime newTime)
        {
            var resultUpdate = await _billRepository.UpdateBussinessDateAutoBillPayment(userId, BillId, newTime);
            if (resultUpdate.Item1 != null && resultUpdate.Item2)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public async Task<ResponseBaseDto> ConfirmCodeAutoBill(ConfirmationAutoBillPaymentDto model)
        {
            var result = await _billRepository.CheckValidConfirmCodeAutoBillPayment(model.UserID, model.ConfirmCode, model.BillId);
            if (result)
            {
                var UserBill = await _billRepository.GetUserBillBy(u => u.UserID == model.UserID && u.BillID == model.BillId);
                if (UserBill.Item1 == null && !UserBill.Item2)
                {
                    return new ResponseBaseDto
                    {
                        Status = -3,
                        Message = "خطا در بازیابی قبض مورد نظر"
                    };
                }
                var activatedResult = await _billRepository.ActiveUsersBill(UserBill.Item1);
                if (activatedResult.Item1 != null && activatedResult.Item2)
                {
                    return new ResponseBaseDto
                    {
                        Status = 0,
                        Message = "کد اعتبار سنجی درست می باشد،فعال سازی قبض با موفقیت انجام شد"
                    };
                }
                else if (activatedResult.Item1 != null && !activatedResult.Item2)
                {
                    return new ResponseBaseDto
                    {
                        Status = 0,
                        Message = "کد اعتبار سنجی درست می باشد،فعال سازی قبض قبلا انجام شده است"
                    };
                }
                else
                {
                    return new ResponseBaseDto
                    {
                        Status = 3,
                        Message = "قبض مورد نظر شما یافت نشد"
                    };
                }

            }
            else
            {
                return new ResponseBaseDto
                {
                    Status = -1,
                    Message = "کد اعتبار سنجی به درستی وارد نشده است"
                };
            }
        }

        public async Task<List<UserBillDto>> GetReadyAutoBillPaymentWithWallet()
        {
            try
            {
                var data = await _billRepository.GetReadyAutoBillPaymentWithWallet();
                var requestDto = _mapper.Map<List<UserBillDto>>(data);
                return requestDto;
            }
            catch (Exception e)
            {
                return null;
            }
        }

        public async Task<ResponseBaseDto> AddUserBillByAutoPayment(UserBillDto userBillDto)
        {
            try
            {
                var userBill = _mapper.Map<UsersBill>(userBillDto);
                var insertResult = await _billRepository.AddUserBill(userBill);
                //return insertResult;
                var confirmCode = new Random().Next(1000, 9999);
                Tuple<AutoBillPayment, int> tupleAutoBillPayment = null;
                switch (insertResult.Item2)
                {
                    case 0:
                        if (userBillDto.AutoPaymentActiveated)
                        {
                            tupleAutoBillPayment = await _billRepository.AddAutoBillPayment(new AutoBillPayment()
                            {
                                UsersBillId = insertResult.Item1.Id,
                                MaxAmountPayment = userBillDto.MaxAmountPayment.Value,
                                DailyPayment = userBillDto.DailyPayment.Value,
                                ExpireDatePayment = userBillDto.ExpireDatePayment.Value,
                                ConfirmCode = confirmCode
                            });
                            if (tupleAutoBillPayment.Item2 == 0)
                            {
                                await _billRepository.AddAutoBillPaymentAudit(new AutoBillPaymentAudit()
                                {
                                    AutoBillPaymentId = tupleAutoBillPayment.Item1.Id,
                                    Status = 1,
                                    DateActivity = DateTime.Now,
                                    ConfirmCode = confirmCode
                                });

                                var sendsms = await SendSmsAutoBillPayment(new SendSmsAutoBillDto
                                {
                                    BillID = userBillDto.BillId,
                                    MobileNo = userBillDto.MobileNo,
                                    UserId = userBillDto.UserID.Value
                                });

                                return sendsms;
                            }
                            else
                            {
                                return new ResponseBaseDto
                                {
                                    Status = -4,
                                    Message = "خطا در درج قبض خودکار"
                                };
                            }
                        }
                        else
                        {
                            var activatedResult = await _billRepository.ActiveUsersBill(userBill);
                            if (activatedResult.Item1 != null && activatedResult.Item2)
                            {
                                return new ResponseBaseDto
                                {
                                    Status = 0,
                                    Message = "قبض با موفقیت افزوده شد"
                                };
                            }
                            else if (activatedResult.Item1 != null && !activatedResult.Item2)
                            {
                                return new ResponseBaseDto
                                {
                                    Status = 1,
                                    Message = "فعال سازی قبض قبلا انجام شده است"
                                };
                            }
                            else
                            {
                                return new ResponseBaseDto
                                {
                                    Status = -3,
                                    Message = "قبض مورد نظر شما یافت نشد"
                                };
                            }
                        }
                    case -1:
                        if (userBillDto.AutoPaymentActiveated)
                        {
                            tupleAutoBillPayment = await _billRepository.AddAutoBillPayment(new AutoBillPayment()
                            {
                                UsersBillId = insertResult.Item1.Id,
                                MaxAmountPayment = userBillDto.MaxAmountPayment.Value,
                                DailyPayment = userBillDto.DailyPayment.Value,
                                ExpireDatePayment = userBillDto.ExpireDatePayment.Value,
                                ConfirmCode = confirmCode
                            });
                            if (tupleAutoBillPayment.Item2 == 0)
                            {
                                await _billRepository.AddAutoBillPaymentAudit(new AutoBillPaymentAudit()
                                {
                                    AutoBillPaymentId = tupleAutoBillPayment.Item1.Id,
                                    Status = 1,
                                    DateActivity = DateTime.Now,
                                    ConfirmCode = confirmCode
                                });

                                var sendsms = await SendSmsAutoBillPayment(new SendSmsAutoBillDto
                                {
                                    BillID = userBillDto.BillId,
                                    MobileNo = userBillDto.MobileNo,
                                    UserId = userBillDto.UserID.Value
                                });

                                return sendsms;
                            }
                            else
                            {
                                return new ResponseBaseDto
                                {
                                    Status = -4,
                                    Message = "خطا در درج قبض خودکار"
                                };
                            }
                        }
                        else
                        {
                            var activatedResult = await _billRepository.ActiveUsersBill(userBill);
                            if (activatedResult.Item1 != null && activatedResult.Item2)
                            {
                                return new ResponseBaseDto
                                {
                                    Status = 0,
                                    Message = "قبض با موفقیت افزوده شد"
                                };
                            }
                            else if (activatedResult.Item1 != null && !activatedResult.Item2)
                            {
                                return new ResponseBaseDto
                                {
                                    Status = 1,
                                    Message = "شناسه قبض قبلا افزوده شده است"
                                };
                            }
                            else
                            {
                                return new ResponseBaseDto
                                {
                                    Status = -3,
                                    Message = "قبض مورد نظر شما برای فعال سازی یافت نشد"
                                };
                            }

                        }

                    case -2:
                        return new ResponseBaseDto
                        {
                            Status = -19,
                            Message = "خطا در درج قبض"
                        };


                    case -3:
                        return new ResponseBaseDto
                        {
                            Status = -20,
                            Message = "خطای نا شناخته،خطا در روند افزودن قبض"
                        };

                    default:
                        return new ResponseBaseDto
                        {
                            Status = -20,
                            Message = "خطای نا شناخته،خطا در روند افزودن قبض"
                        };
                }
            }
            catch (Exception)
            {
                return new ResponseBaseDto
                {
                    Status = -21,
                    Message = "خطا در روند افزودن قبض"
                };
            }
        }
        public async Task<ResponseBaseDto> ActiveUserBill(UserBillDto userBillDto)
        {
            try
            {
                var userBill = _mapper.Map<UsersBill>(userBillDto);
                var activatedResult = await _billRepository.ActiveUsersBill(userBill);
                if (activatedResult.Item1 != null && activatedResult.Item2)
                {
                    return new ResponseBaseDto
                    {
                        Status = 0,
                        Message = "فعال سازی قبض با موفقیت انجام شد"
                    };
                }
                else if (activatedResult.Item1 != null && !activatedResult.Item2)
                {
                    return new ResponseBaseDto
                    {
                        Status = 0,
                        Message = "فعال سازی قبض قبلا انجام شده است"
                    };
                }
                else
                {
                    return new ResponseBaseDto
                    {
                        Status = 3,
                        Message = "قبض مورد نظر شما یافت نشد"
                    };
                }
            }
            catch (Exception ex)
            {
                return new ResponseBaseDto
                {
                    Status = -3,
                    Message = "خطا در روند فعال سازی قبض"
                };
            }
        }
        public async Task<ResponseBaseDto> DeleteUserBill(UserBillDto userBillDto)
        {
            try
            {
                var userBill = _mapper.Map<UsersBill>(userBillDto);
                var deletedResult = await _billRepository.DeleteUsersBill(userBill);
                if (deletedResult.Item1 != null && deletedResult.Item2)
                {
                    return new ResponseBaseDto
                    {
                        Status = 0,
                        Message = "قبض با موفقیت حذف گردید"
                    };
                }
                else if (deletedResult.Item1 != null && !deletedResult.Item2)
                {
                    return new ResponseBaseDto
                    {
                        Status = -1,
                        Message = "قبض مورد نظر، قبلا حذف شده است"
                    };
                }
                else
                {
                    return new ResponseBaseDto
                    {
                        Status = -2,
                        Message = "قبض مورد نظر یافت نشد"
                    };
                }
            }
            catch (Exception ex)
            {
                return new ResponseBaseDto
                {
                    Status = -3,
                    Message = "خطا در روند اجرای حذف قبض"
                };
            }
        }
        public async Task<List<UserBillDto>> GetUserBill(long userId)
        {
            try
            {
                var usersBills = await _billRepository.GetUsersBills(userId);
                var userBill = _mapper.Map<List<UserBillDto>>(usersBills);
                return userBill;
            }
            catch (Exception)
            {

                return null;
            }
        }
        public async Task<List<BillTypeDto>> GetBillType()
        {
            var billTypeResult = new List<BillTypeDto>();
            try
            {
                var billType = await _billRepository.GetBillType();
                billTypeResult = _mapper.Map<List<BillTypeDto>>(billType);
                return billTypeResult;
            }
            catch (Exception)
            {

                return billTypeResult;
            }
        }
        public async Task<List<OrganizationDto>> GetOrganization()
        {
            var OrganizationResult = new List<OrganizationDto>();
            try
            {
                var Organization = await _billRepository.GetOrganization();
                OrganizationResult = _mapper.Map<List<OrganizationDto>>(Organization);
                return OrganizationResult;
            }
            catch (Exception)
            {

                return OrganizationResult;
            }
        }

        public async Task<bool> UpdateBillTable(long orderId, int status, long token, long RRN, string message)
        {
            try
            {
                var reps = await _billRepository.UpdateBillTable(orderId, status, token, RRN, message);
                return reps;
            }
            catch (Exception)
            {
                return false;

            }
        }

        public async Task<bool> UpdateBillTableForWallet(long orderId, int status, string message, long returnIdWallet, long DetailOrderID, string response)
        {
            try
            {
                var reps = await _billRepository.UpdateBillTableForWallet(orderId, status, message, returnIdWallet, response);
                return reps;
            }
            catch (Exception)
            {
                return false;

            }
        }

        public async Task<bool> AddWalletPayBillTransaction(WalletPayBillTransactionDto walletPayBillDto)
        {
            try
            {
                var result = _mapper.Map<WalletPayBillTransaction>(walletPayBillDto);
                var resp = await _billRepository.AddWalletPayBillTransaction(result);

                return resp;
            }
            catch (Exception)
            {
                return false;

            }
        }
        public async Task<bool> updateBillRequestDetail(long orderID, long Token, string TransactionDate, int Paystatus)
        {
            try
            {
                //تراکنش توسط کیف پول مادر انجام شده
                var reps = await _billRepository.UpdateBillRequestDetailData(orderID, Token, TransactionDate, Paystatus);
                return reps;
            }
            catch (Exception)
            {
                return false;

            }
        }

        public async Task<PaymentDetailResponseDto> getPaymentDetail(PaymentDetailRequestDto req)
        {
            var response = new PaymentDetailResponseDto();
            response.bills = new List<PaidWalletBillsDto>();
            var resp = await _billRepository.getPaymentDetail(req.pgwToken);
            if (resp != null)
            {
                response.OrderId = resp.OrderID;
                response.MerchatnID = resp.MerchatnID;
                response.RRN = resp.RRN;
                response.ServiceMessage = resp.ServiceMessage;
                response.Status = resp.Status;
                response.Token = resp.Token;
                response.TotalAmount = resp.TotalAmount;
                response.TransType = resp.TransType;
                response.UserID = resp.UserID;
                response.WalletCode = resp.WalletId;

                foreach (var item in resp.BillRequestDetails)
                {
                    PaidWalletBillsDto bills = new PaidWalletBillsDto();
                    bills.Amount = item.Amount;
                    bills.BillId = item.BillID;
                    bills.OrderId = item.OrderId;
                    bills.PayDate = item.PayDate;
                    bills.PayId = item.PayId;
                    bills.PayStatus = item.Status;
                    bills.ReturnId = item.ReturnID;
                    response.bills.Add(bills);
                }
                return response;

            }
            else
            {
                /// read charge  table
                return null;
            }

        }

        public async Task<BillRequestDto> GetPayBills(long token)
        {
            var data = await _billRepository.GetPayBills(token);
            if (data != null)
            {
                BillRequestDto response = new BillRequestDto()
                {
                    BillRequestUniqID = data.BillRequestUniqID,
                    BussinessDate = data.BussinessDate,
                    CreateDate = data.CreateDate,
                    Id = data.Id,
                    MerchatnID = data.MerchatnID,
                    OrderID = data.OrderID,
                    RRN = data.RRN,
                    ServiceMessage = data.ServiceMessage,
                    WalletId = data.WalletId,
                    Token = data.Token,
                    Status = data.Status,
                    TotalAmount = data.TotalAmount,
                    TransType = data.TransType,
                    UserID = data.UserID
                };
                response.BillRequestDetails = new List<BillRequestDetailDto>();
                foreach (var item in data.BillRequestDetails)
                {
                    BillRequestDetailDto detailDto = new BillRequestDetailDto();
                    detailDto.Amount = item.Amount;
                    detailDto.BillID = item.BillID;
                    detailDto.BillRequestID = item.BillRequestID;
                    detailDto.CreateDate = item.CreateDate;
                    detailDto.Id = item.Id;
                    detailDto.OrderId = item.OrderId;
                    detailDto.OrganizationID = item.OrganizationID;
                    detailDto.PayDate = item.PayDate;
                    detailDto.PayId = item.PayId;
                    detailDto.ReturnID = item.ReturnID;
                    detailDto.Status = item.Status;
                    response.BillRequestDetails.Add(detailDto);
                }
                return response;
            }
            else
            {
                return null;
            }
        }

        public async Task<GetBillPaymentHistoryResponseDto> GetPaymentHistory(GetBillPaymentHistoryRequestDto getBill)
        {
            try
            {
                GetBillPaymentHistoryResponseDto historyResponse = new GetBillPaymentHistoryResponseDto();
                var data = await _billRepository.GetPaymentHistory(getBill);
                var resp = _mapper.Map<List<BillRequestDto>>(data.Item2);
                List<BillRequestDto> billList = new List<BillRequestDto>();
                foreach (var item in resp)
                {
                    BillRequestDto bill = new BillRequestDto();
                    bill.BillRequestDetails = item.BillRequestDetails;
                    bill.BillRequestUniqID = item.BillRequestUniqID;
                    bill.BussinessDate = item.BussinessDate;
                    bill.CreateDate = item.CreateDate;
                    bill.MerchatnID = item.MerchatnID;
                    bill.OrderID = item.OrderID;
                    bill.RRN = item.RRN;
                    bill.ServiceMessage = item.ServiceMessage;
                    bill.Status = item.Status;
                    bill.Token = item.Token;
                    bill.TotalAmount = item.TotalAmount;
                    bill.TransType = item.TransType;
                    bill.UserID = item.UserID;
                    bill.WalletId = item.WalletId;
                    billList.Add(bill);
                }
                historyResponse.TotalCount = data.Item1;
                historyResponse.bills = billList;
                return historyResponse;
            }
            catch (Exception)
            {

                return null;
            }
        }

        public async Task<UserBillPaymentHistoryResponseDto> GetUserPaymentHistory(UserBillPaymentHistoryRequestDto requestDto)
        {
            UserBillPaymentHistoryResponseDto resp = new UserBillPaymentHistoryResponseDto();
            try
            {
                var data = await _billRepository.GetUserPaymentHistory(requestDto);
                if (data != null)
                {

                    var response = _mapper.Map<List<BillRequestDto>>(data.Item2);
                    List<BillRequestDto> billList = new List<BillRequestDto>();
                    foreach (var item in response)
                    {
                        BillRequestDto bill = new BillRequestDto();
                        bill.BillRequestDetails = item.BillRequestDetails;
                        bill.BillRequestUniqID = item.BillRequestUniqID;
                        bill.BussinessDate = item.BussinessDate;
                        bill.CreateDate = item.CreateDate;
                        bill.MerchatnID = item.MerchatnID;
                        bill.OrderID = item.OrderID;
                        bill.RRN = item.RRN;
                        bill.ServiceMessage = item.ServiceMessage;
                        bill.Status = item.Status;
                        bill.Token = item.Token;
                        bill.TotalAmount = item.TotalAmount;
                        bill.TransType = item.TransType;
                        bill.UserID = item.UserID;
                        bill.WalletId = item.WalletId;
                        billList.Add(bill);
                    }
                    resp.TotalCount = data.Item1;
                    resp.bills = billList;
                    return resp;
                }
                else
                {
                    return resp;
                }
            }
            catch (Exception)
            {

                return resp;
            }
        }

        public async Task<List<WalletPayBillDto>> GetReadyBillToPayWithWallet()
        {
            try
            {
                var data = await _billRepository.GetReadyBillToPayWithWallet();
                var requestDto = _mapper.Map<List<WalletPayBillDto>>(data);
                return requestDto;
            }
            catch (Exception e)
            {
                return null;
            }
        }
        public async Task<ResponseBaseDto<GetTollBillResponseDto>> GetTollBillByOrderId(int OrderId)
        {
            try
            {
                GetTollBillResponseDto responseDto = new GetTollBillResponseDto();
                long totalAmount = 0;
                var bills = await _billRepository.GetToll(OrderId);
                if (bills != null)
                {
                    if (bills.RRN == 0)
                    {
                        responseDto.BillPayMessage = "پرداخت قبض ناموفق است";
                        responseDto.Status = -1;
                    }
                    else
                    {
                        responseDto.BillPayMessage = "پرداخت موفق";
                    }

                    List<TollBillsDto> tollBillsList = new List<TollBillsDto>();
                    foreach (var item in bills.TollPlateBill)
                    {
                        TollBillsDto tollBills = new TollBillsDto();
                        tollBills.Amount = item.Amount;
                        tollBills.BillId = item.BillID;
                        tollBills.TraversDate = item.TraversDate;
                        totalAmount = totalAmount + item.Amount;
                        tollBillsList.Add(tollBills);
                    }

                    responseDto.Amount = totalAmount;
                    responseDto.OrderId = bills.OrderID;
                    responseDto.RRN = bills.RRN;
                    responseDto.token = bills.PGWToken;
                    responseDto.TollBills = tollBillsList;
                    responseDto.CreateDate = bills.CreateDate;

                    return new ResponseBaseDto<GetTollBillResponseDto>()
                    {
                        Message = "عملیات موفق",
                        Status = 0,
                        Data = responseDto
                    };
                }
                else
                {
                    return new ResponseBaseDto<GetTollBillResponseDto>()
                    {
                        Message = "قبض یافت نشد",
                        Status = -1,
                        Data = null
                    };
                }
            }
            catch (Exception)
            {
                return new ResponseBaseDto<GetTollBillResponseDto>()
                {
                    Message = "خطا در دریافت اطلاعات قبض",
                    Status = -99
                };
            }
        }

        public async Task<bool> UpdateRequestDetail(string BillId, string PayId, long orderid, long PayOrderId)
        {
            var resonse = await _billRepository.UpdateBillRequestDetail(BillId, PayId, orderid, PayOrderId);
            return resonse;
        }

        public async Task<bool> UpdateWalletPayBill(long OrderId, long WalletReturnId, string ServiceMessage, int PayStatus, long payOrderID)
        {
            var response = await _billRepository.UpdateWalletPayBill(OrderId, WalletReturnId, ServiceMessage, PayStatus, payOrderID);
            return response;
        }

        #endregion

        #region call proxy
        [LoggingAspect]
        public async Task<ResponseBaseDto<NigcBillInquiryResponseDto>> NigcBillInquiry(NigcBillInquiryRequestDto nigcBillInquiry)
        {
            _logger.Log(DateTime.Now.Ticks, null, $"📥 BillService(NigcBillInquiry {JsonConvert.SerializeObject(nigcBillInquiry)}'", null);
            try
            {
                var nigcBillInquiryResponse = new ResponseBaseDto<NigcBillInquiryResponseDto>();
                nigcBillInquiryResponse = await _billProxy.NigcBillInquiry(nigcBillInquiry);
                _logger.Log(DateTime.Now.Ticks, null, $"📥 BillService(NigcBillInquiry Response {JsonConvert.SerializeObject(nigcBillInquiryResponse)}'", null);
                return (new ResponseBaseDto<NigcBillInquiryResponseDto>
                {
                    Message = nigcBillInquiryResponse.Message,
                    Status = nigcBillInquiryResponse.Status,
                    Data = nigcBillInquiryResponse.Data

                });
            }
            catch (Exception ex)
            {
                _logger.Log(DateTime.Now.Ticks, null, $"📥 BillService(NigcBillInquiry exception {JsonConvert.SerializeObject(ex)}'", ex);
                return (new ResponseBaseDto<NigcBillInquiryResponseDto>
                {
                    Message = "خطا در فراخوانی سرویس",
                    Status = -99,
                    Data = null

                });
            }
        }

        [LoggingAspect]
        public async Task<ResponseBaseDto<List<MciBillInquiryResponseDto>>> MciBillInquiry(MciBillInquiryRequestDto mciBill)
        {
            try
            {
                _logger.Log(DateTime.Now.Ticks, null, $"📥 BillService(MciBillInquiry {JsonConvert.SerializeObject(mciBill)}'", null);
                var ResponseMciResult = await _billProxy.MciBillInquiry(mciBill);
                _logger.Log(DateTime.Now.Ticks, null, $"📥 BillService(MciBillInquiry response {JsonConvert.SerializeObject(ResponseMciResult)}'", null);
                return (new ResponseBaseDto<List<MciBillInquiryResponseDto>>
                {
                    Message = ResponseMciResult.Message,
                    Status = ResponseMciResult.Status,
                    Data = ResponseMciResult.Data

                });
            }
            catch (Exception ex)
            {
                _logger.Log(DateTime.Now.Ticks, null, $"📥 BillService(MciBillInquiry exception {JsonConvert.SerializeObject(ex)}'", ex);
                return (new ResponseBaseDto<List<MciBillInquiryResponseDto>>
                {
                    Message = "خطا در فراخوانی سرویس",
                    Status = -99,
                    Data = null
                });
            }
        }

        [LoggingAspect]
        public async Task<ResponseBaseDto<List<TciInquiryResponseDto>>> TciBillInquiry(TciInquiryRequestDto tciBill)
        {
            try
            {
                _logger.Log(DateTime.Now.Ticks, null, $"📥 BillService(TciBillInquiry  {JsonConvert.SerializeObject(tciBill)}'", null);
                var TciResp = await _billProxy.TciInquiry(tciBill);
                _logger.Log(DateTime.Now.Ticks, null, $"📥 BillService(TciBillInquiry response {JsonConvert.SerializeObject(TciResp)}'", null);
                return (new ResponseBaseDto<List<TciInquiryResponseDto>>
                {
                    Message = TciResp.Message,
                    Status = TciResp.Status,
                    Data = TciResp.Data
                });
            }
            catch (Exception ex)
            {
                _logger.Log(DateTime.Now.Ticks, null, $"📥 BillService(TciBillInquiry exception {JsonConvert.SerializeObject(ex)}'", ex);
                return (new ResponseBaseDto<List<TciInquiryResponseDto>>
                {
                    Message = "خطا در فراخوانی سرویس",
                    Status = -99,
                    Data = null
                });
            }
        }

        [LoggingAspect]
        public async Task<ResponseBaseDto<BarghBillInquiryResponseDto>> BarghInquiry(BarghBillInquiryRequestDto barghBill)
        {
            try
            {
                _logger.Log(DateTime.Now.Ticks, null, $"📥 BillService(BarghInquiry  {JsonConvert.SerializeObject(barghBill)}'", null);
                var response = await _billProxy.BarghBillInquiry(barghBill);
                _logger.Log(DateTime.Now.Ticks, null, $"📥 BillService(BarghInquiry  response {JsonConvert.SerializeObject(response)}'", null);
                if (response.Status == 1)
                {
                    return (new ResponseBaseDto<BarghBillInquiryResponseDto>
                    {
                        Message = response.Message,
                        Status = 1,
                        Data = response.Data
                    });
                }
                else if (response.Status != 0)
                {
                    return (new ResponseBaseDto<BarghBillInquiryResponseDto>
                    {
                        Message = response.Message,
                        Status = -1,
                        Data = null
                    });
                }
                response.Data.Amount = response.Data.Amount.Replace(",", "");
                return (new ResponseBaseDto<BarghBillInquiryResponseDto>
                {
                    Message = response.Message,
                    Status = response.Status,
                    Data = response.Data

                });
            }
            catch (Exception ex)
            {
                _logger.Log(DateTime.Now.Ticks, null, $"📥 BillService(BarghInquiry exception {JsonConvert.SerializeObject(ex)}'", ex);
                return (new ResponseBaseDto<BarghBillInquiryResponseDto>
                {
                    Message = "خطا در فراخوانی سرویس",
                    Status = -99,
                    Data = null
                });
            }
        }

        [LoggingAspect]
        public async Task<ResponseBaseDto<BillInfoResponseDto>> GetBillInfo(BillInfoRequestDto BillInfoRequestDto)
        {
            try
            {
                _logger.Log(DateTime.Now.Ticks, null, $"📥 BillService(GetBillInfo  {JsonConvert.SerializeObject(BillInfoRequestDto)}'", null);
                var response = new BillInfoResponseDto();
                var billresponse = await _ipgServiceProxy.GetBillInfo(BillInfoRequestDto);
                _logger.Log(DateTime.Now.Ticks, null, $"📥 BillService(GetBillInfo response  {JsonConvert.SerializeObject(response)}'", null);
                if (billresponse.Status != 0)
                {
                    return (new ResponseBaseDto<BillInfoResponseDto>
                    {
                        Message = billresponse.StatusDescription,
                        Status = -1,
                        Data = null

                    });
                }
                else
                {
                    var org = await _billRepository.GetOrganizationId(Convert.ToInt32(billresponse.UtilityCode));
                    billresponse.OrganizationId = org;
                    return (new ResponseBaseDto<BillInfoResponseDto>
                    {
                        Message = billresponse.StatusDescription,
                        Status = response.Status,
                        Data = billresponse

                    });
                }
            }
            catch (Exception ex)
            {
                _logger.Log(DateTime.Now.Ticks, null, $"📥 BillService(GetBillInfo exception {JsonConvert.SerializeObject(ex)}'", ex);
                return (new ResponseBaseDto<BillInfoResponseDto>
                {
                    Message = "خطا در فراخوانی سرویس",
                    Status = -99,
                    Data = null
                });
            }
        }
        [LoggingAspect]
        public async Task<ResponseBaseDto<List<ValidBillResponseDto>>> GetBillInfoValidation(BillValidRequestDto BillInfoRequestDto)
        {
            try
            {
                _logger.Log(DateTime.Now.Ticks, null, $"📥 BillService(GetBillInfoValidation  {JsonConvert.SerializeObject(BillInfoRequestDto)}'", null);
                var validBills = new List<ValidBillResponseDto>();
                var billData = new ValidBillResponseDto();
                foreach (var item in BillInfoRequestDto.Bills)
                {
                    long orgId = 0;
                    var valid = false;
                    BillInfoRequestDto bill = new BillInfoRequestDto()
                    {
                        BillId = item.BillId,
                        PayId = item.PayId
                    };
                    var billresponse = await _ipgServiceProxy.GetBillInfo(bill);
                    if (billresponse.Status == 0)
                    {
                        valid = true;
                    }
                    if (billresponse.UtilityCode != null)
                    {

                        orgId = await _billRepository.GetOrganizationId(Convert.ToInt32(billresponse.UtilityCode));
                    }
                    billData = new ValidBillResponseDto()
                    {
                        Amount = billresponse.Amount,
                        BillId = item.BillId,
                        BillType = billresponse.BillType,
                        CompanyName = billresponse.CompanyName,
                        IsValid = valid,
                        PayId = item.PayId,
                        StatusDescription = billresponse.StatusDescription,
                        OrganizationId = orgId
                    };
                    validBills.Add(billData);
                    _logger.Log(DateTime.Now.Ticks, null, $"📥 BillService(GetBillInfoValidation response {JsonConvert.SerializeObject(validBills)}'", null);
                }
                return new ResponseBaseDto<List<ValidBillResponseDto>>
                {
                    Data = validBills,
                    Message = "عملیات موفق",
                    Status = 0
                };
            }
            catch (Exception ex)
            {
                _logger.Log(DateTime.Now.Ticks, null, $"📥 BillService(GetBillInfoValidation exception {JsonConvert.SerializeObject(ex)}'", ex);
                return (new ResponseBaseDto<List<ValidBillResponseDto>>
                {
                    Message = "خطا در فراخوانی سرویس",
                    Status = -99,
                    Data = null
                });
            }
        }
        [LoggingAspect]
        public async Task<ResponseBaseDto<TollBillInquiryResponseDto>> TollBillInquiry(TollBillInquiryRequestDto inquiryRequestDto)
        {
            try
            {
                _logger.Log(DateTime.Now.Ticks, null, $"📥 BillService(TollBillInquiry {JsonConvert.SerializeObject(inquiryRequestDto)}'", null);
                var response = await _billProxy.TollBillInquiry(inquiryRequestDto);
                _logger.Log(DateTime.Now.Ticks, null, $"📥 BillService(TollBillInquiry response {JsonConvert.SerializeObject(response)}'", null);
                return (new ResponseBaseDto<TollBillInquiryResponseDto>
                {
                    Message = response.Message,
                    Status = response.Status,
                    Data = response.Data

                });
            }
            catch (Exception ex)
            {
                _logger.Log(DateTime.Now.Ticks, null, $"📥 BillService(TollBillInquiry exception {JsonConvert.SerializeObject(ex)}'", ex);
                return (new ResponseBaseDto<TollBillInquiryResponseDto>
                {
                    Message = "خطا در فراخوانی سرویس",
                    Status = -99,
                    Data = null
                });
            }
        }
        [LoggingAspect]
        public async Task<ResponseBaseDto<TollBillSetPayResponseDto>> TollBillSetPay(TollBillSetPayRequestDto tollBillSetPay)
        {
            try
            {
                _logger.Log(DateTime.Now.Ticks, null, $"📥 BillService(TollBillSetPay {JsonConvert.SerializeObject(tollBillSetPay)}'", null);
                var response = await _billProxy.TollBillSetPay(tollBillSetPay);
                _logger.Log(DateTime.Now.Ticks, null, $"📥 BillService(TollBillSetPay response {JsonConvert.SerializeObject(response)}'", null);
                return (new ResponseBaseDto<TollBillSetPayResponseDto>
                {
                    Message = response.Message,
                    Status = response.Status,
                    Data = response.Data
                });
            }
            catch (Exception ex)
            {
                _logger.Log(DateTime.Now.Ticks, null, $"📥 BillService(TollBillSetPay exception {JsonConvert.SerializeObject(ex)}'", ex);
                return (new ResponseBaseDto<TollBillSetPayResponseDto>
                {
                    Message = "خطا در فراخوانی سرویس",
                    Status = -99,
                    Data = null
                });
            }
        }
        [LoggingAspect]
        public async Task<ResponseBaseDto<BatchBillPaymentResponseDto>> BatchBillPayment(BatchBillPaymentRequestDto batchBillPayment)
        {
            try
            {
                _logger.Log(DateTime.Now.Ticks, null, $"📥 BillService(BatchBillPayment {JsonConvert.SerializeObject(batchBillPayment)}'", null);
                List<ValidBillDataDto> billData = new List<ValidBillDataDto>();
                var response = new ResponseBaseDto<BatchBillPaymentResponseDto>();
                var responseDetail = new BatchBillPaymentResponseDto();
                ValidBillDataDto Bill = new ValidBillDataDto();
                var payStatus = 0;
                var transType = 0;
                int validBillCount;
                if (batchBillPayment.WalletId == 0)
                {
                    payStatus = PaymentStatus.InitialRegister;
                    transType = TransactionTypeStatus.ParentWallet;
                }
                else
                {
                    payStatus = PaymentStatus.ReadyToPayWithWallet;
                    transType = TransactionTypeStatus.Wallet;
                }


                #region CheckValidation of bill 
                foreach (var item in batchBillPayment.Bills)
                {

                    var valid = false;
                    long orgId = 0;
                    BillInfoRequestDto bill = new BillInfoRequestDto()
                    {
                        BillId = item.BillId,
                        PayId = item.PayId
                    };
                    var billresponse = await _ipgServiceProxy.GetBillInfo(bill);

                    if (billresponse.Status == 0)
                    {
                        valid = true;
                        ///گرفتن شناسه جدول سازمان از یوتیلیتی کد جهت ثبت
                        if (billresponse.UtilityCode != null)
                        {

                            orgId = await _billRepository.GetOrganizationId(Convert.ToInt32(billresponse.UtilityCode));
                        }

                    }
                    Bill = new ValidBillDataDto()
                    {
                        Amount = billresponse.Amount,
                        BillId = item.BillId,
                        BillType = billresponse.BillType,
                        CompanyName = billresponse.CompanyName,
                        IsValid = valid,
                        PayId = item.PayId,
                        StatusDescription = billresponse.StatusDescription,
                        OrganizationId = orgId
                    };
                    billData.Add(Bill);

                }
                #endregion
                #region insert data to db
                var data = billData.Where(f => f.IsValid == true).ToList();
                validBillCount = data.Count;
                var orderID = Utility.Utility.GenerateRandomOrderID();
                long totalAmount = 0;
                var billRequest = new BillRequest();
                foreach (var dataItem in data)
                {
                    totalAmount = dataItem.Amount + totalAmount;
                    var detailOrderID = Utility.Utility.GenerateRandomOrderID();
                    billRequest.BillRequestDetails.Add(new BillRequestDetail()
                    {
                        Amount = dataItem.Amount,
                        BillRequestID = billRequest.Id,
                        OrganizationID = dataItem.OrganizationId,
                        PayId = dataItem.PayId,
                        BillID = dataItem.BillId,
                        CreateDate = DateTime.Now,
                        OrderId = detailOrderID,
                        Status = payStatus
                    });
                }

                billRequest.MerchatnID = 0;
                billRequest.TotalAmount = totalAmount;
                billRequest.UserID = batchBillPayment.UserId;
                billRequest.WalletId = batchBillPayment.WalletId;
                billRequest.TransType = transType;
                billRequest.CreateDate = DateTime.Now;
                billRequest.OrderID = orderID;
                billRequest.Status = payStatus;
                _logger.Log(DateTime.Now.Ticks, null, $"📥 BillService(BatchBillPayment insert data to database {JsonConvert.SerializeObject(billRequest)}'", null);
                var addBillRequest = _billRepository.InsertBillrequest(billRequest);
                if (!addBillRequest)
                {
                    return new ResponseBaseDto<BatchBillPaymentResponseDto>()
                    {
                        Message = "خطا در فرایند ثبت اطلاعات لطفا مجددا تلاش نمایید",
                        Status = -1,
                        Data = null
                    };
                }

                #endregion
                if (payStatus == PaymentStatus.PayWithParentWallet)
                {
                    IpgSaleServiceRequestDto ipgTokenReq = new IpgSaleServiceRequestDto()
                    {
                        AdditionalData = "",
                        //Amount = billRequest.TotalAmount,
                        Amount = totalAmount,
                        ///call back pay bill  + check mid
                        CallBackUrl = _setting.CallBackUrlBatchBillPay,
                        LoginAccount = _setting.LoginAccount,
                        OrderId = orderID,
                        Originator = ""
                    };
                    _logger.Log(DateTime.Now.Ticks, null, $"📥 BillService(BatchBillPayment get ipg token  {JsonConvert.SerializeObject(ipgTokenReq)}'", null);
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
                            Amount = totalAmount,
                            DestinationWalletId = _setting.MotherWalletCode.ToString(),
                            WalletType = 21, /// ?????????
                            CreateDate = DateTime.Now,
                            IsSendToSettlement = false,
                            WalletOrderId = orderID,
                            UserId = batchBillPayment.UserId,
                            PGWToken = getSaleToken.Token.ToString(),
                            TrackingCode = obj.ToString(),
                            ApplicationId = 8,
                            PaymentStatus = PaymentStatus.UnSuccess

                        };
                        var chargeData = _mapper.Map<Charge>(charge);
                        var insertIntoChargeDb = await _walletRepository.InsertChargeData(chargeData);
                        if (!insertIntoChargeDb)
                        {
                            return new ResponseBaseDto<BatchBillPaymentResponseDto>()
                            {
                                Message = "خطا در فرایند شارژ کیف پول",
                                Status = -1,
                                Data = null
                            };

                        }

                        responseDetail.OrderId = orderID;
                        responseDetail.Token = getSaleToken.Token;
                        responseDetail.Bills = billData;
                        responseDetail.ValidBillCount = validBillCount;
                        _logger.Log(DateTime.Now.Ticks, null, $"📥 BillService(BatchBillPayment response  {JsonConvert.SerializeObject(responseDetail)}'", null);
                        return new ResponseBaseDto<BatchBillPaymentResponseDto>()
                        {
                            Message = "عملیات موفق",
                            Status = 0,
                            Data = responseDetail
                        };

                    }
                    else
                    {
                        BillRequest UpdateData = new BillRequest()
                        {
                            Token = getSaleToken.Token,
                            OrderID = orderID,
                            ServiceMessage = getSaleToken.Message
                        };
                        var resp = await _billRepository.UpdateBillRequest(UpdateData);
                        _logger.Log(DateTime.Now.Ticks, null, $"📥 BillService(BatchBillPayment response  {JsonConvert.SerializeObject(UpdateData)}'", null);
                        return new ResponseBaseDto<BatchBillPaymentResponseDto>()
                        {
                            Message = getSaleToken.Message,
                            Status = getSaleToken.status,
                            Data = null
                        };

                    }

                }
                else
                {
                    responseDetail.OrderId = orderID;
                    responseDetail.Token = 0;
                    responseDetail.Bills = billData;
                    responseDetail.ValidBillCount = validBillCount;
                    return new ResponseBaseDto<BatchBillPaymentResponseDto>()
                    {
                        Message = "عملیات موفق",
                        Status = 0,
                        Data = responseDetail
                    };

                }
            }
            catch (Exception)
            {
                return (new ResponseBaseDto<BatchBillPaymentResponseDto>
                {
                    Message = "خطا در فراخوانی سرویس",
                    Status = -99,
                    Data = null
                });
            }
        }

        public async Task<ResponseBaseDto<ConfirmBatchBillPaymentResponseDto>> ConfirmBatchBillPayment(ConfrimBillRequestDto requestDto)
        {
            try
            {
                var message = "";
                _logger.Log(DateTime.Now.Ticks, null, $"📥 BillService(ConfirmBatchBillPayment {JsonConvert.SerializeObject(requestDto)}'", null);
                var batchPayBills = await _billRepository.GetPayBills(requestDto.Token);
                List<PaidWalletBillsDto> paids = new List<PaidWalletBillsDto>();

                if (requestDto.PgwStatus != 0)
                {
                    var update = await UpdateBillTable(requestDto.OrderId, PaymentStatus.UnSuccess, requestDto.Token, requestDto.RRN, message);
                    foreach (var item in batchPayBills.BillRequestDetails)
                    {
                        PaidWalletBillsDto bills = new PaidWalletBillsDto();

                        bills.Amount = item.Amount;
                        bills.BillId = item.BillID;
                        bills.OrderId = item.OrderId;
                        bills.PayId = item.PayId;
                        bills.PayStatus = item.Status;
                        bills.ReturnId = item.ReturnID;
                        paids.Add(bills);
                    }
                    ConfirmBatchBillPaymentResponseDto responseDto = new ConfirmBatchBillPaymentResponseDto()
                    {
                        bills = paids,
                        OrderId = requestDto.OrderId,
                        RRN = requestDto.RRN,
                        Token = requestDto.Token,
                        TotalAmount = requestDto.Amount

                    };
                    _logger.Log(DateTime.Now.Ticks, null, $"📥 BillService(ConfirmBatchBillPayment response{JsonConvert.SerializeObject(responseDto)}'", null);
                    return new ResponseBaseDto<ConfirmBatchBillPaymentResponseDto>()
                    {
                        Message = "خطا در دریافت اطلاعات قبوض",
                        Status = -1,
                        Data = responseDto
                    };
                }
                else
                {
                    ConfirmPaymentRequestDto confirm = new ConfirmPaymentRequestDto()
                    {
                        LoginAccount = _setting.LoginAccount,
                        Token = requestDto.Token
                    };
                    _logger.Log(DateTime.Now.Ticks, null, $"📥 BillService(ConfirmBatchBillPayment confirm ipg{JsonConvert.SerializeObject(confirm)}'", null);
                    var confirmResp = await _ipgServiceProxy.ConfirmPayment(confirm);
                    _logger.Log(DateTime.Now.Ticks, null, $"📥 BillService(ConfirmBatchBillPayment confirm ipg response{JsonConvert.SerializeObject(confirmResp)}'", null);
                    message = confirmResp.status == 0 ? "عملیات موفق" : "عملیات ناموفق";
                    Charge charge = new Charge()
                    {
                        PaymentStatus = confirmResp.status,
                        RRN = confirmResp.RRN,
                        PGWToken = requestDto.Token.ToString()
                    };
                    var resp = await _walletRepository.UpdateChargeTable(charge);
                    var update = await UpdateBillTable(requestDto.OrderId, PaymentStatus.ReadyToPayWithWallet, requestDto.Token, requestDto.RRN, message);
                    foreach (var item in batchPayBills.BillRequestDetails)
                    {
                        PaidWalletBillsDto bills = new PaidWalletBillsDto();
                        bills.Amount = item.Amount;
                        bills.BillId = item.BillID;
                        bills.OrderId = item.OrderId;
                        bills.PayId = item.PayId;
                        bills.PayStatus = item.Status;
                        bills.ReturnId = item.ReturnID;
                        paids.Add(bills);
                    }
                    ConfirmBatchBillPaymentResponseDto responseDto = new ConfirmBatchBillPaymentResponseDto()
                    {
                        bills = paids,
                        OrderId = requestDto.OrderId,
                        RRN = requestDto.RRN,
                        Token = requestDto.Token,
                        TotalAmount = requestDto.Amount

                    };
                    _logger.Log(DateTime.Now.Ticks, null, $"📥 BillService(ConfirmBatchBillPayment  response{JsonConvert.SerializeObject(responseDto)}'", null);
                    return new ResponseBaseDto<ConfirmBatchBillPaymentResponseDto>()
                    {
                        Message = message,
                        Status = confirmResp.status,
                        Data = responseDto
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.Log(DateTime.Now.Ticks, null, $"📥 BillService(ConfirmBatchBillPayment  exception{JsonConvert.SerializeObject(ex)}'", ex);
                return new ResponseBaseDto<ConfirmBatchBillPaymentResponseDto>()
                {
                    Message = "خطا در دریافت اطلاعات قبوض",
                    Status = -99,
                    Data = null
                };
            }

        }

        public async Task<bool> ConfirmBillPayment(ConfrimBillRequestDto confrimBillRequestDto)
        {
            try
            {
                var message = confrimBillRequestDto.PgwStatus == 0 ? "عملیات موفق" : "عملیات ناموفق";
                _logger.Log(DateTime.Now.Ticks, null, $"📥 BillService(ConfirmBillPayment {JsonConvert.SerializeObject(confrimBillRequestDto)}'", null);
                if (confrimBillRequestDto.PgwStatus != 0)
                {

                    await UpdateBillTable(confrimBillRequestDto.OrderId, PaymentStatus.UnSuccess, confrimBillRequestDto.Token, confrimBillRequestDto.RRN, message);
                }
                await UpdateBillTable(confrimBillRequestDto.OrderId, confrimBillRequestDto.PgwStatus, confrimBillRequestDto.Token, confrimBillRequestDto.RRN, message);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Log(DateTime.Now.Ticks, null, $"📥 BillService(ConfirmBillPayment  exception{JsonConvert.SerializeObject(ex)}'", ex);
                return false;
            }

        }
        public async Task<bool> ConfirmTollBillPayment(ConfrimBillRequestDto confrimBillRequestDto)
        {
            try
            {
                _logger.Log(DateTime.Now.Ticks, null, $"📥 BillService(ConfirmTollBillPayment {JsonConvert.SerializeObject(confrimBillRequestDto)}'", null);
                TollBillSetPayRequestDto setPayRequestDto = new TollBillSetPayRequestDto();
                if (confrimBillRequestDto.PgwStatus != 0)
                {
                    var update = await _billRepository.UpdateTollBillSetPayData(Convert.ToInt32(confrimBillRequestDto.OrderId), 0);
                    return false;
                }
                else
                {
                    var tolBills = await _billRepository.GetToll(Convert.ToInt32(confrimBillRequestDto.OrderId));
                    //_logger.Log(confrimBillRequestDto.OrderId, "list of bills => " + JsonConvert.SerializeObject(tolBills),tolBills);
                    if (tolBills == null)
                    {
                        return false;
                    }
                    ConfirmPaymentRequestDto confirm = new ConfirmPaymentRequestDto()
                    {
                        LoginAccount = _setting.TollBillLoginAccount,
                        Token = confrimBillRequestDto.Token
                    };
                    _logger.Log(DateTime.Now.Ticks, null, $"📥 BillService(ConfirmTollBillPayment confirm ipg{JsonConvert.SerializeObject(confirm)}'", null);
                    var confirmResp = await _ipgServiceProxy.ConfirmPayment(confirm);
                    _logger.Log(1368, "confirm result ==> " + JsonConvert.SerializeObject(confirmResp), confirmResp);
                    if (confirmResp.status == 0)
                    {
                        List<BillData> tollBills = new List<BillData>();
                        foreach (var item in tolBills.TollPlateBill)
                        {
                            BillData bills = new BillData();
                            bills.amount = item.Amount;
                            bills.billId = item.BillID;
                            bills.referenceCode = confrimBillRequestDto.RRN.ToString();
                            tollBills.Add(bills);
                        }
                        setPayRequestDto.PlateNumber = Convert.ToInt64(tolBills.PlateNumber);
                        setPayRequestDto.ReferenceNo = confrimBillRequestDto.RRN.ToString();
                        setPayRequestDto.TermNo = Convert.ToInt64(confrimBillRequestDto.TerminalNumber);
                        setPayRequestDto.Token = tolBills.TabanToken;
                        setPayRequestDto.TotalAmount = Convert.ToInt32(confrimBillRequestDto.Amount);
                        setPayRequestDto.BillList = tollBills;
                        _logger.Log(DateTime.Now.Ticks, null, $"📥 BillService(ConfirmTollBillPayment befor call set pay proxy{JsonConvert.SerializeObject(setPayRequestDto)}'", null);
                        var setPay = await TollBillSetPay(setPayRequestDto);
                        _logger.Log(DateTime.Now.Ticks, null, $"📥 BillService(ConfirmTollBillPayment TollBillSetPay response{JsonConvert.SerializeObject(setPay)}'", null);
                        if (setPay.Status != 0)
                        {
                            return false;
                        }
                        else
                        {
                            var update = await _billRepository.UpdateTollBillSetPayData(Convert.ToInt32(confrimBillRequestDto.OrderId), confrimBillRequestDto.RRN);
                            return true;
                        }
                    }
                    else
                    {
                        return false;
                    }
                }


            }
            catch (Exception ex)
            {
                _logger.Log(DateTime.Now.Ticks, null, $"📥 BillService(ConfirmTollBillPayment exception{JsonConvert.SerializeObject(ex)}'", ex);
                return false;
            }
        }

        public async Task<ResponseBaseDto<IrancellInquiryResponseDto>> IrancellBillInquiry(IrancellInquiryRequestDto irancellBill)
        {
            try
            {
                _logger.Log(DateTime.Now.Ticks, null, $"📥 BillService(IrancellBillInquiry  {JsonConvert.SerializeObject(irancellBill)}'", null);
                var irancellResp = await _billProxy.IrancellPostpaidBalance(irancellBill);
                _logger.Log(DateTime.Now.Ticks, null, $"📥 BillService(TciBillInquiry response {JsonConvert.SerializeObject(irancellResp)}'", null);
                return (new ResponseBaseDto<IrancellInquiryResponseDto>
                {
                    Message = irancellResp.Message,
                    Status = irancellResp.Status,
                    Data = irancellResp.Data
                });
            }
            catch (Exception ex)
            {
                _logger.Log(DateTime.Now.Ticks, null, $"📥 BillService(TciBillInquiry exception {JsonConvert.SerializeObject(ex)}'", ex);
                return (new ResponseBaseDto<IrancellInquiryResponseDto>
                {
                    Message = "خطا در فراخوانی سرویس",
                    Status = -99,
                    Data = null
                });
            }
        }

        #endregion

    }
    public interface IBillService
    {
        Task<ResponseBaseDto> AddUserBillByAutoPayment(UserBillDto userBillDto);
        Task<ResponseBaseDto> SendSmsAutoBillPayment(SendSmsAutoBillDto dto);
        Task<ResponseBaseDto> UpdateUserBillByAutoPayment(UserBillDto userBillDto);
        Task<ResponseBaseDto> ConfirmCodeAutoBill(ConfirmationAutoBillPaymentDto model);
        Task<List<UserBillDto>> GetUserBill(long userId);
        Task<ResponseBaseDto<NigcBillInquiryResponseDto>> NigcBillInquiry(NigcBillInquiryRequestDto nigcReq);
        Task<ResponseBaseDto<List<MciBillInquiryResponseDto>>> MciBillInquiry(MciBillInquiryRequestDto mciBill);
        Task<ResponseBaseDto<IrancellInquiryResponseDto>> IrancellBillInquiry(IrancellInquiryRequestDto irancellBill);
        Task<ResponseBaseDto<List<TciInquiryResponseDto>>> TciBillInquiry(TciInquiryRequestDto mciBill);
        Task<List<BillTypeDto>> GetBillType();
        Task<List<OrganizationDto>> GetOrganization();
        Task<ResponseBaseDto> DeleteUserBill(UserBillDto userBillDto);
        Task<ResponseBaseDto> ActiveUserBill(UserBillDto userBillDto);
        Task<ResponseBaseDto<BarghBillInquiryResponseDto>> BarghInquiry(BarghBillInquiryRequestDto barghBill);
        Task<ResponseBaseDto<BillInfoResponseDto>> GetBillInfo(BillInfoRequestDto BillInfoRequestDto);
        Task<ResponseBaseDto<TollBillInquiryResponseDto>> TollBillInquiry(TollBillInquiryRequestDto inquiryRequestDto);
        Task<bool> UpdateBillTable(long orderId, int status, long token, long RRN, string message);
        Task<bool> UpdateBussunessDateAutoBillPayment(long userId, string BillId, DateTime newTime);
        Task<bool> updateBillRequestDetail(long orderID, long Token, string TransactionDate, int status);
        Task<bool> UpdateRequestDetail(string BillId, string PayId, long orderid, long PayOrderId);
        Task<PaymentDetailResponseDto> getPaymentDetail(PaymentDetailRequestDto req);
        Task<bool> ConfirmBillPayment(ConfrimBillRequestDto confrimBillRequestDto);
        Task<BillRequestDto> GetPayBills(long token);
        Task<bool> UpdateWalletPayBill(long OrderId, long WalletReturnId, string ServiceMessage, int PayStatus, long payOrderID);
        Task<GetBillPaymentHistoryResponseDto> GetPaymentHistory(GetBillPaymentHistoryRequestDto getBill);
        Task<List<WalletPayBillDto>> GetReadyBillToPayWithWallet();
        Task<List<UserBillDto>> GetReadyAutoBillPaymentWithWallet();
        Task<bool> UpdateBillTableForWallet(long orderId, int status, string message, long returnIdWallet, long DetailOrderID, string response);
        Task<bool> AddWalletPayBillTransaction(WalletPayBillTransactionDto walletPayBillDto);
        Task<ResponseBaseDto<List<ValidBillResponseDto>>> GetBillInfoValidation(BillValidRequestDto BillInfoRequestDto);
        Task<ResponseBaseDto<BatchBillPaymentResponseDto>> BatchBillPayment(BatchBillPaymentRequestDto batchBillPayment);
        Task<bool> ConfirmTollBillPayment(ConfrimBillRequestDto confrimBillRequestDto);
        Task<ResponseBaseDto<GetTollBillResponseDto>> GetTollBillByOrderId(int OrderId);
        Task<ResponseBaseDto<ConfirmBatchBillPaymentResponseDto>> ConfirmBatchBillPayment(ConfrimBillRequestDto requestDto);
        Task<UserBillPaymentHistoryResponseDto> GetUserPaymentHistory(UserBillPaymentHistoryRequestDto requestDto);
        //Task<bool> ActiveAutoBillPayment(long UsersBillId);
        //Task<bool> DeleteAutoBillPayment(long UsersBillId);
    }
}
