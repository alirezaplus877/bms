using Application.Services;
using AutoMapper;
using Dto.Pagination;
using Dto.Proxy.Request.Naja;
using Dto.Proxy.Request.SMS;
using Dto.Proxy.Request.Wallet;
using Dto.Proxy.Response;
using Dto.Proxy.Response.Naja;
using Dto.Proxy.Response.Wallet;
using Dto.repository;
using Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using PEC.CoreCommon.ExtensionMethods;
using PEC.CoreCommon.Security.Encryptor;
using PecBMS.Helper.Identity;
using PecBMS.Model;
using PecBMS.ViewModel.Request;
using PecBMS.ViewModel.Request.Naja;
using PecBMS.ViewModel.Response.Naja;
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
    [Authorize]
    public class NajaWrapperController : ControllerBase, ILoggable
    {

        #region Private Variables
        private readonly IServiceProvider _serviceProvider;
        private readonly IMapper _mapper;
        private readonly IWalletServices _walletServices;
        private readonly INajaServices _najaServices;
        private readonly IMemoryCache _memoryCache;
        private readonly ISMSService _smsService;
        private readonly IEncryptor _encryptor;
        private IMdbLogger<NajaWrapperController> _logger;
        private readonly IPecBmsSetting _pecBmsSetting;

        #endregion

        #region ctor
        public NajaWrapperController(IServiceProvider serviceProvider,
                                     IMapper mapper,
                                     IMemoryCache memoryCache,
                                     ISMSService smsService, IEncryptor encryptor,
                                     IMdbLogger<NajaWrapperController> logger,
                                     IPecBmsSetting pecBmsSetting,
                                     IWalletServices walletServices)
        {
            _serviceProvider = serviceProvider;
            _mapper = mapper;
            _najaServices = _serviceProvider.GetRequiredService<INajaServices>();
            // _NajaRepository = _serviceProvider.GetRequiredService<INajaRepository>();
            _memoryCache = memoryCache;
            _smsService = smsService;
            _encryptor = encryptor;
            _logger = logger;
            _pecBmsSetting = pecBmsSetting;
            _walletServices = walletServices;
        }
        #endregion

        [HttpPost("AddCustomerWallet")]
        [LoggingAspect]
        public async Task<IActionResult> AddCustomerWallet(CustomerWalletRequestViewModel input)
        {
            #region CheckUser
            var userId = User.GetUserId();
            if (userId == 0)
            {
                return Ok(new MessageModel
                {
                    message = DescriptionUtility.GetDescription<ServiceStatus>("NoUserFound"),
                    status = (int)ServiceStatus.NoUserFound
                });
            }
            #endregion

            var Dto = _mapper.Map<CustomerWalletRequestDto>(input);
            Dto.CorporationPIN = _pecBmsSetting.CorporationPIN;
            Dto.GroupWalletId = _pecBmsSetting.GroupWalletId;

            var response = await _walletServices.AddOrUpdateCustomerWalletAsync(Dto, userId);
            if (response.Status == 0)
            {
                //name & last name log
                return Ok(new NajaResponseViewModelGeneric<CustomerWalletResponseDto>
                {
                    Data = response.Data,
                    Status = response.Status,
                    Message = response.Message
                });
            }
            else
            {
                return Ok(new NajaResponseViewModelGeneric<ViolationInquiryResponseDto>
                {
                    Status = response.Status,
                    Data = null,
                    Message = response.Message
                });
            }
        }



        [HttpPost("CheckInfo")]
        [LoggingAspect]
        public async Task<IActionResult> CheckInfo(CheckInfoRequestViewModel input)
        {
            #region CheckUser
            var userId = User.GetUserId();
            if (userId == 0)
            {
                return Ok(new MessageModel
                {
                    message = DescriptionUtility.GetDescription<ServiceStatus>("NoUserFound"),
                    status = (int)ServiceStatus.NoUserFound
                });
            }
            #endregion

            var Dto = _mapper.Map<CheckInfoRequestDto>(input);
            var response = await _najaServices.CheckInfoAsync(Dto);
            if (response.Status == 0)
            {
                //name & last name log
                return Ok(new NajaResponseViewModelGeneric<CheckInfoResponseDto>
                {
                    Data = null,
                    Status = (int)ServiceStatus.ConfirmCodeIsValid,
                    Message = DescriptionUtility.GetDescription<ServiceStatus>("ConfirmCodeIsValid")
                });
            }
            else
            {
                if (response.Status == -50)
                {
                    return Ok(new NajaResponseViewModelGeneric<ViolationInquiryResponseDto>
                    {
                        Status = response.Status,
                        Data = null,
                        Message = response.Message
                    });
                }
                else
                {

                    //شماره ملی و شماره همراه تطابق ندارد
                    return Ok(new NajaResponseViewModelGeneric<ViolationInquiryResponseDto>
                    {
                        Status = (int)ServiceStatus.NotValidMobileAndNationalCode,
                        Data = null,
                        Message = DescriptionUtility.GetDescription<ServiceStatus>("NotValidMobileAndNationalCode")
                    });

                }
            }
        }


        /// <summary>
        /// دریافت تصاویر خلافی خودرو
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        [HttpPost("GetViolationImage")]
        [LoggingAspect]
        public async Task<IActionResult> GetViolationImage(GetViolationImageInquiryRequestViewModel input)
        {
            #region CheckUser
            var userId = User.GetUserId();
            if (userId == 0)
            {
                return Ok(new MessageModel
                {
                    message = DescriptionUtility.GetDescription<ServiceStatus>("NoUserFound"),
                    status = (int)ServiceStatus.NoUserFound
                });
            }
            #endregion
            if (!long.TryParse(input.OrderId, out long OrderId))
            {
                return Ok(new MessageModel
                {
                    message = DescriptionUtility.GetDescription<ServiceStatus>("OrderIdNotValid"),
                    status = (int)ServiceStatus.OrderIdNotValid
                });
            }
            var ResultRepository = await _najaServices.GetNajaWageViolationImageAsync(OrderId, userId);

            if (ResultRepository.Status != 0)
            {
                return Ok(new MessageModel
                {
                    message = ResultRepository.Message,
                    status = ResultRepository.Status
                });
            }
            else
            {
                var reqUser = JsonConvert.DeserializeObject<CarViolationInquiryObjectModel>(ResultRepository.Data.NajaServiceData);
                var violationInquiryRequest = _mapper.Map<ViolationImageInquiryRequestDto>(reqUser);
                violationInquiryRequest.SerialNo = input.SerialNo;
                violationInquiryRequest.MobileNumber = ResultRepository.Data.Mobile;
                violationInquiryRequest.NationalCode = ResultRepository.Data.NationalCode;
                violationInquiryRequest.TrackingNo = ResultRepository.Data.OrderId.Value;

                var ResponseModel = await _najaServices.ViolationImageInquiryAsync(violationInquiryRequest);

                if (ResponseModel != null && ResponseModel.Status == 0)
                {
                    MessageModel<ViolationImageInquiryResponseViewModel> req = new()
                    {
                        Data = _mapper.Map<ViolationImageInquiryResponseViewModel>(ResponseModel.Data),
                        message = ResponseModel.Message
                    };
                    return Ok(req);
                }
                else
                {
                    return Ok(new MessageModel<ViolationImageInquiryResponseViewModel>
                    {
                        message = ResponseModel.Message,
                        status = ResponseModel.Status,
                        Data = null
                    });
                }
            }
        }

        [HttpGet("GetPaginatedNajaWage")]
        [LoggingAspect]
        public async Task<IActionResult> GetPaginatedNajaWage([FromQuery] PaginationFilterViewModel paginationFilter)
        {
            #region CheckUser
            var userId = User.GetUserId();
            if (userId == 0)
            {
                return Ok(new MessageModel
                {
                    message = DescriptionUtility.GetDescription<ServiceStatus>("NoUserFound"),
                    status = (int)ServiceStatus.NoUserFound
                });
            }
            #endregion

            var paginationFilterDto = _mapper.Map<PaginationFilterDto>(paginationFilter);
            paginationFilterDto.UserId = userId;

            var report = await _najaServices.GetPaginatedNajaWageAsync(paginationFilterDto);

            if (report != null)
            {
                if (report.Data.Count != 0)
                {
                    return Ok(new MessageModel<PagedResponse<List<NajaWageDtoResponsePaginated>>>
                    {
                        Data = report,
                        message = DescriptionUtility.GetDescription<ServiceStatus>("Success"),
                        status = (int)ServiceStatus.Success
                    });
                }
                else
                {
                    return Ok(new MessageModel<PagedResponse<List<NajaWageDtoResponsePaginated>>>
                    {
                        Data = report,
                        message = DescriptionUtility.GetDescription<ServiceStatus>("NotDataFound"),
                        status = (int)ServiceStatus.NotDataFound
                    });
                }
            }
            else
            {
                return Ok(new MessageModel<PagedResponse<List<NajaWageDtoResponsePaginated>>>
                {
                    Data = report,
                    message = DescriptionUtility.GetDescription<ServiceStatus>("NotDataFound"),
                    status = (int)ServiceStatus.NotDataFound
                });
            }
        }


        [HttpPost("InquiryWageByOrderId")]
        [LoggingAspect]
        public async Task<IActionResult> InquiryWageByOrderId(InquiryWageRequestViewModel model)
        {
            _logger.Log(13681368, "moh InquiryWageByOrderId =>" + JsonConvert.SerializeObject(model), null);
            try
            {
                #region CheckUser
                var userId = User.GetUserId();
                if (userId == 0)
                {
                    return Ok(new MessageModel
                    {
                        message = DescriptionUtility.GetDescription<ServiceStatus>("NoUserFound"),
                        status = (int)ServiceStatus.NoUserFound
                    });
                }
                #endregion
                _logger.Log(13681368, "moh To InquiryWageByOrderIdAsync =>" + userId + " = " + model.OrderId, null);
                var inquiry = await _najaServices.InquiryWageByOrderIdAsync(model.OrderId, userId);

                _logger.Log(13681368, "moh from InquiryWageByOrderIdAsync =>" + JsonConvert.SerializeObject(inquiry), null);
                if (inquiry.Status == 2 || inquiry.Status == 6)
                {
                    switch (inquiry.Data.NajaType)
                    {
                        #region [- Case 1 : ViolationInquiry -]
                        case 1:
                            var reqUser = JsonConvert.DeserializeObject<CarViolationInquiryObjectModel>(inquiry.Data.NajaServiceData);
                            var violationInquiryRequest = _mapper.Map<ViolationInquiryRequestDto>(reqUser);
                            violationInquiryRequest.MobileNumber = inquiry.Data.Mobile;
                            violationInquiryRequest.NationalCode = inquiry.Data.NationalCode;
                            violationInquiryRequest.TrackingNo = inquiry.Data.OrderId.Value;

                            if (!inquiry.Data.RequestIsSent)
                            {
                                _logger.Log(13681368, "moh case 1 request is sent false =>" + inquiry.Data.OrderId.Value, null);
                                var result = await _najaServices.UpdateNajaWageRequestIsSentAsync(inquiry.Data.OrderId.Value);
                                _logger.Log(13681368, "moh from UpdateNajaWageRequestIsSentAsync =>" + result, null);
                                if (result)
                                {
                                    _logger.Log(13681368, "moh to ViolationInquiryAsync =>" + JsonConvert.SerializeObject(violationInquiryRequest), null);
                                    var _callNaja = await _najaServices.ViolationInquiryAsync(violationInquiryRequest);
                                    _logger.Log(13681368, "moh from ViolationInquiryAsync =>" + JsonConvert.SerializeObject(_callNaja), null);
                                    if (_callNaja != null && _callNaja.Status == 0)
                                    {
                                        PayWageResponseViewModel<ViolationInquiryResponseViewModel> req = null;

                                        req = new PayWageResponseViewModel<ViolationInquiryResponseViewModel>
                                        {
                                            NajaType = Convert.ToInt32(inquiry.Data.NajaType.Value),
                                            AmountWage = inquiry.Data.Amount.Value,
                                            TransactionDate = inquiry.Data.BussinessDate.Value,
                                            Token = inquiry.Data.PgwToken ?? inquiry.Data.WalletReturnId.Value,
                                            OrderId = inquiry.Data.OrderId.Value.ToString(),
                                            Message = (inquiry.Status == 2) ? "کارمزد از کیف پول پرداخت شده است" : "کارمزد از طریق درگاه پرداخت شده است",
                                            NajaData = _mapper.Map<ViolationInquiryResponseViewModel>(_callNaja.Data)
                                        };


                                        return Ok(new MessageModel<PayWageResponseViewModel<ViolationInquiryResponseViewModel>>
                                        {
                                            message = (inquiry.Status == 2) ? DescriptionUtility.GetDescription<ServiceStatus>("WalletSuccess") :
                                            DescriptionUtility.GetDescription<ServiceStatus>("GetwaySuccess"),
                                            status = (inquiry.Status == 2) ? (int)ServiceStatus.WalletSuccess : (int)ServiceStatus.GetwaySuccess,
                                            Data = req
                                        });
                                    }
                                    else
                                    {
                                        PayWageResponseViewModel<ViolationInquiryResponseViewModel> req = null;

                                        req = new PayWageResponseViewModel<ViolationInquiryResponseViewModel>
                                        {
                                            NajaData = _mapper.Map<ViolationInquiryResponseViewModel>(_callNaja.Data),
                                            AmountWage = inquiry.Data.Amount.Value,
                                            TransactionDate = inquiry.Data.BussinessDate.Value,
                                            NajaType = Convert.ToInt32(inquiry.Data.NajaType.Value),
                                            Token = inquiry.Data.PgwToken ?? inquiry.Data.WalletReturnId.Value,
                                            OrderId = inquiry.Data.OrderId.Value.ToString(),
                                            Message = (inquiry.Status == 2) ? "کارمزد از طریق کیف پول پرداخت شده است" : "کارمزد از طریق درگاه پرداخت شده است",
                                        };

                                        return Ok(new MessageModel<PayWageResponseViewModel<ViolationInquiryResponseViewModel>>
                                        {
                                            message = _callNaja.Message,
                                            status = _callNaja.Status,
                                            Data = req
                                        });
                                    }
                                }
                                else
                                {
                                    PayWageResponseViewModel<ViolationInquiryResponseViewModel> req = null;

                                    req = new PayWageResponseViewModel<ViolationInquiryResponseViewModel>
                                    {
                                        NajaData = null,

                                        NajaType = Convert.ToInt32(inquiry.Data.NajaType.Value),
                                        Token = inquiry.Data.PgwToken.Value,
                                        AmountWage = inquiry.Data.Amount.Value,
                                        TransactionDate = inquiry.Data.BussinessDate.Value,
                                        OrderId = inquiry.Data.OrderId.Value.ToString(),
                                        Message = "خطا در سرویس استعلام - فلگ ارسال به api"
                                    };

                                    return Ok(new MessageModel<PayWageResponseViewModel<ViolationInquiryResponseViewModel>>
                                    {
                                        message = DescriptionUtility.GetDescription<ServiceStatus>("OperationUnSuccess"),
                                        status = (int)ServiceStatus.OperationUnSuccess,
                                        Data = req
                                    });
                                }

                            }
                            else
                            {
                                _logger.Log(13681368, "moh case 1 request is sent true =>" + inquiry.Data.OrderId.Value, null);
                                var GetInquiry = await _najaServices.GetInquiry(new GetInquiryRequestDto()
                                {
                                    TrackingNo = violationInquiryRequest.TrackingNo
                                });
                                _logger.Log(13681368, "moh from GetInquiry =>" + JsonConvert.SerializeObject(GetInquiry), null);
                                if (GetInquiry != null && GetInquiry.Status == 0)
                                {
                                    PayWageResponseViewModel<ViolationInquiryResponseViewModel> req = null;
                                    var najaData = JsonConvert.DeserializeObject<ViolationInquiryResponseDto>(JsonConvert.SerializeObject(GetInquiry.Data));
                                    req = new PayWageResponseViewModel<ViolationInquiryResponseViewModel>
                                    {
                                        NajaType = Convert.ToInt32(inquiry.Data.NajaType.Value),
                                        AmountWage = inquiry.Data.Amount.Value,
                                        TransactionDate = inquiry.Data.BussinessDate.Value,
                                        Token = inquiry.Data.PgwToken ?? inquiry.Data.WalletReturnId.Value,
                                        OrderId = inquiry.Data.OrderId.Value.ToString(),
                                        Message = (inquiry.Status == 2) ? "کارمزد از کیف پول پرداخت شده است" : "کارمزد از طریق درگاه پرداخت شده است",
                                        NajaData = _mapper.Map<ViolationInquiryResponseViewModel>(najaData)
                                    };


                                    return Ok(new MessageModel<PayWageResponseViewModel<ViolationInquiryResponseViewModel>>
                                    {
                                        message = (inquiry.Status == 2) ? DescriptionUtility.GetDescription<ServiceStatus>("WalletSuccess") :
                                        DescriptionUtility.GetDescription<ServiceStatus>("GetwaySuccess"),
                                        status = (inquiry.Status == 2) ? (int)ServiceStatus.WalletSuccess : (int)ServiceStatus.GetwaySuccess,
                                        Data = req
                                    });
                                }
                                else if (GetInquiry.Status == 502)
                                {
                                    PayWageResponseViewModel<ViolationInquiryResponseViewModel> req = null;
                                    var najaData = JsonConvert.DeserializeObject<ViolationInquiryResponseViewModel>(JsonConvert.SerializeObject(GetInquiry.Data));
                                    req = new PayWageResponseViewModel<ViolationInquiryResponseViewModel>
                                    {
                                        NajaData = null,
                                        AmountWage = inquiry.Data.Amount.Value,
                                        TransactionDate = inquiry.Data.BussinessDate.Value,
                                        NajaType = Convert.ToInt32(inquiry.Data.NajaType.Value),
                                        Token = inquiry.Data.PgwToken ?? inquiry.Data.WalletReturnId.Value,
                                        OrderId = inquiry.Data.OrderId.Value.ToString(),
                                        Message = (inquiry.Status == 2) ? "کارمزد از کیف پول پرداخت شده است" : "کارمزد از طریق درگاه پرداخت شده است",
                                    };

                                    return Ok(new MessageModel<PayWageResponseViewModel<ViolationInquiryResponseViewModel>>
                                    {
                                        message = "شماره پلاک با کد ملی تطابق ندارد",
                                        status = GetInquiry.Status,
                                        Data = req
                                    }); ;
                                }
                                else
                                {
                                    PayWageResponseViewModel<ViolationInquiryResponseViewModel> req = null;

                                    req = new PayWageResponseViewModel<ViolationInquiryResponseViewModel>
                                    {
                                        NajaData = null,

                                        NajaType = Convert.ToInt32(inquiry.Data.NajaType.Value),
                                        Token = inquiry.Data.PgwToken.Value,
                                        AmountWage = inquiry.Data.Amount.Value,
                                        TransactionDate = inquiry.Data.BussinessDate.Value,
                                        OrderId = inquiry.Data.OrderId.Value.ToString(),
                                        Message = " خطا در سرویس استعلام - فلگ ارسال به api"
                                    };

                                    return Ok(new MessageModel<PayWageResponseViewModel<ViolationInquiryResponseViewModel>>
                                    {
                                        message = DescriptionUtility.GetDescription<ServiceStatus>("OperationUnSuccess"),
                                        status = (int)ServiceStatus.OperationUnSuccess,
                                        Data = req
                                    });
                                }
                            }

                        #endregion

                        #region [- Case 2 : MotorViolationInquiry -]
                        case 2:
                            var reqMotor = JsonConvert.DeserializeObject<MotorViolationInquiryObjectModel>(inquiry.Data.NajaServiceData);
                            var MotorviolationInquiryRequest = _mapper.Map<MotorViolationInquiryRequestDto>(reqMotor);
                            MotorviolationInquiryRequest.MobileNumber = inquiry.Data.Mobile;
                            MotorviolationInquiryRequest.NationalCode = inquiry.Data.NationalCode;
                            MotorviolationInquiryRequest.TrackingNo = inquiry.Data.OrderId.Value;
                            if (!inquiry.Data.RequestIsSent)
                            {
                                _logger.Log(13681368, "moh case 1 request is sent false =>" + inquiry.Data.OrderId.Value, null);
                                var result = await _najaServices.UpdateNajaWageRequestIsSentAsync(inquiry.Data.OrderId.Value);
                                _logger.Log(13681368, "moh from UpdateNajaWageRequestIsSentAsync =>" + result, null);
                                if (result)
                                {
                                    var _callNajaMotor = await _najaServices.MotorViolationInquiryAsync(MotorviolationInquiryRequest);
                                    if (_callNajaMotor != null && _callNajaMotor.Status == 0)
                                    {
                                        PayWageResponseViewModel<MotorViolationInquiryResponseViewModel> req = null;

                                        req = new PayWageResponseViewModel<MotorViolationInquiryResponseViewModel>
                                        {
                                            NajaData = _mapper.Map<MotorViolationInquiryResponseViewModel>(_callNajaMotor.Data),
                                            AmountWage = inquiry.Data.Amount.Value,
                                            TransactionDate = inquiry.Data.BussinessDate.Value,
                                            NajaType = Convert.ToInt32(inquiry.Data.NajaType.Value),
                                            Token = inquiry.Data.PgwToken ?? inquiry.Data.WalletReturnId.Value,
                                            OrderId = inquiry.Data.OrderId.Value.ToString(),
                                            Message = (inquiry.Status == 2) ? "کارمزد از کیف پول پرداخت شده است" : "کارمزد از طریق درگاه پرداخت شده است",
                                        };
                                        return Ok(new MessageModel<PayWageResponseViewModel<MotorViolationInquiryResponseViewModel>>
                                        {
                                            message = (inquiry.Status == 2) ? DescriptionUtility.GetDescription<ServiceStatus>("WalletSuccess") :
                                                                              DescriptionUtility.GetDescription<ServiceStatus>("GetwaySuccess"),
                                            status = (inquiry.Status == 2) ? (int)ServiceStatus.WalletSuccess : (int)ServiceStatus.GetwaySuccess,
                                            Data = req
                                        });
                                    }
                                    else
                                    {
                                        PayWageResponseViewModel<MotorViolationInquiryResponseViewModel> req = null;

                                        req = new PayWageResponseViewModel<MotorViolationInquiryResponseViewModel>
                                        {
                                            NajaData = _mapper.Map<MotorViolationInquiryResponseViewModel>(_callNajaMotor.Data),
                                            AmountWage = inquiry.Data.Amount.Value,
                                            TransactionDate = inquiry.Data.BussinessDate.Value,
                                            NajaType = Convert.ToInt32(inquiry.Data.NajaType.Value),
                                            Token = inquiry.Data.PgwToken ?? inquiry.Data.WalletReturnId.Value,
                                            OrderId = inquiry.Data.OrderId.Value.ToString(),
                                            Message = (inquiry.Status == 2) ? "کارمزد از طریق کیف پول پرداخت شده است" : "کارمزد از طریق درگاه پرداخت شده است",
                                        };

                                        return Ok(new MessageModel<PayWageResponseViewModel<MotorViolationInquiryResponseViewModel>>
                                        {
                                            message = DescriptionUtility.GetDescription<ServiceStatus>("NotAvailableNajaService"),
                                            status = (int)ServiceStatus.NotAvailableNajaService,
                                            Data = req
                                        });
                                    }
                                }
                                else
                                {
                                    return Ok(new MessageModel<PayWageResponseViewModel<MotorViolationInquiryResponseViewModel>>
                                    {
                                        message = (inquiry.Status == 2) ? DescriptionUtility.GetDescription<ServiceStatus>("WalletUnSuccess") :
                                            DescriptionUtility.GetDescription<ServiceStatus>("GetwayUnSuccess"),
                                        status = (inquiry.Status == 2) ? (int)ServiceStatus.WalletUnSuccess : (int)ServiceStatus.GetwayUnSuccess,
                                        Data = null
                                    });
                                }
                            }
                            else
                            {
                                _logger.Log(13681368, "moh case 1 request is sent true =>" + inquiry.Data.OrderId.Value, null);
                                var _callGetInquiryNajaMotor = await _najaServices.GetInquiry(new GetInquiryRequestDto()
                                {
                                    TrackingNo = MotorviolationInquiryRequest.TrackingNo
                                });
                                _logger.Log(13681368, "moh from GetInquiry =>" + JsonConvert.SerializeObject(_callGetInquiryNajaMotor), null);

                                if (_callGetInquiryNajaMotor != null && _callGetInquiryNajaMotor.Status == 0)
                                {
                                    PayWageResponseViewModel<MotorViolationInquiryResponseViewModel> req = null;
                                    var najaData = JsonConvert.DeserializeObject<MotorViolationInquiryResponseDto>(JsonConvert.SerializeObject(_callGetInquiryNajaMotor.Data));
                                    req = new PayWageResponseViewModel<MotorViolationInquiryResponseViewModel>
                                    {
                                        NajaData = _mapper.Map<MotorViolationInquiryResponseViewModel>(najaData),
                                        AmountWage = inquiry.Data.Amount.Value,
                                        TransactionDate = inquiry.Data.BussinessDate.Value,
                                        NajaType = Convert.ToInt32(inquiry.Data.NajaType.Value),
                                        Token = inquiry.Data.PgwToken ?? inquiry.Data.WalletReturnId.Value,
                                        OrderId = inquiry.Data.OrderId.Value.ToString(),
                                        Message = (inquiry.Status == 2) ? "کارمزد از کیف پول پرداخت شده است" : "کارمزد از طریق درگاه پرداخت شده است",
                                    };
                                    return Ok(new MessageModel<PayWageResponseViewModel<MotorViolationInquiryResponseViewModel>>
                                    {
                                        message = (inquiry.Status == 2) ? DescriptionUtility.GetDescription<ServiceStatus>("WalletSuccess") :
                                                                          DescriptionUtility.GetDescription<ServiceStatus>("GetwaySuccess"),
                                        status = (inquiry.Status == 2) ? (int)ServiceStatus.WalletSuccess : (int)ServiceStatus.GetwaySuccess,
                                        Data = req
                                    });
                                }
                                else if (_callGetInquiryNajaMotor.Status == 502)
                                {
                                    PayWageResponseViewModel<ViolationInquiryResponseViewModel> req = null;
                                    var najaData = JsonConvert.DeserializeObject<ViolationInquiryResponseViewModel>(JsonConvert.SerializeObject(_callGetInquiryNajaMotor.Data));
                                    req = new PayWageResponseViewModel<ViolationInquiryResponseViewModel>
                                    {
                                        NajaData = null,
                                        AmountWage = inquiry.Data.Amount.Value,
                                        TransactionDate = inquiry.Data.BussinessDate.Value,
                                        NajaType = Convert.ToInt32(inquiry.Data.NajaType.Value),
                                        Token = inquiry.Data.PgwToken ?? inquiry.Data.WalletReturnId.Value,
                                        OrderId = inquiry.Data.OrderId.Value.ToString(),
                                        Message = (inquiry.Status == 2) ? "کارمزد از کیف پول پرداخت شده است" : "کارمزد از طریق درگاه پرداخت شده است",
                                    };

                                    return Ok(new MessageModel<PayWageResponseViewModel<ViolationInquiryResponseViewModel>>
                                    {
                                        message = "شماره پلاک با کد ملی تطابق ندارد",
                                        status = _callGetInquiryNajaMotor.Status,
                                        Data = req
                                    }); ;
                                }
                                else
                                {
                                    PayWageResponseViewModel<MotorViolationInquiryResponseViewModel> req = null;

                                    req = new PayWageResponseViewModel<MotorViolationInquiryResponseViewModel>
                                    {
                                        NajaData = _mapper.Map<MotorViolationInquiryResponseViewModel>(_callGetInquiryNajaMotor.Data),
                                        AmountWage = inquiry.Data.Amount.Value,
                                        TransactionDate = inquiry.Data.BussinessDate.Value,
                                        NajaType = Convert.ToInt32(inquiry.Data.NajaType.Value),
                                        Token = inquiry.Data.PgwToken ?? inquiry.Data.WalletReturnId.Value,
                                        OrderId = inquiry.Data.OrderId.Value.ToString(),
                                        Message = (inquiry.Status == 2) ? "کارمزد از طریق کیف پول پرداخت شده است" : "کارمزد از طریق درگاه پرداخت شده است",
                                    };

                                    return Ok(new MessageModel<PayWageResponseViewModel<MotorViolationInquiryResponseViewModel>>
                                    {
                                        message = DescriptionUtility.GetDescription<ServiceStatus>("NotAvailableNajaService"),
                                        status = (int)ServiceStatus.NotAvailableNajaService,
                                        Data = req
                                    });
                                }
                            }
                        #endregion

                        #region [- Case 3 : PaymentExitInquiryInquiry -]
                        case 3:

                            return Ok(new MessageModel<PayWageResponseViewModel<PaymentExitInquiryResponseDto>>
                            {
                                message = DescriptionUtility.GetDescription<ServiceStatus>("NotActiveService"),
                                status = (int)ServiceStatus.NotActiveService,
                                Data = null
                            });

                        #endregion

                        #region [- Case 4 : LicenseNegativePointInquiry -]
                        case 4:
                            var reqLicenseNegativePoint = JsonConvert.DeserializeObject<LicenseNegativePointInquiryObjectModel>(inquiry.Data.NajaServiceData);
                            var ViewModelLicenseNegativePointInquiry = _mapper.Map<LicenseNegativePointInquiryRequestViewModel>(reqLicenseNegativePoint);
                            ViewModelLicenseNegativePointInquiry.MobileNumber = inquiry.Data.Mobile;
                            ViewModelLicenseNegativePointInquiry.NationalCode = inquiry.Data.NationalCode;
                            ViewModelLicenseNegativePointInquiry.TrackingNo = inquiry.Data.OrderId.Value;
                            if (!inquiry.Data.RequestIsSent)
                            {
                                _logger.Log(13681368, "moh case 1 request is sent false =>" + inquiry.Data.OrderId.Value, null);
                                var result = await _najaServices.UpdateNajaWageRequestIsSentAsync(inquiry.Data.OrderId.Value);
                                _logger.Log(13681368, "moh from UpdateNajaWageRequestIsSentAsync =>" + result, null);
                                if (result)
                                {
                                    var Dto = _mapper.Map<LicenseNegativePointInquiryRequestDto>(ViewModelLicenseNegativePointInquiry);

                                    var _callLicenseNegativePointInquiry = await _najaServices.LicenseNegativePointInquiryAsync(Dto);
                                    if (_callLicenseNegativePointInquiry != null && _callLicenseNegativePointInquiry.Status == 0)
                                    {

                                        PayWageResponseViewModel<LicenseNegativePointInquiryResponseViewModel> req = null;
                                        req = new PayWageResponseViewModel<LicenseNegativePointInquiryResponseViewModel>
                                        {
                                            NajaData = _mapper.Map<LicenseNegativePointInquiryResponseViewModel>(_callLicenseNegativePointInquiry.Data),
                                            AmountWage = inquiry.Data.Amount.Value,
                                            TransactionDate = inquiry.Data.BussinessDate.Value,
                                            NajaType = Convert.ToInt32(inquiry.Data.NajaType.Value),
                                            Token = inquiry.Data.PgwToken ?? inquiry.Data.WalletReturnId.Value,
                                            OrderId = inquiry.Data.OrderId.Value.ToString(),
                                            Message = (inquiry.Status == 2) ? "کارمزد از کیف پول پرداخت شده است" : "کارمزد از طریق درگاه پرداخت شده است",
                                        };

                                        return Ok(new MessageModel<PayWageResponseViewModel<LicenseNegativePointInquiryResponseViewModel>>
                                        {
                                            message = (inquiry.Status == 2) ? DescriptionUtility.GetDescription<ServiceStatus>("WalletSuccess") :
                                                                              DescriptionUtility.GetDescription<ServiceStatus>("GetwaySuccess"),
                                            status = (inquiry.Status == 2) ? (int)ServiceStatus.WalletSuccess : (int)ServiceStatus.GetwaySuccess,
                                            Data = req
                                        });
                                    }
                                    else
                                    {
                                        PayWageResponseViewModel<LicenseNegativePointInquiryResponseViewModel> req = null;

                                        req = new PayWageResponseViewModel<LicenseNegativePointInquiryResponseViewModel>
                                        {
                                            NajaData = _mapper.Map<LicenseNegativePointInquiryResponseViewModel>(_callLicenseNegativePointInquiry.Data),
                                            AmountWage = inquiry.Data.Amount.Value,
                                            TransactionDate = inquiry.Data.BussinessDate.Value,
                                            NajaType = Convert.ToInt32(inquiry.Data.NajaType.Value),
                                            Token = inquiry.Data.PgwToken ?? inquiry.Data.WalletReturnId.Value,
                                            OrderId = inquiry.Data.OrderId.Value.ToString(),
                                            Message = (inquiry.Status == 2) ? "کارمزد از طریق کیف پول پرداخت شده است" : "کارمزد از طریق درگاه پرداخت شده است",
                                        };

                                        return Ok(new MessageModel<PayWageResponseViewModel<LicenseNegativePointInquiryResponseViewModel>>
                                        {
                                            message = DescriptionUtility.GetDescription<ServiceStatus>("NotAvailableNajaService"),
                                            status = (int)ServiceStatus.NotAvailableNajaService,
                                            Data = req
                                        });
                                    }
                                }
                                else
                                {
                                    return Ok(new MessageModel<PayWageResponseViewModel<LicenseNegativePointInquiryResponseViewModel>>
                                    {
                                        message = (inquiry.Status == 2) ? DescriptionUtility.GetDescription<ServiceStatus>("WalletUnSuccess") :
                                           DescriptionUtility.GetDescription<ServiceStatus>("GetwayUnSuccess"),
                                        status = (inquiry.Status == 2) ? (int)ServiceStatus.WalletUnSuccess : (int)ServiceStatus.GetwayUnSuccess,
                                        Data = null
                                    });
                                }
                            }
                            else
                            {
                                _logger.Log(13681368, "moh case 1 request is sent true =>" + inquiry.Data.OrderId.Value, null);
                                var _callNaja = await _najaServices.GetInquiry(new GetInquiryRequestDto()
                                {
                                    TrackingNo = ViewModelLicenseNegativePointInquiry.TrackingNo
                                });
                                _logger.Log(13681368, "moh from GetInquiry =>" + JsonConvert.SerializeObject(_callNaja), null);
                                if (_callNaja != null && _callNaja.Status == 0)
                                {
                                    PayWageResponseViewModel<LicenseNegativePointInquiryResponseViewModel> req = null;
                                    var najaData = JsonConvert.DeserializeObject<LicenseNegativePointInquiryResponseDto>(JsonConvert.SerializeObject(_callNaja.Data));
                                    req = new PayWageResponseViewModel<LicenseNegativePointInquiryResponseViewModel>
                                    {
                                        NajaData = _mapper.Map<LicenseNegativePointInquiryResponseViewModel>(najaData),
                                        AmountWage = inquiry.Data.Amount.Value,
                                        TransactionDate = inquiry.Data.BussinessDate.Value,
                                        NajaType = Convert.ToInt32(inquiry.Data.NajaType.Value),
                                        Token = inquiry.Data.PgwToken ?? inquiry.Data.WalletReturnId.Value,
                                        OrderId = inquiry.Data.OrderId.Value.ToString(),
                                        Message = (inquiry.Status == 2) ? "کارمزد از کیف پول پرداخت شده است" : "کارمزد از طریق درگاه پرداخت شده است",
                                    };

                                    return Ok(new MessageModel<PayWageResponseViewModel<LicenseNegativePointInquiryResponseViewModel>>
                                    {
                                        message = (inquiry.Status == 2) ? DescriptionUtility.GetDescription<ServiceStatus>("WalletSuccess") :
                                                                          DescriptionUtility.GetDescription<ServiceStatus>("GetwaySuccess"),
                                        status = (inquiry.Status == 2) ? (int)ServiceStatus.WalletSuccess : (int)ServiceStatus.GetwaySuccess,
                                        Data = req
                                    });
                                }
                                else if (_callNaja.Status == 502)
                                {
                                    PayWageResponseViewModel<LicenseNegativePointInquiryResponseViewModel> req = null;
                                    var najaData = JsonConvert.DeserializeObject<LicenseNegativePointInquiryResponseDto>(JsonConvert.SerializeObject(_callNaja.Data));
                                    req = new PayWageResponseViewModel<LicenseNegativePointInquiryResponseViewModel>
                                    {
                                        NajaData = null,
                                        AmountWage = inquiry.Data.Amount.Value,
                                        TransactionDate = inquiry.Data.BussinessDate.Value,
                                        NajaType = Convert.ToInt32(inquiry.Data.NajaType.Value),
                                        Token = inquiry.Data.PgwToken ?? inquiry.Data.WalletReturnId.Value,
                                        OrderId = inquiry.Data.OrderId.Value.ToString(),
                                        Message = (inquiry.Status == 2) ? "کارمزد از کیف پول پرداخت شده است" : "کارمزد از طریق درگاه پرداخت شده است",
                                    };

                                    return Ok(new MessageModel<PayWageResponseViewModel<LicenseNegativePointInquiryResponseViewModel>>
                                    {
                                        message = "شماره پلاک با کد ملی تطابق ندارد",
                                        status = _callNaja.Status,
                                        Data = req
                                    }); ;
                                }
                                else
                                {
                                    PayWageResponseViewModel<LicenseNegativePointInquiryResponseViewModel> req = null;

                                    req = new PayWageResponseViewModel<LicenseNegativePointInquiryResponseViewModel>
                                    {
                                        NajaData = _mapper.Map<LicenseNegativePointInquiryResponseViewModel>(_callNaja.Data),
                                        AmountWage = inquiry.Data.Amount.Value,
                                        TransactionDate = inquiry.Data.BussinessDate.Value,
                                        NajaType = Convert.ToInt32(inquiry.Data.NajaType.Value),
                                        Token = inquiry.Data.PgwToken ?? inquiry.Data.WalletReturnId.Value,
                                        OrderId = inquiry.Data.OrderId.Value.ToString(),
                                        Message = (inquiry.Status == 2) ? "کارمزد از طریق کیف پول پرداخت شده است" : "کارمزد از طریق درگاه پرداخت شده است",
                                    };

                                    return Ok(new MessageModel<PayWageResponseViewModel<LicenseNegativePointInquiryResponseViewModel>>
                                    {
                                        message = DescriptionUtility.GetDescription<ServiceStatus>("NotAvailableNajaService"),
                                        status = (int)ServiceStatus.NotAvailableNajaService,
                                        Data = req
                                    });
                                }
                            }

                        #endregion

                        #region [- Case 5 : PassportStatusInquiry -]
                        case 5:
                            return Ok(new MessageModel<PayWageResponseViewModel<PaymentExitInquiryResponseDto>>
                            {
                                message = DescriptionUtility.GetDescription<ServiceStatus>("NotActiveService"),
                                status = (int)ServiceStatus.NotActiveService,
                                Data = null
                            });
                        //var PassportStatusInquiry = new PassportStatusInquiryRequestViewModel()
                        //{
                        //    MobileNumber = inquiry.Data.Mobile,
                        //    NationalCode = inquiry.Data.NationalCode,
                        //    TrackingNo = inquiry.Data.OrderId.Value
                        //};
                        //var reqPassportStatusInquiry = _mapper.Map<PassportStatusInquiryRequestDto>(PassportStatusInquiry);

                        //var _callPassportStatusInquiry = await _najaServices.PassportStatusInquiry(reqPassportStatusInquiry);

                        //if (_callPassportStatusInquiry != null && _callPassportStatusInquiry.Status == 0)
                        //{
                        //    PayWageResponseViewModel<PassportStatusInquiryResponseViewModel> req = null;

                        //    req = new PayWageResponseViewModel<PassportStatusInquiryResponseViewModel>
                        //    {
                        //        NajaData = _mapper.Map<PassportStatusInquiryResponseViewModel>(_callPassportStatusInquiry.Data),
                        //        AmountWage = inquiry.Data.Amount.Value,
                        //        TransactionDate = inquiry.Data.BussinessDate.Value,
                        //        NajaType = Convert.ToInt32(inquiry.Data.NajaType.Value),
                        //        Token = inquiry.Data.PgwToken ?? inquiry.Data.WalletReturnId.Value,
                        //        OrderId = inquiry.Data.OrderId.Value.ToString(),
                        //        Message = (inquiry.Status == 2) ? "کارمزد از کیف پول پرداخت شده است" : "کارمزد از طریق درگاه پرداخت شده است",
                        //    };

                        //    return Ok(new MessageModel<PayWageResponseViewModel<PassportStatusInquiryResponseViewModel>>
                        //    {
                        //        message = inquiry.Message,
                        //        status = (int)ServiceStatus.WalletSuccess,
                        //        Data = req
                        //    });
                        //}
                        //else
                        //{
                        //    return Ok(new MessageModel<PayWageResponseViewModel<PassportStatusInquiryResponseViewModel>>
                        //    {
                        //        message = inquiry.Message,
                        //        status = (int)ServiceStatus.WalletUnSuccess,
                        //        Data = null
                        //    });
                        //}
                        #endregion

                        #region [- Case 6 : AccumulationViolationsInquiry -]
                        case 6:
                            var ReqCarPlate = JsonConvert.DeserializeObject<CarViolationInquiryObjectModel>(inquiry.Data.NajaServiceData);
                            var AccumulationViolationsInquiryRequest = _mapper.Map<AccumulationViolationsInquiryRequestViewModel>(ReqCarPlate);
                            AccumulationViolationsInquiryRequest.MobileNumber = inquiry.Data.Mobile;
                            AccumulationViolationsInquiryRequest.NationalCode = inquiry.Data.NationalCode;
                            AccumulationViolationsInquiryRequest.TrackingNo = inquiry.Data.OrderId.Value;
                            if (!inquiry.Data.RequestIsSent)
                            {
                                _logger.Log(13681368, "moh case 6 request is sent false =>" + inquiry.Data.OrderId.Value, null);
                                var result = await _najaServices.UpdateNajaWageRequestIsSentAsync(inquiry.Data.OrderId.Value);
                                _logger.Log(13681368, "moh from UpdateNajaWageRequestIsSentAsync =>" + result, null);
                                if (result)
                                {
                                    var reqAccumulationViolationsInquiry = _mapper.Map<AccumulationViolationsInquiryRequestDto>(AccumulationViolationsInquiryRequest);

                                    var _callAccumulationViolationsInquiry = await _najaServices.AccumulationViolationsInquiryAsync(reqAccumulationViolationsInquiry);

                                    if (_callAccumulationViolationsInquiry != null && _callAccumulationViolationsInquiry.Status == 0)
                                    {
                                        PayWageResponseViewModel<AccumulationViolationsInquiryResponseViewModel> req = null;

                                        req = new PayWageResponseViewModel<AccumulationViolationsInquiryResponseViewModel>
                                        {
                                            NajaData = _mapper.Map<AccumulationViolationsInquiryResponseViewModel>(_callAccumulationViolationsInquiry.Data),
                                            AmountWage = inquiry.Data.Amount.Value,
                                            TransactionDate = inquiry.Data.BussinessDate.Value,
                                            NajaType = Convert.ToInt32(inquiry.Data.NajaType.Value),
                                            Token = inquiry.Data.PgwToken ?? inquiry.Data.WalletReturnId.Value,
                                            OrderId = inquiry.Data.OrderId.Value.ToString(),
                                            Message = (inquiry.Status == 2) ? "کارمزد از کیف پول پرداخت شده است" : "کارمزد از طریق درگاه پرداخت شده است",
                                        };

                                        return Ok(new MessageModel<PayWageResponseViewModel<AccumulationViolationsInquiryResponseViewModel>>
                                        {
                                            message = inquiry.Message,
                                            status = (int)ServiceStatus.WalletSuccess,
                                            Data = req
                                        });
                                    }
                                    else
                                    {
                                        PayWageResponseViewModel<AccumulationViolationsInquiryResponseViewModel> req = null;

                                        req = new PayWageResponseViewModel<AccumulationViolationsInquiryResponseViewModel>
                                        {
                                            NajaData = _mapper.Map<AccumulationViolationsInquiryResponseViewModel>(_callAccumulationViolationsInquiry.Data),
                                            AmountWage = inquiry.Data.Amount.Value,
                                            TransactionDate = inquiry.Data.BussinessDate.Value,
                                            NajaType = Convert.ToInt32(inquiry.Data.NajaType.Value),
                                            Token = inquiry.Data.PgwToken ?? inquiry.Data.WalletReturnId.Value,
                                            OrderId = inquiry.Data.OrderId.Value.ToString(),
                                            Message = (inquiry.Status == 2) ? "کارمزد از طریق کیف پول پرداخت شده است" : "کارمزد از طریق درگاه پرداخت شده است",
                                        };

                                        return Ok(new MessageModel<PayWageResponseViewModel<AccumulationViolationsInquiryResponseViewModel>>
                                        {
                                            message = DescriptionUtility.GetDescription<ServiceStatus>("NotAvailableNajaService"),
                                            status = (int)ServiceStatus.NotAvailableNajaService,
                                            Data = req
                                        });
                                    }
                                }
                                else
                                {
                                    return Ok(new MessageModel<PayWageResponseViewModel<AccumulationViolationsInquiryResponseViewModel>>
                                    {
                                        message = inquiry.Message,
                                        status = (int)ServiceStatus.WalletUnSuccess,
                                        Data = null
                                    });
                                }

                            }
                            else
                            {

                                _logger.Log(13681368, "moh case 1 request is sent true =>" + inquiry.Data.OrderId.Value, null);
                                var _callAccumulationViolationsInquiry = await _najaServices.GetInquiry(new GetInquiryRequestDto()
                                {
                                    TrackingNo = AccumulationViolationsInquiryRequest.TrackingNo
                                });
                                _logger.Log(13681368, "moh from GetInquiry =>" + JsonConvert.SerializeObject(_callAccumulationViolationsInquiry), null);

                                if (_callAccumulationViolationsInquiry != null && _callAccumulationViolationsInquiry.Status == 0)
                                {
                                    PayWageResponseViewModel<AccumulationViolationsInquiryResponseViewModel> req = null;
                                    var najaData = JsonConvert.DeserializeObject<AccumulationViolationsInquiryResponseDto>(JsonConvert.SerializeObject(_callAccumulationViolationsInquiry.Data));
                                    req = new PayWageResponseViewModel<AccumulationViolationsInquiryResponseViewModel>
                                    {
                                        NajaData = _mapper.Map<AccumulationViolationsInquiryResponseViewModel>(najaData),
                                        AmountWage = inquiry.Data.Amount.Value,
                                        TransactionDate = inquiry.Data.BussinessDate.Value,
                                        NajaType = Convert.ToInt32(inquiry.Data.NajaType.Value),
                                        Token = inquiry.Data.PgwToken ?? inquiry.Data.WalletReturnId.Value,
                                        OrderId = inquiry.Data.OrderId.Value.ToString(),
                                        Message = (inquiry.Status == 2) ? "کارمزد از کیف پول پرداخت شده است" : "کارمزد از طریق درگاه پرداخت شده است",
                                    };

                                    return Ok(new MessageModel<PayWageResponseViewModel<AccumulationViolationsInquiryResponseViewModel>>
                                    {
                                        message = inquiry.Message,
                                        status = (int)ServiceStatus.WalletSuccess,
                                        Data = req
                                    });
                                }
                                else if (_callAccumulationViolationsInquiry.Status == 502)
                                {
                                    PayWageResponseViewModel<ViolationInquiryResponseViewModel> req = null;
                                    var najaData = JsonConvert.DeserializeObject<ViolationInquiryResponseViewModel>(JsonConvert.SerializeObject(_callAccumulationViolationsInquiry.Data));
                                    req = new PayWageResponseViewModel<ViolationInquiryResponseViewModel>
                                    {
                                        NajaData = null,
                                        AmountWage = inquiry.Data.Amount.Value,
                                        TransactionDate = inquiry.Data.BussinessDate.Value,
                                        NajaType = Convert.ToInt32(inquiry.Data.NajaType.Value),
                                        Token = inquiry.Data.PgwToken ?? inquiry.Data.WalletReturnId.Value,
                                        OrderId = inquiry.Data.OrderId.Value.ToString(),
                                        Message = (inquiry.Status == 2) ? "کارمزد از کیف پول پرداخت شده است" : "کارمزد از طریق درگاه پرداخت شده است",
                                    };

                                    return Ok(new MessageModel<PayWageResponseViewModel<ViolationInquiryResponseViewModel>>
                                    {
                                        message = "شماره پلاک با کد ملی تطابق ندارد",
                                        status = _callAccumulationViolationsInquiry.Status,
                                        Data = req
                                    }); ;
                                }
                                else
                                {
                                    PayWageResponseViewModel<AccumulationViolationsInquiryResponseViewModel> req = null;

                                    req = new PayWageResponseViewModel<AccumulationViolationsInquiryResponseViewModel>
                                    {
                                        NajaData = _mapper.Map<AccumulationViolationsInquiryResponseViewModel>(_callAccumulationViolationsInquiry.Data),
                                        AmountWage = inquiry.Data.Amount.Value,
                                        TransactionDate = inquiry.Data.BussinessDate.Value,
                                        NajaType = Convert.ToInt32(inquiry.Data.NajaType.Value),
                                        Token = inquiry.Data.PgwToken ?? inquiry.Data.WalletReturnId.Value,
                                        OrderId = inquiry.Data.OrderId.Value.ToString(),
                                        Message = (inquiry.Status == 2) ? "کارمزد از طریق کیف پول پرداخت شده است" : "کارمزد از طریق درگاه پرداخت شده است",
                                    };

                                    return Ok(new MessageModel<PayWageResponseViewModel<AccumulationViolationsInquiryResponseViewModel>>
                                    {
                                        message = DescriptionUtility.GetDescription<ServiceStatus>("NotAvailableNajaService"),
                                        status = (int)ServiceStatus.NotAvailableNajaService,
                                        Data = req
                                    });
                                }
                            }

                        #endregion

                        #region [- Case 7 : ExitTaxesReceipt -]
                        case 7:
                            return Ok(new MessageModel<PayWageResponseViewModel<ExitTaxesReceiptResponseViewModel>>
                            {
                                message = DescriptionUtility.GetDescription<ServiceStatus>("NotActiveService"),
                                status = (int)ServiceStatus.NotActiveService,
                                Data = null
                            });
                        #endregion

                        #region [- Case 8 : LicenseStatusInquiry -]
                        case 8:
                            var LicenseStatusInquiry = new LicenseStatusInquiryRequestViewModel()
                            {
                                MobileNumber = inquiry.Data.Mobile,
                                NationalCode = inquiry.Data.NationalCode,
                                TrackingNo = inquiry.Data.OrderId.Value
                            };

                            if (!inquiry.Data.RequestIsSent)
                            {
                                _logger.Log(13681368, "moh case 1 request is sent false =>" + inquiry.Data.OrderId.Value, null);
                                var result = await _najaServices.UpdateNajaWageRequestIsSentAsync(inquiry.Data.OrderId.Value);
                                _logger.Log(13681368, "moh from UpdateNajaWageRequestIsSentAsync =>" + result, null);
                                if (result)
                                {
                                    var reqLicenseStatusInquiry = _mapper.Map<LicenseStatusInquiryRequestDto>(LicenseStatusInquiry);

                                    var _callLicenseStatusInquiry = await _najaServices.LicenseStatusInquiryAsync(reqLicenseStatusInquiry);

                                    if (_callLicenseStatusInquiry != null && _callLicenseStatusInquiry.Status == 0)
                                    {
                                        PayWageResponseViewModel<List<LicenseStatusInquiryResponseViewModel>> req = null;

                                        req = new PayWageResponseViewModel<List<LicenseStatusInquiryResponseViewModel>>
                                        {
                                            NajaData = _mapper.Map<List<LicenseStatusInquiryResponseViewModel>>(_callLicenseStatusInquiry.Data),
                                            AmountWage = inquiry.Data.Amount.Value,
                                            TransactionDate = inquiry.Data.BussinessDate.Value,
                                            NajaType = Convert.ToInt32(inquiry.Data.NajaType.Value),
                                            Token = inquiry.Data.PgwToken ?? inquiry.Data.WalletReturnId.Value,
                                            OrderId = inquiry.Data.OrderId.Value.ToString(),
                                            Message = (inquiry.Status == 2) ? "کارمزد از کیف پول پرداخت شده است" : "کارمزد از طریق درگاه پرداخت شده است",
                                        };
                                        return Ok(new MessageModel<PayWageResponseViewModel<List<LicenseStatusInquiryResponseViewModel>>>
                                        {
                                            message = inquiry.Message,
                                            status = (int)ServiceStatus.WalletSuccess,
                                            Data = req
                                        });
                                    }
                                    else
                                    {
                                        PayWageResponseViewModel<List<LicenseStatusInquiryResponseViewModel>> req = null;

                                        req = new PayWageResponseViewModel<List<LicenseStatusInquiryResponseViewModel>>
                                        {
                                            NajaData = _mapper.Map<List<LicenseStatusInquiryResponseViewModel>>(_callLicenseStatusInquiry.Data),
                                            AmountWage = inquiry.Data.Amount.Value,
                                            TransactionDate = inquiry.Data.BussinessDate.Value,
                                            NajaType = Convert.ToInt32(inquiry.Data.NajaType.Value),
                                            Token = inquiry.Data.PgwToken ?? inquiry.Data.WalletReturnId.Value,
                                            OrderId = inquiry.Data.OrderId.Value.ToString(),
                                            Message = (inquiry.Status == 2) ? "کارمزد از طریق کیف پول پرداخت شده است" : "کارمزد از طریق درگاه پرداخت شده است",
                                        };

                                        return Ok(new MessageModel<PayWageResponseViewModel<List<LicenseStatusInquiryResponseViewModel>>>
                                        {
                                            message = DescriptionUtility.GetDescription<ServiceStatus>("NotAvailableNajaService"),
                                            status = (int)ServiceStatus.NotAvailableNajaService,
                                            Data = req
                                        });
                                    }
                                }
                                else
                                {
                                    return Ok(new MessageModel<PayWageResponseViewModel<List<LicenseStatusInquiryResponseViewModel>>>
                                    {
                                        message = inquiry.Message,
                                        status = (int)ServiceStatus.WalletUnSuccess,
                                        Data = null
                                    });
                                }

                            }
                            else
                            {
                                _logger.Log(13681368, "moh case 1 request is sent true =>" + inquiry.Data.OrderId.Value, null);
                                var _callLicenseStatusInquiry = await _najaServices.GetInquiry(new GetInquiryRequestDto()
                                {
                                    TrackingNo = LicenseStatusInquiry.TrackingNo
                                });
                                _logger.Log(13681368, "moh from GetInquiry =>" + JsonConvert.SerializeObject(_callLicenseStatusInquiry), null);

                                if (_callLicenseStatusInquiry != null && _callLicenseStatusInquiry.Status == 0)
                                {
                                    PayWageResponseViewModel<List<LicenseStatusInquiryResponseViewModel>> req = null;
                                    var najaData = JsonConvert.DeserializeObject<List<LicenseStatusInquiryResponseViewModel>>(JsonConvert.SerializeObject(_callLicenseStatusInquiry.Data));
                                    req = new PayWageResponseViewModel<List<LicenseStatusInquiryResponseViewModel>>
                                    {
                                        NajaData = _mapper.Map<List<LicenseStatusInquiryResponseViewModel>>(najaData),
                                        AmountWage = inquiry.Data.Amount.Value,
                                        TransactionDate = inquiry.Data.BussinessDate.Value,
                                        NajaType = Convert.ToInt32(inquiry.Data.NajaType.Value),
                                        Token = inquiry.Data.PgwToken ?? inquiry.Data.WalletReturnId.Value,
                                        OrderId = inquiry.Data.OrderId.Value.ToString(),
                                        Message = (inquiry.Status == 2) ? "کارمزد از کیف پول پرداخت شده است" : "کارمزد از طریق درگاه پرداخت شده است",
                                    };
                                    return Ok(new MessageModel<PayWageResponseViewModel<List<LicenseStatusInquiryResponseViewModel>>>
                                    {
                                        message = inquiry.Message,
                                        status = (int)ServiceStatus.WalletSuccess,
                                        Data = req
                                    });
                                }
                                else
                                {
                                    PayWageResponseViewModel<List<LicenseStatusInquiryResponseViewModel>> req = null;

                                    req = new PayWageResponseViewModel<List<LicenseStatusInquiryResponseViewModel>>
                                    {
                                        NajaData = _mapper.Map<List<LicenseStatusInquiryResponseViewModel>>(_callLicenseStatusInquiry.Data),
                                        AmountWage = inquiry.Data.Amount.Value,
                                        TransactionDate = inquiry.Data.BussinessDate.Value,
                                        NajaType = Convert.ToInt32(inquiry.Data.NajaType.Value),
                                        Token = inquiry.Data.PgwToken ?? inquiry.Data.WalletReturnId.Value,
                                        OrderId = inquiry.Data.OrderId.Value.ToString(),
                                        Message = (inquiry.Status == 2) ? "کارمزد از طریق کیف پول پرداخت شده است" : "کارمزد از طریق درگاه پرداخت شده است",
                                    };

                                    return Ok(new MessageModel<PayWageResponseViewModel<List<LicenseStatusInquiryResponseViewModel>>>
                                    {
                                        message = DescriptionUtility.GetDescription<ServiceStatus>("NotAvailableNajaService"),
                                        status = (int)ServiceStatus.NotAvailableNajaService,
                                        Data = req
                                    });
                                }
                            }

                        #endregion

                        #region [- Case 9 : ActivePlakInquiry -]
                        case 9:
                            var ActivePlakInquiry = new ActivePlakInquiryRequestViewModel()
                            {
                                MobileNumber = inquiry.Data.Mobile,
                                NationalCode = inquiry.Data.NationalCode,
                                TrackingNo = inquiry.Data.OrderId.Value
                            };

                            if (!inquiry.Data.RequestIsSent)
                            {
                                _logger.Log(13681368, "moh case 1 request is sent false =>" + inquiry.Data.OrderId.Value, null);
                                var result = await _najaServices.UpdateNajaWageRequestIsSentAsync(inquiry.Data.OrderId.Value);
                                _logger.Log(13681368, "moh from UpdateNajaWageRequestIsSentAsync =>" + result, null);
                                if (result)
                                {
                                    var reqActivePlakInquiry = _mapper.Map<ActivePlakInquiryRequestDto>(ActivePlakInquiry);

                                    var _callActivePlakInquiry = await _najaServices.ActivePlakInquiryAsync(reqActivePlakInquiry);

                                    if (_callActivePlakInquiry != null && _callActivePlakInquiry.Status == 0)
                                    {
                                        PayWageResponseViewModel<ListActivePlakInquiryResponseViewModel> req = null;
                                        ListActivePlakInquiryResponseViewModel listActivePlak = new ListActivePlakInquiryResponseViewModel();
                                        var list = _mapper.Map<List<ActivePlakInquiryResponseViewModel>>(_callActivePlakInquiry.Data);
                                        listActivePlak.ActivePlateList.AddRange(list);

                                        req = new PayWageResponseViewModel<ListActivePlakInquiryResponseViewModel>
                                        {
                                            NajaData = listActivePlak,
                                            AmountWage = inquiry.Data.Amount.Value,
                                            TransactionDate = inquiry.Data.BussinessDate.Value,
                                            NajaType = Convert.ToInt32(inquiry.Data.NajaType.Value),
                                            Token = inquiry.Data.PgwToken ?? inquiry.Data.WalletReturnId.Value,
                                            OrderId = inquiry.Data.OrderId.Value.ToString(),
                                            Message = (inquiry.Status == 2) ? "کارمزد از کیف پول پرداخت شده است" : "کارمزد از طریق درگاه پرداخت شده است",
                                        };

                                        return Ok(new MessageModel<PayWageResponseViewModel<ListActivePlakInquiryResponseViewModel>>
                                        {
                                            message = inquiry.Message,
                                            status = (int)ServiceStatus.WalletSuccess,
                                            Data = req
                                        });
                                    }
                                    else
                                    {
                                        PayWageResponseViewModel<ListActivePlakInquiryResponseViewModel> req = null;

                                        req = new PayWageResponseViewModel<ListActivePlakInquiryResponseViewModel>
                                        {
                                            NajaData = _mapper.Map<ListActivePlakInquiryResponseViewModel>(_callActivePlakInquiry.Data),
                                            AmountWage = inquiry.Data.Amount.Value,
                                            TransactionDate = inquiry.Data.BussinessDate.Value,
                                            NajaType = Convert.ToInt32(inquiry.Data.NajaType.Value),
                                            Token = inquiry.Data.PgwToken ?? inquiry.Data.WalletReturnId.Value,
                                            OrderId = inquiry.Data.OrderId.Value.ToString(),
                                            Message = (inquiry.Status == 2) ? "کارمزد از طریق کیف پول پرداخت شده است" : "کارمزد از طریق درگاه پرداخت شده است",
                                        };

                                        return Ok(new MessageModel<PayWageResponseViewModel<ListActivePlakInquiryResponseViewModel>>
                                        {
                                            message = DescriptionUtility.GetDescription<ServiceStatus>("NotAvailableNajaService"),
                                            status = (int)ServiceStatus.NotAvailableNajaService,
                                            Data = req
                                        });
                                    }
                                }
                                else
                                {
                                    return Ok(new MessageModel<PayWageResponseViewModel<List<ActivePlakInquiryResponseViewModel>>>
                                    {
                                        message = inquiry.Message,
                                        status = (int)ServiceStatus.WalletUnSuccess,
                                        Data = null
                                    });
                                }
                            }
                            else
                            {
                                _logger.Log(13681368, "moh case 1 request is sent true =>" + inquiry.Data.OrderId.Value, null);
                                var _callActivePlakInquiry = await _najaServices.GetInquiry(new GetInquiryRequestDto()
                                {
                                    TrackingNo = ActivePlakInquiry.TrackingNo
                                });
                                _logger.Log(13681368, "moh from GetInquiry =>" + JsonConvert.SerializeObject(_callActivePlakInquiry), null);

                                if (_callActivePlakInquiry != null && _callActivePlakInquiry.Status == 0)
                                {
                                    PayWageResponseViewModel<ListActivePlakInquiryResponseViewModel> req = null;
                                    ListActivePlakInquiryResponseViewModel listActivePlak = new ListActivePlakInquiryResponseViewModel();
                                    //var list = _mapper.Map<List<ActivePlakInquiryResponseViewModel>>(_callActivePlakInquiry.Data);
                                    var najaData = JsonConvert.DeserializeObject<List<ActivePlakInquiryResponseViewModel>>(JsonConvert.SerializeObject(_callActivePlakInquiry.Data));
                                    listActivePlak.ActivePlateList.AddRange(najaData);

                                    req = new PayWageResponseViewModel<ListActivePlakInquiryResponseViewModel>
                                    {
                                        NajaData = listActivePlak,
                                        AmountWage = inquiry.Data.Amount.Value,
                                        TransactionDate = inquiry.Data.BussinessDate.Value,
                                        NajaType = Convert.ToInt32(inquiry.Data.NajaType.Value),
                                        Token = inquiry.Data.PgwToken ?? inquiry.Data.WalletReturnId.Value,
                                        OrderId = inquiry.Data.OrderId.Value.ToString(),
                                        Message = (inquiry.Status == 2) ? "کارمزد از کیف پول پرداخت شده است" : "کارمزد از طریق درگاه پرداخت شده است",
                                    };

                                    return Ok(new MessageModel<PayWageResponseViewModel<ListActivePlakInquiryResponseViewModel>>
                                    {
                                        message = inquiry.Message,
                                        status = (int)ServiceStatus.WalletSuccess,
                                        Data = req
                                    });
                                }
                                else
                                {
                                    PayWageResponseViewModel<ListActivePlakInquiryResponseViewModel> req = null;

                                    req = new PayWageResponseViewModel<ListActivePlakInquiryResponseViewModel>
                                    {
                                        NajaData = _mapper.Map<ListActivePlakInquiryResponseViewModel>(_callActivePlakInquiry.Data),
                                        AmountWage = inquiry.Data.Amount.Value,
                                        TransactionDate = inquiry.Data.BussinessDate.Value,
                                        NajaType = Convert.ToInt32(inquiry.Data.NajaType.Value),
                                        Token = inquiry.Data.PgwToken ?? inquiry.Data.WalletReturnId.Value,
                                        OrderId = inquiry.Data.OrderId.Value.ToString(),
                                        Message = (inquiry.Status == 2) ? "کارمزد از طریق کیف پول پرداخت شده است" : "کارمزد از طریق درگاه پرداخت شده است",
                                    };

                                    return Ok(new MessageModel<PayWageResponseViewModel<ListActivePlakInquiryResponseViewModel>>
                                    {
                                        message = DescriptionUtility.GetDescription<ServiceStatus>("NotAvailableNajaService"),
                                        status = (int)ServiceStatus.NotAvailableNajaService,
                                        Data = req
                                    });
                                }
                            }

                        #endregion

                        #region [- Case 10 : NoExitInquiry -]
                        case 10:
                            return Ok(new MessageModel<PayWageResponseViewModel<PaymentExitInquiryResponseDto>>
                            {
                                message = DescriptionUtility.GetDescription<ServiceStatus>("NotActiveService"),
                                status = (int)ServiceStatus.NotActiveService,
                                Data = null
                            });
                        //var NoExitInquiry = new NoExitInquiryRequestViewModel()
                        //{
                        //    MobileNumber = inquiry.Data.Mobile,
                        //    NationalCode = inquiry.Data.NationalCode
                        //};
                        //var reqNoExitInquiry = _mapper.Map<NoExitInquiryRequestDto>(NoExitInquiry);

                        //var _callNoExitInquiry = await _najaServices.NoExitInquiry(reqNoExitInquiry);

                        //if (_callNoExitInquiry != null && _callNoExitInquiry.Status == 0)
                        //{
                        //    PayWageResponseViewModel<NoExitInquiryResponseViewModel> req = null;

                        //    req = new PayWageResponseViewModel<NoExitInquiryResponseViewModel>
                        //    {
                        //        NajaData = _mapper.Map<NoExitInquiryResponseViewModel>(_callNoExitInquiry.Data),
                        //        NajaType = Convert.ToInt32(inquiry.Data.NajaType.Value),
                        //        AmountWage = inquiry.Data.Amount.Value,
                        //        TransactionDate = inquiry.Data.BussinessDate.Value,
                        //        Token = inquiry.Data.PgwToken ?? inquiry.Data.WalletReturnId.Value,
                        //        OrderId = inquiry.Data.OrderId.Value.ToString(),
                        //        Message = (inquiry.Status == 2) ? "کارمزد از کیف پول پرداخت شده است" : "کارمزد از طریق درگاه پرداخت شده است",
                        //    };

                        //    return Ok(new MessageModel<PayWageResponseViewModel<NoExitInquiryResponseViewModel>>
                        //    {
                        //        message = inquiry.Message,
                        //        status = (int)ServiceStatus.WalletSuccess,
                        //        Data = req
                        //    });
                        //}
                        //else
                        //{
                        //    return Ok(new MessageModel<PayWageResponseViewModel<NoExitInquiryResponseViewModel>>
                        //    {
                        //        message = inquiry.Message,
                        //        status = (int)ServiceStatus.WalletUnSuccess,
                        //        Data = null
                        //    });
                        //}
                        #endregion

                        #region [- Case 11 : ViolationImageInquiry -]
                        case 11:

                            var reqViolationImageInquiry = JsonConvert.DeserializeObject<ViolationImageInquiryRequestViewModel>(inquiry.Data.NajaServiceData);
                            var ViolationImageInquiryRequest = _mapper.Map<ViolationImageInquiryRequestDto>(reqViolationImageInquiry);
                            ViolationImageInquiryRequest.MobileNumber = inquiry.Data.Mobile;
                            ViolationImageInquiryRequest.NationalCode = inquiry.Data.NationalCode;
                            ViolationImageInquiryRequest.TrackingNo = inquiry.Data.OrderId.Value;


                            var _callViolationImageInquiry = await _najaServices.ViolationImageInquiryAsync(ViolationImageInquiryRequest);

                            if (_callViolationImageInquiry != null && _callViolationImageInquiry.Status == 0)
                            {
                                PayWageResponseViewModel<ViolationImageInquiryResponseViewModel> req = null;

                                req = new PayWageResponseViewModel<ViolationImageInquiryResponseViewModel>
                                {
                                    NajaData = _mapper.Map<ViolationImageInquiryResponseViewModel>(_callViolationImageInquiry.Data),
                                    AmountWage = inquiry.Data.Amount.Value,
                                    TransactionDate = inquiry.Data.BussinessDate.Value,
                                    NajaType = Convert.ToInt32(inquiry.Data.NajaType.Value),
                                    Token = inquiry.Data.PgwToken ?? inquiry.Data.WalletReturnId.Value,
                                    OrderId = inquiry.Data.OrderId.Value.ToString(),
                                    Message = (inquiry.Status == 2) ? "کارمزد از کیف پول پرداخت شده است" : "کارمزد از طریق درگاه پرداخت شده است",
                                };

                                return Ok(new MessageModel<PayWageResponseViewModel<ViolationImageInquiryResponseViewModel>>
                                {
                                    message = inquiry.Message,
                                    status = (int)ServiceStatus.WalletSuccess,
                                    Data = req
                                });
                            }
                            else
                            {
                                return Ok(new MessageModel<PayWageResponseViewModel<ViolationImageInquiryResponseViewModel>>
                                {
                                    message = inquiry.Message,
                                    status = (int)ServiceStatus.WalletUnSuccess,
                                    Data = null
                                });
                            }
                        #endregion

                        default:
                            return Ok(new MessageModel<PayWageResponseViewModel<object>>
                            {
                                message = DescriptionUtility.GetDescription<ServiceStatus>("InValidNajaType"),
                                status = (int)ServiceStatus.InValidNajaType,
                                Data = null
                            });
                    }
                }
                else
                {
                    var req = new PayWageResponseViewModel<object>
                    {
                        NajaData = null,
                        NajaType = -1,
                        Token = -100,
                        OrderId = "-100",
                        Message = inquiry.Message
                    };

                    return Ok(new MessageModel<PayWageResponseViewModel<object>>
                    {
                        message = DescriptionUtility.GetDescription<ServiceStatus>("OperationUnSuccess"),
                        status = (int)ServiceStatus.OrderIdNotValid,
                        Data = req
                    });
                }
            }
            catch (Exception e)
            {
                return Ok(new MessageModel<PayWageResponseViewModel<object>>
                {
                    message = DescriptionUtility.GetDescription<ServiceStatus>("OperationFaild"),
                    status = (int)ServiceStatus.OperationFaild,
                    Data = null
                });
            }
        }

        [HttpPost("ConfirmWage")]
        [AllowAnonymous]
        public async Task<IActionResult> ConfirmWage(string mid)
        {
            var model = new NajaWageDto();

            try
            {
                var status = short.Parse(Request.Form["status"]);
                model.PgwToken = long.Parse(Request.Form["Token"]);
                model.OrderId = long.Parse(Request.Form["OrderId"]);
                model.PaymentStatus = short.Parse(Request.Form["status"]);
                model.MaskCardNumber = Request.Form["HashCardNumber"];
                var urlReceipt = "";
                if (string.IsNullOrEmpty(mid) || mid == "0")
                {
                    urlReceipt = _pecBmsSetting.ReceiptPayWage + model.OrderId;
                }
                else
                {
                    urlReceipt = _pecBmsSetting.ReceiptPayWage + model.OrderId + "?mid=" + mid;
                }

                if (status != 0)
                {
                    _logger.Log(806, $"📥 ConfirmWage When Status != 0", JsonConvert.SerializeObject(model), null);

                    await _najaServices.ConfirmPayWageAsync(model);
                    return Redirect(urlReceipt);
                }

                //model.TerminalNumber = Request.Form["TerminalNo"];
                _logger.Log(813, $"📥 ConfirmWage When Status == 0", JsonConvert.SerializeObject(model), null);
                model.RRN = Convert.ToInt64(Request.Form["RRN"]);
                var amount = Request.Form["Amount"].ToString().Replace(",", "");
                model.Amount = Convert.ToInt64(amount);
                await _najaServices.ConfirmPayWageAsync(model);
                return Redirect(urlReceipt);
            }
            catch (Exception ex)
            {
                _logger.Log(822, $"📥 get error when ConfirmWage", JsonConvert.SerializeObject(model), ex);
                string urlReceipt;
                if (string.IsNullOrEmpty(mid) && mid == "0")
                {
                    urlReceipt = _pecBmsSetting.ReceiptPayWage + -100;
                }
                else
                {
                    urlReceipt = _pecBmsSetting.ReceiptPayWage + -100 + "?mid=" + mid;
                }
                return Redirect(urlReceipt);
            }
        }


        [HttpPost("PayWage")]
        [LoggingAspect]
        public async Task<IActionResult> PayWage(PayWageRequestViewModel payWageRequest)
        {
            try
            {
                #region CheckUser
                var userId = User.GetUserId();
                if (userId == 0)
                {
                    return Ok(new MessageModel
                    {
                        message = DescriptionUtility.GetDescription<ServiceStatus>("NoUserFound"),
                        status = (int)ServiceStatus.NoUserFound
                    });
                }
                #endregion

                #region checkIsOwnerWallet
                if (payWageRequest.TransactionType != 1 && payWageRequest.TransactionType != 2)
                {
                    return Ok(new MessageModel
                    {
                        message = DescriptionUtility.GetDescription<ServiceStatus>("InValidTransactionType"),
                        status = (int)ServiceStatus.InValidTransactionType
                    });
                }
                else if (payWageRequest.TransactionType == 2)
                {
                    var userWallets = await _walletServices.GetUserWallet(userId);
                    //تست
                    var isOwner = userWallets.Any(p => p.WalletCode == payWageRequest.WalletCode);
                    // var isOwnerWallet = User.IsWalletOwner(payWageRequest.WalletCode.ToString());
                    if (!isOwner)
                    {
                        return Ok(new MessageModel
                        {
                            message = DescriptionUtility.GetDescription<ServiceStatus>("WalletNotUser"),
                            status = (int)ServiceStatus.WalletNotUser
                        });
                    }
                }
                #endregion

                var FirstRequestpayWage = _mapper.Map<PayWageRequestDto>(payWageRequest);
                FirstRequestpayWage.UserId = userId;
                FirstRequestpayWage.Amount = _pecBmsSetting.PayWageNajaAmount;
                //generate orderId
                var OrderId = Utility.Utility.GenerateNajaOrderID();

                var najaPayDto = new NajaWageDto
                {
                    CreateDate = DateTime.Now,
                    BussinessDate = DateTime.Now,
                    Mobile = FirstRequestpayWage.MobileNumber,
                    NationalCode = FirstRequestpayWage.NationalCode,
                    OrderId = OrderId,
                    UserId = userId,
                    MerchantId = FirstRequestpayWage.MerchantId,
                    NajaType = FirstRequestpayWage.NajaType,
                    TransactionType = FirstRequestpayWage.TransactionType,
                    WalletId = FirstRequestpayWage.WalletId,
                    NajaServiceData = FirstRequestpayWage.NajaServiceData,
                    Amount = FirstRequestpayWage.Amount,
                    PaymentStatus = payWageRequest.TransactionType == 1 ? PaymentStatus.FirstRequestPGW : PaymentStatus.FirstRequestWallet, //First Status Insert
                };

                var addnajaPay = await _najaServices.AddNajaWageAsync(najaPayDto);
                if (!addnajaPay)
                {
                    return Ok(new MessageModel<PayWageResponseViewModel<object>>
                    {
                        message = DescriptionUtility.GetDescription<ServiceStatus>("OperationUnSuccess"),
                        status = (int)ServiceStatus.OperationUnSuccess,
                        Data = null
                    });
                }
                FirstRequestpayWage.OrderId = OrderId;

                //insert into paywage
                var response = new ResponseBaseDto<PayWageResponseDto>();
                switch (payWageRequest.TransactionType)
                {
                    case 1:
                        response = await _najaServices.ShetabPayWageAsync(FirstRequestpayWage);
                        if (response != null && response.Status == 101)
                        {
                            var req = _mapper.Map<PayWageResponseViewModel<object>>(response.Data);
                            req.NajaData = null;
                            req.NajaType = FirstRequestpayWage.NajaType;
                            req.AmountWage = najaPayDto.Amount.Value;
                            req.TransactionDate = najaPayDto.BussinessDate.Value;
                            return Ok(new MessageModel<PayWageResponseViewModel<object>>
                            {
                                message = response.Message,
                                status = response.Status,
                                Data = req
                            });
                        }
                        else
                        {
                            return Ok(new MessageModel<PayWageResponseViewModel<object>>
                            {
                                message = response.Message,
                                status = (int)ServiceStatus.GetwayNotToken,
                                Data = null
                            });
                        }
                    case 2:
                        response = await _najaServices.WalletPayWageAsync(FirstRequestpayWage);
                        if (response.Status == 0)
                        {
                            var GetNajaWage = await _najaServices.GetNajaWageByExperssionAsync(n => n.OrderId == FirstRequestpayWage.OrderId &&
                                                                                   n.WalletReturnId == response.Data.Token);
                            var PaidNajaWage = GetNajaWage.FirstOrDefault();
                            if (PaidNajaWage != null && PaidNajaWage.Status == 0)
                            {
                                var req = _mapper.Map<PayWageResponseViewModel<object>>(response.Data);
                                req.NajaData = null;
                                req.NajaType = FirstRequestpayWage.NajaType;
                                req.AmountWage = najaPayDto.Amount.Value;
                                req.TransactionDate = najaPayDto.BussinessDate.Value;
                                return Ok(new MessageModel<PayWageResponseViewModel<object>>
                                {
                                    message = DescriptionUtility.GetDescription<ServiceStatus>("WalletSuccess"),
                                    status = (int)ServiceStatus.WalletSuccess,
                                    Data = req
                                });
                            }
                            else
                            {
                                return Ok(new MessageModel<PayWageResponseViewModel<object>>
                                {
                                    message = response.Message,
                                    status = (int)ServiceStatus.WalletUnSuccess,
                                    Data = null
                                });
                            }
                            #region Old_Comment
                            //var najaWage = GetNajaWage.FirstOrDefault();

                            //switch (najaWage.NajaType)
                            //{
                            //    #region [- Case 1 : ViolationInquiry -]
                            //    case 1:
                            //        var reqCar = JsonConvert.DeserializeObject<CarViolationInquiryObjectModel>(najaWage.NajaServiceData);
                            //        var violationInquiryRequest = _mapper.Map<ViolationInquiryRequestDto>(reqCar);
                            //        violationInquiryRequest.MobileNumber = najaWage.Mobile;
                            //        violationInquiryRequest.NationalCode = najaWage.NationalCode;
                            //        violationInquiryRequest.TrackingNo = najaWage.OrderId.Value;
                            //        var _callNajaCar = await _najaServices.ViolationInquiryAsync(violationInquiryRequest);
                            //        if (_callNajaCar != null && _callNajaCar.Status == 0)
                            //        {
                            //            var req = _mapper.Map<PayWageResponseViewModel<ViolationInquiryResponseDto>>(response.Data);
                            //            req.NajaData = _mapper.Map<ViolationInquiryResponseDto>(_callNajaCar.Data);
                            //            req.NajaType = Convert.ToInt32(najaWage.NajaType.Value);
                            //            req.AmountWage = najaWage.Amount.Value;
                            //            req.TransactionDate = najaWage.BussinessDate.Value;

                            //            return Ok(new MessageModel<PayWageResponseViewModel<ViolationInquiryResponseDto>>
                            //            {
                            //                message = response.Message,
                            //                status = (int)ServiceStatus.WalletSuccess,
                            //                Data = req
                            //            });
                            //        }
                            //        else
                            //        {
                            //            return Ok(new MessageModel<PayWageResponseViewModel<ViolationInquiryResponseDto>>
                            //            {
                            //                message = response.Message,
                            //                status = (int)ServiceStatus.WalletUnSuccess,
                            //                Data = null
                            //            });
                            //        }

                            //    #endregion

                            //    #region [- Case 2 : MotorViolationInquiry -]
                            //    case 2:
                            //        var reqMotor = JsonConvert.DeserializeObject<MotorViolationInquiryObjectModel>(najaWage.NajaServiceData);
                            //        var MotorviolationInquiryRequest = _mapper.Map<MotorViolationInquiryRequestDto>(reqMotor);
                            //        MotorviolationInquiryRequest.MobileNumber = najaWage.Mobile;
                            //        MotorviolationInquiryRequest.NationalCode = najaWage.NationalCode;
                            //        MotorviolationInquiryRequest.TrackingNo = najaWage.OrderId.Value;
                            //        var _callNajaMotor = await _najaServices.MotorViolationInquiryAsync(MotorviolationInquiryRequest);
                            //        if (_callNajaMotor != null && _callNajaMotor.Status == 0)
                            //        {
                            //            var req = _mapper.Map<PayWageResponseViewModel<MotorViolationInquiryResponseDto>>(response.Data);
                            //            req.NajaData = _callNajaMotor.Data;
                            //            req.NajaType = Convert.ToInt32(najaWage.NajaType.Value);
                            //            req.AmountWage = najaWage.Amount.Value;
                            //            req.TransactionDate = najaWage.BussinessDate.Value;
                            //            return Ok(new MessageModel<PayWageResponseViewModel<MotorViolationInquiryResponseDto>>
                            //            {
                            //                message = response.Message,
                            //                status = (int)ServiceStatus.WalletSuccess,
                            //                Data = req
                            //            });
                            //        }
                            //        else
                            //        {
                            //            return Ok(new MessageModel<PayWageResponseViewModel<MotorViolationInquiryResponseDto>>
                            //            {
                            //                message = response.Message,
                            //                status = (int)ServiceStatus.WalletUnSuccess,
                            //                Data = null
                            //            });
                            //        }
                            //    #endregion

                            //    #region [- Case 3 : PaymentExitInquiryInquiry -]
                            //    case 3:

                            //        return Ok(new MessageModel<PayWageResponseViewModel<PaymentExitInquiryResponseDto>>
                            //        {
                            //            message = DescriptionUtility.GetDescription<ServiceStatus>("NotActiveService"),
                            //            status = (int)ServiceStatus.NotActiveService,
                            //            Data = null
                            //        });

                            //    #endregion

                            //    #region [- Case 4 : LicenseNegativePointInquiry -]
                            //    case 4:
                            //        var reqLicenseNegativePoint = JsonConvert.DeserializeObject<LicenseNegativePointInquiryObjectModel>(najaWage.NajaServiceData);
                            //        var ViewModelLicenseNegativePointInquiry = _mapper.Map<LicenseNegativePointInquiryRequestViewModel>(reqLicenseNegativePoint);
                            //        ViewModelLicenseNegativePointInquiry.MobileNumber = najaWage.Mobile;
                            //        ViewModelLicenseNegativePointInquiry.NationalCode = najaWage.NationalCode;
                            //        ViewModelLicenseNegativePointInquiry.TrackingNo = najaWage.OrderId.Value;
                            //        var Dto = _mapper.Map<LicenseNegativePointInquiryRequestDto>(ViewModelLicenseNegativePointInquiry);

                            //        var _callLicenseNegativePointInquiry = await _najaServices.LicenseNegativePointInquiryAsync(Dto);
                            //        if (_callLicenseNegativePointInquiry != null && _callLicenseNegativePointInquiry.Status == 0)
                            //        {
                            //            var req = _mapper.Map<PayWageResponseViewModel<LicenseNegativePointInquiryResponseViewModel>>(response.Data);
                            //            req.NajaData = _mapper.Map<LicenseNegativePointInquiryResponseViewModel>(_callLicenseNegativePointInquiry.Data);
                            //            req.NajaType = Convert.ToInt32(najaWage.NajaType.Value);
                            //            req.AmountWage = najaWage.Amount.Value;
                            //            req.TransactionDate = najaWage.BussinessDate.Value;
                            //            return Ok(new MessageModel<PayWageResponseViewModel<LicenseNegativePointInquiryResponseViewModel>>
                            //            {
                            //                message = response.Message,
                            //                status = (int)ServiceStatus.WalletSuccess,
                            //                Data = req
                            //            });
                            //        }
                            //        else
                            //        {
                            //            return Ok(new MessageModel<PayWageResponseViewModel<LicenseNegativePointInquiryResponseViewModel>>
                            //            {
                            //                message = response.Message,
                            //                status = (int)ServiceStatus.WalletUnSuccess,
                            //                Data = null
                            //            });
                            //        }
                            //    #endregion

                            //    #region [- Case 5 : PassportStatusInquiry -]
                            //    case 5:
                            //        return Ok(new MessageModel<PayWageResponseViewModel<PaymentExitInquiryResponseDto>>
                            //        {
                            //            message = DescriptionUtility.GetDescription<ServiceStatus>("NotActiveService"),
                            //            status = (int)ServiceStatus.NotActiveService,
                            //            Data = null
                            //        });
                            //    //var PassportStatusInquiry = new PassportStatusInquiryRequestViewModel()
                            //    //{
                            //    //    MobileNumber = najaWage.Mobile,
                            //    //    NationalCode = najaWage.NationalCode,
                            //    //    TrackingNo = najaWage.OrderId.Value
                            //    //};
                            //    //var reqPassportStatusInquiry = _mapper.Map<PassportStatusInquiryRequestDto>(PassportStatusInquiry);

                            //    //var _callPassportStatusInquiry = await _najaServices.PassportStatusInquiry(reqPassportStatusInquiry);

                            //    //if (_callPassportStatusInquiry != null && _callPassportStatusInquiry.Status == 0)
                            //    //{
                            //    //    var req = _mapper.Map<PayWageResponseViewModel<PassportStatusInquiryResponseViewModel>>(response.Data);
                            //    //    req.NajaData = _mapper.Map<PassportStatusInquiryResponseViewModel>(_callPassportStatusInquiry.Data);
                            //    //    req.NajaType = Convert.ToInt32(najaWage.NajaType.Value);
                            //    //    req.AmountWage = najaWage.Amount.Value;
                            //    //    req.TransactionDate = najaWage.BussinessDate.Value;
                            //    //    return Ok(new MessageModel<PayWageResponseViewModel<PassportStatusInquiryResponseViewModel>>
                            //    //    {
                            //    //        message = response.Message,
                            //    //        status = (int)ServiceStatus.WalletSuccess,
                            //    //        Data = req
                            //    //    });
                            //    //}
                            //    //else
                            //    //{
                            //    //    return Ok(new MessageModel<PayWageResponseViewModel<PassportStatusInquiryResponseViewModel>>
                            //    //    {
                            //    //        message = response.Message,
                            //    //        status = (int)ServiceStatus.WalletUnSuccess,
                            //    //        Data = null
                            //    //    });
                            //    //}
                            //    #endregion

                            //    #region [- Case 6 : AccumulationViolationsInquiry -]
                            //    case 6:
                            //        var ReqCarPlate = JsonConvert.DeserializeObject<CarViolationInquiryObjectModel>(najaWage.NajaServiceData);
                            //        var AccumulationViolationsInquiryRequest = _mapper.Map<AccumulationViolationsInquiryRequestViewModel>(ReqCarPlate);
                            //        AccumulationViolationsInquiryRequest.MobileNumber = najaWage.Mobile;
                            //        AccumulationViolationsInquiryRequest.NationalCode = najaWage.NationalCode;
                            //        AccumulationViolationsInquiryRequest.TrackingNo = najaWage.OrderId.Value;

                            //        var reqAccumulationViolationsInquiry = _mapper.Map<AccumulationViolationsInquiryRequestDto>(AccumulationViolationsInquiryRequest);

                            //        var _callAccumulationViolationsInquiry = await _najaServices.AccumulationViolationsInquiryAsync(reqAccumulationViolationsInquiry);

                            //        if (_callAccumulationViolationsInquiry != null && _callAccumulationViolationsInquiry.Status == 0)
                            //        {
                            //            var req = _mapper.Map<PayWageResponseViewModel<AccumulationViolationsInquiryResponseViewModel>>(response.Data);
                            //            req.NajaData = _mapper.Map<AccumulationViolationsInquiryResponseViewModel>(_callAccumulationViolationsInquiry.Data);
                            //            req.NajaType = Convert.ToInt32(najaWage.NajaType.Value);
                            //            req.AmountWage = najaWage.Amount.Value;
                            //            req.TransactionDate = najaWage.BussinessDate.Value;
                            //            return Ok(new MessageModel<PayWageResponseViewModel<AccumulationViolationsInquiryResponseViewModel>>
                            //            {
                            //                message = response.Message,
                            //                status = (int)ServiceStatus.WalletSuccess,
                            //                Data = req
                            //            });
                            //        }
                            //        else
                            //        {
                            //            return Ok(new MessageModel<PayWageResponseViewModel<AccumulationViolationsInquiryResponseViewModel>>
                            //            {
                            //                message = response.Message,
                            //                status = (int)ServiceStatus.WalletUnSuccess,
                            //                Data = null
                            //            });
                            //        }
                            //    #endregion

                            //    #region [- Case 7 : ExitTaxesReceipt -]
                            //    case 7:
                            //        return Ok(new MessageModel<PayWageResponseViewModel<ExitTaxesReceiptResponseViewModel>>
                            //        {
                            //            message = DescriptionUtility.GetDescription<ServiceStatus>("NotActiveService"),
                            //            status = (int)ServiceStatus.NotActiveService,
                            //            Data = null
                            //        });


                            //    #endregion

                            //    #region [- Case 8 : LicenseStatusInquiry -]
                            //    case 8:
                            //        var LicenseStatusInquiry = new LicenseStatusInquiryRequestViewModel()
                            //        {
                            //            MobileNumber = najaWage.Mobile,
                            //            NationalCode = najaWage.NationalCode
                            //        };
                            //        var reqLicenseStatusInquiry = _mapper.Map<LicenseStatusInquiryRequestDto>(LicenseStatusInquiry);

                            //        var _callLicenseStatusInquiry = await _najaServices.LicenseStatusInquiryAsync(reqLicenseStatusInquiry);

                            //        if (_callLicenseStatusInquiry != null && _callLicenseStatusInquiry.Status == 0)
                            //        {
                            //            var req = _mapper.Map<PayWageResponseViewModel<LicenseStatusInquiryResponseViewModel>>(response.Data);
                            //            req.NajaData = _mapper.Map<LicenseStatusInquiryResponseViewModel>(_callLicenseStatusInquiry.Data);
                            //            req.NajaType = Convert.ToInt32(najaWage.NajaType.Value);
                            //            req.AmountWage = najaWage.Amount.Value;
                            //            req.TransactionDate = najaWage.BussinessDate.Value;
                            //            return Ok(new MessageModel<PayWageResponseViewModel<LicenseStatusInquiryResponseViewModel>>
                            //            {
                            //                message = response.Message,
                            //                status = (int)ServiceStatus.WalletSuccess,
                            //                Data = req
                            //            });
                            //        }
                            //        else
                            //        {
                            //            return Ok(new MessageModel<PayWageResponseViewModel<LicenseStatusInquiryResponseViewModel>>
                            //            {
                            //                message = response.Message,
                            //                status = (int)ServiceStatus.WalletUnSuccess,
                            //                Data = null
                            //            });
                            //        }
                            //    #endregion

                            //    #region [- Case 9 : ActivePlakInquiry -]
                            //    case 9:

                            //var ActivePlakInquiry = new ActivePlakInquiryRequestViewModel()
                            //{
                            //    MobileNumber = najaWage.Mobile,
                            //    NationalCode = najaWage.NationalCode
                            //};
                            //var reqActivePlakInquiry = _mapper.Map<ActivePlakInquiryRequestDto>(ActivePlakInquiry);

                            //var _callActivePlakInquiry = await _najaServices.ActivePlakInquiryAsync(reqActivePlakInquiry);

                            //if (_callActivePlakInquiry != null && _callActivePlakInquiry.Status == 0)
                            //{

                            //    PayWageResponseViewModel<ListActivePlakInquiryResponseViewModel> req = null;
                            //    ListActivePlakInquiryResponseViewModel listActivePlak = new ListActivePlakInquiryResponseViewModel();
                            //    var list = _mapper.Map<List<ActivePlakInquiryResponseViewModel>>(_callActivePlakInquiry.Data);
                            //    listActivePlak.ActivePlateList.AddRange(list);

                            //    req = new PayWageResponseViewModel<ListActivePlakInquiryResponseViewModel>
                            //    {
                            //        NajaData = listActivePlak,
                            //        NajaType = Convert.ToInt32(najaWage.NajaType.Value),
                            //        AmountWage = najaWage.Amount.Value,
                            //        TransactionDate = najaWage.BussinessDate.Value,
                            //        Token = najaWage.PgwToken ?? najaWage.WalletReturnId.Value,
                            //        OrderId = najaWage.OrderId.Value.ToString(),
                            //        Message = (najaWage.Status == 2) ? "کارمزد از کیف پول پرداخت شده است" : "کارمزد از طریق درگاه پرداخت شده است",
                            //    };

                            //    return Ok(new MessageModel<PayWageResponseViewModel<ListActivePlakInquiryResponseViewModel>>
                            //    {
                            //        message = response.Message,
                            //        status = (int)ServiceStatus.WalletSuccess,
                            //        Data = req
                            //    });
                            //}
                            //else
                            //{
                            //    return Ok(new MessageModel<PayWageResponseViewModel<List<ActivePlakInquiryResponseViewModel>>>
                            //    {
                            //        message = response.Message,
                            //        status = (int)ServiceStatus.WalletUnSuccess,
                            //        Data = null
                            //    });
                            //}
                            //#endregion

                            //#region [- Case 10 : NoExitInquiry -]
                            //case 10:
                            //    return Ok(new MessageModel<PayWageResponseViewModel<PaymentExitInquiryResponseDto>>
                            //    {
                            //        message = DescriptionUtility.GetDescription<ServiceStatus>("NotActiveService"),
                            //        status = (int)ServiceStatus.NotActiveService,
                            //        Data = null
                            //    });
                            ////var NoExitInquiry = new NoExitInquiryRequestViewModel()
                            ////{
                            ////    MobileNumber = najaWage.Mobile,
                            ////    NationalCode = najaWage.NationalCode
                            ////};
                            ////var reqNoExitInquiry = _mapper.Map<NoExitInquiryRequestDto>(NoExitInquiry);

                            ////var _callNoExitInquiry = await _najaServices.NoExitInquiry(reqNoExitInquiry);

                            ////if (_callNoExitInquiry != null && _callNoExitInquiry.Status == 0)
                            ////{
                            ////    var req = _mapper.Map<PayWageResponseViewModel<NoExitInquiryResponseViewModel>>(response.Data);
                            ////    req.NajaData = _mapper.Map<NoExitInquiryResponseViewModel>(_callNoExitInquiry.Data);
                            ////    req.NajaType = Convert.ToInt32(najaWage.NajaType.Value);
                            ////    req.AmountWage = najaWage.Amount.Value;
                            ////    req.TransactionDate = najaWage.BussinessDate.Value;
                            ////    return Ok(new MessageModel<PayWageResponseViewModel<NoExitInquiryResponseViewModel>>
                            ////    {
                            ////        message = response.Message,
                            ////        status = (int)ServiceStatus.WalletSuccess,
                            ////        Data = req
                            ////    });
                            ////}
                            ////else
                            ////{
                            ////    return Ok(new MessageModel<PayWageResponseViewModel<NoExitInquiryResponseViewModel>>
                            ////    {
                            ////        message = response.Message,
                            ////        status = (int)ServiceStatus.WalletUnSuccess,
                            ////        Data = null
                            ////    });
                            ////}
                            //#endregion

                            //#region [- Case 11 : ViolationImageInquiry -]
                            //case 11:

                            //    var reqViolationImageInquiry = JsonConvert.DeserializeObject<ViolationImageInquiryRequestViewModel>(najaWage.NajaServiceData);
                            //    var ViolationImageInquiryRequest = _mapper.Map<ViolationImageInquiryRequestDto>(reqViolationImageInquiry);
                            //    ViolationImageInquiryRequest.MobileNumber = najaWage.Mobile;
                            //    ViolationImageInquiryRequest.NationalCode = najaWage.NationalCode;
                            //    ViolationImageInquiryRequest.TrackingNo = najaWage.OrderId.Value;

                            //    var _callViolationImageInquiry = await _najaServices.ViolationImageInquiryAsync(ViolationImageInquiryRequest);

                            //    if (_callViolationImageInquiry != null && _callViolationImageInquiry.Status == 0)
                            //    {
                            //        var req = _mapper.Map<PayWageResponseViewModel<ViolationImageInquiryResponseViewModel>>(response.Data);
                            //        req.NajaData = _mapper.Map<ViolationImageInquiryResponseViewModel>(_callViolationImageInquiry.Data);
                            //        req.NajaType = Convert.ToInt32(najaWage.NajaType.Value);
                            //        req.AmountWage = najaWage.Amount.Value;
                            //        req.TransactionDate = najaWage.BussinessDate.Value;
                            //        return Ok(new MessageModel<PayWageResponseViewModel<ViolationImageInquiryResponseViewModel>>
                            //        {
                            //            message = response.Message,
                            //            status = (int)ServiceStatus.WalletSuccess,
                            //            Data = req
                            //        });
                            //    }
                            //    else
                            //    {
                            //        return Ok(new MessageModel<PayWageResponseViewModel<ViolationImageInquiryResponseViewModel>>
                            //        {
                            //            message = response.Message,
                            //            status = (int)ServiceStatus.WalletUnSuccess,
                            //            Data = null
                            //        });
                            //    }
                            //#endregion

                            //default:
                            //    return Ok(new MessageModel<PayWageResponseViewModel<object>>
                            //    {
                            //        message = response.Message,
                            //        status = response.Status,
                            //        Data = null
                            //    });
                        }
                        //    } 
                        #endregion
                        break;
                    default:
                        return Ok(new MessageModel
                        {
                            message = "نوع تراکنش را مشخص نمایید",
                            status = -1,
                        });
                }
                return Ok(new MessageModel<PayWageResponseViewModel<object>>
                {
                    message = response.Message,
                    status = response.Status,
                    Data = null
                });
            }
            catch (Exception ex)
            {

                return Ok(new MessageModel<PayWageResponseViewModel<object>>
                {
                    message = JsonConvert.SerializeObject(ex),
                    status = (int)ServiceStatus.OperationFaild,
                    Data = null
                });
                //return Ok(new MessageModel<PayWageResponseViewModel<object>>
                //{
                //    message = DescriptionUtility.GetDescription<ServiceStatus>("OperationFaild"),
                //    status = (int)ServiceStatus.OperationFaild,
                //    Data = null
                //});
            }
        }



        #region [Old Code For Auth]

        //private int SetMemory(object key, int value)
        //{
        //    try
        //    {
        //        var ratecallCode = (int)_memoryCache.Set(key, value);
        //        return ratecallCode;
        //    }
        //    catch (Exception)
        //    {
        //        return 0;
        //    }

        //}


        //private int GetMemory(object key)
        //{
        //    try
        //    {
        //        var ratecallCode = (int)_memoryCache.Get(key);
        //        return ratecallCode;
        //    }
        //    catch (NullReferenceException)
        //    {
        //        return -1;
        //    }
        //}


        ///// <summary>
        ///// دریافت احراز هویت ناجا
        ///// </summary>
        ///// <param name="MobileNumber"></param>
        ///// <param name="NationalCode"></param>
        ///// <returns></returns>
        //[HttpPost("CheckAuth")]
        //[LoggingAspect]
        //public async Task<IActionResult> CheckAuth(CheckInfoRequestViewModel input)
        //{
        //    #region CheckUser
        //    var userId = User.GetUserId();
        //    if (userId == 0)
        //    {
        //        return Ok(new MessageModel
        //        {
        //            message = DescriptionUtility.GetDescription<ServiceStatus>("NoUserFound"),
        //            status = (int)ServiceStatus.NoUserFound
        //        });
        //    }
        //    #endregion

        //    var Dto = _mapper.Map<CheckInfoRequestDto>(input);
        //    var response = await _najaServices.CheckInfo(Dto);
        //    if (response.Status == 0)
        //    {
        //        var OrderId = Utility.Utility.GenerateNajaOrderID();
        //        var najaPayDto = new NajaWageDto
        //        {
        //            CreateDate = DateTime.Now,
        //            BussinessDate = DateTime.Now,
        //            Mobile = input.MobileNumber,
        //            NationalCode = input.NationalCode,
        //            OrderId = OrderId,
        //            UserId = userId,
        //            MobileEnabled = false,
        //            PGWStatus = PaymentStatus.FirstRequestPGW //First Insert
        //        };

        //        //var najaPay = _mapper.Map<NajaWage>(najaPayDto);
        //        var addnajaPay = _najaServices.AddNajaWage(najaPayDto);
        //        if (addnajaPay)
        //        {
        //            // call otp
        //            var authCode = Utility.Utility.GenerateAuthCode();
        //            var sendsms = await _smsService.SendSMS(new SMSRequestDto
        //            {
        //                MobileNo = input.MobileNumber,
        //                Content = Utility.Utility.AuthSMS(authCode)
        //            });

        //            if (sendsms > 0)
        //            {
        //                var najaPayDomainModel = _najaServices.GetNajaWageByOrderId(orderId: OrderId);
        //                najaPayDomainModel.BussinessDate = DateTime.Now;
        //                najaPayDomainModel.SmsTracker = sendsms;
        //                najaPayDomainModel.ConfirmCode = authCode;

        //                _najaServices.UpdateNajaWage(najaPayDomainModel);

        //                string EncOrderId = _encryptor.Encrypt(OrderId.ToString());
        //                //response.Data.OrderId = EncOrderId;
        //                //response.Data.EncData =
        //                _encryptor.Encrypt($"{najaPayDomainModel.NationalCode}:{najaPayDomainModel.Mobile}");

        //                SetMemory(OrderId, 5);

        //                return Ok(new NajaResponseViewModelGeneric<CheckInfoResponseDto>
        //                {
        //                    Data = response.Data,
        //                    Status = (int)ServiceStatus.SendConfirmCodeSuccess,
        //                    Message = DescriptionUtility.GetDescription<ServiceStatus>("SendConfirmCodeSuccess")
        //                });
        //            }
        //            else
        //            {
        //                return Ok(new NajaResponseViewModelGeneric<CheckInfoResponseDto>
        //                {
        //                    Data = response.Data,
        //                    Status = (int)ServiceStatus.SendConfirmCodeUnSuccess,
        //                    Message = DescriptionUtility.GetDescription<ServiceStatus>("SendConfirmCodeUnSuccess")
        //                });
        //            }
        //        }
        //        else
        //        {
        //            //اطلاعات درج نشده است
        //            return Ok(new NajaResponseViewModelGeneric<ViolationInquiryResponseDto>
        //            {
        //                Status = (int)ServiceStatus.OperationFaild,
        //                Data = null,
        //                Message = DescriptionUtility.GetDescription<ServiceStatus>("OperationFaild")
        //            });
        //        }
        //    }
        //    else
        //    {
        //        return Ok(new NajaResponseViewModelGeneric<CheckInfoResponseDto>
        //        {
        //            Data = response.Data,
        //            Status = response.Status,
        //            Message = response.Message
        //        });
        //    }
        //}
        ///// <summary>
        ///// تایید احراز هویت ناجا
        ///// </summary>
        ///// <param name="EncData"></param>
        ///// <param name="OrderId"></param>
        ///// <param name="Code"></param>
        ///// <returns></returns>
        //[HttpPost("ConfirmSMS")]
        //[LoggingAspect]
        //public IActionResult ConfirmSMS(ConfirmSMSRequestDto input)
        //{
        //    var ValidOrderId = long.TryParse(_encryptor.Decrypt(input.OrderId), out long OrderId);
        //    if (!ValidOrderId)
        //    {
        //        return Ok(new NajaResponseViewModelGeneric<object>
        //        {
        //            Data = null,
        //            Status = (int)ServiceStatus.OrderIdNotValid,
        //            Message = DescriptionUtility.GetDescription<ServiceStatus>("OrderIdNotValid")
        //        });
        //    }
        //    var ratecallCode = GetMemory(OrderId);
        //    if (ratecallCode == 1)
        //    {
        //        return Ok(new NajaResponseViewModelGeneric<object>
        //        {
        //            Data = null,
        //            Status = (int)ServiceStatus.ConfirmCodeMaximumLimited,
        //            Message = DescriptionUtility.GetDescription<ServiceStatus>("ConfirmCodeMaximumLimited")
        //        });
        //    }
        //    else if (ratecallCode == -1)
        //    {
        //        return Ok(new NajaResponseViewModelGeneric<object>
        //        {
        //            Data = null,
        //            Status = (int)ServiceStatus.RequestIsNotValid,
        //            Message = DescriptionUtility.GetDescription<ServiceStatus>("RequestIsNotValid")
        //        });
        //    }

        //    NajaWageDto najaPay = _najaServices.GetNajaWageByOrderId(OrderId);
        //    var _encData = _encryptor.Decrypt(input.EncData).Split(":");
        //    if (najaPay != null && input.Code == najaPay.ConfirmCode &&
        //        _encData[0].ToString() == najaPay.NationalCode &&
        //        _encData[1].ToString() == najaPay.Mobile)
        //    {
        //        najaPay.ConfirmCode = new Random().Next(1000, 9999);
        //        najaPay.MobileEnabled = true;
        //        _najaServices.UpdateNajaWage(najaPay);
        //        return Ok(new NajaResponseViewModelGeneric<object>
        //        {
        //            Data = new { input.OrderId },
        //            Status = (int)ServiceStatus.ConfirmCodeIsValid,
        //            Message = DescriptionUtility.GetDescription<ServiceStatus>("ConfirmCodeIsValid")
        //        });
        //    }
        //    else
        //    {

        //        SetMemory(OrderId, --ratecallCode);
        //        return Ok(new NajaResponseViewModelGeneric<object>
        //        {
        //            Data = null,
        //            Status = (int)ServiceStatus.ConfirmCodeUnValid,
        //            Message = DescriptionUtility.GetDescription<ServiceStatus>("ConfirmCodeUnValid")
        //        });
        //    }

        //}

        #endregion

    }
}
