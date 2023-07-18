using Application;
using AspNetCoreRateLimit;
using Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.PlatformAbstractions;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PEC.CoreCommon.Security.Authorization;
using PEC.CoreCommon.SecurityMiddleware;
using PEC.CoreCommon.ServiceActivator;
using PecBMS.Helper;
using PecBMS.Helper.ExceptionHndling;
using PecBMS.Helper.Filter;
using PecBMS.Helper.Identity;
using PecBMS.Model;
using PecBMS.ViewModel;
using Pigi.MDbLogging;
using ProxyService.Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Utility;

namespace PecBMS
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }
        public string SpecificOrigins { get; set; } = "_SpecificOrigins";
        public IConfiguration Configuration { get; }
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddAntiforgery(o => o.SuppressXFrameOptionsHeader = true);
            services.AddControllers();
            services.AddSwaggerGen(opt =>
             {

                 opt.SwaggerDoc("v1", new OpenApiInfo { Title = "PecBms", Version = "v1.01" });
                 var basePath = PlatformServices.Default.Application.ApplicationBasePath;
                 var xmlPath = Path.Combine(basePath, "PecBms.xml");
                 opt.IncludeXmlComments(xmlPath);

                 opt.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                 {
                     In = ParameterLocation.Header,
                     Description = "لطفا توکن را وارد نمایید",
                     Name = "Authorization",
                     Type = SecuritySchemeType.Http,
                     BearerFormat = "JWT",
                     Scheme = "bearer"
                 });
                 opt.AddSecurityRequirement(new OpenApiSecurityRequirement
                        {
                            {
                                new OpenApiSecurityScheme
                                {
                                    Reference = new OpenApiReference
                                    {
                                        Type=ReferenceType.SecurityScheme,
                                        Id="Bearer"
                                    }
                                },
                                new string[]{}
                            }
                        });
                 //opt.SchemaFilter<EnumTypesSchemaFilter>(xmlPath);

                 //opt.SchemaFilter<EnumDictionaryToStringDictionarySchemaFilter>();
                 opt.DocumentFilter<SwaggerDocumentFilter<ServiceStatus>>();
                 //opt.DocumentFilter<EnumTypesDocumentFilter>();

             });

            #region Log     

            services.AddPigiLogger(Configuration);
            #endregion

            #region AutoMapper
            services.AddAutoMapper(typeof(Startup));
            #endregion


            services.AddMemoryCache();

            #region Register service
            services.RegisterApplication(Configuration);
            #endregion

            #region Repository dependeny
            services.RegisterCommon(Configuration);
            #endregion

            #region CookiePolicyOptions
            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = c => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });
            #endregion
            services.RegisterHttpClientServices(Configuration);

            #region RatingLimited

            services.RegisterRateLimiting(Configuration);

            #endregion


            #region [- AddCors -]
            services.AddCors();
            #endregion

            #region MVC
            services.AddMvc(config =>
            {
                config.Filters.Add(typeof(ValidateModelFilter));
                //config.Filters.Add(typeof(ClaimBaseAuthorizeFilter));
            }).AddNewtonsoftJson()
            .ConfigureApiBehaviorOptions(options =>
            {
                options.InvalidModelStateResponseFactory = actionContext =>
                {
                    return CustomErrorResponse(actionContext);
                };
            });

            #endregion

            #region Setting
            services.AddSingleton<IPecBmsSetting, PecBmsSetting>(e => Configuration.GetSection("BMSSetting").Get<PecBmsSetting>());
            #endregion

            services.AddSingleton<IAuthorizationHandler, IsPecBmsUser>();

            services.AddAuthentication("Bearer")
                .AddJwtBearer("Bearer", options =>
                {
                    options.Authority = "https://sts.pec.ir";
                    //options.Authority = "https://prelivempl.pec.ir/sts";
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateAudience = false
                    };
                });
            services.AddAuthorization(o =>
            {
                o.AddPolicy("PecBMS", policyBuilder =>
                    policyBuilder.AddRequirements(
                        new IsPecBmsUserEnabledRequirement()
                    ));
            });

            services.AddTransient<IHttpContextAccessor, HttpContextAccessor>();

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            ServiceActivator.Configure(app.ApplicationServices);
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();

                app.UseSwaggerUI(options =>
                {
                    options.SwaggerEndpoint("/swagger/v1/swagger.json", "DebugMode Api");
                    options.RoutePrefix = string.Empty;
                });
            }
            else
            {
                app.UseSwagger();

                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/pecbms/swagger/v1/swagger.json", "PecBms API");
                    c.RoutePrefix = "swagger";
                });
            }
            app.UseIpRateLimiting();

            //app.UseCheckIP(new List<string> {"172.30.2.90","172.30.2.155","192.168.10.182","127.0.0.1"});
            app.UseHttpsRedirection();
            app.UsePigiLogger();
            app.UseStaticFiles();
            app.UseCookiePolicy();
            app.UseRouting();
            app.UseCors(x => x.AllowAnyOrigin()
                              .WithOrigins()
                              .AllowAnyHeader()
                              .WithMethods("GET", "POST"));
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            #region 500 Error Handling
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.ConfigureExceptionHandler();
            #endregion

            #region 404 NotFound Exception Handling
            app.Use(async (context, next) =>
            {
                await next();
                if (context.Response.StatusCode == (int)HttpStatusCode.NotFound)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(new ErrorDetails()
                    {
                        StatusCode = context.Response.StatusCode,
                        Message = "آدرس مورد نظر یافت نشد"
                    }.ToString());
                }
            });
            #endregion

            app.UseMiddleware(typeof(CustomResponseHeaderMiddleware));

        }

        private BadRequestObjectResult CustomErrorResponse(ActionContext actionContext)
        {
            //BadRequestObjectResult is class found Microsoft.AspNetCore.Mvc and is inherited from ObjectResult.    
            //Rest code is linq.    

            var errosData = actionContext.ModelState
             .Where(modelError => modelError.Value.Errors.Count > 0)
             .Select(modelError => new Error
             {
                 ErrorField = modelError.Key,
                 ErrorDescription = modelError.Value.Errors.FirstOrDefault().ErrorMessage
             }).ToList();
            var errors = new MessageModel<List<Error>>
            {
                message = "خطا در پارامترهای ورودی",
                status = 400,
                Data = errosData
            };
            return new BadRequestObjectResult(errors);
        }

    }
    public class Error
    {
        public string ErrorField { get; set; }
        public string ErrorDescription { get; set; }
    }
}

