using AutoMapper;
using Dto.Proxy.Request.SMS;
using Dto.Proxy.Request.Wallet;
using Dto.Proxy.Response.Wallet;
using Dto.Proxy.Wallet;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using PEC.CoreCommon.Attribute;
using Pigi.MDbLogging;
using ProxyService.ProxyModel.Request.SMS;
using ProxyService.ProxyModel.Request.Wallet;
using ProxyService.ProxyModel.Response.Wallet;
using ProxyService.Shared;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Utility;

namespace ProxyService.services
{
    [ScopedService]
    public class SMSProxy : HttpClientBase, ISMSProxy, ILoggable
    {
        #region Private Variables       
        private readonly IMapper _mapper;

        #endregion
        #region Ctor
        public SMSProxy(IHttpClientFactory clientFactoy, IConfiguration configuration, IMapper mapper) : base(clientFactoy, configuration)
        {
            _mapper = mapper;
        }
        #endregion

        [LoggingAspect]
        public async Task<long> SendSMS(SMSRequestDto input)
        {
            //return 1;
            try
            {
                #region Creat proxy model
                var MapModel = _mapper.Map<SMSRequestModel>(input);
                #endregion

                Proxy pclient = this.CreateInstance("SMS");
                ProxySetting setting = pclient.Setting;
                HttpClient httpClient = pclient.httpClient;

                var geturi = setting.uris.TryGetValue("Send", out string Send).ToString();
                var url = setting.host + Send;
                string authParams = string.Format("{0}:{1}", setting.userName, setting.password);
                byte[] bytes = Encoding.UTF8.GetBytes(authParams);
                string encodedAuthParams = Convert.ToBase64String(bytes);
                httpClient.DefaultRequestHeaders.Add("Authorization", "Basic " + encodedAuthParams.ToString());
                StringContent Content = new StringContent(JsonConvert.SerializeObject(MapModel), Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(url, Content);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string content = await response.Content.ReadAsStringAsync();
                    var resultService = JsonConvert.DeserializeObject<long>(content);
                    return resultService;
                }
                else
                {
                    return -1;
                }
            }
            catch
            {
                return -2;
            }
        }
    }

    public interface ISMSProxy
    {
        Task<long> SendSMS(SMSRequestDto input);
    }
}
