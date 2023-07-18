using Application.Services;
using AutoMapper;
using Barcoder.Code128;
using Barcoder.Renderer.Image;
using Dto.Pagination;
using Dto.Proxy.Request.Tosan;
using Dto.Proxy.Response;
using Dto.Proxy.Response.Naja;
using Dto.Proxy.Response.Tosan;
using Entities.Transportation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using PEC.CoreCommon.ExtensionMethods;
using PecBMS.Helper;
using PecBMS.Helper.Identity;
using PecBMS.Model;
using PecBMS.ViewModel.Request.Naja;
using PecBMS.ViewModel.Response.Naja;
using PecBMS.ViewModel.Transportation;
using PecBMS.ViewModel.Transportation.Request;
using PecBMS.ViewModel.Transportation.Response;
using Pigi.MDbLogging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utility;
using Utility.TosanSohaStatus;

namespace PecBMS.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TransportationController : ControllerBase, ILoggable
    {
        #region Private Variables
        private readonly IServiceProvider _serviceProvider;
        private readonly IMapper _mapper;
        private readonly IWalletServices _walletServices;
        private IMdbLogger<TransportationController> _logger;
        private readonly IPecBmsSetting _pecBmsSetting;
        private readonly ITosanSohaServices _tosanSohaServices;
        private readonly IConfiguration _config;

        #endregion

        public TransportationController(IServiceProvider serviceProvider, IWalletServices walletService, IMapper mapper, IMemoryCache memoryCache,
            IMdbLogger<TransportationController> logger, IPecBmsSetting pecBmsSetting, IConfiguration config, IWalletServices walletServices)
        {
            _serviceProvider = serviceProvider;
            _mapper = mapper;
            _tosanSohaServices = _serviceProvider.GetRequiredService<ITosanSohaServices>();
            _logger = logger;
            _pecBmsSetting = pecBmsSetting;
            _walletServices = walletServices;
            _config = config;
        }

        #region CardCharge

        [HttpGet("GetVoucherListCharge")]
        [LoggingAspect]
        public async Task<IActionResult> GetVoucherListCharge(string cardSerial, VoucherListRequestDto voucherList)
        {
            try
            {
                var report = await _tosanSohaServices.VoucherInfoList(voucherList, cardSerial);

                if (report != null)
                {
                    if (report.Items.Count != 0)
                    {
                        return Ok(new MessageModel<VoucherInfoListResponseDto>
                        {
                            Data = report,
                            message = DescriptionUtility.GetDescription<ServiceStatus>("Success"),
                            status = (int)ServiceStatus.Success
                        });
                    }
                    else
                    {
                        return Ok(new MessageModel<VoucherInfoListResponseDto>
                        {
                            Data = report,
                            message = DescriptionUtility.GetDescription<ServiceStatus>("NotDataFound"),
                            status = (int)ServiceStatus.NotDataFound
                        });
                    }
                }
                else
                {
                    return Ok(new MessageModel<VoucherInfoListResponseDto>
                    {
                        Data = report,
                        message = DescriptionUtility.GetDescription<ServiceStatus>("NotDataFound"),
                        status = (int)ServiceStatus.NotDataFound
                    });
                }
            }
            catch (Exception e)
            {

                return Ok(new MessageModel<VoucherInfoListResponseDto>
                {
                    Data = null,
                    message = DescriptionUtility.GetDescription<ServiceStatus>("NotDataFound"),
                    status = (int)ServiceStatus.NotDataFound
                });
            }
        }

        [HttpPost("GetBalanceCompanyCard")]
        [LoggingAspect]
        public async Task<IActionResult> GetBalanceCompanyCard()
        {
            try
            {
                #region Check user
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
                var response = await _tosanSohaServices.GetBalanceCompanyCard();

                return Ok(new MessageModel<int>
                {
                    message = response.resultMessage,
                    status = Convert.ToInt32(response.resultCode),
                    Data = response.Result
                });

            }
            catch (Exception ex)
            {

                return Ok(new MessageModel<TosanSohaResponseVM<object>>
                {
                    message = JsonConvert.SerializeObject(ex),
                    status = (int)ServiceStatus.OperationFaild,
                    Data = null
                });
            }
        }

        [HttpPost("InquiryChargeCardByOrderId")]
        [LoggingAspect]
        public async Task<IActionResult> InquiryChargeCardByOrderId(long reqOrderId)
        {
            _logger.Log(13681368, "InquiryChargeCardByOrderId =>" + JsonConvert.SerializeObject(reqOrderId), null);
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

                _logger.Log(13681368, " To InquiryChargeCardByOrderId =>" + userId + " = " + reqOrderId, null);
                var inquiry = await _tosanSohaServices.InquiryChargeCardByOrderIdAsync(reqOrderId, userId);

                _logger.Log(13681368, "from InquiryChargeCardByOrderId =>" + JsonConvert.SerializeObject(inquiry), null);
                if (inquiry.Status == 2 || inquiry.Status == 6)
                {
                    if (!inquiry.Data.RequestIsSent)
                    {
                        _logger.Log(13681368, " request is sent false =>" + inquiry.Data.OrderId.Value, null);
                        var result = await _tosanSohaServices.UpdateTicketCardInfoRequestAsync(inquiry.Data.OrderId.Value);//
                        _logger.Log(13681368, " from UpdateTicketCardInfoRequestAsync =>" + result, null);
                        if (result)
                        {
                            _logger.Log(13681368, " to CreateVoucherCardInquiryAsync =>" + JsonConvert.SerializeObject(inquiry), null);
                            var request = new CreateVoucherRequestDto
                            {
                                CardSerial = inquiry.Data.CardSerial.ToString(),
                                NationalCode = inquiry.Data.NationalCode.ToString(),
                                UinqueId = inquiry.Data.Id,
                                VoucherAmount = inquiry.Data.Amount.ToString(),
                            };
                            var _callTosanSoha = await _tosanSohaServices.CreateVoucher(request);
                            _logger.Log(13681368, " CreateCardVoucherAsync =>" + JsonConvert.SerializeObject(_callTosanSoha), null);
                            if (_callTosanSoha != null && _callTosanSoha.resultCode == ResultCodeStatus.Success)
                            {
                                CreateVoucherResponseDto req = null;

                                req = new CreateVoucherResponseDto
                                {
                                    Amount = inquiry.Data.Amount.Value,
                                    TransactionDate = inquiry.Data.BussinessDate.Value,
                                    Token = inquiry.Data.Token ?? inquiry.Data.WalletReturnId.Value,
                                    OrderId = inquiry.Data.OrderId.Value,
                                    resultMessage = (inquiry.Status == 2) ? "شارژکارت از کیف پول پرداخت شده است" : "شارژ کارت از طریق درگاه پرداخت شده است",
                                    resultCode = _callTosanSoha.resultCode,
                                    resultType = _callTosanSoha.resultType,
                                    voucherId = _callTosanSoha.voucherId,
                                    voucherExpireDate = _callTosanSoha.voucherExpireDate,
                                    uniqueId = _callTosanSoha.uniqueId,
                                };

                                return Ok(new MessageModel<CreateVoucherResponseDto>
                                {
                                    message = (inquiry.Status == 2) ? DescriptionUtility.GetDescription<ServiceStatus>("WalletSuccess") :
                                    DescriptionUtility.GetDescription<ServiceStatus>("GetwaySuccess"),
                                    status = (inquiry.Status == 2) ? (int)ServiceStatus.WalletSuccess : (int)ServiceStatus.GetwaySuccess,
                                    Data = req
                                });
                            }
                            else
                            {
                                CreateVoucherResponseDto req = null;

                                req = new CreateVoucherResponseDto
                                {
                                    Amount = inquiry.Data.Amount.Value,
                                    TransactionDate = inquiry.Data.BussinessDate.Value,
                                    Token = inquiry.Data.Token ?? inquiry.Data.WalletReturnId.Value,
                                    OrderId = inquiry.Data.OrderId.Value,
                                    resultMessage = (inquiry.Status == 2) ? "شارژکارت از کیف پول پرداخت شده است" : "شارژ کارت از طریق درگاه پرداخت شده است",
                                };
                                return Ok(new MessageModel<CreateVoucherResponseDto>
                                {
                                    message = _callTosanSoha.resultMessage,
                                    status = _callTosanSoha.resultCode,
                                    Data = req
                                });
                            }
                        }
                        else
                        {
                            CreateVoucherResponseDto req = null;

                            req = new CreateVoucherResponseDto
                            {
                                Amount = inquiry.Data.Amount.Value,
                                TransactionDate = inquiry.Data.BussinessDate.Value,
                                Token = inquiry.Data.Token ?? inquiry.Data.WalletReturnId.Value,
                                OrderId = inquiry.Data.OrderId.Value,
                                resultMessage = "خطا در سرویس استعلام - فلگ ارسال به api"
                            };

                            return Ok(new MessageModel<CreateVoucherResponseDto>
                            {
                                message = DescriptionUtility.GetDescription<ServiceStatus>("OperationUnSuccess"),
                                status = (int)ServiceStatus.OperationUnSuccess,
                                Data = req
                            });
                        }

                    }
                    else
                    {
                        _logger.Log(13681368, "  request is sent true =>" + inquiry.Data.OrderId.Value, null);
                        var GetInquiries = await _tosanSohaServices.GetTicketCardInfoByExperssionAsync(n => n.OrderId == reqOrderId);
                        var GetInquiry = GetInquiries.FirstOrDefault();
                        _logger.Log(13681368, "from GetInquiry =>" + JsonConvert.SerializeObject(GetInquiry), null);
                        if (GetInquiry != null && GetInquiry.Status == 0)
                        {
                            CreateVoucherResponseDto req = null;

                            req = new CreateVoucherResponseDto
                            {
                                Amount = inquiry.Data.Amount.Value,
                                TransactionDate = inquiry.Data.BussinessDate.Value,
                                Token = inquiry.Data.Token ?? inquiry.Data.WalletReturnId.Value,
                                OrderId = inquiry.Data.OrderId.Value,
                                voucherExpireDate = inquiry.Data.VoucherExpireDate,
                                resultCode = inquiry.Data.Status.Value,
                                uniqueId = inquiry.Data.Id,
                                voucherId = inquiry.Data.VoucherId,
                                resultMessage = (inquiry.Status == 2) ? "شارژکارت از کیف پول پرداخت شده است" : "شارژ کارت از طریق درگاه پرداخت شده است",
                            };
                            return Ok(new MessageModel<CreateVoucherResponseDto>
                            {
                                message = (inquiry.Status == 2) ? DescriptionUtility.GetDescription<ServiceStatus>("WalletSuccess") :
                                    DescriptionUtility.GetDescription<ServiceStatus>("GetwaySuccess"),
                                status = (inquiry.Status == 2) ? (int)ServiceStatus.WalletSuccess : (int)ServiceStatus.GetwaySuccess,
                                Data = req
                            });


                        }
                        else
                        {
                            CreateVoucherResponseDto req = null;

                            req = new CreateVoucherResponseDto
                            {
                                Amount = inquiry.Data.Amount.Value,
                                TransactionDate = inquiry.Data.BussinessDate.Value,
                                Token = inquiry.Data.Token ?? inquiry.Data.WalletReturnId.Value,
                                OrderId = inquiry.Data.OrderId.Value,
                                resultMessage = " خطا در سرویس استعلام - فلگ ارسال به api"
                            };

                            return Ok(new MessageModel<CreateVoucherResponseDto>
                            {
                                message = DescriptionUtility.GetDescription<ServiceStatus>("OperationUnSuccess"),
                                status = (int)ServiceStatus.OperationUnSuccess,
                                Data = req
                            });
                        }
                    }
                }
                else
                {
                    var req = new CreateVoucherResponseDto
                    {
                        Amount = inquiry.Data.Amount.Value,
                        TransactionDate = inquiry.Data.BussinessDate.Value,
                        Token = inquiry.Data.Token ?? inquiry.Data.WalletReturnId.Value,
                        OrderId = inquiry.Data.OrderId.Value,
                        resultMessage = inquiry.Message

                    };

                    return Ok(new MessageModel<CreateVoucherResponseDto>
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


        [HttpPost("ConfirmChargeCard")]
        [AllowAnonymous]
        public async Task<IActionResult> ConfirmChargeCard(string mid)
        {
            var model = new TicketCardInfo();

            try
            {
                var status = short.Parse(Request.Form["status"]);
                model.Token = long.Parse(Request.Form["Token"]);
                model.OrderId = long.Parse(Request.Form["OrderId"]);
                model.PaymentStatus = short.Parse(Request.Form["status"]);
                model.MaskCardNumber = Request.Form["HashCardNumber"];
                var urlReceipt = "";
                if (string.IsNullOrEmpty(mid) || mid == "0")
                {
                    urlReceipt = _config.GetSection("ReceiptPayChargeCard").Value + model.OrderId;
                }
                else
                {
                    urlReceipt = _config.GetSection("ReceiptPayChargeCard").Value + "?mid=" + mid;
                }

                if (status != 0)
                {
                    _logger.Log(806, $"📥 ConfirmChargeCard When Status != 0", JsonConvert.SerializeObject(model), null);

                    await _tosanSohaServices.ConfirmChargeCardAsync(model);
                    return Redirect(urlReceipt);
                }

                _logger.Log(813, $"📥 ConfirmChargeCard When Status == 0", JsonConvert.SerializeObject(model), null);
                model.RRN = Convert.ToInt64(Request.Form["RRN"]);
                var amount = Request.Form["Amount"].ToString().Replace(",", "");
                model.Amount = Convert.ToInt64(amount);
                await _tosanSohaServices.ConfirmChargeCardAsync(model);
                return Redirect(urlReceipt);
            }
            catch (Exception ex)
            {
                _logger.Log(822, $"📥 get error when ConfirmChargeCardAsync", JsonConvert.SerializeObject(model), ex);
                string urlReceipt;
                if (string.IsNullOrEmpty(mid) && mid == "0")
                {
                    urlReceipt = _config.GetSection("ReceiptPayChargeCard").Value + -100;
                }
                else
                {
                    urlReceipt = _config.GetSection("ReceiptPayChargeCard").Value + -100 + "?mid=" + mid;
                }
                return Redirect(urlReceipt);
            }
        }

        [HttpPost("PayCardCharge")]
        [LoggingAspect]
        public async Task<IActionResult> PayCardCharge(PayCardChargeReqVM payCardChargeReqVM)
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

                #region IsValidCard

                var isValidCard = await _tosanSohaServices.IsValidCardType(payCardChargeReqVM.CardSerial);
                if (!isValidCard.result)
                {
                    return Ok(new MessageModel
                    {
                        message = "کارت معتبر نمی باشد",
                        status = (int)ServiceStatus.OperationFaild
                    });
                }

                #endregion

                #region checkIsOwnerWallet
                if (payCardChargeReqVM.TransactionType != 1 && payCardChargeReqVM.TransactionType != 2)
                {
                    return Ok(new MessageModel
                    {
                        message = DescriptionUtility.GetDescription<ServiceStatus>("InValidTransactionType"),
                        status = (int)ServiceStatus.InValidTransactionType
                    });
                }
                //get user wallet
                else if (payCardChargeReqVM.TransactionType == 2)
                {
                    var userWallets = await _walletServices.GetUserWallet(userId);
                    //تست
                    var isOwner = userWallets.Any(p => p.WalletCode == payCardChargeReqVM.WalletCode);
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

                var FirstRequestpayCharge = _mapper.Map<PayTosanSohaRequestDto>(payCardChargeReqVM);
                FirstRequestpayCharge.UserId = userId;
                //generate orderId
                var OrderId = Utility.Utility.GenerateNajaOrderID();

                var TicketCardInfoDto = new TicketCardInfo
                {
                    CreateDate = DateTime.Now,
                    BussinessDate = DateTime.Now,
                    MobileNo = FirstRequestpayCharge.MobileNumber,
                    NationalCode = FirstRequestpayCharge.NationalCode,
                    OrderId = OrderId,
                    UserId = userId,
                    MerchantId = FirstRequestpayCharge.MerchantId,
                    TransactionType = FirstRequestpayCharge.TransactionType,
                    WalletId = FirstRequestpayCharge.WalletId,
                    Amount = FirstRequestpayCharge.Amount,
                    CardSerial = FirstRequestpayCharge.CardSerial,
                    PaymentStatus = payCardChargeReqVM.TransactionType == 1 ? PaymentStatus.FirstRequestPGW : PaymentStatus.FirstRequestWallet, //First Status Insert
                };

                var addPay = await _tosanSohaServices.AddTicketCardInfo(TicketCardInfoDto);
                if (!addPay)
                {
                    return Ok(new MessageModel<PayWageResponseViewModel<object>>
                    {
                        message = DescriptionUtility.GetDescription<ServiceStatus>("OperationUnSuccess"),
                        status = (int)ServiceStatus.OperationUnSuccess,
                        Data = null
                    });
                }
                FirstRequestpayCharge.OrderId = OrderId;

                //insert into pay
                var response = new ResponseBaseDto<PayWageResponseDto>();
                switch (payCardChargeReqVM.TransactionType)
                {
                    case 1:
                        response = await _tosanSohaServices.ShetabPayTicketCardAsync(FirstRequestpayCharge);
                        if (response != null && response.Status == 101)
                        {
                            var req = _mapper.Map<TosanSohaResponseVM<object>>(response.Data);
                            req.TosanSohaData = null;

                            req.Amount = payCardChargeReqVM.Amount;
                            req.TransactionDate = TicketCardInfoDto.BussinessDate.Value;
                            return Ok(new MessageModel<TosanSohaResponseVM<object>>
                            {
                                message = response.Message,
                                status = response.Status,
                                Data = req
                            });
                        }
                        else
                        {
                            return Ok(new MessageModel<TosanSohaResponseVM<object>>
                            {
                                message = response.Message,
                                status = (int)ServiceStatus.GetwayNotToken,
                                Data = null
                            });
                        }
                    case 2:
                        response = await _tosanSohaServices.WalletPayTicketCardAsync(FirstRequestpayCharge);
                        if (response.Status == 0)
                        {
                            var getChargeCard = await _tosanSohaServices.GetTicketCardInfoByExperssionAsync(n => n.OrderId == FirstRequestpayCharge.OrderId &&
                                                                                   n.WalletReturnId == response.Data.Token);
                            var PaidChargeCard = getChargeCard.FirstOrDefault();
                            if (PaidChargeCard != null && PaidChargeCard.Status == 0)
                            {
                                var req = _mapper.Map<TosanSohaResponseVM<object>>(response.Data);
                                req.TosanSohaData = null;

                                req.Amount = payCardChargeReqVM.Amount;
                                req.TransactionDate = TicketCardInfoDto.BussinessDate.Value;
                                return Ok(new MessageModel<TosanSohaResponseVM<object>>
                                {
                                    message = DescriptionUtility.GetDescription<ServiceStatus>("WalletSuccess"),
                                    status = (int)ServiceStatus.WalletSuccess,
                                    Data = req
                                });
                            }
                            else
                            {
                                return Ok(new MessageModel<TosanSohaResponseVM<object>>
                                {
                                    message = response.Message,
                                    status = (int)ServiceStatus.WalletUnSuccess,
                                    Data = null
                                });
                            }
                        }
                        break;
                    default:
                        return Ok(new MessageModel
                        {
                            message = "نوع تراکنش را مشخص نمایید",
                            status = -1,
                        });
                }
                return Ok(new MessageModel<TosanSohaResponseVM<object>>
                {
                    message = response.Message,
                    status = response.Status,
                    Data = null
                });
            }
            catch (Exception ex)
            {
                return Ok(new MessageModel<TosanSohaResponseVM<object>>
                {
                    message = JsonConvert.SerializeObject(ex),
                    status = (int)ServiceStatus.OperationFaild,
                    Data = null
                });
            }
        }
        #endregion

        #region SingleDirectTicket



        [HttpGet("GetPaginatedSingleTicket")]
        [LoggingAspect]
        public async Task<IActionResult> GetPaginatedSingleTicket([FromQuery] PaginationFilterViewModel paginationFilter)
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

            var report = await _tosanSohaServices.GetPaginatedSingleTicketAsync(paginationFilterDto);

            if (report != null)
            {
                if (report.Data.Count != 0)
                {
                    return Ok(new MessageModel<PagedResponse<List<SingleTicketResponsePaginated>>>
                    {
                        Data = report,
                        message = DescriptionUtility.GetDescription<ServiceStatus>("Success"),
                        status = (int)ServiceStatus.Success
                    });
                }
                else
                {
                    return Ok(new MessageModel<PagedResponse<List<SingleTicketResponsePaginated>>>
                    {
                        Data = report,
                        message = DescriptionUtility.GetDescription<ServiceStatus>("NotDataFound"),
                        status = (int)ServiceStatus.NotDataFound
                    });
                }
            }
            else
            {
                return Ok(new MessageModel<PagedResponse<List<SingleTicketResponsePaginated>>>
                {
                    Data = report,
                    message = DescriptionUtility.GetDescription<ServiceStatus>("NotDataFound"),
                    status = (int)ServiceStatus.NotDataFound
                });
            }
        }

        [HttpGet("GetSingleDirectBalance")]
        [LoggingAspect]
        public async Task<IActionResult> GetSingleDirectBalance()
        {
            try
            {
                #region Check user
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
                var clientUniqueId = "123456";
                var response = await _tosanSohaServices.ClientBalance(clientUniqueId);

                return Ok(new MessageModel<int>
                {
                    message = response.message,
                    status = Convert.ToInt32(response.resultCode),
                    Data = response.balance
                });
            }
            catch (Exception ex)
            {
                return Ok(new MessageModel
                {
                    message = JsonConvert.SerializeObject(ex),
                    status = (int)ServiceStatus.OperationFaild,
                    //Data = null
                });
            }
        }

        [HttpGet("GetAllSingleDirectVoucherHistory")]
        [LoggingAspect]
        public async Task<IActionResult> GetAllSingleDirectVoucherHistory()
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


            var result = await _tosanSohaServices.GetSingleDirectionTicketsAsync(userId);
            result = result.Where(t => t.Status == 0).OrderByDescending(o => o.Id).ToList();
            var listsingleTickets = new List<SingleTicketResponseDto>();

            foreach (var dto in result)
            {
                var ticket = new SingleTicketResponseDto
                {
                    ticketId = dto.serialNo.ToString(),
                    expiredate = dto.expireDate,
                    linkqr = dto.Barcode.GetAztecQrCode(),
                    price = dto.Amount.Value,
                    title = VoucherMunicipalCode.VoucherMunicipalCodeMembers.FirstOrDefault(c => c.Value == dto.voucherMunicipalCode).Key,
                    type = dto.expireDate.ToDataTime() > DateTime.Now ? 0 : 1,
                    transactiondate = dto.BussinessDate.ToString()
                };
                listsingleTickets.Add(ticket);

            }
            return Ok(new MessageModel<List<SingleTicketResponseDto>>
            {
                CountType = listsingleTickets.GetCountTypeTicket(),
                Data = listsingleTickets,
                message = DescriptionUtility.GetDescription<ServiceStatus>("Success"),
                status = (int)ServiceStatus.Success
            });
        }

        [HttpGet("GetSingleDirectVoucherHistory")]
        [LoggingAspect]
        public async Task<IActionResult> GetSingleDirectVoucherHistory(VoucherHistoryRequestDto req)
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

            try
            {

                var report = await _tosanSohaServices.VoucherHistory(req);

                if (report != null)
                {
                    if (report.Vouchers.Count != 0)
                    {
                        return Ok(new MessageModel<VoucherHistoryResponseDto>
                        {
                            Data = report,
                            message = DescriptionUtility.GetDescription<ServiceStatus>("Success"),
                            status = (int)ServiceStatus.Success
                        });
                    }
                    else
                    {
                        return Ok(new MessageModel<VoucherHistoryResponseDto>
                        {
                            Data = report,
                            message = DescriptionUtility.GetDescription<ServiceStatus>("NotDataFound"),
                            status = (int)ServiceStatus.NotDataFound
                        });
                    }
                }
                else
                {
                    return Ok(new MessageModel<VoucherHistoryResponseDto>
                    {
                        Data = null,
                        message = DescriptionUtility.GetDescription<ServiceStatus>("NotDataFound"),
                        status = (int)ServiceStatus.NotDataFound
                    });
                }

            }
            catch (Exception)
            {
                return Ok(new MessageModel<VoucherHistoryResponseDto>
                {
                    Data = null,
                    message = DescriptionUtility.GetDescription<ServiceStatus>("NotActiveService"),
                    status = (int)ServiceStatus.NotActiveService
                });
            }
        }

        [HttpPost("InquirySingleDirectByOrderId")]
        [LoggingAspect]
        public async Task<IActionResult> InquirySingleDirectByOrderId(InquiryTosanSohaRequestViewModel model)
        {
            _logger.Log(136813681, "InquirySingleDirectByOrderId =>" + JsonConvert.SerializeObject(model.OrderId), null);
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

                _logger.Log(13681368, "To InquirySingleDirectByOrderId =>" + userId + " = " + model.OrderId, null);
                var inquiry = await _tosanSohaServices.InquirySingleDirectionTicketByOrderIdAsync(model.OrderId, userId);

                _logger.Log(13681368, "from InquirySingleDirectByOrderId =>" + JsonConvert.SerializeObject(inquiry), null);
                if (inquiry.Status == 2 || inquiry.Status == 6)
                {
                    if (!inquiry.Data.RequestIsSent)
                    {
                        _logger.Log(13681368, " request is sent false =>" + inquiry.Data.OrderId.Value, null);
                        var result = await _tosanSohaServices.UpdateSingleDirectionTicketRequestAsync(inquiry.Data.OrderId.Value);//
                        _logger.Log(13681368, "moh from updateInquirySingleDirectByOrderId =>" + result, null);
                        if (result)
                        {
                            _logger.Log(13681368, "moh to CreateSingleDirectInquiryAsync =>" + JsonConvert.SerializeObject(inquiry), null);
                            var request = new SingleDirectVoucherSellRequestDto
                            {
                                operatorUniqueId = "7062",
                                clientUniqueId = "top",
                                count = 1,
                                referenceId = inquiry.Data.OrderId.Value.ToString(),
                                price = inquiry.Data.Amount.Value,
                                userPhoneNumber = inquiry.Data.MobileNo,
                                voucherMunicipalCode = inquiry.Data.voucherMunicipalCode
                            };
                            var _callTosanSoha = await _tosanSohaServices.VoucherSell(request);
                            _logger.Log(13681368, "moh from VoucherSellAsync =>" + JsonConvert.SerializeObject(_callTosanSoha), null);
                            if (_callTosanSoha != null && _callTosanSoha.resultCode == ResultCodeStatus.Success)
                            {



                                var req = _callTosanSoha.voucherDetailsModel.Select(e => new VoucherDetailsModel
                                {
                                    serialNo = e.serialNo,
                                    expireDate = e.expireDate,
                                    expireDateEn = e.expireDateEn,
                                    voucher = e.voucher.GetAztecQrCode()

                                }).FirstOrDefault();

                                inquiry.Data.expireDate = req.expireDate;
                                inquiry.Data.expireDateEn = req.expireDateEn;
                                inquiry.Data.Barcode = _callTosanSoha.voucherDetailsModel.FirstOrDefault().voucher;
                                inquiry.Data.BussinessDate = DateTime.Now;
                                inquiry.Data.serialNo = req.serialNo;

                                var updatedResult = await _tosanSohaServices.UpdateSingleDirectionTicketAsync(inquiry.Data);
                                if (!updatedResult)
                                {
                                    await _tosanSohaServices.UpdateSingleDirectionTicketAsync(inquiry.Data);
                                }
                                var reponsemain = new TosanSohaResponseVM<VoucherDetailsModel>()
                                {
                                    Amount = inquiry.Data.Amount.Value,
                                    Token = inquiry.Data.WalletReturnId.Value,
                                    Message = inquiry.Message,
                                    OrderId = inquiry.Data.OrderId.Value.ToString(),
                                    TransactionDate = inquiry.Data.BussinessDate,
                                    TosanSohaData = req
                                };

                                return Ok(new MessageModel<TosanSohaResponseVM<VoucherDetailsModel>>
                                {
                                    message = (inquiry.Status == 2) ? DescriptionUtility.GetDescription<ServiceStatus>("WalletSuccess") :
                                    DescriptionUtility.GetDescription<ServiceStatus>("GetwaySuccess"),
                                    status = (inquiry.Status == 2) ? (int)ServiceStatus.WalletSuccess : (int)ServiceStatus.GetwaySuccess,
                                    Data = reponsemain
                                });
                            }
                            else
                            {
                                // byte[] bytes = Encoding.UTF8.GetBytes(_callTosanSoha.Data.voucher);

                                return Ok(new MessageModel<List<VoucherDetailsModel>>
                                {
                                    message = _callTosanSoha.message,
                                    status = _callTosanSoha.resultCode,
                                    Data = null
                                });
                            }
                        }
                        else
                        {
                            return Ok(new MessageModel<List<VoucherDetailsModel>>
                            {
                                message = DescriptionUtility.GetDescription<ServiceStatus>("OperationUnSuccess"),
                                status = (int)ServiceStatus.OperationUnSuccess,
                                Data = null
                            });
                        }
                    }
                    else
                    {
                        _logger.Log(13681368, "  request is sent true =>" + inquiry.Data.OrderId.Value, null);
                        var GetInquiries = await _tosanSohaServices.GetSingleDirectionTicketByExperssionAsync(n => n.OrderId == model.OrderId);
                        var GetInquiry = GetInquiries.FirstOrDefault();
                        _logger.Log(13681368, "moh from GetInquiry =>" + JsonConvert.SerializeObject(GetInquiry), null);
                        if (GetInquiry != null && GetInquiry.Status == 0)
                        {

                            var req = new VoucherDetailsModel
                            {
                                serialNo = GetInquiry.serialNo.Value,
                                expireDate = GetInquiry.expireDate,
                                expireDateEn = GetInquiry.expireDateEn,
                                voucher = GetInquiry.Barcode.GetAztecQrCode(),
                                path = VoucherMunicipalCode.VoucherMunicipalCodeMembers.FirstOrDefault(c => c.Value == GetInquiry.voucherMunicipalCode).Key,
                                state = GetInquiry.expireDate.ToDataTime() > DateTime.Now ? "قابل استفاده" : "منقضی شده"
                            };

                            var reponsemain = new TosanSohaResponseVM<VoucherDetailsModel>()
                            {
                                Amount = GetInquiry.Amount.Value,
                                Token = GetInquiry.WalletReturnId.Value,
                                Message = GetInquiry.ResultMessage,
                                OrderId = GetInquiry.OrderId.Value.ToString(),
                                TransactionDate = GetInquiry.BussinessDate,
                                TosanSohaData = req
                            };

                            return Ok(new MessageModel<TosanSohaResponseVM<VoucherDetailsModel>>
                            {
                                message = (inquiry.Status == 2) ? DescriptionUtility.GetDescription<ServiceStatus>("WalletSuccess") :
                                DescriptionUtility.GetDescription<ServiceStatus>("GetwaySuccess"),
                                status = (inquiry.Status == 2) ? (int)ServiceStatus.WalletSuccess : (int)ServiceStatus.GetwaySuccess,
                                Data = reponsemain
                            });


                        }
                        else
                        {
                            return Ok(new MessageModel<VoucherDetailsModel>
                            {
                                message = DescriptionUtility.GetDescription<ServiceStatus>("OperationUnSuccess"),
                                status = (int)ServiceStatus.OperationUnSuccess,
                                Data = null
                            });
                        }
                    }
                }
                else
                {
                    return Ok(new MessageModel<SingleDirectVoucherSellResponseDto>
                    {
                        message = DescriptionUtility.GetDescription<ServiceStatus>("OperationUnSuccess"),
                        status = (int)ServiceStatus.OrderIdNotValid,
                        Data = null
                    });
                }
            }
            catch (Exception e)
            {
                return Ok(new MessageModel<SingleDirectVoucherSellResponseDto>
                {
                    message = DescriptionUtility.GetDescription<ServiceStatus>("OperationFaild"),
                    status = (int)ServiceStatus.OperationFaild,
                    Data = null
                });
            }
        }


        [HttpPost("ConfirmSingleDirect")]
        [AllowAnonymous]
        public async Task<IActionResult> ConfirmSingleDirect(string mid)
        {
            var model = new SingleDirectionTicket();

            try
            {
                var status = short.Parse(Request.Form["status"]);
                model.Token = long.Parse(Request.Form["Token"]);
                model.OrderId = long.Parse(Request.Form["OrderId"]);
                model.PaymentStatus = short.Parse(Request.Form["status"]);
                model.MaskCardNumber = Request.Form["HashCardNumber"];
                var urlReceipt = "";
                if (string.IsNullOrEmpty(mid) || mid == "0")
                {
                    urlReceipt = _config.GetSection("Proxy:Url:TosanSoha:ReceiptPaySingleDirectCard").Value + model.OrderId;
                }
                else
                {
                    urlReceipt = _config.GetSection("Proxy:Url:TosanSoha:ReceiptPaySingleDirectCard").Value + "?mid=" + mid;
                }

                if (status != 0)
                {
                    _logger.Log(806, $"📥 ConfirmSingleDirect When Status != 0", JsonConvert.SerializeObject(model), null);

                    await _tosanSohaServices.ConfirmSingleDirectionTicketAsync(model);
                    return Redirect(urlReceipt);
                }

                _logger.Log(813, $"📥 ConfirmSingleDirect When Status == 0", JsonConvert.SerializeObject(model), null);
                model.RRN = Convert.ToInt64(Request.Form["RRN"]);
                var amount = Request.Form["Amount"].ToString().Replace(",", "");
                model.Amount = Convert.ToInt64(amount);
                await _tosanSohaServices.ConfirmSingleDirectionTicketAsync(model);
                return Redirect(urlReceipt);
            }
            catch (Exception ex)
            {
                _logger.Log(822, $"📥 get error when ConfirmSingleDirectionAsync", JsonConvert.SerializeObject(model), ex);
                string urlReceipt;
                if (string.IsNullOrEmpty(mid) && mid == "0")
                {
                    urlReceipt = _config.GetSection("Proxy:Url:TosanSoha:ReceiptPaySingleDirectCard").Value + -100;
                }
                else
                {
                    urlReceipt = _config.GetSection("Proxy:Url:TosanSoha:ReceiptPaySingleDirectCard").Value + -100 + "?mid=" + mid;
                }
                return Redirect(urlReceipt);
            }
        }

        [HttpPost("PaySingleDirectTicket")]
        [LoggingAspect]
        public async Task<IActionResult> PaySingleDirectTicket(SingleDirectVoucherSellReqVM VoucherSellReq)
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
                if (VoucherSellReq.TransactionType != 1 && VoucherSellReq.TransactionType != 2)
                {
                    return Ok(new MessageModel
                    {
                        message = DescriptionUtility.GetDescription<ServiceStatus>("InValidTransactionType"),
                        status = (int)ServiceStatus.InValidTransactionType
                    });
                }
                //get user wallet
                else if (VoucherSellReq.TransactionType == 2)
                {
                    var userWallets = await _walletServices.GetUserWallet(userId);
                    //تست
                    var isOwner = userWallets.Any(p => p.WalletCode == VoucherSellReq.WalletCode);
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

                //generate orderId
                var OrderId = Utility.Utility.GenerateNajaOrderID();

                var singleDirectionTicket = new SingleDirectionTicket
                {
                    CreateDate = DateTime.Now,
                    BussinessDate = DateTime.Now,
                    MobileNo = VoucherSellReq.userPhoneNumber,
                    CustomerWalletId = VoucherSellReq.WalletCode,
                    NationalCode = VoucherSellReq.WalletCode,

                    // NationalCode = VoucherSellReq.na,
                    OrderId = OrderId,
                    referenceId = OrderId.ToString(),
                    count = 1,
                    UserId = userId,
                    MerchantId = VoucherSellReq.MerchantId.HasValue ? VoucherSellReq.MerchantId : 0,
                    TransactionType = VoucherSellReq.TransactionType,
                    WalletId = VoucherSellReq.WalletId,
                    Amount = VoucherSellReq.price,
                    voucherMunicipalCode = VoucherSellReq.voucherMunicipalCode,
                    PaymentStatus = VoucherSellReq.TransactionType == 1 ? PaymentStatus.FirstRequestPGW : PaymentStatus.FirstRequestWallet, //First Status Insert
                };

                var addPay = await _tosanSohaServices.AddSingleDirectionTicket(singleDirectionTicket);
                if (!addPay)
                {
                    return Ok(new MessageModel<PayWageResponseViewModel<object>>
                    {
                        message = DescriptionUtility.GetDescription<ServiceStatus>("OperationUnSuccess"),
                        status = (int)ServiceStatus.OperationUnSuccess,
                        Data = null
                    });
                }
                singleDirectionTicket.OrderId = OrderId;

                //insert into pay
                var response = new ResponseBaseDto<GetIpgTokenResponseDto>();
                switch (VoucherSellReq.TransactionType)
                {
                    case 1:
                        response = await _tosanSohaServices.ShetabSingleDirectionTicket(singleDirectionTicket);
                        if (response != null && response.Status == 101)
                        {
                            var req = _mapper.Map<TosanSohaResponseVM<object>>(response.Data);
                            req.TosanSohaData = null;

                            req.Amount = VoucherSellReq.price;
                            req.TransactionDate = DateTime.Now;
                            return Ok(new MessageModel<TosanSohaResponseVM<object>>
                            {
                                message = response.Message,
                                status = response.Status,
                                Data = req
                            });
                        }
                        else
                        {
                            return Ok(new MessageModel<TosanSohaResponseVM<object>>
                            {
                                message = response.Message,
                                status = (int)ServiceStatus.GetwayNotToken,
                                Data = null
                            });
                        }
                    case 2:
                        response = await _tosanSohaServices.WalletSingleDirectionTicket(singleDirectionTicket);
                        if (response.Status == 0)
                        {
                            var getChargeCard = await _tosanSohaServices.GetSingleDirectionTicketByExperssionAsync(n => n.OrderId == singleDirectionTicket.OrderId &&
                                                                                   n.WalletReturnId == response.Data.Token);
                            var PaidChargeCard = getChargeCard.FirstOrDefault();
                            if (PaidChargeCard != null && PaidChargeCard.Status == 0)
                            {
                                var req = _mapper.Map<TosanSohaResponseVM<object>>(response.Data);
                                req.TosanSohaData = null;

                                req.Amount = singleDirectionTicket.Amount.Value;
                                req.TransactionDate = singleDirectionTicket.BussinessDate;
                                return Ok(new MessageModel<TosanSohaResponseVM<object>>
                                {
                                    message = DescriptionUtility.GetDescription<ServiceStatus>("WalletSuccess"),
                                    status = (int)ServiceStatus.WalletSuccess,
                                    Data = req
                                });
                            }
                            else
                            {
                                return Ok(new MessageModel<TosanSohaResponseVM<object>>
                                {
                                    message = response.Message,
                                    status = (int)ServiceStatus.WalletUnSuccess,
                                    Data = null
                                });
                            }
                        }
                        break;
                    default:
                        return Ok(new MessageModel
                        {
                            message = "نوع تراکنش را مشخص نمایید",
                            status = -1,
                        });
                }
                return Ok(new MessageModel<TosanSohaResponseVM<object>>
                {
                    message = response.Message,
                    status = response.Status,
                    Data = null
                });
            }
            catch (Exception ex)
            {
                return Ok(new MessageModel<TosanSohaResponseVM<object>>
                {
                    message = JsonConvert.SerializeObject(ex),
                    status = (int)ServiceStatus.OperationFaild,
                    Data = null
                });
            }
        }

        [HttpGet("GetSingleDirectVoucherPrice")]
        [LoggingAspect]
        public async Task<IActionResult> GetSingleDirectVoucherPrice()
        {
            try
            {
                //#region Check user
                //var userId = User.GetUserId();
                //if (userId == 0)
                //{
                //    return Ok(new MessageModel
                //    {
                //        message = DescriptionUtility.GetDescription<ServiceStatus>("NoUserFound"),
                //        status = (int)ServiceStatus.NoUserFound
                //    });
                //}
                //#endregion

                var response = await _tosanSohaServices.VoucherPriceInquiry();

                return Ok(new MessageModel<List<SingleVoucherPriceResponseDto>>
                {
                    message = "موفقیت",
                    status = (int)ServiceStatus.Success,
                    Data = response
                });
            }
            catch (Exception ex)
            {
                return Ok(new MessageModel
                {
                    message = JsonConvert.SerializeObject(ex),
                    status = (int)ServiceStatus.OperationFaild,
                    //Data = null
                });
            }
        }
        //استعلام فروش بلیت
        [HttpGet("GetSingleDirectTicketVoucherSellInquiry")]
        [LoggingAspect]
        public async Task<IActionResult> GetSingleDirectTicketVoucherSellInquiry(InquirySellTicketRequestDto req)
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

            var report = await _tosanSohaServices.VoucherSellInquiry(req);

            if (report != null)
            {
                if (report.voucherDetailsModel.Count != 0)
                {
                    return Ok(new MessageModel<List<VoucherDetailsModel>>
                    {
                        Data = report.voucherDetailsModel,
                        message = DescriptionUtility.GetDescription<ServiceStatus>("Success"),
                        status = (int)ServiceStatus.Success
                    });
                }
                else
                {
                    return Ok(new MessageModel<List<VoucherDetailsModel>>
                    {
                        Data = report.voucherDetailsModel,
                        message = DescriptionUtility.GetDescription<ServiceStatus>("NotDataFound"),
                        status = (int)ServiceStatus.NotDataFound
                    });
                }
            }
            else
            {
                return Ok(new MessageModel<List<VoucherDetailsModel>>
                {
                    Data = null,
                    message = DescriptionUtility.GetDescription<ServiceStatus>("NotDataFound"),
                    status = (int)ServiceStatus.NotDataFound
                });
            }
        }


        #endregion

        #region ClientCard
        [HttpGet("GetClientCards")]
        [LoggingAspect]
        public async Task<IActionResult> GetClientCards()
        {
            try
            {
                #region Check user
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
                var response = await _tosanSohaServices.GetTicketCardsAsync(userId);
                var data = _mapper.Map<List<ClientCardTicketResponseVM>>(response);

                return Ok(new MessageModel<List<ClientCardTicketResponseVM>>
                {
                    message = "Success",
                    status = 0,
                    Data = data
                });

            }
            catch (Exception ex)
            {

                return Ok(new MessageModel<ClientCardTicketResponseVM>
                {
                    message = JsonConvert.SerializeObject(ex),
                    status = (int)ServiceStatus.OperationFaild,
                    Data = null
                });
            }
        }

        [HttpPost("AddClientCard")]
        [LoggingAspect]
        public async Task<IActionResult> AddClientCard([FromBody] ClientCardTicketReqVM clientCard)
        {

            #region Check user
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

            var req = _mapper.Map<TicketCard>(clientCard);
            req.CreateDate = DateTime.Now;
            req.UserId = userId;
            var response = await _tosanSohaServices.AddTicketCard(req);
            if (response.Status == 0)
            {
                return Ok(new MessageModel<ClientCardTicketResponseVM>
                {
                    message = response.Message,
                    status = (int)ServiceStatus.Success,
                    Data = null
                });
            }
            else
            {
                return Ok(response);
            }

        }


        [HttpPost("UpdateClientCard")]
        [LoggingAspect]
        public async Task<IActionResult> UpdateClientCard(ClientCardTicketReqVM clientCard)
        {
            try
            {
                #region Check user
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

                var req = _mapper.Map<TicketCard>(clientCard);
                var response = await _tosanSohaServices.UpdateTicketCard(req);
                if (!response)
                {
                    return Ok(new MessageModel<ClientCardTicketResponseVM>
                    {
                        message = "UnSuccess",
                        status = (int)ServiceStatus.UnSuccess,
                        Data = null
                    });
                }
                return Ok(new MessageModel<ClientCardTicketResponseVM>
                {
                    message = "Success",
                    status = 0,
                    Data = null
                });

            }
            catch (Exception ex)
            {

                return Ok(new MessageModel<ClientCardTicketResponseVM>
                {
                    message = JsonConvert.SerializeObject(ex),
                    status = (int)ServiceStatus.OperationFaild,
                    Data = null
                });
            }
        }


        [HttpPost("DeleteClientCard")]
        [LoggingAspect]
        public async Task<IActionResult> DeleteClientCard(long cardId)
        {
            try
            {
                #region Check user
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
                var response = await _tosanSohaServices.DeleteTicketCardAsync(cardId);

                return Ok(new MessageModel<ClientCardTicketResponseVM>
                {
                    message = "Success",
                    status = 0,
                    Data = null
                });

            }
            catch (Exception ex)
            {
                return Ok(new MessageModel<ClientCardTicketResponseVM>
                {
                    message = JsonConvert.SerializeObject(ex),
                    status = (int)ServiceStatus.OperationFaild,
                    Data = null
                });
            }
        }
        #endregion
    }
}
