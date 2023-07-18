using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace PecBMS.Helper
{
    public class CustomResponseHeaderMiddleware
    {
        private readonly RequestDelegate _next;

        public CustomResponseHeaderMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            //To add Headers AFTER everything you need to do this
            context.Response.OnStarting(state =>
            {
                var httpContext = (HttpContext)state;
                httpContext.Response.Headers.Add("Strict-Transport-Security", "max-age=31536000");
                httpContext.Response.Headers.Add("X-Content-Type-Options", "nosniff");
                httpContext.Response.Headers.Add("X-Xss-Protection", "1; mode=block");
                httpContext.Response.Headers.Add("X-Frame-Options", "SAMEORIGIN");
                httpContext.Response.Headers.Add("Content-Security-Policy", "...");

                //httpContext.Response.Headers.Remove("X-Powered-By");
                //httpContext.Response.Headers.Remove("X-AspNetMvc-Version");
                //httpContext.Response.Headers.Remove("X-AspNet-Version");
                //httpContext.Response.Headers.Remove("X-AspNetCore-Version");
                //httpContext.Response.Headers.Remove("Server");
                return Task.CompletedTask;
            }, context);

            await _next(context);
        }
    }
}
