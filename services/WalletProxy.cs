using AutoMapper;
using Dto.Proxy.Request.Wallet;
using Dto.Proxy.Response.Wallet;
using Dto.Proxy.Wallet;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Pigi.MDbLogging;
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
using static ProxyService.services.WalletProxy;

namespace ProxyService.services
{
    public class WalletProxy : HttpClientBase, IWalletProxy, ILoggable
    {
        #region Private Variables       
        private readonly IMapper _mapper;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IPecBmsSetting _setting;

        public IHostEnvironment Env { get; }

        #endregion
        #region Ctor
        public WalletProxy(IHttpClientFactory clientFactoy, IPecBmsSetting setting, IConfiguration configuration, IMapper mapper, IHttpContextAccessor httpContextAccessor, IHostEnvironment env) : base(clientFactoy, configuration)
        {
            _mapper = mapper;
            _httpContextAccessor = httpContextAccessor;
            Env = env;
            _setting = setting;
        }
        #endregion



        [LoggingAspect]
        public async Task<WalletBalanceResponseDto> GetBalanceWallet(WalletBalanceRequestDto input)
        {
            var walletResponse = new WalletBalanceResponseDto();
            try
            {
                #region Creat proxy model
                var MapModel = _mapper.Map<WalletBalanceRequestModel>(input);
                #endregion

                Proxy pclient = this.CreateInstance("Walet");
                ProxySetting setting = pclient.Setting;
                HttpClient httpClient = pclient.httpClient;

                string BalanceWallet = "";
                var geturi = setting.uris.TryGetValue("GetBalanceWallet", out BalanceWallet).ToString();
                var url = setting.host + BalanceWallet;
                MapModel.CorporationPIN = _setting.CorporationPIN;
                string authParams = string.Format("{0}:{1}", setting.userName, setting.password);
                byte[] bytes = Encoding.UTF8.GetBytes(authParams);
                string encodedAuthParams = Convert.ToBase64String(bytes);
                httpClient.DefaultRequestHeaders.Add("Authorization", "Basic " + encodedAuthParams.ToString());
                StringContent Content = new StringContent(JsonConvert.SerializeObject(MapModel), Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(url, Content);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string content = await response.Content.ReadAsStringAsync();
                    var data = JsonConvert.DeserializeObject<WalletProxyResponse<WalletBalanceResponseModel>>(content);
                    var responseMapModel = _mapper.Map<WalletBalanceResponseDto>(data.Keys.Key);
                    walletResponse = responseMapModel;
                    return walletResponse;
                }
                else
                {
                    walletResponse.Amount = 0;
                    walletResponse.ResultDesc = "error";
                    walletResponse.ResultId = -1;
                    return walletResponse;
                }
            }
            catch (Exception ex)
            {
                walletResponse.Amount = 0;
                walletResponse.ResultDesc = "خطای ناشناخته";
                walletResponse.ResultId = -99;
                return walletResponse;
            }
        }
        [LoggingAspect]
        public async Task<List<MerchantWalletResponseDto>> GetMerchantWallet(MerchantWalletRequestDto inputDto)
        {
            var merchantWallet = new List<MerchantWalletResponseDto>();
            MerchantWalletResponseDto dto = new MerchantWalletResponseDto();
            try
            {
                #region Creat proxy model
                var MapModel = _mapper.Map<MerchantWalletRequestModel>(inputDto);
                #endregion

                Proxy pclient = this.CreateInstance("Walet");
                ProxySetting setting = pclient.Setting;
                HttpClient httpClient = pclient.httpClient;
                string walletUri = "";
                var geturi = setting.uris.TryGetValue("GetMerchantWallet", out walletUri).ToString();
                var url = setting.host + walletUri;

                string authParams = string.Format("{0}:{1}", setting.userName, setting.password);
                byte[] bytes = Encoding.UTF8.GetBytes(authParams);
                string encodedAuthParams = Convert.ToBase64String(bytes);
                httpClient.DefaultRequestHeaders.Add("Authorization", "Basic " + encodedAuthParams.ToString());
                StringContent Content = new StringContent(JsonConvert.SerializeObject(MapModel), Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(url, Content);
                string content = await response.Content.ReadAsStringAsync();
                if (response.StatusCode == HttpStatusCode.OK)
                {

                    try
                    {
                        var data = JsonConvert.DeserializeObject<WalletProxyResponse<MerchantWalletResponseDto>>(content);
                        if (data.Keys != null)
                        {
                            dto = new MerchantWalletResponseDto()
                            {
                                AdditionalData = data.Keys.Key.AdditionalData,
                                Amount = data.Keys.Key.Amount,
                                CorporationUserId = data.Keys.Key.CorporationUserId,
                                CorporationUserTitle = data.Keys.Key.CorporationUserTitle,
                                CustomerFirstName = data.Keys.Key.CustomerFirstName,
                                CustomerId = data.Keys.Key.CustomerId,
                                CustomerLastName = data.Keys.Key.CustomerLastName,
                                CustomerWalletId = data.Keys.Key.CustomerWalletId,
                                GroupWalletTitle = data.Keys.Key.GroupWalletTitle,
                                GroupWalletId = data.Keys.Key.GroupWalletId,
                                IsActive = data.Keys.Key.IsActive,
                                IsChargeable = data.Keys.Key.IsChargeable,
                                IsDestinationTransfer = data.Keys.Key.IsDestinationTransfer,
                                IsMain = data.Keys.Key.IsMain,
                                IsPurchase = data.Keys.Key.IsPurchase,
                                IsSettlement = data.Keys.Key.IsSettlement,
                                IsSupportMain = data.Keys.Key.IsSupportMain,
                                IsTransfer = data.Keys.Key.IsTransfer,
                            };
                            merchantWallet.Add(dto);
                        }
                        return merchantWallet;


                    }
                    catch (Exception ex)
                    {
                        var data = JsonConvert.DeserializeObject<WalletProxyResponse<List<MerchantWalletResponseDto>>>(content);
                        foreach (var item in data.Keys.Key)
                        {
                            merchantWallet.Add(item);
                        }
                        return merchantWallet;
                    }
                }
                else
                {
                    return merchantWallet;
                }
            }
            catch (Exception)
            {
                return merchantWallet;
            }


        }
        [LoggingAspect]
        public async Task<List<GetCustomerWalletResponseDto>> GetCustomerWallet(GetCustomerWalletRequestDto inputDto)
        {
            var merchantWallet = new List<GetCustomerWalletResponseDto>();
            GetCustomerWalletResponseDto dto = new GetCustomerWalletResponseDto();
            try
            {
                #region Creat proxy model
                var MapModel = _mapper.Map<GetCustomerWalletRequestModel>(inputDto);
                #endregion

                Proxy pclient = this.CreateInstance("Walet");
                ProxySetting setting = pclient.Setting;
                HttpClient httpClient = pclient.httpClient;
                string walletUri = "";
                var geturi = setting.uris.TryGetValue("GetCustomerWallet", out walletUri).ToString();
                var url = setting.host + walletUri;

                string authParams = string.Format("{0}:{1}", setting.userName, setting.password);
                byte[] bytes = Encoding.UTF8.GetBytes(authParams);
                string encodedAuthParams = Convert.ToBase64String(bytes);
                httpClient.DefaultRequestHeaders.Add("Authorization", "Basic " + encodedAuthParams.ToString());
                StringContent Content = new StringContent(JsonConvert.SerializeObject(MapModel), Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(url, Content);
                string content = await response.Content.ReadAsStringAsync();
                if (response.StatusCode == HttpStatusCode.OK)
                {

                    try
                    {
                        var data = JsonConvert.DeserializeObject<WalletProxyResponse<GetCustomerWalletResponseModel>>(content, new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore });
                        var responseMapModel = _mapper.Map<GetCustomerWalletResponseDto>(data.Keys.Key);
                        if (responseMapModel != null)
                        {
                            dto = new GetCustomerWalletResponseDto()
                            {
                                AdditionalData = responseMapModel.AdditionalData,
                                Amount = responseMapModel.Amount,
                                CorporationUserId = responseMapModel.CorporationUserId,
                                CorporationUserTitle = responseMapModel.CorporationUserTitle,
                                CustomerFirstName = responseMapModel.CustomerFirstName,
                                CustomerId = responseMapModel.CustomerId,
                                CustomerLastName = responseMapModel.CustomerLastName,
                                CustomerWalletId = responseMapModel.CustomerWalletId,
                                GroupWalletTitle = responseMapModel.GroupWalletTitle,
                                GroupWalletId = responseMapModel.GroupWalletId,
                                IsActive = responseMapModel.IsActive,
                                IsChargeable = responseMapModel.IsChargeable,
                                IsDestinationTransfer = responseMapModel.IsDestinationTransfer,
                                IsMain = responseMapModel.IsMain,
                                IsPurchase = responseMapModel.IsPurchase,
                                IsSettlement = responseMapModel.IsSettlement,
                                IsSupportMain = responseMapModel.IsSupportMain,
                                IsTransfer = responseMapModel.IsTransfer,
                                CustomerAgencyCode = responseMapModel.CustomerAgencyCode,
                                CustomerMerchantName = responseMapModel.CustomerMerchantName,
                                CustomerSettlementIBAN = responseMapModel.CustomerSettlementIBAN,
                                IsChargeableDeposit = responseMapModel.IsChargeableDeposit
                            };
                            dto.WalletCode = inputDto.NationalCode;
                            merchantWallet.Add(dto);
                        }
                        return merchantWallet;


                    }
                    catch (Exception ex)
                    {
                        var data = JsonConvert.DeserializeObject<WalletProxyResponse<List<GetCustomerWalletResponseModel>>>(content);
                        var responseMapModel = _mapper.Map<List<GetCustomerWalletResponseDto>>(data.Keys.Key);
                        foreach (var item in responseMapModel)
                        {
                            item.WalletCode = inputDto.NationalCode;
                            merchantWallet.Add(item);
                        }
                        return merchantWallet;
                    }
                }
                else
                {
                    return merchantWallet;
                }
            }
            catch (Exception)
            {
                return merchantWallet;
            }


        }

        [LoggingAspect]
        public async Task<WalletBillPaymentResponseDto> BillPayment(WalletBillPaymentRequestDto billPaymentRequestDto)
        {
            WalletBillPaymentResponseDto billPayResp = new WalletBillPaymentResponseDto();

            try
            {
                #region Creat proxy model
                var MapModel = _mapper.Map<WalletBillPaymentRequestModel>(billPaymentRequestDto);
                #endregion



                Proxy pclient = this.CreateInstance("Walet");
                ProxySetting setting = pclient.Setting;
                HttpClient httpClient = pclient.httpClient;


                string BillPaymenturi = "";
                var geturi = setting.uris.TryGetValue("WalletBillPayment", out BillPaymenturi).ToString();
                var url = setting.host + BillPaymenturi;


                string authParams = string.Format("{0}:{1}", setting.userName, setting.password);
                byte[] bytes = Encoding.UTF8.GetBytes(authParams);
                string encodedAuthParams = Convert.ToBase64String(bytes);
                httpClient.DefaultRequestHeaders.Add("Authorization", "Basic " + encodedAuthParams.ToString());
                StringContent Content = new StringContent(JsonConvert.SerializeObject(MapModel), Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(url, Content);
                string content = await response.Content.ReadAsStringAsync();
                if (response.StatusCode == HttpStatusCode.OK)
                {

                    try
                    {
                        var data = JsonConvert.DeserializeObject<WalletProxyResponse<WalletBillPaymentResponseModel>>(content);
                        if (data.Keys != null)
                        {
                            var responseMapModel = _mapper.Map<WalletBillPaymentResponseDto>(data.Keys.Key);
                            billPayResp = responseMapModel;
                            return billPayResp;
                        }
                        else
                        {
                            billPayResp.ResultDesc = "خطا درپرداخت قبض";
                            billPayResp.ResultId = -1;
                            billPayResp.ReturnAmount = 0;
                            billPayResp.ReturnDate = DateTime.Now;
                            billPayResp.ReturnId = 0;
                            billPayResp.ReturnOrderId = 0;
                            return billPayResp;
                        }
                    }
                    catch (Exception ex)
                    {

                        var data = JsonConvert.DeserializeObject<WalletProxyResponse<NillClass>>(content);
                        if (data.Keys != null)
                        {
                            if (data.Keys.Key.ResultId != 0)
                            {
                                billPayResp.ResultDesc = data.Keys.Key.ResultDesc;
                                billPayResp.ResultId = data.Keys.Key.ResultId;
                                billPayResp.ReturnAmount = 0;
                                billPayResp.ReturnDate = DateTime.Now;
                                billPayResp.ReturnId = 0;
                                billPayResp.ReturnOrderId = 0;

                            }
                            //var responseMapModel = _mapper.Map<WalletBillPaymentResponseDto>(data.Keys.Key);
                            //billPayResp = responseMapModel;
                            //return billPayResp;
                            return billPayResp;
                        }
                        else
                        {
                            billPayResp.ResultDesc = "خطا درپرداخت قبض";
                            billPayResp.ResultId = -1;
                            billPayResp.ReturnAmount = 0;
                            billPayResp.ReturnDate = DateTime.Now;
                            billPayResp.ReturnId = 0;
                            billPayResp.ReturnOrderId = 0;
                            return billPayResp;
                        }
                    }

                }
                else
                {
                    billPayResp.ResultDesc = "error";
                    billPayResp.ResultId = -1;
                    billPayResp.ReturnAmount = 0;
                    billPayResp.ReturnDate = DateTime.Now;
                    billPayResp.ReturnId = 0;
                    billPayResp.ReturnOrderId = 0;
                    return billPayResp;
                }

            }
            catch (Exception ex)
            {

                billPayResp.ResultDesc = "خطای ناشناخته";
                billPayResp.ResultId = -99;
                billPayResp.ReturnAmount = 0;
                billPayResp.ReturnDate = DateTime.Now;
                billPayResp.ReturnId = 0;
                billPayResp.ReturnOrderId = 0;
                return billPayResp;
            }

        }

        public async Task<WalletPurchaseResponseDto> WalletPurchase(WalletPurchaseResquestDto walletPurchaseRequest)
        {
            WalletPurchaseResponseDto responseDto = new WalletPurchaseResponseDto();
            try
            {
                #region Creat proxy model
                var MapModel = _mapper.Map<WalletPurchaseRequestModel>(walletPurchaseRequest);
                #endregion

                Proxy pclient = this.CreateInstance("Walet");
                ProxySetting setting = pclient.Setting;
                HttpClient httpClient = pclient.httpClient;
                var geturi = setting.uris.TryGetValue("WalletPurchase", out string walletUri).ToString();
                var url = setting.host + walletUri;

                string authParams = string.Format("{0}:{1}", setting.userName, setting.password);
                byte[] bytes = Encoding.UTF8.GetBytes(authParams);
                string encodedAuthParams = Convert.ToBase64String(bytes);
                httpClient.DefaultRequestHeaders.Add("Authorization", "Basic " + encodedAuthParams.ToString());
                StringContent Content = new StringContent(JsonConvert.SerializeObject(MapModel), Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync(url, Content);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string content = await response.Content.ReadAsStringAsync();
                    try
                    {
                        var data = JsonConvert.DeserializeObject<WalletProxyResponse<WalletPurchaseResponseModel>>(content);
                        if (data.Keys != null)
                        {
                            var responseMapModel = _mapper.Map<WalletPurchaseResponseDto>(data.Keys.Key);
                            return responseMapModel;
                        }



                    }
                    catch (Exception ex)
                    {
                        responseDto.ResultId = -2;
                        responseDto.ResultDesc = "خطا در انجام تراکنش خرید کیف پول";
                    }
                }
                else
                {
                    responseDto.ResultId = -1;
                    responseDto.ResultDesc = "خطا در انجام تراکنش خرید کیف پول";
                }
            }
            catch (Exception ex)
            {
                responseDto.ResultId = -99;
                responseDto.ResultDesc = "خطا در انجام تراکنش خرید کیف پول";

            }
            return responseDto;
        }
        public async Task<CustomerWalletResponseDto> AddOrUpdateCustomerWallet(CustomerWalletRequestDto input)
        {
            CustomerWalletResponseDto responseDto = new CustomerWalletResponseDto();
            try
            {
                #region Creat proxy model
                var MapModel = _mapper.Map<CustomerWalletRequestModel>(input);
                #endregion

                Proxy pclient = this.CreateInstance("Walet");
                ProxySetting setting = pclient.Setting;
                HttpClient httpClient = pclient.httpClient;
                var geturi = setting.uris.TryGetValue("AddCustomerWallet", out string walletUri).ToString();
                var url = setting.host + walletUri;

                string authParams = string.Format("{0}:{1}", setting.userName, setting.password);
                byte[] bytes = Encoding.UTF8.GetBytes(authParams);
                string encodedAuthParams = Convert.ToBase64String(bytes);
                httpClient.DefaultRequestHeaders.Add("Authorization", "Basic " + encodedAuthParams.ToString());
                StringContent Content = new StringContent(JsonConvert.SerializeObject(MapModel), Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync(url, Content);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string content = await response.Content.ReadAsStringAsync();
                    try
                    {
                        var data = JsonConvert.DeserializeObject<WalletProxyResponse<CustomerWalletResponseDto>>(content);
                        if (data.Keys != null)
                        {
                            var responseMapModel = _mapper.Map<CustomerWalletResponseDto>(data.Keys.Key);
                            return responseMapModel;
                        }
                    }
                    catch (Exception ex)
                    {
                        if (Env.IsDevelopment())
                        {
                            responseDto.ResultId = -2;
                            responseDto.ResultDesc = ex.Message + "WalletProxy Ex 1";
                        }
                        else
                        {
                            responseDto.ResultId = -2;
                            responseDto.ResultDesc = "خطا در نرمال سازی شی بازگشتی";
                        }

                    }
                }
                else
                {
                    responseDto.ResultId = -1;
                    responseDto.ResultDesc = "خطا در درج مشتری کیف پول";
                }
            }
            catch (Exception ex)
            {
                if (Env.IsDevelopment())
                {
                    responseDto.ResultId = -99;
                    responseDto.ResultDesc = ex.Message + "WalletProxy Ex 2";
                }
                else
                {
                    responseDto.ResultId = -99;
                    responseDto.ResultDesc = "خطا در فراخوانی سرویس";
                }

            }
            return responseDto;
        }

        public async Task<WalletTransferResponseDto> WalletTransfer(WalletTransferResquestDto walletTransferRequest)
        {

            WalletTransferResponseDto responseDto = new WalletTransferResponseDto();
            try
            {
                #region Creat proxy model
                var MapModel = _mapper.Map<WalletTransferRequestModel>(walletTransferRequest);
                #endregion

                Proxy pclient = this.CreateInstance("Walet");
                ProxySetting setting = pclient.Setting;
                HttpClient httpClient = pclient.httpClient;
                string walletUri = "";
                var geturi = setting.uris.TryGetValue("WalletTransfer", out walletUri).ToString();
                var url = setting.host + walletUri;

                string authParams = string.Format("{0}:{1}", setting.userName, setting.password);
                byte[] bytes = Encoding.UTF8.GetBytes(authParams);
                string encodedAuthParams = Convert.ToBase64String(bytes);
                httpClient.DefaultRequestHeaders.Add("Authorization", "Basic " + encodedAuthParams.ToString());
                StringContent Content = new StringContent(JsonConvert.SerializeObject(MapModel), Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync(url, Content);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string content = await response.Content.ReadAsStringAsync();
                    try
                    {
                        var data = JsonConvert.DeserializeObject<WalletProxyResponse<WalletTransferResponseModel>>(content);
                        if (data.Keys != null)
                        {
                            var responseMapModel = _mapper.Map<WalletTransferResponseDto>(data.Keys.Key);
                            return responseMapModel;
                        }
                    }
                    catch (Exception ex)
                    {
                        responseDto.ResultId = -2;
                        responseDto.ResultDesc = "خطا در انجام تراکنش انتقال کیف پول";
                    }
                }
                else
                {
                    responseDto.ResultId = -1;
                    responseDto.ResultDesc = "خطا در انجام تراکنش انتقال کیف پول";
                }
            }
            catch (Exception ex)
            {
                responseDto.ResultId = -99;
                responseDto.ResultDesc = "خطا در انجام تراکنش انتقال کیف پول";

            }
            return responseDto;
        }

        public interface IWalletProxy
        {
            Task<CustomerWalletResponseDto> AddOrUpdateCustomerWallet(CustomerWalletRequestDto input);
            Task<WalletBalanceResponseDto> GetBalanceWallet(WalletBalanceRequestDto input);
            Task<List<MerchantWalletResponseDto>> GetMerchantWallet(MerchantWalletRequestDto inputDto);
            Task<List<GetCustomerWalletResponseDto>> GetCustomerWallet(GetCustomerWalletRequestDto inputDto);
            Task<WalletBillPaymentResponseDto> BillPayment(WalletBillPaymentRequestDto inputDto);
            Task<WalletPurchaseResponseDto> WalletPurchase(WalletPurchaseResquestDto walletPurchaseRequest);
            Task<WalletTransferResponseDto> WalletTransfer(WalletTransferResquestDto walletTransferRequest);
        }
    }
}
