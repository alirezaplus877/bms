using Application.Services;
using AutoMapper;
using Dto.Proxy.Request;
using Dto.Proxy.Request.Wallet;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PecBMS.ViewModel;
using PecBMS.ViewModel.Request;
using PecBMS.ViewModel.Response;
using Pigi.MDbLogging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Utility;

namespace AutoBillPaymentService
{
    public class AutoBillPaymentWorker : BackgroundService, IDisposable, ILoggable
    {
        private IMdbLogger<AutoBillPaymentWorker> _logger;
        private IMapper _mapper;
        private IPGWUserService _pgwUserService;
        private IPayServices _payServices;
        private IServiceScopeFactory _serviceProvider;
        private IWalletServices _walletServices;
        private IBillService _billService;
        private IPecBmsSetting _setting;
        Guid _workerTraceCode;

        public AutoBillPaymentWorker(IMdbLogger<AutoBillPaymentWorker> logger, IServiceScopeFactory serviceScopeFactory
                                                                             , IMapper mapper)
        {
            _logger = logger;
            _serviceProvider = serviceScopeFactory;
            _mapper = mapper;

        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                _logger = scope.ServiceProvider.GetRequiredService<IMdbLogger<AutoBillPaymentWorker>>();
                _walletServices = scope.ServiceProvider.GetRequiredService<IWalletServices>();
                _billService = scope.ServiceProvider.GetRequiredService<IBillService>();
                _setting = scope.ServiceProvider.GetRequiredService<IPecBmsSetting>();
                _pgwUserService = scope.ServiceProvider.GetRequiredService<IPGWUserService>();
                _payServices = scope.ServiceProvider.GetRequiredService<IPayServices>();
                while (!stoppingToken.IsCancellationRequested)
                {
                    await AutoBillProcess();
                    await Task.Delay(60000, stoppingToken);
                }
            }
        }


        public override Task StopAsync(CancellationToken cancellationToken)
        {
            //لاگ پایان ورکر
            _logger.Log(_workerTraceCode, null, $"◼ AutoBillProcess stopped at ({DateTime.Now})", EventLogEntryType.Information);

            return base.StopAsync(cancellationToken);
        }

        [LoggingAspect]
        private async Task AutoBillProcess()
        {
            try
            {
                //لاگ شروع ورکر
                _logger.Log(_workerTraceCode, null, $"⯈ AutoBillProcess started at ({DateTime.Now})", EventLogEntryType.Information);

                var autoBillList = await _billService.GetReadyAutoBillPaymentWithWallet();

                if (autoBillList == null || !autoBillList.Any())
                {
                    #region Log
                    //لاگ خواب ورکر
                    _logger.Log(_workerTraceCode, null, $"💤 Worker did not fetched any ready  bills and did not call wallet pay service ", EventLogEntryType.Information);
                    #endregion
                    return;

                };

                _logger.Log(_workerTraceCode, null, $"📥 ReceiptWorker fetched {autoBillList.Count()} ready bills'", EventLogEntryType.Information);



                foreach (var detailItem in autoBillList)
                {
                    long UserId = detailItem.UserID.Value;
                    var User = await _pgwUserService.GetUserByIdAsync(UserId);
                    var WalletCode = await _walletServices.GetUserWallet(UserId);


                    List<WalletsInfo> walletsInfos = new List<WalletsInfo>();

                    WalletCode.ForEach(w =>
                    {
                        WalletsInfo walletsInfo = new WalletsInfo()
                        {
                            CorporationPIN = w.CorporationPIN,
                            WalletCode = w.WalletCode
                        };
                        walletsInfos.Add(walletsInfo);
                    });
                    var Walletinfoinput = _mapper.Map<List<WalletsInfo>, List<GetCustomerWalletRequestDto>>(walletsInfos);


                    var getuserWalletList = await _walletServices.GetCustomerWallet(Walletinfoinput);
                    var _getuserWallet = getuserWalletList.FirstOrDefault();
                    //استعلام بر اساس سازمان
                    switch (detailItem.OrganizationId)
                    {
                        //برق inquiry/EdcBillInquiry
                        case 3:
                            var BarghInquiry = await _billService.BarghInquiry(new Dto.Proxy.Request.PecIs.BarghBillInquiryRequestDto
                            {
                                BillId = detailItem.BillId
                            });

                            if (BarghInquiry.Status == 0)
                            {
                                var _BarghInquiry = BarghInquiry.Data;
                                decimal BarghAmount = Convert.ToDecimal(_BarghInquiry.Amount);

                                if (_BarghInquiry != null && _getuserWallet != null && _getuserWallet.Amount > BarghAmount)
                                {
                                    PayBillRequestDto payBillRequestDto = new PayBillRequestDto
                                    {
                                        Bills = new List<BillsDto> {
                                                    new  BillsDto{
                                                          Amount = long.Parse(_BarghInquiry.Amount),
                                                          BillId = _BarghInquiry.BillId,
                                                          OrganizationId = (int)detailItem.OrganizationId,
                                                          PayId = _BarghInquiry.PayId
                                                    }
                                    },
                                        TotalAmount = long.Parse(_BarghInquiry.Amount),
                                        WalletCode = _getuserWallet.WalletCode,
                                        WalletId = _getuserWallet.CustomerWalletId,
                                        UserId = UserId,
                                        TransactionType = 2,
                                        MobileNumber = $"0{User.MobileNo}",
                                        IsSendAutomation = true,
                                    };
                                    await _payServices.WalletPayBill(payBillRequestDto);
                                }
                                
                    var updateBussinessDate = await _billService.UpdateBussunessDateAutoBillPayment(UserId, detailItem.BillId, DateTime.Now);
                    if (!updateBussinessDate)
                    {
                        await _billService.UpdateBussunessDateAutoBillPayment(UserId, detailItem.BillId, DateTime.Now);
                    }
                            }

                            break;

                        //تلفن ثابت Inquiry/TciBillInquiry
                        case 4:

                            var TciBillInquiry = await _billService.TciBillInquiry(new Dto.Proxy.Request.PecIs.TciInquiryRequestDto
                            {
                                TelNo = detailItem.CustomerId
                            });

                            if (TciBillInquiry.Status == 0)
                            {
                                var _TciBillInquiry = TciBillInquiry.Data.FirstOrDefault(t => t.Type == 1);
                                decimal TciAmount = Convert.ToDecimal(_TciBillInquiry.Amount);


                                if (_TciBillInquiry != null && _getuserWallet != null && _getuserWallet.Amount > TciAmount)
                                {
                                    PayBillRequestDto payBillRequestDto = new PayBillRequestDto
                                    {
                                        Bills = new List<BillsDto> {
                                                    new  BillsDto{
                                                          Amount = long.Parse(_TciBillInquiry.Amount.Replace(",","")),
                                                          BillId = _TciBillInquiry.BillId.ToString(),
                                                          OrganizationId = (int)detailItem.OrganizationId,
                                                          PayId = _TciBillInquiry.PayId.ToString()
                                                    }
                                    },
                                        TotalAmount = long.Parse(_TciBillInquiry.Amount.Replace(",", "")),
                                        WalletCode = _getuserWallet.WalletCode,
                                        WalletId = _getuserWallet.CustomerWalletId,
                                        UserId = UserId,
                                        TransactionType = 2,
                                        MobileNumber = $"0{User.MobileNo}",
                                        IsSendAutomation = true,
                                    };
                                    await _payServices.WalletPayBill(payBillRequestDto);
                                }

                                var updateBussinessDate = await _billService.UpdateBussunessDateAutoBillPayment(UserId, detailItem.BillId, DateTime.Now);
                                if (!updateBussinessDate)
                                {
                                    await _billService.UpdateBussunessDateAutoBillPayment(UserId, detailItem.BillId, DateTime.Now);
                                }
                            }

                            break;

                        //تلفن همراه inquiry/MciBillInquiry
                        case 5:
                            var MciBillInquiry = await _billService.MciBillInquiry(new Dto.Proxy.Request.PecIs.MciBillInquiryRequestDto
                            {
                                MobileNumber = detailItem.CustomerId
                            });

                            if (MciBillInquiry.Status == 0)
                            {
                                var _MciBillInquiry = MciBillInquiry.Data.FirstOrDefault();
                                decimal MciAmount = Convert.ToDecimal(_MciBillInquiry.Amount);

                                if (_MciBillInquiry != null && _getuserWallet != null && _getuserWallet.Amount > MciAmount)
                                {
                                    PayBillRequestDto payBillRequestDto = new PayBillRequestDto
                                    {
                                        Bills = new List<BillsDto> {
                                                    new  BillsDto{
                                                          Amount = long.Parse(_MciBillInquiry.Amount),
                                                          BillId = _MciBillInquiry.BillId,
                                                          OrganizationId = (int)detailItem.OrganizationId,
                                                          PayId = _MciBillInquiry.PaymentId
                                                    }
                                    },
                                        TotalAmount = long.Parse(_MciBillInquiry.Amount),
                                        WalletCode = _getuserWallet.WalletCode,
                                        WalletId = _getuserWallet.CustomerWalletId,
                                        UserId = UserId,
                                        TransactionType = 2,
                                        MobileNumber = $"0{User.MobileNo}",
                                        IsSendAutomation = true,
                                    };
                                    await _payServices.WalletPayBill(payBillRequestDto);
                                }

                                var updateBussinessDate = await _billService.UpdateBussunessDateAutoBillPayment(UserId, detailItem.BillId, DateTime.Now);
                                if (!updateBussinessDate)
                                {
                                    await _billService.UpdateBussunessDateAutoBillPayment(UserId, detailItem.BillId, DateTime.Now);
                                }
                            }
                            else if (MciBillInquiry.Status == -1)
                            {
                                //تسویه قبلا انجام شده است
                                //خروج از چرخه پرداخت روز
                                var updateBussinessDate = await _billService.UpdateBussunessDateAutoBillPayment(UserId, detailItem.BillId, DateTime.Now);
                                if (!updateBussinessDate)
                                {
                                    await _billService.UpdateBussunessDateAutoBillPayment(UserId, detailItem.BillId, DateTime.Now);
                                }

                            }
                            break;

                        //گاز inquiry/NigcBillInquiry
                        case 10:
                            var NigcBillInquiry = await _billService.NigcBillInquiry(new Dto.Proxy.Request.PecIs.NigcBillInquiryRequestDto
                            {
                                SubscriptionId = detailItem.CustomerId
                            });

                            if (NigcBillInquiry.Status == 0)
                            {
                                var _NigcBillInquiry = NigcBillInquiry.Data;
                                decimal NigcAmount = Convert.ToDecimal(_NigcBillInquiry.PayAmount);


                                if (_NigcBillInquiry != null && _getuserWallet != null && _getuserWallet.Amount > NigcAmount)
                                {
                                    PayBillRequestDto payBillRequestDto = new PayBillRequestDto
                                    {
                                        Bills = new List<BillsDto> {
                                                    new  BillsDto{
                                                          Amount = long.Parse(_NigcBillInquiry.PayAmount),
                                                          BillId = _NigcBillInquiry.BankBillId,
                                                          OrganizationId = (int)detailItem.OrganizationId,
                                                          PayId = _NigcBillInquiry.BankPayId
                                                    }
                                    },
                                        TotalAmount = long.Parse(_NigcBillInquiry.PayAmount),
                                        WalletCode = _getuserWallet.WalletCode,
                                        WalletId = _getuserWallet.CustomerWalletId,
                                        UserId = UserId,
                                        TransactionType = 2,
                                        MobileNumber = $"0{User.MobileNo}",
                                        IsSendAutomation = true,
                                    };
                                    await _payServices.WalletPayBill(payBillRequestDto);
                                }

                                var updateBussinessDate = await _billService.UpdateBussunessDateAutoBillPayment(UserId, detailItem.BillId, DateTime.Now);
                                if (!updateBussinessDate)
                                {
                                    await _billService.UpdateBussunessDateAutoBillPayment(UserId, detailItem.BillId, DateTime.Now);
                                }
                            }

                            break;

                        default:
                            break;
                    }


                }


            }
            catch (Exception ex)
            {
                _logger.Log(_workerTraceCode, null, $"◼ AutoBillProcess Got Exception at ({DateTime.Now})", EventLogEntryType.Error, ex);
                return;
            }

        }
    }
}
