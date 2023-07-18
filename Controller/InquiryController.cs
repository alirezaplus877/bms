using Application.Services;
using AutoMapper;
using Dto.Proxy.IPG;
using Dto.Proxy.Request.IPG;
using Dto.Proxy.Request.PecIs;
using Dto.Proxy.Request.Wallet;
using Dto.Proxy.Wallet;
using Dto.repository;
using Dto.Request;
using Dto.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using PEC.CoreCommon.Security.Encryptor;
using PecBMS.Helper.Identity;
using PecBMS.Model;
using PecBMS.ViewModel;
using PecBMS.ViewModel.Request;
using PecBMS.ViewModel.Request.PecISInquiry;
using PecBMS.ViewModel.Response;
using PecBMS.ViewModel.Response.PecISInquiry;
using Pigi.MDbLogging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Utility;

namespace PecBMS.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    //[Authorize(Policy = "PecBMS")]
    //[Authorize(AuthenticationSchemes =
    //JwtBearerDefaults.AuthenticationScheme)]
    [Authorize]
    public class InquiryController : ControllerBase, ILoggable
    {
        #region Private Variables
        private readonly IServiceProvider _serviceProvider;
        private readonly IMapper _mapper;
        private readonly IMemoryCache _memoryCache;
        private readonly IBillService _billService;
        private readonly IWalletServices _walletService;
        private readonly IPayServices _payService;
        private readonly IMerchantService _merchantService;
        public IEncryptor _encryptor { get; }

        #endregion

        #region ctor
        public InquiryController(IServiceProvider serviceProvider, IMapper mapper, IMemoryCache memoryCache, IEncryptor encryptor)
        {
            _serviceProvider = serviceProvider;
            _mapper = mapper;
            _billService = _serviceProvider.GetRequiredService<IBillService>();
            _walletService = _serviceProvider.GetRequiredService<IWalletServices>();
            _payService = _serviceProvider.GetRequiredService<IPayServices>();
            _merchantService = _serviceProvider.GetRequiredService<IMerchantService>();
            _memoryCache = memoryCache;
            _encryptor = encryptor;
        }
        #endregion

        /// <summary>
        /// استعلام قبض گاز
        /// </summary>
        /// <param name="nigcBillInquiryRequest"></param>
        /// <returns></returns>
        [HttpPost("NigcBillInquiry")]
        [LoggingAspect]
        [AllowAnonymous]
        public async Task<IActionResult> NigcBillInquiry(NigcBillInquiryRequestViewModel nigcBillInquiryRequest)
        {
            try
            {
                #region checkValidationCaptcha
                if (!string.IsNullOrEmpty(nigcBillInquiryRequest.PecBmsData))
                {
                    var decryptKey = CanEncripted(nigcBillInquiryRequest.PecBmsData);
                    if (!decryptKey.Item2 && decryptKey.Item1 != nigcBillInquiryRequest.SubscriptionId)
                    {

                        ValidCaptchaRequestViewModel captcha = new ValidCaptchaRequestViewModel()
                        {
                            captchaCode = nigcBillInquiryRequest.captchaCode,
                            userinput = nigcBillInquiryRequest.userinput
                        };
                        var validResponse = ValidateCaptcha(captcha);
                        if (validResponse.status != 0)
                        {
                            return Ok(new MessageModel<CreateCaptchaResponse>
                            {
                                message = validResponse.message,
                                status = -101,
                                Data = validResponse.Data

                            });
                        }
                    }
                }
                else
                {
                    ValidCaptchaRequestViewModel captcha = new ValidCaptchaRequestViewModel()
                    {
                        captchaCode = nigcBillInquiryRequest.captchaCode,
                        userinput = nigcBillInquiryRequest.userinput
                    };
                    var validResponse = ValidateCaptcha(captcha);
                    if (validResponse.status != 0)
                    {
                        return Ok(new MessageModel<CreateCaptchaResponse>
                        {
                            message = validResponse.message,
                            status = -101,
                            Data = validResponse.Data

                        });
                    }
                }
                #endregion

                var nigcrequest = _mapper.Map<NigcBillInquiryRequestDto>(nigcBillInquiryRequest);
                var proxyresponse = await _billService.NigcBillInquiry(nigcrequest);
                var response = _mapper.Map<NigcBillInquiryResponseViewModel>(proxyresponse.Data);
                return Ok(new MessageModel<NigcBillInquiryResponseViewModel>
                {
                    message = proxyresponse.Message,
                    status = proxyresponse.Status,
                    Data = response,
                    Key = _encryptor.Encrypt(nigcrequest.SubscriptionId)

                });
            }
            catch (Exception)
            {
                return Ok(new MessageModel
                {
                    message = "خطا در انجام عملیات",
                    status = -1,
                });
            }

        }

        /// <summary>
        /// استعلام قبض ایرانسل
        /// </summary>
        /// <param name="irancellInquiryViewModel"></param>
        /// <returns></returns>
        [HttpPost("IrancellBillInquiry")]
        [LoggingAspect]
        [AllowAnonymous]
        public async Task<IActionResult> IrancellBillInquiry(IrancellInquiryViewModel irancellInquiryViewModel)
        {
            try
            {
                #region checkValidationCaptcha
                if (!string.IsNullOrEmpty(irancellInquiryViewModel.PecBmsData))
                {
                    var decryptKey = CanEncripted(irancellInquiryViewModel.PecBmsData);
                    if (!decryptKey.Item2 && decryptKey.Item1 != irancellInquiryViewModel.MobileNumber)
                    {

                        ValidCaptchaRequestViewModel captcha = new ValidCaptchaRequestViewModel()
                        {
                            captchaCode = irancellInquiryViewModel.captchaCode,
                            userinput = irancellInquiryViewModel.userinput
                        };
                        var validResponse = ValidateCaptcha(captcha);
                        if (validResponse.status != 0)
                        {
                            return Ok(new MessageModel<CreateCaptchaResponse>
                            {
                                message = validResponse.message,
                                status = -101,
                                Data = validResponse.Data

                            });
                        }
                    }
                }
                else
                {
                    ValidCaptchaRequestViewModel captcha = new ValidCaptchaRequestViewModel()
                    {
                        captchaCode = irancellInquiryViewModel.captchaCode,
                        userinput = irancellInquiryViewModel.userinput
                    };
                    var validResponse = ValidateCaptcha(captcha);
                    if (validResponse.status != 0)
                    {
                        return Ok(new MessageModel<CreateCaptchaResponse>
                        {
                            message = validResponse.message,
                            status = -101,
                            Data = validResponse.Data

                        });
                    }
                }
                #endregion


                var irancellRequest = _mapper.Map<IrancellInquiryRequestDto>(irancellInquiryViewModel);
                var proxyresponse = await _billService.IrancellBillInquiry(irancellRequest);

                var response = _mapper.Map<IrancellInquiryResponseViewModel>(proxyresponse.Data);



                List<IrancellBillInquiryResponseExteraViewModel> responseViewModels = new List<IrancellBillInquiryResponseExteraViewModel>() {
                new IrancellBillInquiryResponseExteraViewModel(){
                        Amount  = response.current_balance.ToString(),
                        MobileNumber = irancellInquiryViewModel.MobileNumber,
                        Type = 1,
                        TypeDesc = "قبض میان دوره",
                        BillId = new Random().NextDouble().ToString(),
                        PaymentId = new Random().NextDouble().ToString()
                },
                new IrancellBillInquiryResponseExteraViewModel{
                        Amount  = response.outstanding_balance.ToString(),
                        MobileNumber = irancellInquiryViewModel.MobileNumber,
                        Type = 2,
                        TypeDesc = "قبض پایان دوره",
                        BillId = new Random().NextDouble().ToString(),
                        PaymentId = new Random().NextDouble().ToString()
                }};

                return Ok(new MessageModel<List<IrancellBillInquiryResponseExteraViewModel>>
                {
                    message = proxyresponse.Message,
                    status = proxyresponse.Status,
                    Data = responseViewModels,
                    Key = _encryptor.Encrypt(irancellRequest.MobileNumber)
                });
            }
            catch (Exception)
            {
                return Ok(new MessageModel
                {
                    message = "خطا در انجام عملیات",
                    status = -1,
                });
            }
        }

        /// <summary>
        /// استعلام قبض همراه اول
        /// </summary>
        /// <param name="mciBillInquiryRequest"></param>
        /// <returns></returns>
        [HttpPost("MciBillInquiry")]
        [LoggingAspect]
        [AllowAnonymous]
        public async Task<IActionResult> MciBillInquiry(MciBillInquiryRequestViewModel mciBillInquiryRequest)
        {
            try
            {
                #region checkValidationCaptcha
                if (!string.IsNullOrEmpty(mciBillInquiryRequest.PecBmsData))
                {
                    var decryptKey = CanEncripted(mciBillInquiryRequest.PecBmsData);
                    if (!decryptKey.Item2 && decryptKey.Item1 != mciBillInquiryRequest.MobileNumber)
                    {

                        ValidCaptchaRequestViewModel captcha = new ValidCaptchaRequestViewModel()
                        {
                            captchaCode = mciBillInquiryRequest.captchaCode,
                            userinput = mciBillInquiryRequest.userinput
                        };
                        var validResponse = ValidateCaptcha(captcha);
                        if (validResponse.status != 0)
                        {
                            return Ok(new MessageModel<CreateCaptchaResponse>
                            {
                                message = validResponse.message,
                                status = -101,
                                Data = validResponse.Data

                            });
                        }
                    }
                }
                else
                {
                    ValidCaptchaRequestViewModel captcha = new ValidCaptchaRequestViewModel()
                    {
                        captchaCode = mciBillInquiryRequest.captchaCode,
                        userinput = mciBillInquiryRequest.userinput
                    };
                    var validResponse = ValidateCaptcha(captcha);
                    if (validResponse.status != 0)
                    {
                        return Ok(new MessageModel<CreateCaptchaResponse>
                        {
                            message = validResponse.message,
                            status = -101,
                            Data = validResponse.Data

                        });
                    }
                }
                #endregion


                var mciRequest = _mapper.Map<MciBillInquiryRequestDto>(mciBillInquiryRequest);
                var proxyresponse = await _billService.MciBillInquiry(mciRequest);

                var response = _mapper.Map<List<MciBillInquiryResponseViewModel>>(proxyresponse.Data);
                return Ok(new MessageModel<List<MciBillInquiryResponseViewModel>>
                {
                    message = proxyresponse.Message,
                    status = proxyresponse.Status,
                    Data = response,
                    Key = _encryptor.Encrypt(mciRequest.MobileNumber)
                });
            }
            catch (Exception)
            {
                return Ok(new MessageModel
                {
                    message = "خطا در انجام عملیات",
                    status = -1,
                });
            }

        }
        /// <summary>
        /// استعلام قبض مخابرات
        /// </summary>
        /// <param name="tciInquiryRequest"></param>
        /// <returns></returns>
        [HttpPost("TciBillInquiry")]
        [LoggingAspect]
        [AllowAnonymous]
        public async Task<IActionResult> TciBillInquiry(TciInquiryRequestViewModel tciInquiryRequest)
        {
            try
            {
                #region checkValidationCaptcha
                if (!string.IsNullOrEmpty(tciInquiryRequest.PecBmsData))
                {
                    var decryptKey = CanEncripted(tciInquiryRequest.PecBmsData);
                    if (!decryptKey.Item2 && decryptKey.Item1 != tciInquiryRequest.TelNo)
                    {

                        ValidCaptchaRequestViewModel captcha = new ValidCaptchaRequestViewModel()
                        {
                            captchaCode = tciInquiryRequest.captchaCode,
                            userinput = tciInquiryRequest.userinput
                        };
                        var validResponse = ValidateCaptcha(captcha);
                        if (validResponse.status != 0)
                        {
                            return Ok(new MessageModel<CreateCaptchaResponse>
                            {
                                message = validResponse.message,
                                status = -101,
                                Data = validResponse.Data
                            });
                        }
                    }
                }
                else
                {
                    ValidCaptchaRequestViewModel captcha = new ValidCaptchaRequestViewModel()
                    {
                        captchaCode = tciInquiryRequest.captchaCode,
                        userinput = tciInquiryRequest.userinput
                    };
                    var validResponse = ValidateCaptcha(captcha);
                    if (validResponse.status != 0)
                    {
                        return Ok(new MessageModel<CreateCaptchaResponse>
                        {
                            message = validResponse.message,
                            status = -101,
                            Data = validResponse.Data
                        });
                    }
                }
                #endregion

                var TciRequest = _mapper.Map<TciInquiryRequestDto>(tciInquiryRequest);
                var proxyresponse = await _billService.TciBillInquiry(TciRequest);
                var response = _mapper.Map<List<TciInquiryResponseViewModel>>(proxyresponse.Data);
                return Ok(new MessageModel<List<TciInquiryResponseViewModel>>
                {
                    message = proxyresponse.Message,
                    status = proxyresponse.Status,
                    Data = response,
                    Key = _encryptor.Encrypt(TciRequest.TelNo)
                });
            }
            catch (Exception)
            {
                return Ok(new MessageModel
                {
                    message = "خطا در انجام عملیات",
                    status = -1,
                });
            }

        }
        /// <summary>
        /// استعلام قبض برق
        /// </summary>
        /// <param name="barghBillInquiry"></param>
        /// <returns></returns>
        [HttpPost("EdcBillInquiry")]
        [LoggingAspect]
        [AllowAnonymous]
        public async Task<IActionResult> EdcBillInquiry(BarghBillInquiryRequestViewModel barghBillInquiry)
        {
            try
            {
                #region checkValidationCaptcha
                if (!string.IsNullOrEmpty(barghBillInquiry.PecBmsData))
                {
                    var decryptKey = CanEncripted(barghBillInquiry.PecBmsData);
                    if (!decryptKey.Item2 && decryptKey.Item1 != barghBillInquiry.BillId)
                    {

                        ValidCaptchaRequestViewModel captcha = new ValidCaptchaRequestViewModel()
                        {
                            captchaCode = barghBillInquiry.captchaCode,
                            userinput = barghBillInquiry.userinput
                        };
                        var validResponse = ValidateCaptcha(captcha);
                        if (validResponse.status != 0)
                        {
                            return Ok(new MessageModel<CreateCaptchaResponse>
                            {
                                message = validResponse.message,
                                status = -101,
                                Data = validResponse.Data

                            });
                        }
                    }
                }
                else
                {
                    ValidCaptchaRequestViewModel captcha = new ValidCaptchaRequestViewModel()
                    {
                        captchaCode = barghBillInquiry.captchaCode,
                        userinput = barghBillInquiry.userinput
                    };
                    var validResponse = ValidateCaptcha(captcha);
                    if (validResponse.status != 0)
                    {
                        return Ok(new MessageModel<CreateCaptchaResponse>
                        {
                            message = validResponse.message,
                            status = -101,
                            Data = validResponse.Data

                        });
                    }
                }
                #endregion


                var barghRequest = _mapper.Map<BarghBillInquiryRequestDto>(barghBillInquiry);
                var proxyresponse = await _billService.BarghInquiry(barghRequest);
                var response = _mapper.Map<BarghBillInquiryResponseViewModel>(proxyresponse.Data);
                return Ok(new MessageModel<BarghBillInquiryResponseViewModel>
                {
                    message = proxyresponse.Message,
                    status = proxyresponse.Status,
                    Data = response,
                    Key = _encryptor.Encrypt(barghRequest.BillId)
                });
            }
            catch (Exception)
            {
                return Ok(new MessageModel
                {
                    message = "خطا در انجام عملیات",
                    status = -1,
                });
            }

        }
        /// <summary>
        /// استعلام عوارض آزادراهی
        /// </summary>
        /// <param name="tollBill"></param>
        /// <returns></returns>
        [HttpPost("TollBillInquiry")]
        [LoggingAspect]
        [AllowAnonymous]
        public async Task<IActionResult> TollBillInquiry(TollBillInquiryRequestViewModel tollBill)
        {
            try
            {
                #region checkValidationCaptcha
                if (!string.IsNullOrEmpty(tollBill.PecBmsData))
                {
                    var decryptKey = CanEncripted(tollBill.PecBmsData);
                    if (!decryptKey.Item2 && decryptKey.Item1 != tollBill.PlateNumber.ToString())
                    {

                        ValidCaptchaRequestViewModel captcha = new ValidCaptchaRequestViewModel()
                        {
                            captchaCode = tollBill.captchaCode,
                            userinput = tollBill.userinput
                        };
                        var validResponse = ValidateCaptcha(captcha);
                        if (validResponse.status != 0)
                        {
                            return Ok(new MessageModel<CreateCaptchaResponse>
                            {
                                message = validResponse.message,
                                status = -101,
                                Data = validResponse.Data

                            });
                        }
                    }
                }
                else
                {
                    ValidCaptchaRequestViewModel captcha = new ValidCaptchaRequestViewModel()
                    {
                        captchaCode = tollBill.captchaCode,
                        userinput = tollBill.userinput
                    };
                    var validResponse = ValidateCaptcha(captcha);
                    if (validResponse.status != 0)
                    {
                        return Ok(new MessageModel<CreateCaptchaResponse>
                        {
                            message = validResponse.message,
                            status = -101,
                            Data = validResponse.Data

                        });
                    }
                }
                #endregion


                var inquiryRequestDto = _mapper.Map<TollBillInquiryRequestDto>(tollBill);
                var proxyresponse = await _billService.TollBillInquiry(inquiryRequestDto);
                var response = _mapper.Map<TollBillInquiryResponseViewModel>(proxyresponse.Data);
                return Ok(new MessageModel<TollBillInquiryResponseViewModel>
                {
                    message = proxyresponse.Message,
                    status = proxyresponse.Status,
                    Data = response,
                    Key = _encryptor.Encrypt(inquiryRequestDto.PlateNumber.ToString())
                });
            }
            catch (Exception)
            {

                return Ok(new MessageModel
                {
                    message = "خطا در انجام عملیات",
                    status = -1,
                });
            }
        }
        /// <summary>
        /// استعلام قبض _معتبر بودن قبض
        /// </summary>
        /// <param name="billInfo"></param>
        /// <returns></returns>
        [HttpPost("BillInfoInquiry")]
        [LoggingAspect]
        [AllowAnonymous]
        public async Task<IActionResult> BillInfoInquiry(BillInfoInquiryRequestViewModel billInfo)
        {
            var respdata = new BillInfoInquiryResponseViewModel();
            try
            {
                #region checkValidationCaptcha
                if (!string.IsNullOrEmpty(billInfo.PecBmsData))
                {
                    var decryptKey = CanEncripted(billInfo.PecBmsData);
                    if (!decryptKey.Item2 && decryptKey.Item1 != billInfo.BillId)
                    {

                        ValidCaptchaRequestViewModel captcha = new ValidCaptchaRequestViewModel()
                        {
                            captchaCode = billInfo.captchaCode,
                            userinput = billInfo.userinput
                        };
                        var validResponse = ValidateCaptcha(captcha);
                        if (validResponse.status != 0)
                        {
                            return Ok(new MessageModel<CreateCaptchaResponse>
                            {
                                message = validResponse.message,
                                status = -101,
                                Data = validResponse.Data

                            });
                        }
                    }
                }
                else
                {
                    ValidCaptchaRequestViewModel captcha = new ValidCaptchaRequestViewModel()
                    {
                        captchaCode = billInfo.captchaCode,
                        userinput = billInfo.userinput
                    };
                    var validResponse = ValidateCaptcha(captcha);
                    if (validResponse.status != 0)
                    {
                        return Ok(new MessageModel<CreateCaptchaResponse>
                        {
                            message = validResponse.message,
                            status = -101,
                            Data = validResponse.Data

                        });
                    }
                }
                #endregion


                var billInfoRequest = _mapper.Map<BillInfoRequestDto>(billInfo);
                var proxyresponse = await _billService.GetBillInfo(billInfoRequest);
                var response = _mapper.Map<BillInfoInquiryResponseViewModel>(proxyresponse.Data);
                return Ok(new MessageModel<BillInfoInquiryResponseViewModel>
                {
                    message = proxyresponse.Message,
                    status = proxyresponse.Status,
                    Data = response,
                    Key = _encryptor.Encrypt(billInfoRequest.BillId)
                });
            }
            catch (Exception)
            {
                return Ok(new MessageModel
                {
                    message = "خطا در انجام عملیات",
                    status = -1,
                });
            }

        }
        /// <summary>
        /// گرفتن سوابق پرداخت قبض خاص کاربر
        /// </summary>
        /// <param name="req"></param>
        /// <returns></returns>
        [HttpPost("GetBillPaymentHistory")]
        [LoggingAspect]
        public async Task<IActionResult> GetBillPaymentHistory(GetBillPaymentHistoryRequestViewModel req)
        {
            try
            {
                #region Checkuserid
                var userId = User.GetUserId();
                if (userId == 0)
                {
                    return Ok(new MessageModel
                    {
                        message = "کاربر مورد نظر یافت نشد",
                        status = -1,

                    });
                }
                #endregion

                var historyRequest = _mapper.Map<GetBillPaymentHistoryRequestDto>(req);
                historyRequest.UserId = userId;
                var historyResponse = await _billService.GetPaymentHistory(historyRequest);
                if (historyResponse.bills.Any())
                {
                    var respoosne = _mapper.Map<GetBillPaymentHistoryResponseViewModel>(historyResponse);
                    return Ok(new MessageModel<GetBillPaymentHistoryResponseViewModel>
                    {
                        message = "عملیات با موفقیت انجام شد",
                        status = 0,
                        Data = respoosne

                    });
                }
                else
                {
                    return Ok(new MessageModel
                    {
                        message = "تاریخچه پرداخت قبض یافت نشد",
                        status = -1,

                    });
                }
            }
            catch (Exception ex)
            {
                return Ok(new MessageModel
                {
                    message = ex.Message,
                    status = -99,

                });

            }
        }
        /// <summary>
        /// سرویس استعلام قبوض کاربر
        /// </summary>
        /// <returns></returns>
        [HttpPost("GetUserBillPaymentHistory")]
        //[LoggingAspect]
        public async Task<IActionResult> GetUserBillPaymentHistory(UserBillPaymentHistoryRequestViewModel req)
        {
            #region Checkuserid
            var userId = User.GetUserId();
            if (userId == 0)
            {
                return Ok(new MessageModel
                {
                    message = "کاربر مورد نظر یافت نشد",
                    status = -1,

                });
            }
            #endregion
            var historyRequest = _mapper.Map<UserBillPaymentHistoryRequestDto>(req);
            historyRequest.UserId = userId;
            var historyResponse = await _billService.GetUserPaymentHistory(historyRequest);
            if (historyResponse != null && historyResponse.bills.Any())
            {
                var respoosne = _mapper.Map<GetBillPaymentHistoryResponseViewModel>(historyResponse);
                return Ok(new MessageModel<GetBillPaymentHistoryResponseViewModel>
                {
                    message = "عملیات با موفقیت انجام شد",
                    status = 0,
                    Data = respoosne

                });
            }
            else
            {
                return Ok(new MessageModel
                {
                    message = "تاریخچه پرداخت قبض یافت نشد",
                    status = -1,

                });
            }
        }
        /// <summary>
        /// گرفتن سوابق شارژ کیف پول کاربر
        /// </summary>
        /// <param name="chargeHistory"></param>
        /// <returns></returns>
        [HttpPost("GetChargeHistory")]
        [LoggingAspect]
        public async Task<IActionResult> GetChargeHistory(ChargeHistoryRequestViewModel chargeHistory)
        {
            #region CheckUserid
            var userId = User.GetUserId();
            if (userId == 0)
            {
                return Ok(new MessageModel
                {
                    message = "کاربر مورد نظر یافت نشد",
                    status = -1,

                });
            }
            #endregion
            var historyRequest = _mapper.Map<ChargeHistoryRequestDo>(chargeHistory);
            historyRequest.UserId = userId;
            var history = await _walletService.GetChargeHistory(historyRequest);
            if (history.chargeHistory != null && history.chargeHistory.Count != 0)
            {
                var respoosne = _mapper.Map<ChargeHistoryReponseViewModel>(history);
                return Ok(new MessageModel<ChargeHistoryReponseViewModel>
                {
                    message = "عملیات موفق",
                    status = 0,
                    Data = respoosne

                });
            }
            else
            {
                return Ok(new MessageModel
                {
                    message = "اطلاعاتی یافت نشد",
                    status = -1,

                });
            }
        }
        [HttpPost("InquirybyOrderId")]
        [LoggingAspect]
        [AllowAnonymous]
        public async Task<IActionResult> InquirybyOrderId(InquiryRequestViewModel inquiry)
        {
            var response = await _payService.InquirybyOrderId(inquiry.OrderId);
            if (response.Data != null)
            {
                return Ok(new MessageModel<InquiryByOrderIdResponseDto>
                {
                    message = "عملیات موفق",
                    status = 0,
                    Data = response.Data
                });
            }
            return Ok(new MessageModel<InquiryByOrderIdResponseDto>
            {
                message = "اطلاعات یافت نشد",
                status = -1,
                Data = null
            });

        }
        [HttpPost("TollInquirybyOrderId")]
        [AllowAnonymous]
        [LoggingAspect]
        public async Task<IActionResult> TollInquirybyOrderId(InquiryRequestViewModel inquiry)
        {
            var response = await _billService.GetTollBillByOrderId(Convert.ToInt32(inquiry.OrderId));
            if (response.Data != null)
            {
                var tollBillInquiryRequest = _mapper.Map<GetTollBillResponseViewModel>(response.Data);
                return Ok(new MessageModel<GetTollBillResponseViewModel>
                {
                    message = "عملیات موفق",
                    status = 0,
                    Data = tollBillInquiryRequest

                });
            }
            return Ok(new MessageModel<GetTollBillResponseViewModel>
            {
                message = response.Message,
                status = -1,
                Data = null

            });

        }
        [HttpPost("BillInquiry")]
        [AllowAnonymous]
        [LoggingAspect]
        public async Task<IActionResult> BillInquiry(BillValidRequestViewModel billInfos)
        {
            try
            {
                var billInfoRequest = _mapper.Map<BillValidRequestDto>(billInfos);
                var proxyresponse = await _billService.GetBillInfoValidation(billInfoRequest);
                var response = _mapper.Map<List<ValidBillResponseViewModel>>(proxyresponse.Data);
                return Ok(new MessageModel<List<ValidBillResponseViewModel>>
                {
                    message = "عملیات موفق",
                    status = 0,
                    Data = response

                });
            }
            catch (Exception ex)
            {

                return Ok(new MessageModel
                {
                    message = "خطا در فرایند استعلام",
                    status = -1,
                });
            }

        }
        [HttpPost("GetMerchantInformation")]
        [LoggingAspect]
        [AllowAnonymous]
        public async Task<IActionResult> GetMerchantInformation(MerchantInfoRequestViewModel merchant)
        {
            try
            {
                var getMerchant = _mapper.Map<GetMerchantInformationRequestDto>(merchant);
                var proxyresponse = await _merchantService.GetMerchantInformation(getMerchant);
                var response = _mapper.Map<MerchatnInfoResponseViewModel>(proxyresponse.Data);
                return Ok(new MessageModel<MerchatnInfoResponseViewModel>
                {
                    message = proxyresponse.Message,
                    status = proxyresponse.Status,
                    Data = response

                });
            }
            catch (Exception ex)
            {

                return Ok(new MessageModel
                {
                    message = "خطا در فرایند استعلام",
                    status = -1,
                });
            }

        }
        [HttpPost("GetMerchantBanners")]
        [LoggingAspect]
        [AllowAnonymous]
        public async Task<IActionResult> GetMerchantBanners(GetMerchantBanerRequestViewModel getMerchantBaner)
        {
            try
            {
                var getMerchant = _mapper.Map<GetMerchantBanerRequestDto>(getMerchantBaner);
                var proxyresponse = await _merchantService.GetMerchantBaner(getMerchant);
                var response = _mapper.Map<List<GetMerchantBanerResponseViewModel>>(proxyresponse.Data);
                return Ok(new MessageModel<List<GetMerchantBanerResponseViewModel>>
                {
                    message = proxyresponse.Message,
                    status = proxyresponse.Status,
                    Data = response

                });
            }
            catch (Exception ex)
            {
                return Ok(new MessageModel
                {
                    message = "خطا در فرایند استعلام",
                    status = -99,
                });
            }
        }
        private CreateCaptchaResponse GenerateCaptcha()
        {
            int width = 330;
            int height = 100;
            var captchaCode = Captcha.GenerateCaptchaCode();
            var result = Captcha.GenerateCaptchaImage(width, height, captchaCode);
            var cacheEntryOptions = new MemoryCacheEntryOptions()
            .SetSlidingExpiration(TimeSpan.FromSeconds(60));
            var captchaUnique = Utility.Utility.GenerateRandomOrderID();
            _memoryCache.Set(captchaUnique.ToString(), result.CaptchaCode);
            //Stream s = new MemoryStream(result.CaptchaByteData);
            //var image = new FileStreamResult(s, "image/png");

            CreateCaptchaResponse createCaptchaResponse = new CreateCaptchaResponse()
            {
                CaptchaCode = captchaUnique.ToString(),
                CaptchaImage = result.CaptchBase64Data
            };
            return createCaptchaResponse;
        }


        private MessageModel<CreateCaptchaResponse> ValidateCaptcha(ValidCaptchaRequestViewModel validCaptchaRequest)
        {
            if (string.IsNullOrEmpty(validCaptchaRequest.captchaCode) || string.IsNullOrEmpty(validCaptchaRequest.userinput))
            {
                var createCaptchaResponse = GenerateCaptcha();
                return new MessageModel<CreateCaptchaResponse>
                {
                    message = "کد امنیتی نمی تواند خالی باشد",
                    status = -1,
                    Data = createCaptchaResponse
                };
            }
            var validCaptcha = Captcha.ValidateCaptchaCode(validCaptchaRequest.captchaCode, validCaptchaRequest.userinput, _memoryCache);
            if (validCaptcha)
            {
                return new MessageModel<CreateCaptchaResponse>
                {
                    message = "عملیات موفق",
                    status = 0,
                    Data = null
                };
            }
            else
            {
                var createCaptchaResponse = GenerateCaptcha();
                return new MessageModel<CreateCaptchaResponse>
                {
                    message = "کد امنیتی اشتباه وارد شده است",
                    status = -1,
                    Data = createCaptchaResponse
                };
            }

        }



        private Tuple<string, bool> CanEncripted(string text)
        {
            try
            {
                var decryptKey = _encryptor.Decrypt(text);
                if (!string.IsNullOrEmpty(decryptKey))
                {
                    return new Tuple<string, bool>(decryptKey, true);
                }
                else
                {
                    return new Tuple<string, bool>(null, false);
                }
            }
            catch (Exception ex)
            {
                return new Tuple<string, bool>(ex.Message, false);
            }
        }
    }

}
