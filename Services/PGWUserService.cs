using AutoMapper;
using Dto.Proxy.Response;
using Dto.repository;
using Dto.Request;
using Entities;
using Microsoft.Extensions.DependencyInjection;
using PEC.CoreCommon.Attribute;
using Pigi.MDbLogging;
using Repository.reositories;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Utility;

namespace Application.Services
{
    [ScopedService]
    public class PGWUserService : IPGWUserService, ILoggable
    {
        #region Private Variable
        private readonly IServiceProvider _serviceProvider;
        private readonly IMapper _mapper;
        private readonly IPgwDbRepository _pgwDbRepository;
        private readonly IPecBmsSetting _setting;
        private readonly IMdbLogger<MerchantService> _logger;
        #endregion

        #region ctor
        public PGWUserService(IServiceProvider serviceProvider, IPgwDbRepository pgwDbRepository, IMdbLogger<MerchantService> logger, IPecBmsSetting setting)
        {
            _serviceProvider = serviceProvider;
            _mapper = _serviceProvider.GetRequiredService<IMapper>();
            _pgwDbRepository = pgwDbRepository;
            _setting = setting;
            _logger = logger;
        }
        #endregion
        public async Task<UserDto> GetUserByIdAsync(long userId)
        {
            //var userFilter = _mapper.Map<Expression<Func<User, bool>>>(predicate);
            var retrive = await _pgwDbRepository.GetUserByIdAsync(userId);
            var mapped = _mapper.Map<UserDto>(retrive);
            return mapped;
        }


    }
    public interface IPGWUserService
    {
        Task<UserDto> GetUserByIdAsync(long userId);
    }
}
