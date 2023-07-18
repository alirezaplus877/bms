using Data;
using EFCoreSecondLevelCacheInterceptor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PEC.CoreCommon.Security.Encryptor;
using Repository.reositories;
using StackExchange.Redis.Extensions.Core.Configuration;
using StackExchange.Redis.Extensions.Newtonsoft;
using System;

namespace Reepository
{
    public static partial class RegisterRepository
    {
        public static IServiceCollection RegisterDBContext(this IServiceCollection services, IConfiguration configuration)
        {

            services.AddSingleton<IEncryptor, Encryptor>();

            services.AddEFSecondLevelCache(options =>
                options.UseStackExchange(CacheExpirationMode.Absolute, TimeSpan.FromMinutes(15))
               .DisableLogging(false)
            );

            var redisConfiguration = configuration.GetSection("Redis").Get<RedisConfiguration>();
            services.AddStackExchangeRedisExtensions<NewtonsoftSerializer>(redisConfiguration);

            //string connectionString = services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<IEncryptor>().Decrypt(configuration.GetConnectionString("BatchBillConnectionString"));
            //services.AddDbContextPool<Context>((serviceProvider, optionsBuilder) =>
            //       optionsBuilder
            //           .UseSqlServer(
            //               connectionString, t => t.EnableRetryOnFailure())
            //           .AddInterceptors(serviceProvider.GetRequiredService<SecondLevelCacheInterceptor>())
            //           );

            #region TosanSohaDbContext
            string tosanSohaDbContext = services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<IEncryptor>().Decrypt(configuration.GetConnectionString("TosanSohaConnectionString"));
            services.AddDbContextPool<TosanSohaDbContext>((serviceProvider, optionsBuilder) =>
                   optionsBuilder
                       .UseSqlServer(
                           tosanSohaDbContext, t => t.EnableRetryOnFailure())
                       .AddInterceptors(serviceProvider.GetRequiredService<SecondLevelCacheInterceptor>())
                       );
            #endregion

            #region WalletDbContext
            string walleConnectionString = services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<IEncryptor>().Decrypt(configuration.GetConnectionString("WalletConnectionString"));
            services.AddDbContextPool<WalletContext>((serviceProvider, optionsBuilder) =>
                   optionsBuilder
                       .UseSqlServer(
                           walleConnectionString, t => t.EnableRetryOnFailure())
                       .AddInterceptors(serviceProvider.GetRequiredService<SecondLevelCacheInterceptor>())
                       );
            #endregion

            #region TollBillDbContext
           // string tollBillConnectionString = services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<IEncryptor>().Decrypt(configuration.GetConnectionString("TollBillConnectionString"));
            //services.AddDbContextPool<TollBillContext>((serviceProvider, optionsBuilder) =>
            //       optionsBuilder
            //           .UseSqlServer(
            //               tollBillConnectionString, t => t.EnableRetryOnFailure())
            //           .AddInterceptors(serviceProvider.GetRequiredService<SecondLevelCacheInterceptor>())
            //           );
            #endregion

            #region MerchantConnectionString
            string merchantConnectionString = services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<IEncryptor>().Decrypt(configuration.GetConnectionString("MerchantConnectionString"));
            services.AddDbContextPool<MerchantContext>((serviceProvider, optionsBuilder) =>
                   optionsBuilder
                       .UseSqlServer(
                           merchantConnectionString, t => t.EnableRetryOnFailure())
                       .AddInterceptors(serviceProvider.GetRequiredService<SecondLevelCacheInterceptor>())
                       );
            #endregion


            #region PgwDbContextConnectionString
            string PgwDbConnectionString = services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<IEncryptor>().Decrypt(configuration.GetConnectionString("PgwDbConnectionString"));
            services.AddDbContextPool<PgwDbContext>((serviceProvider, optionsBuilder) =>
                   optionsBuilder
                       .UseSqlServer(
                           PgwDbConnectionString, t => t.EnableRetryOnFailure())
                       .AddInterceptors(serviceProvider.GetRequiredService<SecondLevelCacheInterceptor>())
                       );
            #endregion

            #region PaymentDbContextConnectionString
            string PaymentDbConnectionString = services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<IEncryptor>().Decrypt(configuration.GetConnectionString("PaymentDbConnectionString"));
            services.AddDbContextPool<PaymentDbContext>((serviceProvider, optionsBuilder) =>
                   optionsBuilder
                       .UseSqlServer(
                           PaymentDbConnectionString, t => t.EnableRetryOnFailure())
                       .AddInterceptors(serviceProvider.GetRequiredService<SecondLevelCacheInterceptor>())
                       );
            #endregion

            ////services.AddTransient<DbContext, Context>();
            //services.AddTransient<DbContext, TosanSohaDbContext>();
            //services.AddTransient<DbContext, WalletContext>();
            ////services.AddTransient<DbContext, TollBillContext>();
            //services.AddTransient<DbContext, PgwDbContext>();
            //services.AddTransient<DbContext, PaymentDbContext>();
            ////services.AddScoped<INajaRepository, NajaRepository>();
                

            services.AddTransient(typeof(Repository.IGenericRepository<,>), typeof(Repository.GenericRepository<,>));
            return services;
        }
    }
}
