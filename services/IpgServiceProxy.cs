using AutoMapper;
using Microsoft.Extensions.Configuration;
using BillServiceIpg;
using ConfirmService;
using Dto.Proxy.IPG;
using Dto.Proxy.Request.IPG;
using Dto.Proxy.Response.IPG;
using Pigi.MDbLogging;
using ProxyService.ProxyModel.Request.IPG;
using ProxyService.ProxyModel.Response.IPG;
using ReversalService;
using SaleService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using PEC.CoreCommon.Attribute;
using ProxyService.Shared;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Http;

namespace ProxyService.services
{

    public class IpgServiceProxy : HttpClientBase, IIpgServiceProxy, ILoggable
    {

        #region Private Variables
        private readonly SaleServiceSoap _ipgSaleService;
        private readonly ConfirmServiceSoap _confirm;
        private readonly ReversalServiceSoap _reversal;
        private readonly BillServiceSoap _billService;
        private readonly IMapper _mapper;
        private readonly IMdbLogger<IpgServiceProxy> _logger;
       
        #endregion
        #region Ctor
        public IpgServiceProxy(IMdbLogger<IpgServiceProxy> logger,IHttpClientFactory clientFactoy,
            IConfiguration configuration,
            SaleServiceSoap IpgSale,
            ConfirmServiceSoap confirmService,
            ReversalServiceSoap reversalService,
            BillServiceSoap billService,
            
            IMapper mapper) : base(clientFactoy, configuration)
        {
            _ipgSaleService = IpgSale;
            _confirm = confirmService;
            _mapper = mapper;
            _reversal = reversalService;
            _billService = billService;
            _logger = logger;
        

        }
        #endregion

        [LoggingAspect]
        public async Task<IpgSaleServiceResponseDto> GetIpgSaleToken(IpgSaleServiceRequestDto req)
        {
            var Response = new IpgSaleServiceResponseDto();

            #region Creat proxy model
            var ipgSaleServiceRequest = _mapper.Map<IpgSaleServiceRequestModel>(req);
            ipgSaleServiceRequest.Originator = "";
            ipgSaleServiceRequest.AdditionalData = "";
            #endregion
            try
            {
                SaleService.SalePaymentRequestRequest body = new SaleService.SalePaymentRequestRequest();             
                SalePaymentRequestRequestBody requestBody = new SalePaymentRequestRequestBody();
                body.Body = new SalePaymentRequestRequestBody();
                body.Body.requestData = new ClientSaleRequestData();
                body.Body.requestData.AdditionalData = "";
                body.Body.requestData.Amount = ipgSaleServiceRequest.Amount;
                body.Body.requestData.CallBackUrl = ipgSaleServiceRequest.CallBackUrl;
                body.Body.requestData.LoginAccount = ipgSaleServiceRequest.LoginAccount;
                body.Body.requestData.OrderId = ipgSaleServiceRequest.OrderId;
                body.Body.requestData.Originator = "";
                var response = await _ipgSaleService.SalePaymentRequestAsync(body);
                var saleResponse = new IpgSaleServiceResponseModel()
                {
                    Message = response.Body.SalePaymentRequestResult.Message,
                    status = response.Body.SalePaymentRequestResult.Status,
                    Token = response.Body.SalePaymentRequestResult.Token
                };
                Response = _mapper.Map<IpgSaleServiceResponseDto>(saleResponse);
               
                return Response;
            }
            catch (Exception ex )
            {
                Response = new IpgSaleServiceResponseDto()
                {
                    Message = "خطا رخ داده است لطفا مجدد سعی نمایید",
                    status = -99,
                    Token = 0
                };
                return Response;
            }

        }

        [LoggingAspect]
        public async Task<ConfirmPaymetResponseDto> ConfirmPayment(ConfirmPaymentRequestDto requestDto)
        {
            var Response = new ConfirmPaymetResponseDto();
            #region Creat proxy model
            var cofirmPaymet = _mapper.Map<ConfirmPaymentRequestDto>(requestDto);
            #endregion
            try
            {
                ConfirmPaymentRequest confirmPaymentRequest = new ConfirmPaymentRequest();
                confirmPaymentRequest.Body = new ConfirmPaymentRequestBody();
                confirmPaymentRequest.Body.requestData = new ClientConfirmRequestData();
                confirmPaymentRequest.Body.requestData.LoginAccount = cofirmPaymet.LoginAccount;
                confirmPaymentRequest.Body.requestData.Token = cofirmPaymet.Token;

                var respons = await _confirm.ConfirmPaymentAsync(confirmPaymentRequest);
                var confirmResponse = new ConfirmPaymetResponseModel()
                {
                    CardNumberMasked = respons.Body.ConfirmPaymentResult.CardNumberMasked,
                    RRN = respons.Body.ConfirmPaymentResult.RRN,
                    status = respons.Body.ConfirmPaymentResult.Status,
                    Token = respons.Body.ConfirmPaymentResult.Token
                };
                Response = _mapper.Map<ConfirmPaymetResponseDto>(confirmResponse);
                return Response;
            }
            catch (Exception ex)
            {
                Response.RRN = 0;
                Response.status = -99;
                Response.Token = 0;
                return Response;

            }
            return Response;

        }
        [LoggingAspect]
        public async Task<ReversalServiceResponseDto> ReversalPayment(ReversalServiceRequstDto reversalService)
        {
            var reversResponse = new ReversalServiceResponseDto();
            try
            {
                #region Creat proxy model
                var reversalServiceRequst = _mapper.Map<ReversalServiceRequstModel>(reversalService);
                #endregion

                ReversalRequestRequest reversalRequestRequest = new ReversalRequestRequest();
                reversalRequestRequest.Body.requestData.LoginAccount = reversalServiceRequst.LoginAccount;
                reversalRequestRequest.Body.requestData.Token = reversalServiceRequst.Token;
                var respons = await _reversal.ReversalRequestAsync(reversalRequestRequest);
                var reverseResponse = new ReversalServiceResponseModel()
                {
                    Message = respons.Body.ReversalRequestResult.Message,
                    Status = respons.Body.ReversalRequestResult.Status,
                    Token = respons.Body.ReversalRequestResult.Token
                };
                reversResponse = _mapper.Map<ReversalServiceResponseDto>(reverseResponse);
                return reversResponse;
            }
            catch (Exception)
            {
                return  reversResponse = new ReversalServiceResponseDto
                {
                    Message = "خطای سیستمی مجدد تلاش نمایید",
                    Status = -99,
                    Token = 0
                };
            }
            
        }

        [LoggingAspect]
        public async Task<BillPaymentResponseDto> BillPayment(BillPaymentRequestDto billpaymentRequest)
        {
            var billResponse = new BillPaymentResponseDto();
            try
            {
                #region Creat proxy model
                var billPaymetnServiceRequst = _mapper.Map<BillPaymentRequestModel>(billpaymentRequest);
                #endregion

                BillPaymentRequestRequest billPayment = new BillPaymentRequestRequest();
                billPayment.Body = new BillPaymentRequestRequestBody();
                billPayment.Body.requestData = new ClientBillPaymentRequestData();
                billPayment.Body.requestData.AdditionalData = billPaymetnServiceRequst.AdditionalData;
                billPayment.Body.requestData.BillId = billPaymetnServiceRequst.BillId;
                billPayment.Body.requestData.CallBackUrl = billPaymetnServiceRequst.CallBackUrl;
                billPayment.Body.requestData.Originator = billPaymetnServiceRequst.Originator;
                billPayment.Body.requestData.OrderId = billPaymetnServiceRequst.OrderId;
                billPayment.Body.requestData.PayId = billPaymetnServiceRequst.PayId;
                billPayment.Body.requestData.LoginAccount = billPaymetnServiceRequst.LoginAccount;
         

                 var proxyResponse = await _billService.BillPaymentRequestAsync(billPayment);
                var resp = new BillPaymentResponseModel
                {
                    Message = proxyResponse.Body.BillPaymentRequestResult.Message,
                    Token = proxyResponse.Body.BillPaymentRequestResult.Token,
                    Status = proxyResponse.Body.BillPaymentRequestResult.Status
                };
                var reposneMap = _mapper.Map<BillPaymentResponseDto>(resp);
                billResponse = reposneMap;
                return billResponse;
            }
            catch (Exception ex)
            {
                billResponse.Message = "خطا در فراخوانی سرویس";
                billResponse.Token = 0;
                billResponse.Status = -99;
                return billResponse;
            }
        }

        public async Task<BillInfoResponseDto> GetBillInfo(BillInfoRequestDto infoRequestDto)
        {
            var billinfoResponse = new BillInfoResponseDto();
            try
            {

                #region Creat proxy model
                var billInfoServiceRequst = _mapper.Map<BillInfoRequestModel>(infoRequestDto);
                #endregion
                GetBillInfoRequest getBillInfoRequest = new GetBillInfoRequest();
                getBillInfoRequest.Body = new GetBillInfoRequestBody();
                getBillInfoRequest.Body.billId = billInfoServiceRequst.BillId;
                getBillInfoRequest.Body.payId = billInfoServiceRequst.PayId;

                var proxyResponse = await _billService.GetBillInfoAsync(getBillInfoRequest);
                var resp = new BillInfoResponseModel
                {
                    Amount = proxyResponse.Body.GetBillInfoResult.Amount,
                    BillType = proxyResponse.Body.GetBillInfoResult.BillType,
                    CompanyName = proxyResponse.Body.GetBillInfoResult.CompanyName,
                    RequestDateTime = proxyResponse.Body.GetBillInfoResult.RequestDateTime,
                    Status = proxyResponse.Body.GetBillInfoResult.Status,
                    StatusDescription = proxyResponse.Body.GetBillInfoResult.StatusDescription,
                    SubUtilityCode = proxyResponse.Body.GetBillInfoResult.SubUtilityCode,
                    UtilityCode = proxyResponse.Body.GetBillInfoResult.UtilityCode
                };
                var reposneMap = _mapper.Map<BillInfoResponseDto>(resp);
                billinfoResponse = reposneMap;
                return billinfoResponse;
            }
            catch (Exception)
            {
                billinfoResponse.Status = -99;
                billinfoResponse.StatusDescription = "خطا در پردازش اطاعات دریافتی از سرور";
                return billinfoResponse;
            }
        }
    }

    public interface IIpgServiceProxy
    {
        Task<IpgSaleServiceResponseDto> GetIpgSaleToken(IpgSaleServiceRequestDto req);
        Task<ConfirmPaymetResponseDto> ConfirmPayment(ConfirmPaymentRequestDto requestDto);
        Task<ReversalServiceResponseDto> ReversalPayment(ReversalServiceRequstDto reversalService);
        Task<BillPaymentResponseDto> BillPayment(BillPaymentRequestDto billpaymentRequest);
        Task<BillInfoResponseDto> GetBillInfo(BillInfoRequestDto infoRequestDto);
    }
}
