using AutoMapper;
using CacheManager.Core;
using Dto.Proxy.Request.Tosan;
using Dto.Proxy.Response.Tosan;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using PEC.CoreCommon.Attribute;
using Pigi.MDbLogging;
using ProxyService.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ProxyService.services
{
    [ScopedService]
    public class TosanSohaServiceProxy : HttpClientBase, ITosanSohaServiceProxy, ILoggable
    {
        #region Private Variables  
        private readonly IMapper _mapper;
        private readonly IMdbLogger<TosanSohaServiceProxy> _logger;
        //private readonly IMemoryCache _Cache;


        #endregion

        #region Ctor
        public TosanSohaServiceProxy(IHttpClientFactory clientFactoy, IMdbLogger<TosanSohaServiceProxy> logger, IConfiguration configuration, IMapper mapper/*, IMemoryCache cache*/) : base(clientFactoy, configuration)
        {
            _mapper = mapper;
            _logger = logger;
            //_Cache = cache;
        }
        #endregion

        #region GatehargeCard
        public async Task<TicketGetBalanceDto> GetBalanceCompanyCard()
        {
            var responseBalance = new TicketGetBalanceDto();
            try
            {
                Proxy pclient = this.CreateInstance("TosanSoha");
                ProxySetting setting = pclient.Setting;
                HttpClient httpClient = pclient.httpClient;

                string Balance = "";
                var geturi = setting.uris.TryGetValue("GetBalanceCompanyCard", out Balance).ToString();
                var url = setting.host + Balance;

                string authParams = string.Format("{0}:{1}", setting.userName, setting.password);
                byte[] bytes = Encoding.UTF8.GetBytes(authParams);
                string encodedAuthParams = Convert.ToBase64String(bytes);
                httpClient.DefaultRequestHeaders.Add("Authorization", "Basic " + encodedAuthParams.ToString());
                var response = await httpClient.PostAsync(url, null);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string content = await response.Content.ReadAsStringAsync();
                    var data = JsonConvert.DeserializeObject<TicketGetBalanceDto>(content);
                    return data;
                }
                else
                {
                    responseBalance.Result = 0;
                    responseBalance.resultType = "error";
                    responseBalance.resultCode = "-1";
                    return responseBalance;
                }

            }
            catch (Exception ex)
            {
                responseBalance.Result = 0;
                responseBalance.resultType = "خطای ناشناخته";
                responseBalance.resultCode = "-99";
                return responseBalance;
            }
        }
        public async Task<IsValidCardTypeDto> IsValidCardType(long? cardserial)
        {

            var responseData = new IsValidCardTypeDto();
            try
            {
                Proxy pclient = this.CreateInstance("TosanSoha");
                ProxySetting setting = pclient.Setting;
                HttpClient httpClient = pclient.httpClient;

                string route = "";
                var geturi = setting.uris.TryGetValue("IsValidCardType", out route).ToString();
                var url = setting.host + route + "?cardSerial=" + cardserial;

                string authParams = string.Format("{0}:{1}", setting.userName, setting.password);
                byte[] bytes = Encoding.UTF8.GetBytes(authParams);
                string encodedAuthParams = Convert.ToBase64String(bytes);
                httpClient.DefaultRequestHeaders.Add("Authorization", "Basic " + encodedAuthParams.ToString());
                var response = await httpClient.GetAsync(url);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string content = await response.Content.ReadAsStringAsync();
                    var data = JsonConvert.DeserializeObject<IsValidCardTypeDto>(content);
                    return data;
                }
                else
                {
                    responseData.result = false;
                    responseData.resultType = "error";
                    responseData.resultCode = "-1";
                    return responseData;
                }

            }
            catch (Exception ex)
            {
                responseData.result = false;
                responseData.resultType = "خطای ناشناخته";
                responseData.resultCode = "-99";
                return responseData;
            }
        }
        public async Task<CreateVoucherResponseDto> CreateVoucher(CreateVoucherRequestDto request)
        {
            var responseData = new CreateVoucherResponseDto();
            try
            {
                Proxy pclient = this.CreateInstance("TosanSoha");
                ProxySetting setting = pclient.Setting;
                HttpClient httpClient = pclient.httpClient;

                string route = "";
                var geturi = setting.uris.TryGetValue("CreateCardVoucher", out route).ToString();
                var url = setting.host + route;

                string authParams = string.Format("{0}:{1}", setting.userName, setting.password);
                byte[] bytes = Encoding.UTF8.GetBytes(authParams);
                string encodedAuthParams = Convert.ToBase64String(bytes);
                httpClient.DefaultRequestHeaders.Add("Authorization", "Basic " + encodedAuthParams.ToString());
                StringContent Content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(url, Content);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string content = await response.Content.ReadAsStringAsync();
                    var data = JsonConvert.DeserializeObject<CreateVoucherResponseDto>(content);
                    return data;
                }
                else
                {
                    responseData.resultMessage = "خطای منبع";
                    responseData.resultType = "error";
                    responseData.resultCode = -1;
                    return responseData;
                }

            }
            catch (Exception ex)
            {
                responseData.resultMessage = "خطای ناشناخته";
                responseData.resultType = "خطای ناشناخته";
                responseData.resultCode = -99;
                return responseData;
            }
        }
        public async Task<VoucherInfoListResponseDto> VoucherInfoList(VoucherListRequestDto request, string cardSerial)
        {
            var responseData = new VoucherInfoListResponseDto();
            try
            {
                //Proxy pclient = this.CreateInstance("TosanSoha");
                //ProxySetting setting = pclient.Setting;
                //HttpClient httpClient = pclient.httpClient;

                //string route = "";
                //var geturi = setting.uris.TryGetValue("VoucherInfoList", out route).ToString();
                //var url = setting.host + route + "?cardSerial=" + cardSerial;

                //string authParams = string.Format("{0}:{1}", setting.userName, setting.password);
                //byte[] bytes = Encoding.UTF8.GetBytes(authParams);
                //string encodedAuthParams = Convert.ToBase64String(bytes);
                //httpClient.DefaultRequestHeaders.Add("Authorization", "Basic " + encodedAuthParams.ToString());
                //StringContent Content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
                //var response = await httpClient.PostAsync(url, Content);
                //if (response.StatusCode == HttpStatusCode.OK)
                //{
                //string content = await response.Content.ReadAsStringAsync();
                string content = @"
                                {
                                   
                                        'pageIndex': 0,
                                        'pageSize': 10,
                                        'totalCount': 5,
                                        'totalPages': 1,
                                        'indexFrom': 0,
                                        'items': [
                                            {
                                                    'voucherId': 1000111839,
                                                'created': '2021-01-27T17:33:39.39968',
                                                'modified': '2021-01-27T17:33:39.9669418',
                                                'cardId': 2026271320,
                                                'amount': 1000,
                                                'expireDate': '2021-04-27T00:00:00',
                                                'requestUniqueId': 130011,
                                                'status': 8,
                                                
                                                'consumeStationId': null,
                                                'readerCode': null,
                                                'remainAmountAfterCharge': null,
                                                'consumerSerial': null,
                                                'stationName': null,
                                                'collectDateTime': null,
                                                'seqNumber': null,
                                                'remainAmountBeforeCharge': null,
                                                'depositAmount': null,
                                                'cardTypeCode': null,
                                                'consumeDeviceType': 1
                                            },
                                            {
                                                    'voucherId': 1000111819,
                                                'created': '2021-01-27T17:05:49.0994847',
                                                'modified': '2021-01-27T17:05:49.3366891',
                                                'cardId': 2026271320,
                                                'amount': 1000,
                                                'expireDate': '2021-04-27T00:00:00',
                                                'requestUniqueId': 137011,
                                                'status': 1,
                                                
                                                'consumeStationId': null,
                                                'readerCode': null,
                                                'remainAmountAfterCharge': null,
                                                'consumerSerial': null,
                                                'stationName': null,
                                                'collectDateTime': null,
                                                'seqNumber': null,
                                                'remainAmountBeforeCharge': null,
                                                'depositAmount': null,
                                                'cardTypeCode': null,
                                                'consumeDeviceType': 1
                                            },
                                            {
                                                    'voucherId': 1000111124,
                                                'created': '2021-01-26T07:49:25.5400267',
                                                'modified': '2021-01-26T07:49:25.7739084',
                                                'cardId': 2026271320,
                                                'amount': 100000,
                                                'expireDate': '2021-04-26T00:00:00',
                                                'requestUniqueId': 782095,
                                                'status': 1,
                                                
                                                'consumeStationId': null,
                                                'readerCode': null,
                                                'remainAmountAfterCharge': null,
                                                'consumerSerial': null,
                                                'stationName': null,
                                                'collectDateTime': null,
                                                'seqNumber': null,
                                                'remainAmountBeforeCharge': null,
                                                'depositAmount': null,
                                                'cardTypeCode': null,
                                                'consumeDeviceType': 1
                                            },
                                            {
                                                    'voucherId': 1000088187,
                                                'created': '2020-11-20T12:58:05.3036663',
                                                'modified': '2020-11-26T15:20:00.6770957',
                                                'cardId': 2026271320,
                                                'amount': 100000,
                                                'expireDate': '2021-02-18T00:00:00',
                                                'requestUniqueId': 484234,
                                                'status': 2,
                                                'consumeDateTime': '2020-11-26T15:08:49',
                                                'consumeStationId': 211,
                                                'readerCode': 6843,
                                                'remainAmountAfterCharge': 259957,
                                                'consumerSerial': null,
                                                '(ولیعصر )عج': 'stationName ',
                                                'collectDateTime': '2020-11-26T15:11:06.5215553',
                                                'seqNumber': 50,
                                                'remainAmountBeforeCharge': 159957,
                                                'depositAmount': 5000,
                                                'cardTypeCode': 132,
                                                'consumeDeviceType': 1
                                            },
                                            {
                                                    'voucherId': 1000073339,
                                                'created': '2020-11-10T10:10:53.5853687',
                                                'modified': '2020-11-11T18:20:00.9238908',
                                                'cardId': 2026271320,
                                                'amount': 20000,
                                                'expireDate': '2021-02-08T00:00:00',
                                                'requestUniqueId': 430070,
                                                'status': 2,
                                                'consumeDateTime': '2020-11-11T18:05:05',
                                                'consumeStationId': 210,
                                                'readerCode': 6860,
                                                'remainAmountAfterCharge': 122247,
                                                'consumerSerial': null,
                                                'انقالب اسالمي': 'stationName ',
                                                'collectDateTime': '2020-11-11T18:06:58.0931108',
                                                'seqNumber': 28,
                                                'remainAmountBeforeCharge': 102247,
                                                'depositAmount': 5000,
                                                'cardTypeCode': 132,
                                                'consumeDeviceType': 1
                                            }
                                        ],
                                        'hasPreviousPage': false,
                                        'hasNextPage': false
                                    ,
                                    'resultCode': 100,
                                    'resultType': 'OK',
                                    'موفق': 'resultMessage '
                                }
                    ";
                var data = JsonConvert.DeserializeObject<VoucherInfoListResponseDto>(content);
                return data;
                //}
                //else
                //{
                //    responseData.ResultType = "error";
                //    responseData.ResultCode = -1;
                //    return responseData;
                //}

            }
            catch (Exception ex)
            {
                responseData.ResultType = "خطای ناشناخته";
                responseData.ResultCode = -99;
                return responseData;
            }
        }
        #endregion

        #region SingleDirectTicket
        public async Task<ClientBalance> ClientBalance(string clientUniqueId)
        {
            var responseData = new ClientBalance();
            try
            {
                Proxy pclient = this.CreateInstance("TosanSoha");
                ProxySetting setting = pclient.Setting;
                HttpClient httpClient = pclient.httpClient;

                string route = "";
                var geturi = setting.uris.TryGetValue("ClientBalance", out route).ToString();
                var url = setting.host + route + "/" + clientUniqueId;

                string authParams = string.Format("{0}:{1}", setting.userName, setting.password);
                byte[] bytes = Encoding.UTF8.GetBytes(authParams);
                string encodedAuthParams = Convert.ToBase64String(bytes);
                httpClient.DefaultRequestHeaders.Add("Authorization", "Basic " + encodedAuthParams.ToString());
                var response = await httpClient.GetAsync(url);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string content = await response.Content.ReadAsStringAsync();
                    var data = JsonConvert.DeserializeObject<ClientBalance>(content);
                    return data;
                }
                else
                {
                    responseData.message = "خطا در دریافت";
                    responseData.resultCode = -1;
                    responseData.technicalMessage = "Error";
                    responseData.balance = 0;

                    return responseData;
                }
            }
            catch (Exception ex)
            {
                responseData.message = "خطا ناشناخته";
                responseData.resultCode = -99;
                responseData.technicalMessage = "Error";
                responseData.balance = 0;

                return responseData;
            }
        }
        public async Task<VoucherPriceInquiry> VoucherPriceInquiry(int voucherMunicipalCode)
        {
            var responseData = new VoucherPriceInquiry();
            try
            {
                Proxy pclient = this.CreateInstance("TosanSoha");
                ProxySetting setting = pclient.Setting;
                HttpClient httpClient = pclient.httpClient;

                string route = "";
                var geturi = setting.uris.TryGetValue("VoucherPriceInquiry", out route).ToString();
                var url = setting.host + route + "/" + voucherMunicipalCode;

                string authParams = string.Format("{0}:{1}", setting.userName, setting.password);
                byte[] bytes = Encoding.UTF8.GetBytes(authParams);
                string encodedAuthParams = Convert.ToBase64String(bytes);
                httpClient.DefaultRequestHeaders.Add("Authorization", "Basic " + encodedAuthParams.ToString());
                var response = await httpClient.GetAsync(url);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string content = await response.Content.ReadAsStringAsync();
                    var data = JsonConvert.DeserializeObject<VoucherPriceInquiry>(content);
                    return data;
                }
                else
                {
                    return null;
                }

            }
            catch (Exception ex)
            {
                return null;
            }
        }
        public async Task<SingleDirectVoucherSellResponseDto> VoucherSell(SingleDirectVoucherSellRequestDto request)
        {
            //var result = JsonConvert.DeserializeObject<SingleDirectVoucherSellResponseDto>("{\"voucherDetailsModel\":[{\"serialNo\": \"4039407772987025\",\"voucher\": \"AQFGgzJWB0KL+0zcnGoHYyd/btE+J5GiFDnoeWPhT2SUDTnWmA4=\",\"expireDate\": \"1402/05/29\",\"expireDateEn\": 1692477000000}],\"resultCode\": 0,\"message\": null,\"technicalMessage\": null}");
            //return result;
            var responseData = new SingleDirectVoucherSellResponseDto();
            try
            {
                Proxy pclient = this.CreateInstance("TosanSoha");
                ProxySetting setting = pclient.Setting;
                HttpClient httpClient = pclient.httpClient;

                string route = "";
                var geturi = setting.uris.TryGetValue("VoucherSell", out route).ToString();
                var url = setting.host + route;

                string authParams = string.Format("{0}:{1}", setting.userName, setting.password);
                byte[] bytes = Encoding.UTF8.GetBytes(authParams);
                string encodedAuthParams = Convert.ToBase64String(bytes);
                httpClient.DefaultRequestHeaders.Add("Authorization", "Basic " + encodedAuthParams.ToString());
                StringContent Content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync(url, Content);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string content = await response.Content.ReadAsStringAsync();
                    var data = JsonConvert.DeserializeObject<SingleDirectVoucherSellResponseDto>(content);
                    return data;
                }
                else
                {
                    responseData.message = "خطا در دریافت";
                    responseData.resultCode = -1;
                    responseData.technicalMessage = "Error";
                    responseData.voucherDetailsModel = null;

                    return responseData;
                }
            }
            catch (Exception ex)
            {
                responseData.message = "خطا ناشناخته";
                responseData.resultCode = -99;
                responseData.technicalMessage = "Error";
                responseData.voucherDetailsModel = null;

                return responseData;
            }
        }
        public async Task<SingleDirectVoucherSellResponseDto> VoucherSellInquiry(InquirySellTicketRequestDto request)
        {
            var responseData = new SingleDirectVoucherSellResponseDto();
            try
            {
                Proxy pclient = this.CreateInstance("TosanSoha");
                ProxySetting setting = pclient.Setting;
                HttpClient httpClient = pclient.httpClient;

                string route = "";
                var geturi = setting.uris.TryGetValue("VoucherSellInquiry", out route).ToString();
                var url = setting.host + route + $"/{request.clientUniqueId}/{request.operatorUniqueId}/{request.voucherMunicipalCode}/{request.referenceId}/{request.fast}";
                string authParams = string.Format("{0}:{1}", setting.userName, setting.password);
                byte[] bytes = Encoding.UTF8.GetBytes(authParams);
                string encodedAuthParams = Convert.ToBase64String(bytes);
                httpClient.DefaultRequestHeaders.Add("Authorization", "Basic " + encodedAuthParams.ToString());


                var response = await httpClient.GetAsync(url);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string content = await response.Content.ReadAsStringAsync();
                    var data = JsonConvert.DeserializeObject<SingleDirectVoucherSellResponseDto>(content);
                    return data;
                }
                else
                {
                    responseData.message = "خطا در دریافت";
                    responseData.resultCode = -1;
                    responseData.technicalMessage = "Error";
                    responseData.voucherDetailsModel = null;

                    return responseData;
                }
            }
            catch (Exception ex)
            {
                responseData.message = "خطا ناشناخته";
                responseData.resultCode = -99;
                responseData.technicalMessage = "Error";
                responseData.voucherDetailsModel = null;

                return responseData;
            }
        }

        public async Task<VoucherHistoryResponseDto> VoucherHistory(VoucherHistoryRequestDto request)
        {
            var responseData = new VoucherHistoryResponseDto();
            try
            {
                Proxy pclient = this.CreateInstance("TosanSoha");
                ProxySetting setting = pclient.Setting;
                HttpClient httpClient = pclient.httpClient;

                string route = "";
                var geturi = setting.uris.TryGetValue("VoucherHistory", out route).ToString();
                var url = setting.host + route + $"/{request.phoneNumber}/{request.voucherMunicipalCode}/{request.offset}/{request.length}";
                string authParams = string.Format("{0}:{1}", setting.userName, setting.password);
                byte[] bytes = Encoding.UTF8.GetBytes(authParams);
                string encodedAuthParams = Convert.ToBase64String(bytes);
                httpClient.DefaultRequestHeaders.Add("Authorization", "Basic " + encodedAuthParams.ToString());
                var response = await httpClient.GetAsync(url);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string content = await response.Content.ReadAsStringAsync();
                    var data = JsonConvert.DeserializeObject<VoucherHistoryResponseDto>(content);
                    return data;
                }
                else
                {
                    responseData.Message = "خطا در دریافت";
                    responseData.ResultCode = -1;
                    responseData.TechnicalMessage = "Error";
                    responseData.Vouchers = null;

                    return responseData;
                }
            }
            catch (Exception ex)
            {
                responseData.Message = "خطا ناشناخته";
                responseData.ResultCode = -99;
                responseData.TechnicalMessage = "Error";
                responseData.Vouchers = null;

                return responseData;
            }
        }
        #endregion
    }

    public interface ITosanSohaServiceProxy
    {
        #region GateChargeCard
        Task<TicketGetBalanceDto> GetBalanceCompanyCard();
        Task<IsValidCardTypeDto> IsValidCardType(long? cardserial);
        Task<CreateVoucherResponseDto> CreateVoucher(CreateVoucherRequestDto request);
        Task<VoucherInfoListResponseDto> VoucherInfoList(VoucherListRequestDto request, string cardSerial);
        #endregion

        #region SingleDirectTicket
        Task<ClientBalance> ClientBalance(string clientUniqueId);
        Task<VoucherPriceInquiry> VoucherPriceInquiry(int voucherMunicipalCode);
        Task<SingleDirectVoucherSellResponseDto> VoucherSell(SingleDirectVoucherSellRequestDto request);
        Task<SingleDirectVoucherSellResponseDto> VoucherSellInquiry(InquirySellTicketRequestDto request);
        Task<VoucherHistoryResponseDto> VoucherHistory(VoucherHistoryRequestDto request);
        #endregion
    }
}
