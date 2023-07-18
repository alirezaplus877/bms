using Application.Services;
using AutoMapper;
using Dto;
using Dto.Proxy.Request;
using Dto.Proxy.Request.Wallet;
using Dto.Proxy.Response;
using Dto.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    public class TransactionController : ControllerBase, ILoggable
    {
        #region Private Variables
        private readonly IServiceProvider _serviceProvider;
        private readonly IMapper _mapper;
        private readonly IWalletServices _walletServices;
        private readonly IPayServices _payServices;
        private readonly IPecBmsSetting _setting;
        private readonly IBillService _billService;
        private IMdbLogger<TransactionController> _logger;
        #endregion

        #region ctor
        public TransactionController(IServiceProvider serviceProvider, IMdbLogger<TransactionController> logger, IMapper mapper, IPecBmsSetting setting)
        {
            _serviceProvider = serviceProvider;
            _mapper = mapper;
            _walletServices = _serviceProvider.GetRequiredService<IWalletServices>();
            _payServices = _serviceProvider.GetRequiredService<IPayServices>();
            _billService = _serviceProvider.GetRequiredService<IBillService>();
            _logger = logger;
            _setting = setting;
        }
        #endregion

        /// <summary>
        /// دریافت توکن شارژ کیف پول
        /// </summary>
        /// <param name="chargeIpgTokenRequest"></param>
        /// <returns></returns>

        [HttpPost("ChargeWallet")]
        [LoggingAspect]
        //[Authorize]
        public async Task<IActionResult> ChargeWallet(GetChargeIpgTokenRequestViewModel chargeIpgTokenRequest)
        {
            var userId = User.GetUserId();
            #region checkIsOwnerWallet
            var userWallets = await _walletServices.GetUserWallet(userId);
            //تست
            var isOwner = userWallets.Any(p => p.WalletCode == chargeIpgTokenRequest.WalletCode);
            //var isOwnerWallet = User.IsWalletOwner(chargeIpgTokenRequest.WalletCode.ToString());
            if (!isOwner)
            {
                return Ok(new MessageModel
                {
                    message = "کیف پول اعلام شده برای کاربر نمی باشد",
                    status = -1,
                });
            }
            #endregion
            #region CheckUser
           
            if (userId == 0)
            {
                return Ok(new MessageModel
                {
                    message = "کاربر یافت نشد",
                    status = -1,
                });
            }
            #endregion

            var walletRequestDtos = _mapper.Map<ChargeWalletTokenRequestDto>(chargeIpgTokenRequest);
            walletRequestDtos.UserId = userId;
            var chargIpgToken = await _walletServices.ChargIpgToken(walletRequestDtos);
            var getTokenResponse = _mapper.Map<GetChargeIpgTokenResponseViewModel>(chargIpgToken);
            if (getTokenResponse.token == 0)
            {
                return Ok(new MessageModel
                {
                    message = getTokenResponse.message,
                    status = -1

                });

            }
            return Ok(new MessageModel<GetChargeIpgTokenResponseViewModel>
            {
                message = getTokenResponse.message,
                status = 0,
                Data = getTokenResponse
            });
        }
        [HttpPost("PayBill")]
        [LoggingAspect]
        [AllowAnonymous]
        public async Task<IActionResult> PayBill(PayBillRequestViewModel billPaymentToken)
        {
            try
            {
                #region CheckUser
                var userId = User.GetUserId();
                //if (userId == 0)
                //{
                //    return Ok(new MessageModel
                //    {
                //        message = "کاربر یافت نشد",
                //        status = -1,
                //    });
                //}
                #endregion
                #region checkIsOwnerWallet
                if (billPaymentToken.TransactionType == 2)
                {
                    var userWallets = await _walletServices.GetUserWallet(userId);
                    //تست
                    var isOwner = userWallets.Any(p => p.WalletCode == billPaymentToken.WalletCode);
                    //var isOwnerWallet = User.IsWalletOwner(billPaymentToken.WalletCode.ToString());
                    if (!isOwner)
                    {
                        return Ok(new MessageModel
                        {
                            message = "کیف پول اعلام شده برای کاربر نمی باشد",
                            status = -1,
                        });
                    }
                }
                #endregion
                var payBillRequest = _mapper.Map<PayBillRequestDto>(billPaymentToken);
                payBillRequest.UserId = userId;
                var response = new ResponseBaseDto<PayBillResposneDto>();
                switch (billPaymentToken.TransactionType)
                {
                    case 1:
                        if (payBillRequest.Bills.Count > 1)
                        {
                            return Ok(new MessageModel
                            {
                                message = "نوع تراکنش اشتباه است",
                                status = -1,

                            });
                        }
                        response = await _payServices.ShetabPayBill(payBillRequest);
                        break;
                    case 2:
                        response = await _payServices.WalletPayBill(payBillRequest);
                        break;
                    case 3:
                        response = await _payServices.ParrentWalletPayBill(payBillRequest);
                        break;
                    default:
                        return Ok(new MessageModel
                        {
                            message = "نوع تراکنش را مشخص نمایید",
                            status = -1,

                        });
                }
                var result = _mapper.Map<PayBillResponseViewModel>(response.Data);
                return Ok(new MessageModel<PayBillResponseViewModel>
                {
                    message = response.Message,
                    status = response.Status,
                    Data = result
                });
            }
            catch (Exception ex)
            {

                return Ok(new MessageModel<PayBillResponseViewModel>
                {
                    message = "خطا در عملیات",
                    status = -99,
                    Data = null
                });
            }
        }
        [HttpPost("ConfirmCharge")]
        [LoggingAspect]
        [AllowAnonymous]
        public async Task<IActionResult> ConfirmCharge(string mid)
        {
            try
            {
                var model = new ConfirmChargRequestDto();
                var status = short.Parse(Request.Form["status"]);
                model.PgwToken = long.Parse(Request.Form["Token"]);
                model.OrderId = long.Parse(Request.Form["OrderId"]);
                model.TransDate = DateTime.Now.ToString();
                model.MiladiTransDate = DateTime.Now.ToString();
                model.PgwStatus = short.Parse(Request.Form["status"]);
                var urlReceipt = "";
                if ((string.IsNullOrEmpty(mid)) || mid == "0")
                {
                    urlReceipt = _setting.ReceiptChargeUrl + model.OrderId;
                }
                else
                {
                    urlReceipt = _setting.ReceiptChargeUrl + model.OrderId + "?mid=" + mid;
                }

                if (status != 0)
                {
                    return Redirect(urlReceipt); 
                }
                model.TerminalNumber = Request.Form["TerminalNo"];
                model.RRN = Convert.ToInt64(Request.Form["RRN"]);
                var amount = Request.Form["Amount"].ToString().Replace(",", "");
                model.Amount = Convert.ToInt64(amount);
                var confrimCharge = await _walletServices.ConfirmCharge(model);
                return Redirect(urlReceipt);
            }
            catch (Exception ex)
            {
                _logger.Log(6, mid, $"📥 get error when confirm charge {JsonConvert.SerializeObject(ex)}'", null);
                return Redirect(null);
            }

        }
        //ipg confirm
        [HttpPost("ConfirmPayBill")]
        [AllowAnonymous]
        [LoggingAspect]
        public async Task<IActionResult> ConfirmPayBill(string mid)
        {
            try
            {
                var model = new ConfrimBillRequestDto();
                var status = short.Parse(Request.Form["status"]);
                model.Token = long.Parse(Request.Form["Token"]);
                model.OrderId = long.Parse(Request.Form["OrderId"]);
                model.TransDate = DateTime.Now.ToString();
                model.MiladiTransDate = DateTime.Now.ToString();
                model.PgwStatus = short.Parse(Request.Form["status"]);
                
                var urlReceipt = "";
                if ((string.IsNullOrEmpty(mid)) || mid == "0")
                {
                    urlReceipt = _setting.ReceiptChargeUrl + model.OrderId;
                }
                else
                {
                    urlReceipt = _setting.ReceiptChargeUrl + model.OrderId + "?mid=" + mid;
                }
               
                if (status != 0)
                {
                    await _billService.ConfirmBillPayment(model);
                    return Redirect(urlReceipt);
                }

                model.TerminalNumber = Request.Form["TerminalNo"];
                model.RRN = Convert.ToInt64(Request.Form["RRN"]);
                var amount = Request.Form["Amount"].ToString().Replace(",", "");
                model.Amount = Convert.ToInt64(amount);
                var confrim = await _billService.ConfirmBillPayment(model);
                return Redirect(urlReceipt);
            }
            catch (Exception ex)
            {
                _logger.Log(4, $"📥 get error when ConfirmPayBill {JsonConvert.SerializeObject(ex)}", JsonConvert.SerializeObject(ex), null);
                throw;
            }
        }
        /// <summary>
        /// تراکنش پرداخت قبض گروهی شتابی(کیف پول مادر)
        /// </summary>
        /// <param name="mid"></param>
        /// <returns></returns>
        [AllowAnonymous]
        [HttpPost("ConfirmSalePayBillWallet")]
        //[LoggingAspect]
        public async Task<IActionResult> ConfirmSalePayBillWallet(string mid)
        {
            try
            {
                var model = new ConfirmChargRequestDto();
                var status = short.Parse(Request.Form["status"]);
                model.PgwToken = long.Parse(Request.Form["Token"]);
                model.OrderId = long.Parse(Request.Form["OrderId"]);
                model.RRN = Convert.ToInt64(Request.Form["RRN"]);
                model.PgwStatus = short.Parse(Request.Form["status"]);
                var urlReceipt = "";
                if ((string.IsNullOrEmpty(mid)) || mid == "0")
                {
                    urlReceipt = _setting.ReceiptChargeUrl + model.OrderId;
                }
                else
                {
                    urlReceipt = _setting.ReceiptChargeUrl + model.OrderId + "?mid=" + mid;
                }

                if (status != 0)
                {
                    var updateCancel = await _walletServices.ConfirmSalePayBillWallet(model);
                    return Redirect(urlReceipt);
                }
                var amount = Request.Form["Amount"].ToString().Replace(",", "");
                model.Amount = Convert.ToInt64(amount);
                model.TransDate = DateTime.Now.ToString();
                model.MiladiTransDate = DateTime.Now.ToString();

                var conrirmBillWallet = await _walletServices.ConfirmSalePayBillWallet(model);
                return Redirect(urlReceipt);
            }
            catch (Exception ex)
            {
                _logger.Log(129, "ConfirmSalePayBillWallet", JsonConvert.SerializeObject(ex), null);
                return Redirect(null);

            }
        }
        [HttpPost("GetPaymentDetail")]
        [LoggingAspect]
        public async Task<IActionResult> GetPaymentDetail(PaymentDetailRequestViewModel req)
        {

            var payDetailReq = _mapper.Map<PaymentDetailRequestDto>(req);
            var response = await _billService.getPaymentDetail(payDetailReq);

            if (response != null)
            {
                var detailResponse = _mapper.Map<PaymentDetailResponseViewModel>(response);
                return Ok(new MessageModel<PaymentDetailResponseViewModel>
                {
                    message = "عملیات موفق",
                    status = 0,
                    Data = detailResponse
                });

            }
            else
            {
                return Ok(new MessageModel<PaymentDetailResponseViewModel>
                {
                    message = "اطلاعاتی یافت نشد",
                    status = -1,
                    Data = null
                });
            }

        }
        [HttpPost("TollBillPayment")]
        [LoggingAspect]
        [AllowAnonymous]
        public async Task<IActionResult> TollBillPayment(TollBillPaymentRequestViewModel tollBillPayment)
        {
            var tollBillPaymentRequest = _mapper.Map<TollBillPaymentRequestDto>(tollBillPayment);
            var response = await _payServices.TollBillPayProcess(tollBillPaymentRequest);
            var resp = _mapper.Map<TollBillPaymentResponseViewModel>(response.Data);
            if (response.Status != 0)
            {
                return Ok(new MessageModel
                {
                    message = response.Message,
                    status = -1,
                });
            }
            return Ok(new MessageModel<TollBillPaymentResponseViewModel>
            {
                message = response.Message,
                status = 0,
                Data = resp
            });
        }
        [HttpPost("TollBillConfirmPayment")]
        [LoggingAspect]
        [AllowAnonymous]
        public async Task<IActionResult> TollBillConfirmPayment(string mid)
        {
            try
            {
                var model = new ConfrimBillRequestDto();
                
                model.TransDate = DateTime.Now.ToString();
                model.MiladiTransDate = DateTime.Now.ToString();
                var status = short.Parse(Request.Form["status"]);
                model.Token = long.Parse(Request.Form["Token"]);
                model.OrderId = long.Parse(Request.Form["OrderId"]);
                model.RRN = Convert.ToInt64(Request.Form["RRN"]);
                model.PgwStatus = short.Parse(Request.Form["status"]);
                var urlReceipt = "";
                if ((string.IsNullOrEmpty(mid)) || mid == "0")
                {
                    urlReceipt = _setting.ReceiptTollBillUrl + model.OrderId;
                }
                else
                {
                    urlReceipt = _setting.ReceiptTollBillUrl + model.OrderId + "?mid=" + mid;
                }
                if (status != 0)
                {
                    await _billService.ConfirmTollBillPayment(model);
                    return Redirect(urlReceipt);
                }

                model.TerminalNumber = Request.Form["TerminalNo"];
                model.RRN = Convert.ToInt64(Request.Form["RRN"]);
                var amount = Request.Form["Amount"].ToString().Replace(",", "");
                model.Amount = Convert.ToInt64(amount);
                var confrim = await _billService.ConfirmTollBillPayment(model);
                return Redirect(urlReceipt);
            }
            catch (Exception)
            {
                return Redirect("");
            }

        }
        [HttpPost("BatchBillPayment")]
        [LoggingAspect]
        public async Task<IActionResult> BatchBillPayment(BillValidRequestViewModel billValid)
        {
            try
            {
                var billInfoRequest = _mapper.Map<BatchBillPaymentRequestDto>(billValid);
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
                #region getclaims
                var walletinfo = User.GetWallets();

                for (var i = 0; i < walletinfo.Count - 1; i++)
                {
                    ///دستی فعلا ولت اقای شفیعی
                    billInfoRequest.WalletId = 0;
                }
                #endregion
                billInfoRequest.UserId = userId;
                var response = await _billService.BatchBillPayment(billInfoRequest);
                if (response.Status != 0)
                {
                    return Ok(new MessageModel<BatchBillPaymentResponseViewModel>
                    {
                        message = response.Message,
                        status = response.Status,
                        Data = null
                    });
                }
                var billInfoResponse = _mapper.Map<BatchBillPaymentResponseViewModel>(response.Data);
                return Ok(new MessageModel<BatchBillPaymentResponseViewModel>
                {
                    message = response.Message,
                    status = response.Status,
                    Data = billInfoResponse
                });

            }
            catch (Exception)
            {

                return Ok(new MessageModel
                {
                    message = "خطای سرور",
                    status = -99,
                });
            }

        }
        [HttpPost("ConfrimBatchBillPayment")]
        [LoggingAspect]
        [AllowAnonymous]
        public async Task<IActionResult> ConfrimBatchBillPayment(string mid)
        {
            try
            {
                var model = new ConfrimBillRequestDto();
                var status = short.Parse(Request.Form["status"]);
                model.Token = long.Parse(Request.Form["Token"]);
                model.OrderId = long.Parse(Request.Form["OrderId"]);
                model.RRN = Convert.ToInt64(Request.Form["RRN"]);
                
                model.TransDate = DateTime.Now.ToString();
                model.MiladiTransDate = DateTime.Now.ToString();               
                if (status != 0)
                {
                    var response =await _billService.ConfirmBatchBillPayment(model);
                    var payDetailReq = _mapper.Map<ConfirmBatchBillPaymentResponseViewModel>(response.Data);
                    return Ok(new MessageModel<ConfirmBatchBillPaymentResponseViewModel>
                    {
                        message = response.Message,
                        status = response.Status,
                        Data = payDetailReq
                    });
                }
                var amount = Request.Form["Amount"].ToString().Replace(",", "");
                model.Amount = Convert.ToInt64(amount);
                var responseConfirm = await _billService.ConfirmBatchBillPayment(model);
                var confirmBatch = _mapper.Map<ConfirmBatchBillPaymentResponseViewModel>(responseConfirm.Data);
                return Ok(new MessageModel<ConfirmBatchBillPaymentResponseViewModel>
                {
                    message = responseConfirm.Message,
                    status = responseConfirm.Status,
                    Data = confirmBatch
                });
            }
            catch (Exception)
            {
                return Redirect("");
            }

        }

    }
}
