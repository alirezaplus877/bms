using Application.Services;
using AutoMapper;
using Dto.Proxy.Request.Wallet;
using Dto.Proxy.Response.Wallet;
using Dto.Proxy.Wallet;
using Dto.repository;
using Dto.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using PecBMS.Helper.Identity;
using PecBMS.Model;
using PecBMS.ViewModel;
using PecBMS.ViewModel.Request;
using PecBMS.ViewModel.Response;
using Pigi.MDbLogging;
using System;
using System.Collections.Generic;
using System.IO;
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
    public class DashboardController : ControllerBase, ILoggable
    {
        #region Private Variables
        private readonly IServiceProvider _serviceProvider;
        private readonly IMapper _mapper;
        private readonly IWalletServices walletServices;
        private readonly IBillService billService;
        private readonly IMemoryCache _memoryCache;
        private readonly IPGWUserService _pgwUserService;

        #endregion

        #region ctor
        public DashboardController(IServiceProvider serviceProvider, IMapper mapper, IMemoryCache memoryCache, IPGWUserService pgwUserService)
        {
            _serviceProvider = serviceProvider;
            _mapper = mapper;
            billService = _serviceProvider.GetRequiredService<IBillService>();
            walletServices = _serviceProvider.GetRequiredService<IWalletServices>();
            _memoryCache = memoryCache;
            _pgwUserService = pgwUserService;
        }
        #endregion

        #region [ - Comment ActiveAutoBillPayment DeActive - ]
        ///// <summary>
        ///// ارسال پیامک برای قبض خودکار
        ///// </summary>
        ///// <param name="input"></param>
        ///// <returns></returns>
        //[HttpPost("SendSmsAutoBillPayment")]
        //[LoggingAspect]
        //public async Task<IActionResult> SendSmsAutoBillPayment(SendSmsAutoBillRequestViewModel input)
        //{
        //    var billDto = _mapper.Map<SendSmsAutoBillDto>(input);
        //    #region CheckUser
        //    var userId = User.GetUserId();
        //    if (userId == 0)
        //    {
        //        return Ok(new MessageModel
        //        {
        //            message = ExceptionExtensions.GetDescription((int)ServiceStatus.NoUserFound),
        //            status = (int)ServiceStatus.NoUserFound
        //        });
        //    }
        //    #endregion

        //    billDto.UserId = userId;
        //    var UserFromDb = await _pgwUserService.GetUserByIdAsync(userId);
        //    billDto.MobileNo = UserFromDb != null ? UserFromDb.MobileNo.Value.ToString() : userId.ToString();
        //    var response = await billService.SendSmsAutoBillPayment(billDto);
        //    if (response.Status != 0)
        //    {
        //        return Ok(new MessageModel
        //        {
        //            message = response.Message,
        //            status = response.Status,
        //        });
        //    }
        //    return Ok(new MessageModel
        //    {
        //        message = response.Message,
        //        status = 0,
        //    });
        //}

        ///// <summary>
        ///// حذف کردن قبض خودکار
        ///// </summary>
        ///// <param name="input"></param>
        ///// <returns></returns>
        //[HttpPost("ActiveAutoBillPayment")]
        //[LoggingAspect]
        //public async Task<IActionResult> ActiveAutoBillPayment(DeleteAutoBillRequestViewModel input)
        //{
        //    #region CheckUser
        //    var userId = User.GetUserId();
        //    if (userId == 0)
        //    {
        //        return Ok(new MessageModel
        //        {
        //            message = "کاربر یافت نشد",
        //            status = -1,
        //        });
        //    }
        //    #endregion

        //    var response = await billService.ActiveAutoBillPayment(input.UsersBillId);
        //    if (!response)
        //    {
        //        return Ok(new MessageModel
        //        {
        //            message = "قبض با این مشخصات وجود ندارد",
        //            status = -1,

        //        });
        //    }
        //    return Ok(new MessageModel
        //    {
        //        message = "قبض خودکار با موفقیت فعال شد",
        //        status = 0,

        //    });
        //}

        ///// <summary>
        ///// حذف کردن قبض خودکار
        ///// </summary>
        ///// <param name="input"></param>
        ///// <returns></returns>
        //[HttpPost("DeleteAutoBillPayment")]
        //[LoggingAspect]
        //public async Task<IActionResult> DeleteAutoBillPayment(DeleteAutoBillRequestViewModel input)
        //{
        //    #region CheckUser
        //    var userId = User.GetUserId();
        //    if (userId == 0)
        //    {
        //        return Ok(new MessageModel
        //        {
        //            message = "کاربر یافت نشد",
        //            status = -1,
        //        });
        //    }
        //    #endregion

        //    var response = await billService.DeleteAutoBillPayment(input.UsersBillId);
        //    if (!response)
        //    {
        //        return Ok(new MessageModel
        //        {
        //            message = "قبض با این مشخصات وجود ندارد",
        //            status = -1,

        //        });
        //    }
        //    return Ok(new MessageModel
        //    {
        //        message = "قبض با موفقیت غیر فعال شد",
        //        status = 0,

        //    });
        //} 
        #endregion




        /// <summary>
        /// بروزرسانی قبض برای کاربر
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        [HttpPost("UpdateUsersBill")]
        [LoggingAspect]
        public async Task<IActionResult> UpdateUsersBill(AddUserBillRequestViewModel input)
        {
            var billDto = _mapper.Map<UserBillDto>(input);
            #region CheckUser
            var userId = User.GetUserId();
            var clientId = User.GetClient_Id();
            if (userId == 0)
            {
                return Ok(new MessageModel
                {
                    message = ExceptionExtensions.GetDescription((int)ServiceStatus.NoUserFound),
                    status = (int)ServiceStatus.NoUserFound
                });
            }
            #endregion

            billDto.UserID = userId;
            billDto.ClientId = clientId;
            var response = await billService.UpdateUserBillByAutoPayment(billDto);
            if (response.Status != 0)
            {
                return Ok(new MessageModel
                {
                    message = response.Message,
                    status = response.Status,
                });
            }
            return Ok(new MessageModel
            {
                message = response.Message,
                status = 0,
            });
        }

        /// <summary>
        /// بررسی کد احراز هویت افزودن قبوض خودکار
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        [HttpPost("CheckConfirmCodeAutoBillPayment")]
        [LoggingAspect]
        public async Task<IActionResult> ConfirmCodeAutoBillPayment(ConfirmationAutoBillRequestViewModel input)
        {
            var dto = _mapper.Map<ConfirmationAutoBillPaymentDto>(input);
            #region CheckUser
            var userId = User.GetUserId();
            if (userId == 0)
            {
                return Ok(new MessageModel
                {
                    message = ExceptionExtensions.GetDescription((int)ServiceStatus.NoUserFound),
                    status = (int)ServiceStatus.NoUserFound
                });
            }
            #endregion

            dto.UserID = userId;
            var response = await billService.ConfirmCodeAutoBill(dto);
            if (response.Status != 0)
            {
                return Ok(new MessageModel
                {
                    message = response.Message,
                    status = response.Status,
                });
            }
            else
            {
                return Ok(new MessageModel
                {
                    message = response.Message,
                    status = 0,
                });
            }

        }

        /// <summary>
        /// اضافه کردن قبض برای کاربر
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        [HttpPost("AddUsersBill")]
        [LoggingAspect]
        public async Task<IActionResult> AddUsersBill(AddUserBillRequestViewModel input)
        {
            var billDto = _mapper.Map<UserBillDto>(input);
            #region CheckUser
            var userId = User.GetUserId();
            var clientId = User.GetClient_Id();
            if (userId == 0)
            {
                return Ok(new MessageModel
                {
                    message = ExceptionExtensions.GetDescription((int)ServiceStatus.NoUserFound),
                    status = (int)ServiceStatus.NoUserFound
                });
            }
            #endregion

            billDto.UserID = userId;
            billDto.ClientId = clientId;
            var UserFromDb = await _pgwUserService.GetUserByIdAsync(userId);
            billDto.MobileNo = UserFromDb != null ? UserFromDb.UserName : userId.ToString();
            var response = await billService.AddUserBillByAutoPayment(billDto);
            if (response.Status != 0)
            {
                return Ok(new MessageModel
                {
                    message = response.Message,
                    status = response.Status,
                });
            }
            return Ok(new MessageModel
            {
                message = response.Message,
                status = 0,
            });
        }
        /// <summary>
        /// فعال سازی قبض
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        [HttpPost("ActivateUsersBill")]
        [LoggingAspect]
        public async Task<IActionResult> ActivateUsersBill(DeleteUsersBillRequestViewModel input)
        {
            var billDto = _mapper.Map<UserBillDto>(input);

            #region CheckUser
            var userId = User.GetUserId();
            if (userId == 0)
            {
                return Ok(new MessageModel
                {
                    message = "کاربر یافت نشد",
                    status = -1,
                });
            }
            #endregion

            billDto.UserID = userId;
            var response = await billService.ActiveUserBill(billDto);
            if (response.Status != 0)
            {
                return Ok(new MessageModel
                {
                    message = response.Message,
                    status = response.Status,

                });
            }
            return Ok(new MessageModel
            {
                message = response.Message,
                status = 0,

            });
        }

        /// <summary>
        /// غیر فعال سازی قبض
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        [HttpPost("DeleteUsersBill")]
        [LoggingAspect]
        public async Task<IActionResult> DeleteUsersBill(DeleteUsersBillRequestViewModel input)
        {
            var billDto = _mapper.Map<UserBillDto>(input);

            #region CheckUser
            var userId = User.GetUserId();
            if (userId == 0)
            {
                return Ok(new MessageModel
                {
                    message = "کاربر یافت نشد",
                    status = -1,
                });
            }
            #endregion

            billDto.UserID = userId;
            var response = await billService.DeleteUserBill(billDto);
            if (response.Status != 0)
            {
                return Ok(new MessageModel
                {
                    message = response.Message,
                    status = response.Status,

                });
            }
            else
            {
                return Ok(new MessageModel
                {
                    message = "حذف قبض با موفقیت انجام شد",
                    status = 0,

                });
            }
        }
        /// <summary>
        /// گرفتن قبوض ثبت شده برای فرد
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetUserBill")]
        [LoggingAspect]
        public async Task<IActionResult> GetUserBill()
        {
            #region CheckUser
            var userId = User.GetUserId();
            if (userId == 0)
            {
                return Ok(new MessageModel
                {
                    message = "کاربر یافت نشد",
                    status = -1,
                });
            }
            #endregion
            var response = await billService.GetUserBill(userId);
            if (response.Count > 0)
            {
                var userBillViews = _mapper.Map<List<UserBillViewResponseModel>>(response);

                return Ok(new MessageModel<List<UserBillViewResponseModel>>
                {
                    message = "عملیات موفق",
                    status = 0,
                    Data = userBillViews
                });
            }
            return Ok(new MessageModel
            {
                message = "قبض برای کاربر یافت نشد",
                status = -1,

            });
        }
        /// <summary>
        /// گرفتن نوع قبض 
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetBillType")]
        [LoggingAspect]
        public async Task<IActionResult> GetBillType()
        {

            var response = await billService.GetBillType();
            if (response.Count > 0)
            {
                var billTypes = _mapper.Map<List<BillTypeViewModel>>(response);

                return Ok(new MessageModel<List<BillTypeViewModel>>
                {
                    message = "عملیات موفق",
                    status = 0,
                    Data = billTypes
                });
            }
            return Ok(new MessageModel
            {
                message = "اطلاعاتی یافت نشد",
                status = -1,

            });


        }
        /// <summary>
        /// گرفتن سازمان قبض 
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetBillOrganization")]
        [LoggingAspect]
        public async Task<IActionResult> GetBillOrganization()
        {
            var response = await billService.GetOrganization();
            if (response.Count > 0)
            {
                var billTypes = _mapper.Map<List<OrganizationViewModel>>(response);

                return Ok(new MessageModel<List<OrganizationViewModel>>
                {
                    message = "عملیات موفق",
                    status = 0,
                    Data = billTypes
                });
            }
            return Ok(new MessageModel
            {
                message = "اطلاعاتی یافت نشد",
                status = -1,

            });
        }
        /// <summary>
        /// دریافت کیف پول های کاربر
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetUserWallets")]
        [LoggingAspect]
        //[PecAuthorize("Route", "PecBMS/Bill/api/GetUserWallets")]
        public async Task<IActionResult> GetUserWallets()
        {
            try
            {
                #region CheckUser
                var userId = User.GetUserId();
                if (userId == 0)
                {
                    return Ok(new MessageModel
                    {
                        message = "کاربر یافت نشد",
                        status = -1,
                    });
                }
                #endregion

                var Userwallets = new List<GetCustomerWalletResponseViewModel>();

                #region getclaims

                var UserWallets = await walletServices.GetUserWallet(userId);
                List<WalletsInfo> walletsInfos = new List<WalletsInfo>();
                UserWallets.ForEach(w =>
                {
                    WalletsInfo walletsInfo = new WalletsInfo()
                    {
                        CorporationPIN = w.CorporationPIN,
                        WalletCode = w.WalletCode
                    };
                    walletsInfos.Add(walletsInfo);
                });
                #endregion
                if (walletsInfos.Count > 0)
                {
                    var Walletinfoinput = _mapper.Map<List<WalletsInfo>, List<GetCustomerWalletRequestDto>>(walletsInfos);
                    var walletData = await walletServices.GetCustomerWallet(Walletinfoinput);
                    var merchantWallets = _mapper.Map<List<GetCustomerWalletResponseDto>, List<GetCustomerWalletResponseViewModel>>(walletData);
                    if (merchantWallets.Count > 0)
                    {
                        foreach (var item in merchantWallets)
                        {
                            Userwallets.Add(item);
                        }
                        return Ok(new MessageModel<List<GetCustomerWalletResponseViewModel>>
                        {
                            message = "عملیات با موفقیت انجام شد",
                            status = 0,
                            Data = Userwallets

                        });
                    }
                    return Ok(new MessageModel<List<GetCustomerWalletResponseViewModel>>
                    {
                        message = "کیف پول برای کاربر یافت نشد",
                        status = -1,
                        Data = null
                    });
                }
                else
                {
                    return Ok(new MessageModel<List<GetCustomerWalletResponseViewModel>>
                    {
                        message = "خطا در دریافت اطلاعات کاربر",
                        status = -1,
                        Data = null

                    });
                }

            }
            catch (Exception ex)
            {
                string message = ex.Message;
                return Ok(new MessageModel
                {
                    message = "خطا در انجام عملیات",
                    status = -1,
                });
            }
        }
        /// <summary>
        /// دریافت موجودی کیف پول
        /// </summary>
        /// <param name="walletBalance"></param>
        /// <returns></returns>
        [HttpPost("GetWalletBalance")]
        [LoggingAspect]
        public async Task<IActionResult> GetWalletBalance(WalletBalanceRequestViewModel walletBalance)
        {
            try
            {
                var walleBallance = _mapper.Map<WalletBalanceRequestDto>(walletBalance);
                var walletData = await walletServices.GetWalletBalance(walleBallance);
                var walletBalanceResponse = _mapper.Map<WalletBalanceResponseViewModel>(walletData);
                if (walletData.ResultId != 0)
                {
                    return Ok(new MessageModel<WalletBalanceResponseViewModel>
                    {
                        message = walletData.ResultDesc,
                        status = walletData.ResultId,
                        Data = walletBalanceResponse

                    });
                }
                else
                {
                    return Ok(new MessageModel<WalletBalanceResponseViewModel>
                    {
                        message = "عملیات با موفقیت انجام شد",
                        status = 0,
                        Data = walletBalanceResponse

                    });
                }

            }
            catch (Exception ex)
            {
                return Ok(new MessageModel
                {
                    message = "خطا در انجام عملیات",
                    status = -1,
                });
            }
        }
        [AllowAnonymous]
        [HttpGet("GetCaptcha")]
        public IActionResult GetCaptcha()
        {
            try
            {
                var createCaptchaResponse = GenerateCaptcha();
                return Ok(new MessageModel<CreateCaptchaResponse>
                {
                    message = "عملیات موفق",
                    status = 0,
                    Data = createCaptchaResponse
                });
            }
            catch (Exception)
            {
                return Ok(new MessageModel<CreateCaptchaResponse>
                {
                    message = "خطا",
                    status = -1,
                    Data = null
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
    }
}
