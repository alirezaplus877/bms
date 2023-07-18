using AutoMapper;
using Dto.Proxy.Request.Naja;
using Dto.Proxy.Response.Naja;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using PEC.CoreCommon.Attribute;
using Pigi.MDbLogging;
using ProxyService.ProxyModel.Request.Naja;
using ProxyService.Shared;
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Serialization;

namespace ProxyService.services
{
    [ScopedService]
    public class NajaServiceProxy : HttpClientBase, INajaServiceProxy, ILoggable
    {

        #region Private Variables  
        private readonly IMapper _mapper;
        private readonly IMdbLogger<NajaServiceProxy> _logger;
        public ITokenServcie TokenServcie { get; }

        #endregion

        #region Ctor
        public NajaServiceProxy(IHttpClientFactory clientFactoy, IMdbLogger<NajaServiceProxy> logger, IConfiguration configuration, IMapper mapper, ITokenServcie tokenServcie) : base(clientFactoy, configuration)
        {
            _mapper = mapper;
            _logger = logger;
            TokenServcie = tokenServcie;
        }
        #endregion

        public async Task<GetTokenResponseDto> _GetNajaToken_Old()
        {
            try
            {
                Proxy pclient = this.CreateInstance("StsPecBMS");
                ProxySetting setting = pclient.Setting;
                HttpClient httpClient = pclient.httpClient;


                var grant_typeuri = setting.uris.TryGetValue("grant_type", out string grant_type).ToString();
                var audienceuri = setting.uris.TryGetValue("audience", out string audience).ToString();
                var scopeuri = setting.uris.TryGetValue("scope", out string scope).ToString();
                var client_iduri = setting.uris.TryGetValue("client_id", out string client_id).ToString();
                var client_secreturi = setting.uris.TryGetValue("client_secret", out string client_secret).ToString();

                if (string.IsNullOrEmpty(grant_type)
                    && string.IsNullOrEmpty(audience)
                    && string.IsNullOrEmpty(scope)
                    && string.IsNullOrEmpty(client_id)
                    && string.IsNullOrEmpty(client_secret))
                {
                    return null;
                }

                var nvc = new List<KeyValuePair<string, string>>
                    {
                        new KeyValuePair<string, string>("grant_type", grant_type),
                        new KeyValuePair<string, string>("username", setting.userName),
                        new KeyValuePair<string, string>("password", setting.password),
                        new KeyValuePair<string, string>("audience", audience),
                        new KeyValuePair<string, string>("scope", scope),
                        new KeyValuePair<string, string>("client_id", client_id),
                        new KeyValuePair<string, string>("client_secret", client_secret)
                    };

                var req = new HttpRequestMessage(HttpMethod.Post, setting.host) { Content = new FormUrlEncodedContent(nvc) };
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded"));
                //req.Headers.Add("content-type", "application/x-www-form-urlencoded");

                var response = await httpClient.SendAsync(req);


                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string content = await response.Content.ReadAsStringAsync();


                    var data = JsonConvert.DeserializeObject<GetTokenResponseDto>(content);
                    if (!string.IsNullOrWhiteSpace(data.Access_token))
                    {
                        return data;
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    return null;
                }
            }
            catch (Exception e)
            {

                return null;
            }
        }

        [LoggingAspect]
        public async Task<NajaResponseDtoGeneric<object>> ViolationInquiry(ViolationInquiryRequestDto reqDto)
        {
            var StsToken = await TokenServcie.FetchToken();
            if (!string.IsNullOrEmpty(StsToken))
            {
                try
                {

                    #region create ProxyModel
                    var RequstModel = _mapper.Map<ViolationInquiryRequestModel>(reqDto);
                    #endregion

                    Proxy pclient = this.CreateInstance("NajaServices");
                    ProxySetting setting = pclient.Setting;
                    HttpClient httpClient = pclient.httpClient;
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(scheme: "Bearer", parameter: StsToken);
                    var geturi = setting.uris.TryGetValue("ViolationInquiry", out string ViolationInquiry).ToString();
                    var url = setting.host + ViolationInquiry;

                    StringContent Content = new StringContent(JsonConvert.SerializeObject(RequstModel), Encoding.UTF8, "application/json");
                    var response = await httpClient.PostAsync(url, Content);

                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        string content = await response.Content.ReadAsStringAsync();

                        _logger.Log(reqDto.TrackingNo, "proxy resp ok ViolationInquiry => " + JsonConvert.SerializeObject(content), null);

                        var data = JsonConvert.DeserializeObject<NajaResponseDtoGeneric<object>>(content,
                            new JsonSerializerSettings()
                            {
                                NullValueHandling = NullValueHandling.Ignore,
                                Error = delegate (object sender, ErrorEventArgs args)
                                {
                                    _logger.Log(reqDto.TrackingNo, "proxy JsonSerializerSettings EX ViolationInquiry => " + JsonConvert.SerializeObject(args), sender, null);
                                }
                            });

                        if (data.Status == 0)
                        {
                            var Response = _mapper.Map<ViolationInquiryResponseDto>(data.Data);
                            return (new NajaResponseDtoGeneric<object>
                            {
                                Message = data.Message,
                                Status = 0,
                                Data = Response
                            });
                        }
                        else
                        {
                            return (new NajaResponseDtoGeneric<object>
                            {
                                Message = data.Message,
                                Status = data.Status,
                                Data = null
                            });
                        }
                    }
                    else
                    {
                        _logger.Log(reqDto.TrackingNo, "proxy resp not ok ViolationInquiry => " + JsonConvert.SerializeObject(response.Content.ToString()), null);

                        return (new NajaResponseDtoGeneric<object>
                        {
                            Message = "خطای ارتباط با سرور",
                            Status = -1,
                            Data = null
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log(reqDto.TrackingNo, "proxy ex ViolationInquiry => " + ex.Message, ex);

                    return (new NajaResponseDtoGeneric<object>
                    {
                        Message = "خطای سیستمی",
                        Status = -99,
                        Data = null
                    });
                }
            }
            else
            {
                return (new NajaResponseDtoGeneric<object>
                {
                    Message = "خطا در احراز هویت STS برای دریافت توکن",
                    Status = -99,
                    Data = null

                });
            }

        }

        [LoggingAspect]
        public async Task<NajaResponseDtoGeneric<CheckInfoResponseDto>> CheckInfo(CheckInfoRequestDto dto)
        {
            var StsToken = await TokenServcie.FetchToken();
            if (!string.IsNullOrEmpty(StsToken))
            {
                try
                {

                    #region create ProxyModel
                    var RequstModel = _mapper.Map<CheckInfoRequestModel>(dto);
                    #endregion

                    Proxy pclient = this.CreateInstance("NajaServices");
                    ProxySetting setting = pclient.Setting;
                    HttpClient httpClient = pclient.httpClient;
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                        scheme: "Bearer", parameter: StsToken);
                    var geturi = setting.uris.TryGetValue("CheckInfo", out string CheckInfo).ToString();
                    var url = setting.host + CheckInfo;

                    StringContent Content = new StringContent(
                        JsonConvert.SerializeObject(RequstModel), Encoding.UTF8, "application/json");
                    var response = await httpClient.PostAsync(url, Content);


                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        string content = await response.Content.ReadAsStringAsync();
                        _logger.Log(224, "proxy resp ok CheckInfo => " + JsonConvert.SerializeObject(content), null);

                        var data = JsonConvert.DeserializeObject<NajaResponseDtoGeneric<CheckInfoResponseDto>>(content,
                           new JsonSerializerSettings()
                           {
                               NullValueHandling = NullValueHandling.Ignore,
                               Error = delegate (object sender, ErrorEventArgs args)
                               {
                                   _logger.Log(243, "proxy JsonSerializerSettings EX CheckInfo => " + JsonConvert.SerializeObject(args), sender, null);
                               }
                           });
                        if (data.Status == 0)
                        {
                            return data;
                        }
                        else
                        {
                            return (new NajaResponseDtoGeneric<CheckInfoResponseDto>
                            {
                                Message = data.Message,
                                Status = data.Status,
                                Data = null
                            });
                        }
                    }
                    else
                    {
                        _logger.Log(262, "proxy resp not ok CheckInfo => " + JsonConvert.SerializeObject(response.Content.ToString()), null);

                        return (new NajaResponseDtoGeneric<CheckInfoResponseDto>
                        {
                            Message = "خطای ارتباط با سرور",
                            Status = -1,
                            Data = null
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log(273, "proxy ex ViolationInquiry => " + ex.Message, ex);

                    return (new NajaResponseDtoGeneric<CheckInfoResponseDto>
                    {
                        Message = "خطای سیستمی",
                        Status = -99,
                        Data = null
                    });
                }
            }
            else
            {
                return (new NajaResponseDtoGeneric<CheckInfoResponseDto>
                {
                    Message = "خطا در احراز هویت STS برای دریافت توکن",
                    Status = -99,
                    Data = null
                });
            }
        }

        [LoggingAspect]
        public async Task<NajaResponseDtoGeneric<AccumulationViolationsInquiryResponseDto>> AccumulationViolationsInquiry(AccumulationViolationsInquiryRequestDto reqDto)
        {

            var StsToken = await TokenServcie.FetchToken();
            if (!string.IsNullOrEmpty(StsToken))
            {
                try
                {
                    #region create ProxyModel
                    var RequstModel = _mapper.Map<AccumulationViolationsInquiryRequestModel>(reqDto);
                    #endregion

                    Proxy pclient = this.CreateInstance("NajaServices");
                    ProxySetting setting = pclient.Setting;
                    HttpClient httpClient = pclient.httpClient;
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(scheme: "Bearer", parameter: StsToken);
                    //httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {StsToken.Access_token}");

                    var geturi = setting.uris.TryGetValue("AccumulationViolationsInquiry", out string AccumulationViolationsInquiry).ToString();
                    var url = setting.host + AccumulationViolationsInquiry;

                    _logger.Log(reqDto.TrackingNo, "proxy AccumulationViolationsInquiry req =>" + JsonConvert.SerializeObject(RequstModel), null);

                    StringContent Content = new StringContent(JsonConvert.SerializeObject(RequstModel), Encoding.UTF8, "application/json");
                    var response = await httpClient.PostAsync(url, Content);

                    _logger.Log(reqDto.TrackingNo, "proxy AccumulationViolationsInquiry resp =>" + JsonConvert.SerializeObject(response), null);

                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        string content = await response.Content.ReadAsStringAsync();

                        _logger.Log(reqDto.TrackingNo, "proxy AccumulationViolationsInquiry cont2 =>" + JsonConvert.SerializeObject(content), null);

                        var data = JsonConvert.DeserializeObject<NajaResponseDtoGeneric<AccumulationViolationsInquiryResponseDto>>(content,
                           new JsonSerializerSettings()
                           {
                               NullValueHandling = NullValueHandling.Ignore,
                               Error = delegate (object sender, ErrorEventArgs args)
                               {
                                   _logger.Log(242, "proxy JsonSerializerSettings EX AccumulationViolationsInquiry => " + JsonConvert.SerializeObject(args), sender, null);
                               }
                           });
                        if (data.Status == 0)
                        {
                            var Response = _mapper.Map<AccumulationViolationsInquiryResponseDto>(data.Data);
                            return (new NajaResponseDtoGeneric<AccumulationViolationsInquiryResponseDto>
                            {
                                Message = data.Message,
                                Status = 0,
                                Data = Response
                            });
                        }
                        else
                        {
                            return (new NajaResponseDtoGeneric<AccumulationViolationsInquiryResponseDto>
                            {
                                Message = data.Message,
                                Status = data.Status,
                                Data = null

                            });
                        }
                    }
                    else
                    {
                        return (new NajaResponseDtoGeneric<AccumulationViolationsInquiryResponseDto>
                        {
                            Message = "خطای ارتباط با سرور",
                            Status = -1,
                            Data = null

                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log(reqDto.TrackingNo, "proxy ex AccumulationViolationsInquiry => " + ex.Message, ex);

                    return (new NajaResponseDtoGeneric<AccumulationViolationsInquiryResponseDto>
                    {
                        Message = "خطای سیستمی",
                        Status = -99,
                        Data = null

                    });
                }
            }
            else
            {
                return (new NajaResponseDtoGeneric<AccumulationViolationsInquiryResponseDto>
                {
                    Message = "خطا در احراز هویت STS برای دریافت توکن",
                    Status = -99,
                    Data = null

                });
            }
        }

        [LoggingAspect]
        public async Task<NajaResponseDtoGeneric<List<ActivePlakInquiryResponseDto>>> ActivePlakInquiry(ActivePlakInquiryRequestDto reqDto)
        {
            var StsToken = await TokenServcie.FetchToken();
            if (!string.IsNullOrEmpty(StsToken))
            {
                try
                {

                    #region create ProxyModel
                    var RequstModel = _mapper.Map<ActivePlakInquiryRequestModel>(reqDto);
                    #endregion

                    Proxy pclient = this.CreateInstance("NajaServices");
                    ProxySetting setting = pclient.Setting;
                    HttpClient httpClient = pclient.httpClient;
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(scheme: "Bearer", parameter: StsToken);


                    string ActivePlakInquiry = "";
                    var geturi = setting.uris.TryGetValue("ActivePlakInquiry", out ActivePlakInquiry).ToString();
                    var url = setting.host + ActivePlakInquiry;

                    _logger.Log(reqDto.TrackingNo, "proxy ActivePlakInquiry req =>" + JsonConvert.SerializeObject(RequstModel), null);

                    StringContent Content = new StringContent(JsonConvert.SerializeObject(RequstModel), Encoding.UTF8, "application/json");

                    var response = await httpClient.PostAsync(url, Content);

                    _logger.Log(reqDto.TrackingNo, "proxy ActivePlakInquiry resp =>" + JsonConvert.SerializeObject(response), null);

                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        string content = await response.Content.ReadAsStringAsync();

                        _logger.Log(reqDto.TrackingNo, "proxy ActivePlakInquiry cont2 =>" + JsonConvert.SerializeObject(content), null);

                        var data = JsonConvert.DeserializeObject<NajaResponseDtoGeneric<List<ActivePlakInquiryResponseDto>>>(content,
                            new JsonSerializerSettings()
                            {
                                NullValueHandling = NullValueHandling.Ignore,
                                Error = delegate (object sender, ErrorEventArgs args)
                                {
                                    _logger.Log(reqDto.TrackingNo, "proxy JsonSerializerSettings EX ActivePlakInquiry => " + JsonConvert.SerializeObject(args), sender, null);
                                }
                            });

                        if (data.Status == 0)
                        {
                            var Response = _mapper.Map<List<ActivePlakInquiryResponseDto>>(data.Data);
                            return (new NajaResponseDtoGeneric<List<ActivePlakInquiryResponseDto>>
                            {
                                Message = data.Message,
                                Status = 0,
                                Data = Response
                            });
                        }
                        else
                        {
                            return (new NajaResponseDtoGeneric<List<ActivePlakInquiryResponseDto>>
                            {
                                Message = data.Message,
                                Status = data.Status,
                                Data = null
                            });
                        }
                    }
                    else
                    {
                        _logger.Log(reqDto.TrackingNo, "proxy ActivePlakInquiry else =>" + response.StatusCode, null);

                        return (new NajaResponseDtoGeneric<List<ActivePlakInquiryResponseDto>>
                        {
                            Message = "خطای ارتباط با سرور",
                            Status = -1,
                            Data = null
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log(reqDto.TrackingNo, "proxy ex ActivePlakInquiry => " + ex.Message, ex);

                    return (new NajaResponseDtoGeneric<List<ActivePlakInquiryResponseDto>>
                    {
                        Message = "خطای سیستمی",
                        Status = -99,
                        Data = null
                    });
                }
            }
            else
            {
                return (new NajaResponseDtoGeneric<List<ActivePlakInquiryResponseDto>>
                {
                    Message = "خطا در احراز هویت STS برای دریافت توکن",
                    Status = -99,
                    Data = null

                });
            }

        }

        [LoggingAspect]
        public async Task<NajaResponseDtoGeneric<CardSanadInquiryResponseDto>> CardSanadInquiry(CardSanadInquiryRequestDto reqDto)
        {
            try
            {

                #region create ProxyModel
                var RequstModel = _mapper.Map<CardSanadInquiryRequestModel>(reqDto);
                #endregion

                Proxy pclient = this.CreateInstance("NajaServices");
                ProxySetting setting = pclient.Setting;
                HttpClient httpClient = pclient.httpClient;

                var geturi = setting.uris.TryGetValue("CardSanadInquiry", out string CardSanadInquiry).ToString();
                var url = setting.host + CardSanadInquiry;

                StringContent Content = new StringContent(JsonConvert.SerializeObject(RequstModel), Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(url, Content);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string content = await response.Content.ReadAsStringAsync();
                    _logger.Log(reqDto.TrackingNo, "proxy resp ok CardSanadInquiry => " + JsonConvert.SerializeObject(content), null);

                    var data = JsonConvert.DeserializeObject<NajaResponseDtoGeneric<CardSanadInquiryRequestDto>>(content,
                         new JsonSerializerSettings()
                         {
                             NullValueHandling = NullValueHandling.Ignore,
                             Error = delegate (object sender, ErrorEventArgs args)
                             {
                                 _logger.Log(reqDto.TrackingNo, "proxy JsonSerializerSettings EX CardSanadInquiry => " + JsonConvert.SerializeObject(args), sender, null);
                             }
                         });
                    if (data.Status == 0)
                    {
                        var Response = _mapper.Map<CardSanadInquiryResponseDto>(data.Data);
                        return (new NajaResponseDtoGeneric<CardSanadInquiryResponseDto>
                        {
                            Message = data.Message,
                            Status = 0,
                            Data = Response
                        });
                    }
                    else
                    {
                        return (new NajaResponseDtoGeneric<CardSanadInquiryResponseDto>
                        {
                            Message = data.Message,
                            Status = data.Status,
                            Data = null
                        });
                    }
                }
                else
                {
                    _logger.Log(reqDto.TrackingNo, "proxy resp not ok CardSanadInquiry => " + JsonConvert.SerializeObject(response.Content.ToString()), null);

                    return (new NajaResponseDtoGeneric<CardSanadInquiryResponseDto>
                    {
                        Message = "خطای ارتباط با سرور",
                        Status = -1,
                        Data = null
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.Log(reqDto.TrackingNo, "proxy ex CardSanadInquiry => " + ex.Message, ex);
                return (new NajaResponseDtoGeneric<CardSanadInquiryResponseDto>
                {
                    Message = "خطای سیستمی",
                    Status = -99,
                    Data = null
                });
            }
        }

        [LoggingAspect]
        public async Task<NajaResponseDtoGeneric<ExitTaxesReceiptResponseDto>> ExitTaxesReceipt(ExitTaxesReceiptRequestDto reqDto)
        {
            try
            {

                #region create ProxyModel
                var RequstModel = _mapper.Map<ExitTaxesReceiptRequestModel>(reqDto);
                #endregion

                Proxy pclient = this.CreateInstance("NajaServices");
                ProxySetting setting = pclient.Setting;
                HttpClient httpClient = pclient.httpClient;

                var geturi = setting.uris.TryGetValue("ExitTaxesReceipt", out string ExitTaxesReceipt).ToString();
                var url = setting.host + ExitTaxesReceipt;

                StringContent Content = new StringContent(JsonConvert.SerializeObject(RequstModel), Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(url, Content);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string content = await response.Content.ReadAsStringAsync();


                    var data = JsonConvert.DeserializeObject<NajaResponseDtoGeneric<ExitTaxesReceiptRequestDto>>(content,
                            new JsonSerializerSettings()
                            {
                                NullValueHandling = NullValueHandling.Ignore,
                                Error = delegate (object sender, ErrorEventArgs args) { _logger.Log(242, "proxy JsonSerializerSettings EX ExitTaxesReceipt => " + JsonConvert.SerializeObject(args), sender, null); }
                            });
                    if (data.Status == 0)
                    {
                        var Response = _mapper.Map<ExitTaxesReceiptResponseDto>(data.Data);
                        return (new NajaResponseDtoGeneric<ExitTaxesReceiptResponseDto>
                        {
                            Message = data.Message,
                            Status = 0,
                            Data = Response
                        });
                    }
                    else
                    {
                        return (new NajaResponseDtoGeneric<ExitTaxesReceiptResponseDto>
                        {
                            Message = data.Message,
                            Status = data.Status,
                            Data = null
                        });
                    }
                }
                else
                {
                    return (new NajaResponseDtoGeneric<ExitTaxesReceiptResponseDto>
                    {
                        Message = "خطای ارتباط با سرور",
                        Status = -1,
                        Data = null
                    });
                }
            }
            catch (Exception)
            {

                return (new NajaResponseDtoGeneric<ExitTaxesReceiptResponseDto>
                {
                    Message = "خطای سیستمی",
                    Status = -99,
                    Data = null
                });
            }
        }

        [LoggingAspect]
        public async Task<NajaResponseDtoGeneric<LicenseNegativePointInquiryResponseDto>> LicenseNegativePointInquiry(LicenseNegativePointInquiryRequestDto reqDto)
        {
            _logger.Log(reqDto.TrackingNo, "proxy LicenseNegativePointInquiry enter =>" + JsonConvert.SerializeObject(reqDto), null);

            var StsToken = await TokenServcie.FetchToken();
            if (!string.IsNullOrEmpty(StsToken))
            {
                try
                {

                    #region create ProxyModel
                    var RequstModel = _mapper.Map<LicenseNegativePointInquiryRequestModel>(reqDto);
                    #endregion

                    Proxy pclient = this.CreateInstance("NajaServices");
                    ProxySetting setting = pclient.Setting;
                    HttpClient httpClient = pclient.httpClient;
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(scheme: "Bearer", parameter: StsToken);
                    var geturi = setting.uris.TryGetValue("LicenseNegativePointInquiry", out string LicenseNegativePointInquiry).ToString();
                    var url = setting.host + LicenseNegativePointInquiry;

                    _logger.Log(reqDto.TrackingNo, "proxy LicenseNegativePointInquiry req =>" + JsonConvert.SerializeObject(RequstModel), null);

                    StringContent Content = new StringContent(JsonConvert.SerializeObject(RequstModel), Encoding.UTF8, "application/json");
                    var response = await httpClient.PostAsync(url, Content);

                    _logger.Log(reqDto.TrackingNo, "proxy LicenseNegativePointInquiry resp =>" + JsonConvert.SerializeObject(response), null);

                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        string content = await response.Content.ReadAsStringAsync();

                        _logger.Log(reqDto.TrackingNo, "proxy LicenseNegativePointInquiry cont2 =>" + JsonConvert.SerializeObject(content), null);

                        var data = JsonConvert.DeserializeObject<NajaResponseDtoGeneric<LicenseNegativePointInquiryResponseDto>>(content,
                            new JsonSerializerSettings()
                            {
                                NullValueHandling = NullValueHandling.Ignore,
                                Error = delegate (object sender, ErrorEventArgs args) { _logger.Log(reqDto.TrackingNo, "proxy JsonSerializerSettings EX LicenseNegativePointInquiry => " + JsonConvert.SerializeObject(args), sender, null); }
                            });
                        if (data.Status == 0)
                        {
                            var Response = _mapper.Map<LicenseNegativePointInquiryResponseDto>(data.Data);
                            return (new NajaResponseDtoGeneric<LicenseNegativePointInquiryResponseDto>
                            {
                                Message = data.Message,
                                Status = 0,
                                Data = Response
                            });
                        }
                        else
                        {
                            return (new NajaResponseDtoGeneric<LicenseNegativePointInquiryResponseDto>
                            {
                                Message = data.Message,
                                Status = data.Status,
                                Data = null
                            });
                        }
                    }
                    else
                    {
                        _logger.Log(reqDto.TrackingNo, "proxy LicenseNegativePointInquiry else =>" + response.StatusCode, null);

                        return (new NajaResponseDtoGeneric<LicenseNegativePointInquiryResponseDto>
                        {
                            Message = "خطای ارتباط با سرور",
                            Status = -1,
                            Data = null
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log(reqDto.TrackingNo, "proxy ex LicenseNegativePointInquiry => " + ex.Message, ex);

                    return (new NajaResponseDtoGeneric<LicenseNegativePointInquiryResponseDto>
                    {
                        Message = "خطای سیستمی",
                        Status = -99,
                        Data = null
                    });
                }
            }
            else
            {
                return (new NajaResponseDtoGeneric<LicenseNegativePointInquiryResponseDto>
                {
                    Message = "خطا در احراز هویت STS برای دریافت توکن",
                    Status = -99,
                    Data = null

                });
            }

        }

        [LoggingAspect]
        public async Task<NajaResponseDtoGeneric<List<LicenseStatusInquiryResponseDto>>> LicenseStatusInquiry(LicenseStatusInquiryRequestDto reqDto)
        {
            var StsToken = await TokenServcie.FetchToken();
            if (!string.IsNullOrEmpty(StsToken))
            {
                try
                {

                    #region create ProxyModel
                    var RequstModel = _mapper.Map<LicenseStatusInquiryRequestModel>(reqDto);
                    #endregion

                    Proxy pclient = this.CreateInstance("NajaServices");
                    ProxySetting setting = pclient.Setting;
                    HttpClient httpClient = pclient.httpClient;
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(scheme: "Bearer", parameter: StsToken);
                    var geturi = setting.uris.TryGetValue("LicenseStatusInquiry", out string LicenseStatusInquiry).ToString();
                    var url = setting.host + LicenseStatusInquiry;

                    _logger.Log(reqDto.TrackingNo, "proxy LicenseStatusInquiry req =>" + JsonConvert.SerializeObject(RequstModel), null);

                    StringContent Content = new StringContent(JsonConvert.SerializeObject(RequstModel), Encoding.UTF8, "application/json");
                    var response = await httpClient.PostAsync(url, Content);

                    _logger.Log(reqDto.TrackingNo, "proxy LicenseStatusInquiry resp =>" + JsonConvert.SerializeObject(response), null);
                    if (response.StatusCode == HttpStatusCode.OK)

                    {
                        string content = await response.Content.ReadAsStringAsync();

                        _logger.Log(reqDto.TrackingNo, "proxy LicenseStatusInquiry cont2 =>" + JsonConvert.SerializeObject(content), null);

                        var data = JsonConvert.DeserializeObject<NajaResponseDtoGeneric<List<LicenseStatusInquiryResponseDto>>>(content,
                            new JsonSerializerSettings()
                            {
                                NullValueHandling = NullValueHandling.Ignore,
                                Error = delegate (object sender, ErrorEventArgs args) { _logger.Log(reqDto.TrackingNo, "proxy JsonSerializerSettings EX LicenseStatusInquiry => " + JsonConvert.SerializeObject(args), sender, null); }
                            });
                        if (data.Status == 0)
                        {
                            var Response = _mapper.Map<List<LicenseStatusInquiryResponseDto>>(data.Data);
                            return (new NajaResponseDtoGeneric<List<LicenseStatusInquiryResponseDto>>
                            {
                                Message = data.Message,
                                Status = 0,
                                Data = Response
                            });
                        }
                        else
                        {
                            _logger.Log(reqDto.TrackingNo, "proxy resp not ok LicenseStatusInquiry => " + JsonConvert.SerializeObject(response.Content.ToString()), null);

                            return (new NajaResponseDtoGeneric<List<LicenseStatusInquiryResponseDto>>
                            {
                                Message = data.Message,
                                Status = data.Status,
                                Data = null
                            });
                        }
                    }
                    else
                    {
                        return (new NajaResponseDtoGeneric<List<LicenseStatusInquiryResponseDto>>
                        {
                            Message = "خطای ارتباط با سرور",
                            Status = -1,
                            Data = null
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log(reqDto.TrackingNo, "proxy ex LicenseStatusInquiry => " + ex.Message, ex);
                    return (new NajaResponseDtoGeneric<List<LicenseStatusInquiryResponseDto>>
                    {
                        Message = "خطای سیستمی",
                        Status = -99,
                        Data = null
                    });
                }
            }
            else
            {
                return (new NajaResponseDtoGeneric<List<LicenseStatusInquiryResponseDto>>
                {
                    Message = "خطا در احراز هویت STS برای دریافت توکن",
                    Status = -99,
                    Data = null

                });
            }

        }

        [LoggingAspect]
        public async Task<NajaResponseDtoGeneric<MotorViolationInquiryResponseDto>> MotorViolationInquiry(
            MotorViolationInquiryRequestDto reqDto)
        {
            var StsToken = await TokenServcie.FetchToken();
            if (!string.IsNullOrEmpty(StsToken))
            {
                try
                {

                    _logger.Log(reqDto.TrackingNo, "proxy MotorViolationInquiry enter => " + JsonConvert.SerializeObject(reqDto), null);
                    #region create ProxyModel
                    var RequstModel = _mapper.Map<MotorViolationInquiryRequestModel>(reqDto);
                    #endregion

                    Proxy pclient = this.CreateInstance("NajaServices");
                    ProxySetting setting = pclient.Setting;
                    HttpClient httpClient = pclient.httpClient;
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(scheme: "Bearer", parameter: StsToken);
                    var geturi = setting.uris.TryGetValue("MotorViolationInquiry", out string MotorViolationInquiry).ToString();
                    var url = setting.host + MotorViolationInquiry;

                    _logger.Log(reqDto.TrackingNo, "proxy MotorViolationInquiry req =>" + JsonConvert.SerializeObject(RequstModel), null);

                    StringContent Content = new StringContent(JsonConvert.SerializeObject(RequstModel), Encoding.UTF8, "application/json");

                    var response = await httpClient.PostAsync(url, Content);

                    _logger.Log(reqDto.TrackingNo, "proxy MotorViolationInquiry resp =>" + JsonConvert.SerializeObject(response), null);

                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        string content = await response.Content.ReadAsStringAsync();

                        _logger.Log(1371, "proxy MotorViolationInquiry cont2 =>" + JsonConvert.SerializeObject(content), null);
                        var data = JsonConvert.DeserializeObject<NajaResponseDtoGeneric<MotorViolationInquiryResponseDto>>(content,
                            new JsonSerializerSettings()
                            {
                                NullValueHandling = NullValueHandling.Ignore,
                                Error = delegate (object sender, ErrorEventArgs args) { _logger.Log(242, "proxy JsonSerializerSettings EX MotorViolationInquiry => " + JsonConvert.SerializeObject(args), sender, null); }
                            });
                        if (data.Status == 0)
                        {
                            var Response = _mapper.Map<MotorViolationInquiryResponseDto>(data.Data);
                            return (new NajaResponseDtoGeneric<MotorViolationInquiryResponseDto>
                            {
                                Message = data.Message,
                                Status = 0,
                                Data = Response
                            });
                        }
                        else
                        {
                            return (new NajaResponseDtoGeneric<MotorViolationInquiryResponseDto>
                            {
                                Message = data.Message,
                                Status = data.Status,
                                Data = null
                            });
                        }
                    }
                    else
                    {
                        return (new NajaResponseDtoGeneric<MotorViolationInquiryResponseDto>
                        {
                            Message = "خطای ارتباط با سرور",
                            Status = -1,
                            Data = null
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log(reqDto.TrackingNo, "proxy ex MotorViolationInquiry => " + ex.Message, ex);

                    return (new NajaResponseDtoGeneric<MotorViolationInquiryResponseDto>
                    {
                        Message = "خطای سیستمی",
                        Status = -99,
                        Data = null
                    });
                }
            }
            else
            {
                return (new NajaResponseDtoGeneric<MotorViolationInquiryResponseDto>
                {
                    Message = "خطا در احراز هویت STS برای دریافت توکن",
                    Status = -99,
                    Data = null

                });
            }

        }

        [LoggingAspect]
        public async Task<NajaResponseDtoGeneric<NoExitInquiryResponseDto>> NoExitInquiry(NoExitInquiryRequestDto reqDto)
        {
            try
            {

                #region create ProxyModel
                var RequstModel = _mapper.Map<NoExitInquiryRequestModel>(reqDto);
                #endregion

                Proxy pclient = this.CreateInstance("NajaServices");
                ProxySetting setting = pclient.Setting;
                HttpClient httpClient = pclient.httpClient;

                var geturi = setting.uris.TryGetValue("NoExitInquiry", out string NoExitInquiry).ToString();
                var url = setting.host + NoExitInquiry;

                StringContent Content = new StringContent(JsonConvert.SerializeObject(RequstModel), Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(url, Content);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string content = await response.Content.ReadAsStringAsync();


                    var data = JsonConvert.DeserializeObject<NajaResponseDtoGeneric<NoExitInquiryResponseDto>>(content,
                            new JsonSerializerSettings()
                            {
                                NullValueHandling = NullValueHandling.Ignore,
                                Error = delegate (object sender, ErrorEventArgs args) { _logger.Log(242, "proxy JsonSerializerSettings EX NoExitInquiry => " + JsonConvert.SerializeObject(args), sender, null); }
                            });
                    if (data.Status == 0)
                    {
                        var Response = _mapper.Map<NoExitInquiryResponseDto>(data.Data);
                        return (new NajaResponseDtoGeneric<NoExitInquiryResponseDto>
                        {
                            Message = data.Message,
                            Status = 0,
                            Data = Response
                        });
                    }
                    else
                    {
                        return (new NajaResponseDtoGeneric<NoExitInquiryResponseDto>
                        {
                            Message = data.Message,
                            Status = data.Status,
                            Data = null
                        });
                    }
                }
                else
                {
                    return (new NajaResponseDtoGeneric<NoExitInquiryResponseDto>
                    {
                        Message = "خطای ارتباط با سرور",
                        Status = -1,
                        Data = null
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.Log(reqDto.TrackingNo, "proxy ex NoExitInquiry => " + ex.Message, ex);

                return (new NajaResponseDtoGeneric<NoExitInquiryResponseDto>
                {
                    Message = "خطای سیستمی",
                    Status = -99,
                    Data = null
                });
            }
        }

        [LoggingAspect]
        public async Task<NajaResponseDtoGeneric<PassportStatusInquiryResponseDto>> PassportStatusInquiry(
            PassportStatusInquiryRequestDto reqDto)
        {
            try
            {

                #region create ProxyModel
                var RequstModel = _mapper.Map<PassportStatusInquiryRequestModel>(reqDto);
                #endregion

                Proxy pclient = this.CreateInstance("NajaServices");
                ProxySetting setting = pclient.Setting;
                HttpClient httpClient = pclient.httpClient;

                var geturi = setting.uris.TryGetValue("PassportStatusInquiry", out string PassportStatusInquiry).ToString();
                var url = setting.host + PassportStatusInquiry;

                StringContent Content = new StringContent(JsonConvert.SerializeObject(RequstModel), Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(url, Content);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string content = await response.Content.ReadAsStringAsync();


                    var data = JsonConvert.DeserializeObject<NajaResponseDtoGeneric<PassportStatusInquiryResponseDto>>(content,
                            new JsonSerializerSettings()
                            {
                                NullValueHandling = NullValueHandling.Ignore,
                                Error = delegate (object sender, ErrorEventArgs args) { _logger.Log(242, "proxy JsonSerializerSettings EX PassportStatusInquiry => " + JsonConvert.SerializeObject(args), sender, null); }
                            });
                    if (data.Status == 0)
                    {
                        var Response = _mapper.Map<PassportStatusInquiryResponseDto>(data.Data);
                        return (new NajaResponseDtoGeneric<PassportStatusInquiryResponseDto>
                        {
                            Message = data.Message,
                            Status = 0,
                            Data = Response
                        });
                    }
                    else
                    {
                        return (new NajaResponseDtoGeneric<PassportStatusInquiryResponseDto>
                        {
                            Message = data.Message,
                            Status = data.Status,
                            Data = null
                        });
                    }
                }
                else
                {
                    return (new NajaResponseDtoGeneric<PassportStatusInquiryResponseDto>
                    {
                        Message = "خطای ارتباط با سرور",
                        Status = -1,
                        Data = null
                    });
                }
            }
            catch (Exception)
            {

                return (new NajaResponseDtoGeneric<PassportStatusInquiryResponseDto>
                {
                    Message = "خطای سیستمی",
                    Status = -99,
                    Data = null
                });
            }
        }

        [LoggingAspect]
        public async Task<NajaResponseDtoGeneric<PaymentExitInquiryResponseDto>> PaymentExitInquiry(
            PaymentExitInquiryRequestDto reqDto)
        {
            try
            {

                #region create ProxyModel
                var RequstModel = _mapper.Map<PaymentExitInquiryRequestModel>(reqDto);
                #endregion

                Proxy pclient = this.CreateInstance("NajaServices");
                ProxySetting setting = pclient.Setting;
                HttpClient httpClient = pclient.httpClient;

                var geturi = setting.uris.TryGetValue("PaymentExitInquiry", out string PaymentExitInquiry).ToString();
                var url = setting.host + PaymentExitInquiry;

                StringContent Content = new StringContent(JsonConvert.SerializeObject(RequstModel), Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(url, Content);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string content = await response.Content.ReadAsStringAsync();


                    var data = JsonConvert.DeserializeObject<NajaResponseDtoGeneric<PaymentExitInquiryResponseDto>>(content,
                            new JsonSerializerSettings()
                            {
                                NullValueHandling = NullValueHandling.Ignore,
                                Error = delegate (object sender, ErrorEventArgs args) { _logger.Log(242, "proxy JsonSerializerSettings EX PaymentExitInquiry => " + JsonConvert.SerializeObject(args), sender, null); }
                            });
                    if (data.Status == 0)
                    {
                        var Response = _mapper.Map<PaymentExitInquiryResponseDto>(data.Data);
                        return (new NajaResponseDtoGeneric<PaymentExitInquiryResponseDto>
                        {
                            Message = data.Message,
                            Status = 0,
                            Data = Response
                        });
                    }
                    else
                    {
                        return (new NajaResponseDtoGeneric<PaymentExitInquiryResponseDto>
                        {
                            Message = data.Message,
                            Status = data.Status,
                            Data = null
                        });
                    }
                }
                else
                {
                    return (new NajaResponseDtoGeneric<PaymentExitInquiryResponseDto>
                    {
                        Message = "خطای ارتباط با سرور",
                        Status = -1,
                        Data = null
                    });
                }
            }
            catch (Exception)
            {

                return (new NajaResponseDtoGeneric<PaymentExitInquiryResponseDto>
                {
                    Message = "خطای سیستمی",
                    Status = -99,
                    Data = null
                });
            }
        }

        [LoggingAspect]
        public async Task<NajaResponseDtoGeneric<object>> SetPayment(SetPaymentRequestDto reqDto)
        {
            try
            {

                #region create ProxyModel
                var RequstModel = _mapper.Map<SetPaymentRequestModel>(reqDto);
                #endregion

                Proxy pclient = this.CreateInstance("NajaServices");
                ProxySetting setting = pclient.Setting;
                HttpClient httpClient = pclient.httpClient;

                var geturi = setting.uris.TryGetValue("SetPayment", out string SetPayment).ToString();
                var url = setting.host + SetPayment;

                StringContent Content = new StringContent(JsonConvert.SerializeObject(RequstModel), Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(url, Content);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string content = await response.Content.ReadAsStringAsync();


                    var data = JsonConvert.DeserializeObject<NajaResponseDtoGeneric<object>>(content);
                    if (data.Status == 0)
                    {
                        var Response = _mapper.Map<object>(data.Data);
                        return (new NajaResponseDtoGeneric<object>
                        {
                            Message = data.Message,
                            Status = 0,
                            Data = Response
                        });
                    }
                    else
                    {
                        return (new NajaResponseDtoGeneric<object>
                        {
                            Message = data.Message,
                            Status = data.Status,
                            Data = null
                        });
                    }
                }
                else
                {
                    return new NajaResponseDtoGeneric<object>
                    {
                        Message = "خطای ارتباط با سرور",
                        Status = -1,
                        Data = null
                    };
                }
            }
            catch (Exception)
            {

                return (new NajaResponseDtoGeneric<object>
                {
                    Message = "خطای سیستمی",
                    Status = -99,
                    Data = null
                });
            }
        }

        [LoggingAspect]
        public async Task<NajaResponseDtoGeneric<ViolationImageInquiryResponseDto>> ViolationImageInquiry(
            ViolationImageInquiryRequestDto reqDto)
        {
            var StsToken = await TokenServcie.FetchToken();
            if (!string.IsNullOrEmpty(StsToken))
            {
                try
                {

                    #region create ProxyModel
                    var RequstModel = _mapper.Map<ViolationImageInquiryRequestModel>(reqDto);
                    #endregion

                    Proxy pclient = this.CreateInstance("NajaServices");
                    ProxySetting setting = pclient.Setting;
                    HttpClient httpClient = pclient.httpClient;
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(scheme: "Bearer", parameter: StsToken);

                    var geturi = setting.uris.TryGetValue("ViolationImageInquiry", out string ViolationImageInquiry).ToString();
                    var url = setting.host + ViolationImageInquiry;

                    StringContent Content = new StringContent(JsonConvert.SerializeObject(RequstModel), Encoding.UTF8, "application/json");
                    var response = await httpClient.PostAsync(url, Content);
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        string content = await response.Content.ReadAsStringAsync();
                        _logger.Log(reqDto.TrackingNo, "proxy resp ok ViolationImageInquiry => " + JsonConvert.SerializeObject(content), null);

                        var data = JsonConvert.DeserializeObject<NajaResponseDtoGeneric<ViolationImageInquiryResponseDto>>(content,
                            new JsonSerializerSettings()
                            {
                                NullValueHandling = NullValueHandling.Ignore,
                                Error = delegate (object sender, ErrorEventArgs args) { _logger.Log(242, "proxy JsonSerializerSettings EX ViolationImageInquiry => " + JsonConvert.SerializeObject(args), sender, null); }
                            });
                        if (data.Status == 0)
                        {
                            var Response = _mapper.Map<ViolationImageInquiryResponseDto>(data.Data);
                            return (new NajaResponseDtoGeneric<ViolationImageInquiryResponseDto>
                            {
                                Message = data.Message,
                                Status = 0,
                                Data = Response
                            });
                        }
                        else
                        {
                            _logger.Log(reqDto.TrackingNo, "proxy resp not ok ViolationImageInquiry => " + JsonConvert.SerializeObject(response.Content.ToString()), null);

                            return (new NajaResponseDtoGeneric<ViolationImageInquiryResponseDto>
                            {
                                Message = data.Message,
                                Status = data.Status,
                                Data = null
                            });
                        }
                    }
                    else
                    {
                        return (new NajaResponseDtoGeneric<ViolationImageInquiryResponseDto>
                        {
                            Message = "خطای ارتباط با سرور",
                            Status = -1,
                            Data = null
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log(reqDto.TrackingNo, "proxy ex ViolationInquiry => " + ex.Message, ex);

                    return (new NajaResponseDtoGeneric<ViolationImageInquiryResponseDto>
                    {
                        Message = "خطای سیستمی",
                        Status = -99,
                        Data = null
                    });
                }
            }
            else
            {
                return (new NajaResponseDtoGeneric<ViolationImageInquiryResponseDto>
                {
                    Message = "خطا در احراز هویت STS برای دریافت توکن",
                    Status = -99,
                    Data = null

                });
            }
        }


        public async Task<NajaResponseDtoGeneric<object>> GetInquiry(GetInquiryRequestDto dto)
        {
            _logger.Log(13681369, "proxy GetInquiry =>" + JsonConvert.SerializeObject(dto), null);
            var StsToken = await TokenServcie.FetchToken();
            if (!string.IsNullOrEmpty(StsToken))
            {
                try
                {
                    _logger.Log(dto.TrackingNo, "proxy GetInquiry sts=>" + JsonConvert.SerializeObject(StsToken), null);

                    #region create ProxyModel
                    var RequstModel = _mapper.Map<GetInquiryRequestModel>(dto);
                    #endregion

                    _logger.Log(13681369, "proxy GetInquiry req=>" + JsonConvert.SerializeObject(RequstModel), null);

                    Proxy pclient = this.CreateInstance("NajaServices");
                    ProxySetting setting = pclient.Setting;
                    HttpClient httpClient = pclient.httpClient;
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(scheme: "Bearer", parameter: StsToken);
                    var geturi = setting.uris.TryGetValue("GetInquiry", out string GetInquiry).ToString();
                    var url = setting.host + GetInquiry;

                    StringContent Content = new StringContent(JsonConvert.SerializeObject(RequstModel), Encoding.UTF8, "application/json");

                    _logger.Log(13681369, "proxy GetInquiry content=>" + JsonConvert.SerializeObject(Content), null);

                    var response = await httpClient.PostAsync(url, Content);

                    _logger.Log(13681369, "proxy GetInquiry resp=>" + JsonConvert.SerializeObject(response), null);

                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        string content = await response.Content.ReadAsStringAsync();

                        _logger.Log(13681369, "proxy GetInquiry cont2=>" + JsonConvert.SerializeObject(content), null);

                        var data = JsonConvert.DeserializeObject<NajaResponseDtoGeneric<object>>(content,
                                 new JsonSerializerSettings()
                                 {
                                     NullValueHandling = NullValueHandling.Ignore,
                                     Error = delegate (object sender, ErrorEventArgs args) { _logger.Log(242, "proxy JsonSerializerSettings EX GetInquiry => " + JsonConvert.SerializeObject(args), sender, null); }
                                 });
                        if (data.Status == 0)
                        {
                            return (new NajaResponseDtoGeneric<object>
                            {
                                Message = data.Message,
                                Status = 0,
                                Data = data.Data
                            });
                        }
                        else
                        {
                            return (new NajaResponseDtoGeneric<object>
                            {
                                Message = data.Message,
                                Status = data.Status,
                                Data = null
                            });
                        }
                    }
                    else
                    {
                        return (new NajaResponseDtoGeneric<object>
                        {
                            Message = "خطای ارتباط با سرور",
                            Status = -1,
                            Data = null
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log(dto.TrackingNo, "proxy ex GetInquiry => " + ex.Message, ex);

                    return (new NajaResponseDtoGeneric<object>
                    {
                        Message = "خطای سیستمی",
                        Status = -99,
                        Data = null
                    });
                }
            }
            else
            {

                return (new NajaResponseDtoGeneric<object>
                {
                    Message = "خطا در احراز هویت STS برای دریافت توکن",
                    Status = -99,
                    Data = null

                });
            }
        }

    }


    public interface INajaServiceProxy
    {
        //Task<GetTokenResponseDto> GetNajaToken(); deprecated
        Task<NajaResponseDtoGeneric<AccumulationViolationsInquiryResponseDto>> AccumulationViolationsInquiry(AccumulationViolationsInquiryRequestDto reqDto);
        Task<NajaResponseDtoGeneric<List<ActivePlakInquiryResponseDto>>> ActivePlakInquiry(ActivePlakInquiryRequestDto reqDto);
        Task<NajaResponseDtoGeneric<CardSanadInquiryResponseDto>> CardSanadInquiry(CardSanadInquiryRequestDto reqDto);
        Task<NajaResponseDtoGeneric<ExitTaxesReceiptResponseDto>> ExitTaxesReceipt(ExitTaxesReceiptRequestDto reqDto);
        Task<NajaResponseDtoGeneric<LicenseNegativePointInquiryResponseDto>> LicenseNegativePointInquiry(LicenseNegativePointInquiryRequestDto reqDto);
        Task<NajaResponseDtoGeneric<List<LicenseStatusInquiryResponseDto>>> LicenseStatusInquiry(LicenseStatusInquiryRequestDto reqDto);
        Task<NajaResponseDtoGeneric<MotorViolationInquiryResponseDto>> MotorViolationInquiry(MotorViolationInquiryRequestDto reqDto);
        Task<NajaResponseDtoGeneric<NoExitInquiryResponseDto>> NoExitInquiry(NoExitInquiryRequestDto reqDto);
        Task<NajaResponseDtoGeneric<PassportStatusInquiryResponseDto>> PassportStatusInquiry(PassportStatusInquiryRequestDto reqDto);
        Task<NajaResponseDtoGeneric<PaymentExitInquiryResponseDto>> PaymentExitInquiry(PaymentExitInquiryRequestDto reqDto);
        Task<NajaResponseDtoGeneric<object>> SetPayment(SetPaymentRequestDto reqDto);
        Task<NajaResponseDtoGeneric<ViolationImageInquiryResponseDto>> ViolationImageInquiry(ViolationImageInquiryRequestDto reqDto);
        Task<NajaResponseDtoGeneric<object>> ViolationInquiry(ViolationInquiryRequestDto reqDto);
        Task<NajaResponseDtoGeneric<CheckInfoResponseDto>> CheckInfo(CheckInfoRequestDto dto);
        Task<NajaResponseDtoGeneric<object>> GetInquiry(GetInquiryRequestDto dto);
    }
}
