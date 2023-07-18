using AutoMapper;
using Dto.Proxy.Request.PecIs;
using Dto.Proxy.Response;
using Dto.Proxy.Response.Naja;
using Dto.Proxy.Response.PecIs;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Pigi.MDbLogging;
using ProxyService.ProxyModel.Request.PecIS;
using ProxyService.ProxyModel.Response;
using ProxyService.ProxyModel.Response.PecIS;
using ProxyService.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace ProxyService.services
{
    public class BillInquiryServiceProxy : HttpClientBase, IBillInquiryServiceProxy, ILoggable
    {
        #region Private Variables  
        private readonly IMapper _mapper;
        #endregion

        #region Ctor
        public BillInquiryServiceProxy(IHttpClientFactory clientFactoy, IConfiguration configuration, IMapper mapper) : base(clientFactoy, configuration)
        {
            _mapper = mapper;
        }
        #endregion

        public async Task<GetTokenResponseDto> GetSTSToken()
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
            catch (Exception)
            {

                return null;
            }
        }
        public async Task<ResponseBaseDto<List<MciBillInquiryResponseDto>>> MciBillInquiry(MciBillInquiryRequestDto inquiryRequestDto)
        {
            var StsToken = await GetSTSToken();
            if (StsToken != null && !string.IsNullOrEmpty(StsToken.Access_token))
            {
                try
                {

                    #region create ProxyModel
                    var MciRequstModel = _mapper.Map<MciBillInquiryRequestModel>(inquiryRequestDto);
                    #endregion

                    Proxy pclient = this.CreateInstance("pecServices");
                    ProxySetting setting = pclient.Setting;
                    HttpClient httpClient = pclient.httpClient;

                    string mciBillInquiry = "";
                    var geturi = setting.uris.TryGetValue("mciBillInquiry", out mciBillInquiry).ToString();
                    var url = setting.host + mciBillInquiry;

                    //string authParams = string.Format("{0}:{1}", setting.userName, setting.password);
                    //byte[] bytes = Encoding.UTF8.GetBytes(authParams);
                    //string encodedAuthParams = Convert.ToBase64String(bytes);
                    //httpClient.DefaultRequestHeaders.Add("Authorization", "Basic " + encodedAuthParams.ToString());
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(scheme: "Bearer", parameter: StsToken.Access_token);

                    StringContent Content = new StringContent(JsonConvert.SerializeObject(MciRequstModel), Encoding.UTF8, "application/json");
                    var response = await httpClient.PostAsync(url, Content);
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        string content = await response.Content.ReadAsStringAsync();


                        var data = JsonConvert.DeserializeObject<ResponseBaseDto<List<MciBillInquiryResponseModel>>>(content);
                        if (data.Status == 0)
                        {
                            var MciInquiryResponse = _mapper.Map<List<MciBillInquiryResponseDto>>(data.Data);
                            return (new ResponseBaseDto<List<MciBillInquiryResponseDto>>
                            {
                                Message = data.Message,
                                Status = 0,
                                Data = MciInquiryResponse

                            });
                        }
                        else
                        {
                            return (new ResponseBaseDto<List<MciBillInquiryResponseDto>>
                            {
                                Message = data.Message,
                                Status = -1,
                                Data = null

                            });

                        }

                    }
                    else
                    {
                        return (new ResponseBaseDto<List<MciBillInquiryResponseDto>>
                        {
                            Message = "خطای ارتباط با سرور",
                            Status = -1,
                            Data = null

                        });
                    }
                }
                catch (Exception)
                {

                    return (new ResponseBaseDto<List<MciBillInquiryResponseDto>>
                    {
                        Message = "خطای سیستمی",
                        Status = -99,
                        Data = null

                    });
                }
            }
            else
            {
                return (new ResponseBaseDto<List<MciBillInquiryResponseDto>>
                {
                    Message = "خطا در دریافت شناسه احراز هویت",
                    Status = -401,
                    Data = null

                });
            }
        }

        public async Task<ResponseBaseDto<List<TciInquiryResponseDto>>> TciInquiry(TciInquiryRequestDto tciInquiryRequest)
        {
            var StsToken = await GetSTSToken();
            if (StsToken != null && !string.IsNullOrEmpty(StsToken.Access_token))
            {
                var tciResponse = new List<TciInquiryResponseDto>();
                try
                {

                    #region create ProxyModel
                    var tciRequstModel = _mapper.Map<TciInquiryRequestModel>(tciInquiryRequest);
                    #endregion

                    Proxy pclient = this.CreateInstance("pecServices");
                    ProxySetting setting = pclient.Setting;
                    HttpClient httpClient = pclient.httpClient;
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(scheme: "Bearer", parameter: StsToken.Access_token);

                    string tciBillInquiryuri = "";
                    var geturi = setting.uris.TryGetValue("tciBillInquiryuri", out tciBillInquiryuri).ToString();
                    var url = setting.host + tciBillInquiryuri;

                    //string authParams = string.Format("{0}:{1}", setting.userName, setting.password);
                    //byte[] bytes = Encoding.UTF8.GetBytes(authParams);
                    //string encodedAuthParams = Convert.ToBase64String(bytes);
                    //httpClient.DefaultRequestHeaders.Add("Authorization", "Basic " + encodedAuthParams.ToString());
                    StringContent Content = new StringContent(JsonConvert.SerializeObject(tciRequstModel), Encoding.UTF8, "application/json");
                    var response = await httpClient.PostAsync(url, Content);
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        string content = await response.Content.ReadAsStringAsync();

                        var data = JsonConvert.DeserializeObject<ResponseBase<List<TciInquiryResponseModel>>>(content);
                        if (data.status == 0)
                        {
                            tciResponse = _mapper.Map<List<TciInquiryResponseDto>>(data.Data);
                            return (new ResponseBaseDto<List<TciInquiryResponseDto>>
                            {
                                Message = data.message,
                                Status = 0,
                                Data = tciResponse

                            });
                        }
                        else
                        {
                            return (new ResponseBaseDto<List<TciInquiryResponseDto>>
                            {
                                Message = data.message,
                                Status = -1,
                                Data = null

                            });
                        }
                    }
                    else
                    {
                        return (new ResponseBaseDto<List<TciInquiryResponseDto>>
                        {
                            Message = "خطای ارتباط با سرور",
                            Status = -1,
                            Data = null

                        });

                    }
                }
                catch (Exception)
                {

                    return (new ResponseBaseDto<List<TciInquiryResponseDto>>
                    {
                        Message = "خطای سیستمی",
                        Status = -99,
                        Data = null

                    });
                }
            }
            else
            {
                return (new ResponseBaseDto<List<TciInquiryResponseDto>>
                {
                    Message = "خطا در دریافت شناسه احراز هویت",
                    Status = -401,
                    Data = null

                });
            }

        }

        public async Task<ResponseBaseDto<NigcBillInquiryResponseDto>> NigcBillInquiry(NigcBillInquiryRequestDto nigcBillInquiryRequest)
        {
            var StsToken = await GetSTSToken();
            if (StsToken != null && !string.IsNullOrEmpty(StsToken.Access_token))
            {
                try
                {
                    #region create ProxyModel
                    var nigcBillModel = _mapper.Map<NigcBillInquiryRequestModel>(nigcBillInquiryRequest);
                    #endregion
                    Proxy pclient = this.CreateInstance("pecServices");
                    ProxySetting setting = pclient.Setting;
                    HttpClient httpClient = pclient.httpClient;

                    string nigcBillInquiry = "";
                    var geturi = setting.uris.TryGetValue("nigcBillInquiry", out nigcBillInquiry).ToString();
                    var url = setting.host + nigcBillInquiry;

                    string authParams = string.Format("{0}:{1}", setting.userName, setting.password);
                    byte[] bytes = Encoding.UTF8.GetBytes(authParams);
                    //string encodedAuthParams = Convert.ToBase64String(bytes);
                    //httpClient.DefaultRequestHeaders.Add("Authorization", "Basic " + encodedAuthParams.ToString());
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(scheme: "Bearer", parameter: StsToken.Access_token);

                    StringContent Content = new StringContent(JsonConvert.SerializeObject(nigcBillModel), Encoding.UTF8, "application/json");
                    var response = await httpClient.PostAsync(url, Content);
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        string content = await response.Content.ReadAsStringAsync();
                        var data = JsonConvert.DeserializeObject<ResponseBase<NigcBillInquiryResponseModel>>(content);
                        if (data.status == 0)
                        {
                            var nigcResponse = _mapper.Map<NigcBillInquiryResponseDto>(data.Data);
                            nigcResponse.PayAmount = nigcResponse.PayAmount.Replace(",", "");
                            return (new ResponseBaseDto<NigcBillInquiryResponseDto>
                            {
                                Message = data.message,
                                Status = 0,
                                Data = nigcResponse
                            });
                        }
                        else
                        {
                            return (new ResponseBaseDto<NigcBillInquiryResponseDto>
                            {
                                Message = data.message,
                                Status = -1,
                                Data = null

                            });

                        }

                    }
                    else
                    {

                        return (new ResponseBaseDto<NigcBillInquiryResponseDto>
                        {
                            Message = "خطا در انجام عملیات",
                            Status = -1,
                            Data = null

                        });
                    }
                }
                catch (Exception)
                {
                    return (new ResponseBaseDto<NigcBillInquiryResponseDto>
                    {
                        Message = "خطای سیستمی",
                        Status = -99,
                        Data = null

                    });
                }
            }
            else
            {
                return (new ResponseBaseDto<NigcBillInquiryResponseDto>
                {
                    Message = "خطا در دریافت شناسه احراز هویت",
                    Status = -401,
                    Data = null

                });
            }


        }

        public async Task<ResponseBaseDto<BarghBillInquiryResponseDto>> BarghBillInquiry(BarghBillInquiryRequestDto barghBillInquiry)
        {
            var StsToken = await GetSTSToken();
            if (StsToken != null && !string.IsNullOrEmpty(StsToken.Access_token))
            {
                var barghResponse = new ResponseBaseDto<BarghBillInquiryResponseDto>();
                try
                {

                    #region create ProxyModel
                    var barghRequestModel = _mapper.Map<BarghBillInquiryRequestModel>(barghBillInquiry);
                    #endregion

                    Proxy pclient = this.CreateInstance("pecServices");
                    ProxySetting setting = pclient.Setting;
                    HttpClient httpClient = pclient.httpClient;

                    string barghBillUrlInquiry = "";
                    var geturi = setting.uris.TryGetValue("BarghBillInquiry", out barghBillUrlInquiry).ToString();
                    var url = setting.host + barghBillUrlInquiry;

                    string authParams = string.Format("{0}:{1}", setting.userName, setting.password);
                    byte[] bytes = Encoding.UTF8.GetBytes(authParams);
                    string encodedAuthParams = Convert.ToBase64String(bytes);
                    //httpClient.DefaultRequestHeaders.Add("Authorization", "Basic " + encodedAuthParams.ToString());
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(scheme: "Bearer", parameter: StsToken.Access_token);

                    StringContent Content = new StringContent(JsonConvert.SerializeObject(barghRequestModel), Encoding.UTF8, "application/json");
                    var response = await httpClient.PostAsync(url, Content);

                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        string content = await response.Content.ReadAsStringAsync();
                        var data = JsonConvert.DeserializeObject<ResponseBaseDto<BarghBillInquiryResponseModel>>(content);
                        if (data.Status == 0)
                        {
                            var barghResponseModel = _mapper.Map<BarghBillInquiryResponseDto>(data.Data);
                            barghResponse.Status = data.Status;
                            barghResponse.Message = data.Message;
                            barghResponse.Data = barghResponseModel;

                            return barghResponse;
                        }
                        else if (data.Status == 1)
                        {
                            barghResponse.Status = data.Status;
                            barghResponse.Message = data.Message;
                            barghResponse.Data = _mapper.Map<BarghBillInquiryResponseDto>(data.Data);

                            return barghResponse;
                        }
                        else
                        {
                            barghResponse.Status = data.Status;
                            barghResponse.Message = data.Message;
                            return barghResponse;
                        }
                    }
                    else
                    {
                        barghResponse.Status = -1;
                        barghResponse.Message = "خطا در دریافت اطلاعات از برق";
                        return barghResponse;
                    }

                }
                catch (Exception)
                {
                    barghResponse.Status = -99;
                    barghResponse.Message = "خطا در دریافت اطلاعات از برق";
                    return barghResponse;
                }
            }
            else
            {
                return (new ResponseBaseDto<BarghBillInquiryResponseDto>
                {
                    Message = "خطا در دریافت شناسه احراز هویت",
                    Status = -401,
                    Data = null

                });
            }

        }

        public async Task<ResponseBaseDto<TollBillInquiryResponseDto>> TollBillInquiry(TollBillInquiryRequestDto tollBillInquiryRequest)
        {
            var StsToken = await GetSTSToken();
            if (StsToken != null && !string.IsNullOrEmpty(StsToken.Access_token))
            {
                try
                {

                    #region create ProxyModel
                    var tollRequestModel = _mapper.Map<TollBillInquiryRequestModel>(tollBillInquiryRequest);
                    #endregion

                    Proxy pclient = this.CreateInstance("pecServices");
                    ProxySetting setting = pclient.Setting;
                    HttpClient httpClient = pclient.httpClient;

                    string tollBillInquiry = "";
                    var geturi = setting.uris.TryGetValue("tollBillInquiry", out tollBillInquiry).ToString();
                    var url = setting.host + tollBillInquiry;

                    string authParams = string.Format("{0}:{1}", setting.userName, setting.password);
                    byte[] bytes = Encoding.UTF8.GetBytes(authParams);
                    string encodedAuthParams = Convert.ToBase64String(bytes);
                    //httpClient.DefaultRequestHeaders.Add("Authorization", "Basic " + encodedAuthParams.ToString());
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(scheme: "Bearer", parameter: StsToken.Access_token);

                    StringContent Content = new StringContent(JsonConvert.SerializeObject(tollRequestModel), Encoding.UTF8, "application/json");
                    var response = await httpClient.PostAsync(url, Content);

                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        string content = await response.Content.ReadAsStringAsync();
                        var data = JsonConvert.DeserializeObject<ResponseBaseDto<TollBillInquiryResponseModel>>(content);
                        if (data.Status == 0)
                        {
                            var ResponseModel = _mapper.Map<TollBillInquiryResponseDto>(data.Data);
                            return (new ResponseBaseDto<TollBillInquiryResponseDto>
                            {
                                Message = data.Message,
                                Status = data.Status,
                                Data = ResponseModel

                            });
                        }
                        else
                        {
                            return (new ResponseBaseDto<TollBillInquiryResponseDto>
                            {
                                Message = data.Message,
                                Status = data.Status,
                                Data = null

                            });
                        }
                    }
                    else
                    {
                        return (new ResponseBaseDto<TollBillInquiryResponseDto>
                        {
                            Message = "خطا در دریافت اطلاعات قبض عوارض",
                            Status = -1,
                            Data = null

                        });
                    }


                }
                catch (Exception ex)
                {
                    return (new ResponseBaseDto<TollBillInquiryResponseDto>
                    {
                        Message = "خطای سیستمی ",
                        Status = -99,
                        Data = null

                    });
                }
            }
            else
            {
                return (new ResponseBaseDto<TollBillInquiryResponseDto>
                {
                    Message = "خطا در دریافت شناسه احراز هویت",
                    Status = -401,
                    Data = null

                });
            }
        }

        public async Task<ResponseBaseDto<TollBillSetPayResponseDto>> TollBillSetPay(TollBillSetPayRequestDto tollBillSetPay)
        {
            var StsToken = await GetSTSToken();
            if (StsToken != null && !string.IsNullOrEmpty(StsToken.Access_token))
            {
                try
                {

                    #region create ProxyModel
                    var tollRequestModel = _mapper.Map<TollBillSetPayRequestModel>(tollBillSetPay);
                    #endregion

                    Proxy pclient = this.CreateInstance("pecServices");
                    ProxySetting setting = pclient.Setting;
                    HttpClient httpClient = pclient.httpClient;

                    string tollBillSetPayUrl = "";
                    var geturi = setting.uris.TryGetValue("tollBillSetPay", out tollBillSetPayUrl).ToString();
                    var url = setting.host + tollBillSetPayUrl;

                    string authParams = string.Format("{0}:{1}", setting.userName, setting.password);
                    byte[] bytes = Encoding.UTF8.GetBytes(authParams);
                    string encodedAuthParams = Convert.ToBase64String(bytes);
                    //httpClient.DefaultRequestHeaders.Add("Authorization", "Basic " + encodedAuthParams.ToString());
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(scheme: "Bearer", parameter: StsToken.Access_token);

                    StringContent Content = new StringContent(JsonConvert.SerializeObject(tollRequestModel), Encoding.UTF8, "application/json");
                    var response = await httpClient.PostAsync(url, Content);

                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        string content = await response.Content.ReadAsStringAsync();
                        var data = JsonConvert.DeserializeObject<ResponseBaseDto<TollBillSetPayResponseModel>>(content);
                        if (data.Status == 0)
                        {
                            var ResponseModel = _mapper.Map<TollBillSetPayResponseDto>(data.Data);
                            return (new ResponseBaseDto<TollBillSetPayResponseDto>
                            {
                                Message = data.Message,
                                Status = data.Status,
                                Data = ResponseModel

                            });
                        }
                        else
                        {
                            return (new ResponseBaseDto<TollBillSetPayResponseDto>
                            {
                                Message = data.Message,
                                Status = data.Status,
                                Data = null

                            });
                        }
                    }
                    else
                    {
                        return (new ResponseBaseDto<TollBillSetPayResponseDto>
                        {
                            Message = "خطا در پرداخت قبض عوارض",
                            Status = -1,
                            Data = null

                        });
                    }


                }
                catch (Exception)
                {
                    return (new ResponseBaseDto<TollBillSetPayResponseDto>
                    {
                        Message = "خطای سیستمی ",
                        Status = -99,
                        Data = null

                    });
                }
            }
            else
            {
                return (new ResponseBaseDto<TollBillSetPayResponseDto>
                {
                    Message = "خطا در دریافت شناسه احراز هویت",
                    Status = -401,
                    Data = null

                });
            }
        }

        public async Task<ResponseBaseDto<IrancellInquiryResponseDto>> IrancellPostpaidBalance(IrancellInquiryRequestDto irancellInquiry)
        {
            var StsToken = await GetSTSToken();
            if (StsToken != null && !string.IsNullOrEmpty(StsToken.Access_token))
            {
                var irancellResponse = new IrancellInquiryResponseDto();
                try
                {

                    #region create ProxyModel
                    var irancellRequstModel = _mapper.Map<IrancellInquiryRequestModel>(irancellInquiry);
                    #endregion

                    Proxy pclient = this.CreateInstance("pecServices");
                    ProxySetting setting = pclient.Setting;
                    HttpClient httpClient = pclient.httpClient;
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(scheme: "Bearer", parameter: StsToken.Access_token);

                    var geturi = setting.uris.TryGetValue("irancellBillInquiryuri", out string irancellInquiryuri).ToString();
                    var url = setting.host + irancellInquiryuri;

                    StringContent Content = new StringContent(JsonConvert.SerializeObject(irancellRequstModel), Encoding.UTF8, "application/json");
                    var response = await httpClient.PostAsync(url, Content);
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        string content = await response.Content.ReadAsStringAsync();

                        var data = JsonConvert.DeserializeObject<ResponseBaseDto<IrancellInquiryResponseDto>>(content);
                        if (data.Status == 0)
                        {
                            irancellResponse = _mapper.Map<IrancellInquiryResponseDto>(data.Data);
                            return (new ResponseBaseDto<IrancellInquiryResponseDto>
                            {
                                Message = data.Message,
                                Status = 0,
                                Data = irancellResponse

                            });
                        }
                        else
                        {
                            return (new ResponseBaseDto<IrancellInquiryResponseDto>
                            {
                                Message = data.Message,
                                Status = -1,
                                Data = null

                            });
                        }
                    }
                    else
                    {
                        return (new ResponseBaseDto<IrancellInquiryResponseDto>
                        {
                            Message = "خطای ارتباط با سرور",
                            Status = -1,
                            Data = null

                        });

                    }
                }
                catch (Exception)
                {

                    return (new ResponseBaseDto<IrancellInquiryResponseDto>
                    {
                        Message = "خطای سیستمی",
                        Status = -99,
                        Data = null

                    });
                }
            }
            else
            {
                return (new ResponseBaseDto<IrancellInquiryResponseDto>
                {
                    Message = "خطا در دریافت شناسه احراز هویت",
                    Status = -401,
                    Data = null

                });
            }
        }
    }
    public interface IBillInquiryServiceProxy
    {
        Task<ResponseBaseDto<List<MciBillInquiryResponseDto>>> MciBillInquiry(MciBillInquiryRequestDto inquiryRequestDto);
        Task<ResponseBaseDto<List<TciInquiryResponseDto>>> TciInquiry(TciInquiryRequestDto tciInquiryRequest);
        Task<ResponseBaseDto<NigcBillInquiryResponseDto>> NigcBillInquiry(NigcBillInquiryRequestDto nigcBillInquiryRequest);
        Task<ResponseBaseDto<BarghBillInquiryResponseDto>> BarghBillInquiry(BarghBillInquiryRequestDto barghBillInquiry);
        Task<ResponseBaseDto<TollBillInquiryResponseDto>> TollBillInquiry(TollBillInquiryRequestDto tollBillInquiryRequest);
        Task<ResponseBaseDto<TollBillSetPayResponseDto>> TollBillSetPay(TollBillSetPayRequestDto tollBillSetPay);
        Task<ResponseBaseDto<IrancellInquiryResponseDto>> IrancellPostpaidBalance(IrancellInquiryRequestDto tollBillSetPay);
    }
}
