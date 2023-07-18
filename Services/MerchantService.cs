using AutoMapper;
using Dto.Proxy.Response;
using Dto.repository;
using Dto.Request;
using Microsoft.Extensions.DependencyInjection;
using PEC.CoreCommon.Attribute;
using Pigi.MDbLogging;
using Repository.reositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utility;

namespace Application.Services
{
    [ScopedService]
    public class MerchantService : IMerchantService, ILoggable
    {
        #region Private Variable
        private readonly IServiceProvider _serviceProvider;
        private readonly IMapper _mapper;
        private readonly IMerchantRepository _merchantRepository;
        private readonly IPecBmsSetting _setting;
        private readonly IMdbLogger<MerchantService> _logger;
        #endregion

        #region ctor
        public MerchantService(IServiceProvider serviceProvider, IMerchantRepository merchantRepository, IMdbLogger<MerchantService> logger,IPecBmsSetting setting)
        {
            _serviceProvider = serviceProvider;
            _mapper = _serviceProvider.GetRequiredService<IMapper>();
            _merchantRepository = merchantRepository;     
            _setting = setting;
            _logger = logger;
        }
        #endregion

        public async Task<ResponseBaseDto<MerchantTopUpDto>> GetMerchantInformation(GetMerchantInformationRequestDto getMerchant)
        {
            try
            {
                var merchantInformation = await _merchantRepository.GetMerchant(getMerchant.Id);
                var merchants = _mapper.Map<MerchantTopUpDto>(merchantInformation);
                if(merchants==null)
                {
                    return new ResponseBaseDto<MerchantTopUpDto>()
                    {
                        Data = null,
                        Message = "اطلاعات پذیرنده یافت نشد",
                        Status = -1
                    };
                }
                return new ResponseBaseDto<MerchantTopUpDto>()
                {
                    Data = merchants,
                    Message = "عملیات موفق",
                    Status = 0
                };

            }
            catch (Exception)
            {

                return new ResponseBaseDto<MerchantTopUpDto>()
                {
                    Data = null,
                    Message = "خطای سیستمی",
                    Status = -99
                };
            }
       
        }

        public async Task<ResponseBaseDto<List<MerchantTopUpBanerDto>>> GetMerchantBaner(GetMerchantBanerRequestDto getMerchantBaner)
        {
            try
            {
                var merchantInformation = await _merchantRepository.GetMerchantBaner(getMerchantBaner.MerchantId);
                if (merchantInformation == null)
                {
                    return new ResponseBaseDto<List<MerchantTopUpBanerDto>>()
                    {
                        Data = null,
                        Message = "بنر های مربوط به پذیرنده یافت نشد",
                        Status = -1
                    };
                }
                var merchants = _mapper.Map<List<MerchantTopUpBanerDto>>(merchantInformation);
                return new ResponseBaseDto<List<MerchantTopUpBanerDto>>()
                {
                    Data = merchants,
                    Message = "عملیات موفق",
                    Status = 0
                };

            }
            catch (Exception)
            {

                return new ResponseBaseDto<List<MerchantTopUpBanerDto>>()
                {
                    Data = null,
                    Message = "خطای سیستمی",
                    Status = -99
                };
            }

        }

    }
    public interface IMerchantService
    {
        Task<ResponseBaseDto<MerchantTopUpDto>> GetMerchantInformation(GetMerchantInformationRequestDto getMerchant);
        Task<ResponseBaseDto<List<MerchantTopUpBanerDto>>> GetMerchantBaner(GetMerchantBanerRequestDto getMerchantBaner);
    }
}
