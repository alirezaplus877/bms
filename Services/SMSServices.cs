using AutoMapper;
using Dto.Proxy.Request.Naja;
using Dto.Proxy.Request.SMS;
using Dto.repository;
using PEC.CoreCommon.Attribute;
using ProxyService.services;
using System.Threading.Tasks;

namespace Application.Services
{
    [ScopedService]
    public class SMSService : ISMSService
    {
        #region Private Variable
        private readonly IMapper _mapper;
        private readonly ISMSProxy _proxy;
        #endregion

        #region ctor
        public SMSService(IMapper mapper, ISMSProxy proxy)
        {
            _mapper = mapper;
            _proxy = proxy;
        }
        #endregion

        public async Task<long> SendSMS(SMSRequestDto dto)
        {
            var Issend = await _proxy.SendSMS(dto);
            if (Issend > 0)
            {
                return Issend;
            }
            else
            {
                return await _proxy.SendSMS(dto);
            }
        }

    }


    public interface ISMSService
    {
        public Task<long> SendSMS(SMSRequestDto dto);
    }

}
