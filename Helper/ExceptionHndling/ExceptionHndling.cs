using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using PEC.CoreCommon.ServiceActivator;
using PecBMS.ViewModel;
using Pigi.MDbLogging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace PecBMS.Helper.ExceptionHndling
{
    public class ExceptionLogger : ILoggable
    {
    }
    public static class ExceptionMiddlewareExtensions
    {
        public static void ConfigureExceptionHandler(this IApplicationBuilder app)
        {
            var Logger = ServiceActivator.GetScope().ServiceProvider.GetRequiredService<IMdbLogger<ExceptionLogger>>();
            app.UseExceptionHandler(appError =>
            {
                appError.Run(async context =>
                {
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    context.Response.ContentType = "application/json";
                    var contextFeature = context.Features.Get<IExceptionHandlerFeature>();
                    if (contextFeature != null)
                    {
                        //logger.LogError($"Something went wrong: {contextFeature.Error}");
                        Logger.Log(null, $"Something went wrong: {contextFeature.Error}", null, contextFeature.Error);
                        await context.Response.WriteAsync(new ErrorDetails()
                        {
                            StatusCode = context.Response.StatusCode,
                            Message = string.IsNullOrEmpty(contextFeature.Error.Message) ? "خطای داخلی سرور" : contextFeature.Error.Message
                        }.ToString());
                    }
                });
            });
        }
    }
}
