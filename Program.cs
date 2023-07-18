using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using PEC.CoreCommon.ServiceActivator;
using Pigi.MDbLogging;
using Utility;
using ProxyService.Shared;
using Application;
using Common;

namespace AutoBillPaymentService
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

                       services.AddHostedService<AutoBillPaymentWorker>();


                   }).UseWindowsService();
    }
}
