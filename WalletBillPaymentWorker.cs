using Application.Services;
using AutoMapper;
using Dto.Proxy.Wallet;
using Dto.repository;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Pigi.MDbLogging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Utility;

namespace WalletBillPaymentService
{
    public class WalletBillPaymentWorker : BackgroundService, IDisposable, ILoggable
    {
        private IMdbLogger<WalletBillPaymentWorker> _logger;
        private IMapper _mapper;
        private IServiceScopeFactory _serviceProvider;
        private IWalletServices _walletServices;
        private IBillService _billService;
        private IPecBmsSetting _setting;
        Guid _workerTraceCode;

        public WalletBillPaymentWorker(IMdbLogger<WalletBillPaymentWorker> logger, IServiceScopeFactory serviceScopeFactory
            )
        {
            _logger = logger;
            _serviceProvider = serviceScopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                _logger = scope.ServiceProvider.GetRequiredService<IMdbLogger<WalletBillPaymentWorker>>();
                _walletServices = scope.ServiceProvider.GetRequiredService<IWalletServices>();
                _billService = scope.ServiceProvider.GetRequiredService<IBillService>();
                _setting = scope.ServiceProvider.GetRequiredService<IPecBmsSetting>();
                while (!stoppingToken.IsCancellationRequested)
                {
                    await WalletPayBillProcess();
                    await Task.Delay(60000, stoppingToken);
                }
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            //لاگ پایان ورکر
            _logger.Log(_workerTraceCode, null, $"◼ WalletBillPaymentWorker stopped at ({DateTime.Now})", EventLogEntryType.Information);

            return base.StopAsync(cancellationToken);
        }

        [LoggingAspect]
        private async Task WalletPayBillProcess()
        {
            try
            {
                //لاگ شروع ورکر
                _logger.Log(_workerTraceCode, null, $"⯈ WalletPayBillProcess started at ({DateTime.Now})", EventLogEntryType.Information);

                var billList = await _billService.GetReadyBillToPayWithWallet();

                if (billList == null || !billList.Any())
                {
                    #region Log
                    //لاگ خواب ورکر
                    _logger.Log(_workerTraceCode, null, $"💤 Worker did not fetched any ready  bills and did not call wallet pay service ", EventLogEntryType.Information);
                    #endregion
                    return;

                };

                _logger.Log(_workerTraceCode, null, $"📥 ReceiptWorker fetched {billList.Count()} ready bills'", EventLogEntryType.Information);



                foreach (var detailItem in billList)
                {

                    //var GetCustomerWallet = await _walletServices.GetCustomerWallet(new List<Dto.Proxy.Request.Wallet.GetCustomerWalletRequestDto>() {
                    //    new Dto.Proxy.Request.Wallet.GetCustomerWalletRequestDto(){
                    //        CorporationUserId = _setting.CorporationUserId.ToString(),
                    //        GroupWalletId = _setting.ParrentWalletType.ToString(),
                    //        NationalCode = item.WalletId.ToString()
                    //    }});
                    //var sourceWalletId = GetCustomerWallet.FirstOrDefault(c => c.WalletCode == item.WalletId.ToString());
                    var orderId = DateTime.Now.Ticks;
                    WalletBillPaymentRequestDto paymentRequestDto = new WalletBillPaymentRequestDto()
                    {
                        AdditionalData = detailItem.BillId.PadLeft(13, '0') + "=" + detailItem.PayId.PadLeft(13, '0'),
                        Amount = detailItem.Amount,
                        //call back pay bill
                        CallBackURL = _setting.CallBackUrlParentWallet,
                        CorporationPIN = _setting.CorporationPIN,
                        DestinationNationalCode = _setting.MotherWalletCode.ToString(),//// ؟؟؟؟
                        IpAddress = "",
                        MediaTypeId = _setting.WalletMediaType,
                        OrderId = orderId,
                        PIN = "234",
                        SourceWalletId = Convert.ToInt64(detailItem.SourceWallet), // sourceWalletId.CustomerWalletId,
                        TransactionDateTime = DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss")

                    };
                    _logger.Log(orderId,"request=>"+ JsonConvert.SerializeObject(paymentRequestDto), EventLogEntryType.Information);
                    var insertToDb = await _billService.AddWalletPayBillTransaction(new WalletPayBillTransactionDto()
                    {
                        CreateDate = DateTime.Now,
                        OrderId = orderId,
                        ParentId = detailItem.Id,
                        Request = JsonConvert.SerializeObject(paymentRequestDto),
                        Response = "",
                        ResultDesc = "",
                        ResultId = -1,
                        ReturnId = 0,
                        BusinessDate = DateTime.Now.Date
                    });
                    var paymentProcess = await _walletServices.WalletPayBill(paymentRequestDto);
                    _logger.Log(orderId,"response=>"+ JsonConvert.SerializeObject(paymentProcess), EventLogEntryType.Information);
                    #region Log
                    //لاگ بعد فراخوانی سرویس اعلام وصول
                  
                    #endregion

                    var updateDb = await _billService.UpdateBillTableForWallet(orderId,paymentProcess.Data.ResultId,
                        paymentProcess.Data.ResultDesc,paymentProcess.Data.ReturnId,orderId,JsonConvert.SerializeObject(paymentProcess.Data));
                    #region Log
                    //لاگ بعد به روز رسانی قبض اعلام وصول شده
                 
                    #endregion
                }


            }
            catch (Exception ex)
            {

            }

        }
    }
}
