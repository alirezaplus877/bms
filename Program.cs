using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using PEC.CoreCommon.ServiceActivator;
using Pigi.MDbLogging;
using AutoMapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Utility;
using ProxyService.Shared;
using Application;
using Application.Services;
using Common;

namespace WalletBillPaymentService
{
    public class Program
    {
        public static void Main(string[] args)
        {

            var scope = CreateHostBuilder(args).Build();
            scope.Services.UsePigiLoggerStandard();
            ServiceActivator.Configure(scope.Services);
            scope.Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                   .ConfigureServices((hostContext, services) =>
               {
                   services.AddAutoMapper(typeof(Program));
                   services.RegisterApplication(hostContext.Configuration);
                   services.RegisterCommon(hostContext.Configuration);
                   services.AddPigiLogger(hostContext.Configuration);                    
                   services.RegisterHttpClientServices(hostContext.Configuration);
                   services.AddSingleton<IPecBmsSetting, PecBmsSetting>(e => hostContext.Configuration.GetSection("BMSSetting").Get<PecBmsSetting>());

                   services.AddHostedService<WalletBillPaymentWorker>();


               }).UseWindowsService();
    }
}
