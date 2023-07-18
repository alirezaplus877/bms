using AutoMapper;
using CacheRepository.Service;
using Data;
using Dto.repository;
using Dto.Request;
using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEC.CoreCommon.Attribute;
using PEC.CoreCommon.ExtensionMethods;
using Pigi.MDbLogging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Utility;

namespace Repository.reositories
{
    [ScopedService]
    public class BillRepository : Service<UsersBill, PaymentDbContext>, IBillRepository
    {
        #region Private Variable
        private readonly IServiceProvider _serviceProvider;
        private readonly IMapper _mapper;

        #endregion

        #region Ctor
        public BillRepository(IServiceProvider serviceProvider, IMapper mapper) : base(serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _mapper = mapper;

        }
        #endregion


        public async Task<int> UpdateAutoBillPayment(AutoBillPayment dto)
        {
            try
            {
                var _repAutoBill = this.GetRepository<AutoBillPayment, PaymentDbContext>();
                var AutoBill = await _repAutoBill.Get(t => t.Id == dto.Id).FirstOrDefaultAsync();
                if (AutoBill == null)
                {
                    return -1;
                }
                else
                {
                    AutoBill.MaxAmountPayment = dto.MaxAmountPayment;
                    AutoBill.DailyPayment = dto.DailyPayment;
                    AutoBill.ExpireDatePayment = dto.ExpireDatePayment;
                    AutoBill.ConfirmCode = dto.ConfirmCode;
                    _repAutoBill.UpdateField(AutoBill, c => c.ConfirmCode, c => c.DailyPayment, c => c.MaxAmountPayment, c => c.ExpireDatePayment);

                    var res = await _repAutoBill.SaveChangesAsync() > 0;
                    if (res)
                    {
                        return 0;
                    }
                    else
                    {
                        return -3;
                    }

                }

            }
            catch (Exception)
            {
                return -100;
            }
        }

        public async Task<int> UpdateUserBillAutoPayment(UserBillDto dto)
        {
            try
            {
                var _repUsersBill = this.GetRepository<UsersBill, PaymentDbContext>();
                var UsersBill = await _repUsersBill.Get(t => t.UserID == dto.UserID && t.BillID == dto.BillId).FirstOrDefaultAsync();
                if (UsersBill == null)
                {
                    return -1;
                }
                else
                {
                    UsersBill.Title = dto.Title;
                    UsersBill.AutoPaymentActiveated = dto.AutoPaymentActiveated;
                    var updateUserBill = await _repUsersBill.SaveChangesAsync() > 0;

                    if (dto.AutoPaymentActiveated)
                    {
                        var _repAutoBillPayment = this.GetRepository<AutoBillPayment, PaymentDbContext>();
                        var AutoBillPayment = await _repAutoBillPayment.Get(t => t.UsersBillId == UsersBill.Id).FirstOrDefaultAsync();
                        if (AutoBillPayment == null)
                        {
                            _repAutoBillPayment.Insert(new AutoBillPayment
                            {
                                MaxAmountPayment = dto.MaxAmountPayment.Value,
                                DailyPayment = dto.DailyPayment.Value,
                                ExpireDatePayment = dto.ExpireDatePayment.Value,
                                UsersBillId = UsersBill.Id,
                            });
                            await _repAutoBillPayment.SaveChangesAsync();
                            return 0;
                        }
                        else
                        {
                            AutoBillPayment.DailyPayment = dto.DailyPayment.Value;
                            AutoBillPayment.ExpireDatePayment = dto.ExpireDatePayment.Value;
                            AutoBillPayment.MaxAmountPayment = dto.MaxAmountPayment.Value;
                            await _repAutoBillPayment.SaveChangesAsync();
                            return 0;
                        }
                    }
                    else
                    {
                        return 0;
                    }
                }
            }
            catch (Exception ex)
            {
                return -100;
            }
        }

        public async Task<Tuple<AutoBillPayment, bool>> UpdateBussinessDateAutoBillPayment(long userId, string BillId, DateTime newTime)
        {
            try
            {
                var _repUsersBill = this.GetRepository<UsersBill, PaymentDbContext>();
                var UsersBill = await _repUsersBill.Get(t => t.UserID == userId && t.BillID == BillId).FirstOrDefaultAsync();
                if (UsersBill == null)
                {
                    return new Tuple<AutoBillPayment, bool>(null, false);
                }
                else
                {
                    var _repAutoBillPayment = this.GetRepository<AutoBillPayment, PaymentDbContext>();
                    var AutoBillPayment = await _repAutoBillPayment.Get(t => t.UsersBillId == UsersBill.Id).FirstOrDefaultAsync();
                    if (AutoBillPayment == null)
                    {
                        return new Tuple<AutoBillPayment, bool>(null, false);
                    }
                    else
                    {
                        AutoBillPayment.BussinesDate = newTime;
                        await _repAutoBillPayment.SaveChangesAsync();
                        return new Tuple<AutoBillPayment, bool>(AutoBillPayment, true);
                    }
                }
            }
            catch (Exception ex)
            {
                return new Tuple<AutoBillPayment, bool>(null, false);
            }
        }

        public async Task<Tuple<UsersBill, bool>> GetUserBillBy(Expression<Func<UsersBill, bool>> predicate)
        {
            try
            {
                var _rep = this.GetRepository<UsersBill, PaymentDbContext>();
                var obj = await _rep.Get(predicate).FirstOrDefaultAsync();
                if (obj != null)
                {
                    return new Tuple<UsersBill, bool>(obj, true);
                }
                else
                {
                    return new Tuple<UsersBill, bool>(null, false);
                }
            }
            catch (Exception)
            {
                return new Tuple<UsersBill, bool>(null, false);
            }
        }

        public async Task<Tuple<AutoBillPayment, bool>> GetAutoBillBy(Expression<Func<AutoBillPayment, bool>> predicate)
        {
            try
            {
                var _rep = this.GetRepository<AutoBillPayment, PaymentDbContext>();
                var obj = await _rep.Get(predicate).FirstOrDefaultAsync();
                if (obj != null)
                {
                    return new Tuple<AutoBillPayment, bool>(obj, true);
                }
                else
                {
                    return new Tuple<AutoBillPayment, bool>(null, false);
                }
            }
            catch (Exception)
            {
                return new Tuple<AutoBillPayment, bool>(null, false);
            }
        }

        public async Task<bool> CheckValidConfirmCodeAutoBillPayment(long userId, int confirmCode, string billId)
        {
            var _repUsersBill = this.GetRepository<UsersBill, PaymentDbContext>();
            var _repAutoBillPayment = this.GetRepository<AutoBillPayment, PaymentDbContext>();
            var getautoBillPayment = from usersBill in _repUsersBill.Get(t => t.UserID == userId && t.BillID == billId)
                                     join autoBillPayment in _repAutoBillPayment.Get() on usersBill.Id equals autoBillPayment.UsersBillId into sr
                                     from x in sr.DefaultIfEmpty()
                                     where x.ConfirmCode == confirmCode
                                     select new UserBillDto
                                     {
                                         MaxAmountPayment = x == null ? -1 : x.MaxAmountPayment,
                                         ExpireDatePayment = x == null ? null : x.ExpireDatePayment,
                                         DailyPayment = x == null ? (byte)0 : x.DailyPayment,
                                         UserID = usersBill.UserID,
                                         BillId = usersBill.BillID,
                                         BillType = usersBill.BillType,
                                         OrganizationId = usersBill.OrganizationID,
                                         Title = usersBill.Title,
                                         ClientId = usersBill.ClientId,
                                         CustomerId = usersBill.CustomerID,
                                         AutoPaymentActiveated = usersBill.AutoPaymentActiveated
                                     };

            var result = await getautoBillPayment.AnyAsync();
            if (result)
            {
                var _UsersBill = this.GetRepository<UsersBill, PaymentDbContext>();
                var UserBill = await _UsersBill.Get(t => t.UserID == userId && t.BillID == billId).FirstOrDefaultAsync();

                var _AutoBillPayment = this.GetRepository<AutoBillPayment, PaymentDbContext>();
                var AutoBillPayment = await _AutoBillPayment.Get(t => t.UsersBillId == UserBill.Id).FirstOrDefaultAsync();
                AutoBillPayment.ConfirmCode = new Random().Next(1000, 9999);
                await _AutoBillPayment.SaveChangesAsync();

                return true;
            }
            else
            {
                return false;
            }
        }

        public async Task<Tuple<AutoBillPaymentAudit, bool>> AddAutoBillPaymentAudit(AutoBillPaymentAudit autoBillPaymentAudit)
        {
            try
            {
                var _rep = this.GetRepository<AutoBillPaymentAudit, PaymentDbContext>();
                var obj = await _rep.Get(t => t.Id == autoBillPaymentAudit.Id && t.AutoBillPaymentId == autoBillPaymentAudit.AutoBillPaymentId).FirstOrDefaultAsync();
                if (obj != null)
                {
                    return new Tuple<AutoBillPaymentAudit, bool>(obj, false);
                }
                else
                {
                    _rep.Insert(autoBillPaymentAudit);
                    int m = await _rep.SaveChangesAsync();
                    if (m > 0)
                    {
                        return new Tuple<AutoBillPaymentAudit, bool>(autoBillPaymentAudit, true);
                    }
                    else
                    {
                        return new Tuple<AutoBillPaymentAudit, bool>(null, false);
                    }
                }
            }
            catch (Exception ex)
            {
                return new Tuple<AutoBillPaymentAudit, bool>(null, false);
            }
        }
        public async Task<Tuple<AutoBillPayment, int>> AddAutoBillPayment(AutoBillPayment autoBillPayment)
        {
            try
            {
                var _rep = this.GetRepository<AutoBillPayment, PaymentDbContext>();
                var obj = await _rep.Get(t => t.Id == autoBillPayment.Id && t.UsersBillId == autoBillPayment.UsersBillId).FirstOrDefaultAsync();
                if (obj != null)
                {
                    return new Tuple<AutoBillPayment, int>(obj, -1);
                }
                else
                {
                    _rep.Insert(autoBillPayment);
                    int m = await _rep.SaveChangesAsync();
                    if (m > 0)
                    {
                        return new Tuple<AutoBillPayment, int>(autoBillPayment, 0);
                    }
                    else
                    {
                        return new Tuple<AutoBillPayment, int>(null, -2);
                    }
                }
            }
            catch (Exception ex)
            {
                return new Tuple<AutoBillPayment, int>(null, -3);
            }
        }
        public async Task<Tuple<UsersBill, int>> AddUserBill(UsersBill usersBill)
        {
            try
            {
                var _rep = this.GetRepository<UsersBill, PaymentDbContext>();
                var obj = await _rep.Get(t => t.UserID == usersBill.UserID && t.BillID == usersBill.BillID && t.BillType == usersBill.BillType).FirstOrDefaultAsync();
                if (obj != null)
                {
                    //قبض موجود می باشد
                    return new Tuple<UsersBill, int>(obj, -1);
                }
                else
                {
                    _rep.Insert(usersBill);
                    int m = await _rep.SaveChangesAsync();
                    if (m > 0)
                    {
                        //قبض با موفقیت افزوده شد
                        return new Tuple<UsersBill, int>(usersBill, 0);
                    }
                    else
                    {
                        //خطا در درج قبض 
                        return new Tuple<UsersBill, int>(usersBill, -2);
                    }
                }
            }
            catch (Exception ex)
            {
                //خطای نا شناخته
                return new Tuple<UsersBill, int>(usersBill, -3);
            }
        }
        public async Task<List<UserBillDto>> GetUsersBills(long userID)
        {
            try
            {
                var _repUsersBill = this.GetRepository<UsersBill, PaymentDbContext>();
                var _repAutoBillPayment = this.GetRepository<AutoBillPayment, PaymentDbContext>();
                var innerJoinQuery = from usersBill in _repUsersBill.Get(t => t.UserID == userID && t.IsActive)
                                     join autoBillPayment in _repAutoBillPayment.Get() on usersBill.Id equals autoBillPayment.UsersBillId into sr
                                     from x in sr.DefaultIfEmpty()                                     
                                     select new UserBillDto
                                     {
                                         MaxAmountPayment = x == null ? null : x.MaxAmountPayment,
                                         ExpireDatePayment = x == null ? null : x.ExpireDatePayment,
                                         DailyPayment = x == null ? null : x.DailyPayment,
                                         UserID = usersBill.UserID,
                                         BillId = usersBill.BillID,
                                         BillType = usersBill.BillType,
                                         OrganizationId = usersBill.OrganizationID,
                                         Title = usersBill.Title,
                                         ClientId = usersBill.ClientId,
                                         CustomerId = usersBill.CustomerID,
                                         AutoPaymentActiveated = usersBill.AutoPaymentActiveated
                                     };

                return await innerJoinQuery.ToListAsync();
            }
            catch (Exception ex)
            {
                return null;
            }
        }
        public override IQueryable<UsersBill> Get(Expression<Func<UsersBill, bool>> filter = null, Func<IQueryable<UsersBill>, IOrderedQueryable<UsersBill>> orderBy = null, params Expression<Func<UsersBill, object>>[] includeProperties)
        {
            throw new NotImplementedException();
        }
        public override UsersBill GetById(object Id)
        {
            throw new NotImplementedException();
        }
        public async Task<List<BillType>> GetBillType()
        {
            try
            {
                var _rep = this.GetRepository<BillType, PaymentDbContext>();
                var obj = await _rep.Get().OrderByDescending(ot => ot.Id).ToListAsync();
                if (obj == null)
                {
                    return null;
                }
                return obj;
            }
            catch (Exception ex)
            {

                return null;
            }
        }
        public async Task<List<Organization>> GetOrganization()
        {
            try
            {
                var _rep = this.GetRepository<Organization, PaymentDbContext>();
                var obj = await _rep.Get().OrderByDescending(ot => ot.Id).ToListAsync();
                if (obj == null)
                {
                    return null;
                }
                return obj;
            }
            catch (Exception ex)
            {

                return null;
            }
        }
        public async Task<long> GetOrganizationId(int utilityCode)
        {

            try
            {
                var _rep = this.GetRepository<Organization, PaymentDbContext>();
                var obj = await _rep.Get(g => g.UtilityCode == utilityCode).OrderByDescending(ot => ot.Id).FirstOrDefaultAsync();
                if (obj == null)
                {
                    return 0;
                }
                return obj.Id;
            }
            catch (Exception ex)
            {

                return 0;
            }
        }
        public async Task<Tuple<UsersBill, bool>> DeleteUsersBill(UsersBill usersBill)
        {
            try
            {
                var _rep = this.GetRepository<UsersBill, PaymentDbContext>();
                var obj = await _rep.Get(t => t.UserID == usersBill.UserID && t.BillID == usersBill.BillID && t.BillType == usersBill.BillType).OrderByDescending(ot => ot.Id).FirstOrDefaultAsync();
                if (obj != null)
                {
                    //reopsitory.Remove(obj);
                    obj.IsActive = false;
                    int m = await reopsitory.SaveChangesAsync();
                    if (m > 0)
                    {
                        return new Tuple<UsersBill, bool>(obj, true);
                    }
                    else
                    {
                        return new Tuple<UsersBill, bool>(obj, false);

                    }
                }
                else
                {

                    return new Tuple<UsersBill, bool>(obj, false);
                }



            }
            catch (Exception ex)
            {
                return new Tuple<UsersBill, bool>(null, false);
            }
        }
        public async Task<Tuple<UsersBill, bool>> ActiveUsersBill(UsersBill usersBill)
        {
            try
            {
                var _rep = this.GetRepository<UsersBill, PaymentDbContext>();
                var obj = await _rep.Get(t => t.UserID == usersBill.UserID && t.BillID == usersBill.BillID && t.BillType == usersBill.BillType).OrderByDescending(ot => ot.Id).FirstOrDefaultAsync();
                if (obj != null)
                {
                    //reopsitory.Remove(obj);
                    obj.IsActive = true;
                    int m = await reopsitory.SaveChangesAsync();
                    if (m > 0)
                    {
                        return new Tuple<UsersBill, bool>(obj, true);
                    }
                    else
                    {
                        return new Tuple<UsersBill, bool>(obj, false);

                    }
                }
                else
                {

                    return new Tuple<UsersBill, bool>(obj, false);
                }

            }
            catch (Exception ex)
            {
                return new Tuple<UsersBill, bool>(null, false);
            }

        }
        public bool InsertBillrequest(BillRequest billrequest)
        {
            try
            {
                var _repo = this.GetRepository<BillRequest, PaymentDbContext>();
                _repo.Insert(billrequest);
                int m = _repo.SaveChanges();
                if (m > 0)
                {
                    return true;
                }
                else
                    return false;
            }
            catch (Exception)
            {
                return false;
            }
        }
        public bool InsertTollBillData(TollPlateData tollPlate)
        {
            try
            {
                var _repo = this.GetRepository<TollPlateData, PaymentDbContext>();
                _repo.Insert(tollPlate);
                int m = _repo.SaveChanges();
                if (m > 0)
                {
                    return true;
                }
                else
                    return false;

            }
            catch (Exception ex)
            {
                return false;
            }
        }
        public async Task<bool> UpdateTollBill(TollPlateData tollPlate)
        {
            try
            {
                var _repo = this.GetRepository<TollPlateData, PaymentDbContext>();
                var billData = await _repo.Get(t => t.OrderID == tollPlate.OrderID).FirstOrDefaultAsync();
                if (billData == null)
                {
                    return false;
                }
                billData.PGWToken = tollPlate.PGWToken;
                _repo.UpdateField(billData, p => p.PGWToken);
                await _repo.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        public async Task<bool> UpdateTollBillSetPayData(int orderId, long RRN)
        {
            try
            {
                var _repo = this.GetRepository<TollPlateData, PaymentDbContext>();
                var billData = await _repo.Get(t => t.OrderID == orderId).FirstOrDefaultAsync();
                if (billData == null)
                {
                    return false;
                }
                billData.RRN = RRN;
                _repo.UpdateField(billData, p => p.RRN);
                await _repo.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        public async Task<TollPlateData> GetToll(int orderId)
        {
            try
            {
                var _repo = this.GetRepository<TollPlateData, PaymentDbContext>();
                var billData = await _repo.Get(t => t.OrderID == orderId, null, a => a.TollPlateBill).FirstOrDefaultAsync();
                if (billData == null)
                {
                    return null;
                }

                return billData;
            }
            catch (Exception ex)
            {
                return null;
            }
        }
        public async Task<bool> UpdateBillRequest(BillRequest billRequest)
        {
            try
            {
                var _repo = this.GetRepository<BillRequest, PaymentDbContext>();
                var obj = await _repo.Get(t => t.OrderID == billRequest.OrderID).FirstOrDefaultAsync();
                if (obj == null)
                {
                    return false;
                }
                obj.Token = billRequest.Token;
                obj.ServiceMessage = billRequest.ServiceMessage;
                _repo.UpdateField(obj, p => p.Token, p => p.ServiceMessage);
                await _repo.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)

            {

                return false;
            }
        }
        public async Task<bool> UpdateWalletPayBill(long OrderId, long WalletReturnId, string ServiceMessage, int PayStatus, long payOrderID)
        {
            try
            {
                var _repo = this.GetRepository<BillRequest, PaymentDbContext>();
                var obj = await _repo.Get(t => t.OrderID == OrderId).FirstOrDefaultAsync();
                if (obj != null)
                {
                    obj.RRN = WalletReturnId;
                    obj.Status = PayStatus;
                    obj.Token = WalletReturnId;
                    obj.ServiceMessage = ServiceMessage;
                    _repo.UpdateField(obj, p => p.Token, p => p.ServiceMessage, p => p.RRN, p => p.Status);
                    await _repo.SaveChangesAsync();

                    var _repoDetail = this.GetRepository<BillRequestDetail, PaymentDbContext>();
                    var obj2 = await _repoDetail.Get(t => t.BillRequestID == obj.Id && t.OrderId == payOrderID).FirstOrDefaultAsync();
                    if (obj2 != null)
                    {
                        obj2.ReturnID = WalletReturnId;
                        obj2.Status = PayStatus;
                        obj2.PayDate = DateTime.Now.ToPersianDateTime().Date.ToGeorianNumbers();
                        _repoDetail.UpdateField(obj2, p => p.ReturnID, p => p.Status);
                        await _repo.SaveChangesAsync();
                    };
                    return true;
                }
                else { return false; }

            }
            catch (Exception ex)
            {

                return false;
            }

        }
        public async Task<bool> UpdateBillRequestDetail(string BillId, string PayId, long orderid, long PayOrderId)
        {
            try
            {
                var _repo = this.GetRepository<BillRequest, PaymentDbContext>();
                var obj = await _repo.Get(t => t.OrderID == orderid).FirstOrDefaultAsync();
                if (obj != null)
                {
                    var _repoDetail = this.GetRepository<BillRequestDetail, PaymentDbContext>();
                    var obj2 = await _repoDetail.Get(t => t.BillRequestID == obj.Id && t.BillID == BillId && t.PayId == PayId).FirstOrDefaultAsync();
                    if (obj2 != null)
                    {
                        obj2.OrderId = PayOrderId;
                        _repoDetail.UpdateField(obj2, p => p.OrderId);
                        await _repo.SaveChangesAsync();
                    }

                    return true;
                }
                else { return false; }

            }
            catch (Exception ex)
            {

                return false;
            }
        }
        public async Task<bool> UpdateBillTable(long orderId, int status, long token, long RRN, string message)
        {
            try
            {
                var _repo = this.GetRepository<BillRequest, PaymentDbContext>();
                var obj = await _repo.Get(t => t.OrderID == orderId && t.Token == token).FirstOrDefaultAsync();
                if (obj == null)
                {
                    return false;
                }
                obj.Status = status;
                obj.RRN = RRN;
                obj.ServiceMessage = message;
                _repo.UpdateField(obj, p => p.Status, p => p.RRN, p => p.ServiceMessage);
                await _repo.SaveChangesAsync();
                var _repo2 = this.GetRepository<BillRequestDetail, PaymentDbContext>();
                var obj2 = await _repo2.Get(t => t.BillRequestID == obj.Id).ToListAsync();
                if (obj2 == null)
                {
                    return false;
                }
                foreach (var item in obj2)
                {
                    item.Status = status;
                    item.PayDate = DateTime.Now.ToPersianDateTime().Date.ToGeorianNumbers();
                    _repo2.UpdateField(item, p => p.Status, p => p.PayDate);

                }
                await _repo.SaveChangesAsync();
                return true;

            }
            catch (Exception ex)
            {

                return false;
            }
        }
        public async Task<bool> UpdateBillTableForWallet(long orderId, int status, string message, long returnIdWallet, string response)
        {
            try
            {
                var _repoChild = this.GetRepository<WalletPayBillTransaction, PaymentDbContext>();
                var obj = await _repoChild.Get(t => t.OrderId == orderId).FirstOrDefaultAsync();
                if (obj == null)
                {
                    return false;
                }
                obj.ResultId = status;
                obj.ResultDesc = message;
                obj.ReturnId = returnIdWallet;
                obj.Response = response;

                _repoChild.UpdateField(obj, p => p.ResultId, p => p.ResultDesc, p => p.ReturnId, p => p.Response);
                await _repoChild.SaveChangesAsync();
                var _repoParent = this.GetRepository<WalletPayBill, PaymentDbContext>();
                var parent = await _repoParent.Get(t => t.Id == obj.ParentId).FirstOrDefaultAsync();
                if (parent == null)
                {
                    return false;
                }
                parent.FinalResult = status;
                parent.PayFinalResultId = returnIdWallet;
                parent.PayDate = DateTime.Now;
                parent.Message = message;

                if (status == 0)
                    parent.IsPaid = true;
                //obj2.PayDate = DateTime.Now.ToPersianDateTime().Date.ToGeorianNumbers();
                _repoParent.UpdateField(parent, p => p.FinalResult, p => p.PayFinalResultId,
                    p => p.PayDate, p => p.Message, p => p.IsPaid);
                await _repoParent.SaveChangesAsync();
                return true;

            }
            catch (Exception ex)
            {

                return false;
            }
        }

        public async Task<bool> AddWalletPayBillTransaction(WalletPayBillTransaction data)
        {
            try
            {
                var _repo = this.GetRepository<WalletPayBillTransaction, PaymentDbContext>();
                _repo.Insert(data);

                var res = await _repo.SaveChangesAsync();
                if (res > 0)
                    return true;

                return false;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        public async Task<bool> UpdateBillRequestDetailData(long orderID, long Token, string TransactionDate, int payStatus)
        {
            try
            {
                var _repo = this.GetRepository<BillRequest, PaymentDbContext>();
                var obj = await _repo.Get(t => t.OrderID == orderID && t.Token == Token).FirstOrDefaultAsync();
                if (obj == null)
                {
                    return false;
                }
                else
                {
                    var _repoDetail = this.GetRepository<BillRequestDetail, PaymentDbContext>();
                    var objDetail = await _repoDetail.Get(t => t.BillRequestID == obj.Id).ToListAsync();
                    foreach (var item in objDetail)
                    {
                        item.PayDate = TransactionDate;
                        item.Status = payStatus;//// pardakht tavasot kif pol madar anjam shode bayad vosoli bokhord
                        item.OrderId = orderID;
                        _repoDetail.UpdateField(item, p => p.PayDate, p => p.Status, p => p.OrderId);
                        await _repoDetail.SaveChangesAsync();

                    };
                    return true;
                }


            }
            catch (Exception ex)
            {

                return false;
            }
        }
        public async Task<BillRequest> getPaymentDetail(long Token)
        {
            try
            {
                var _repo = this.GetRepository<BillRequest, PaymentDbContext>();
                var obj = await _repo.Get(t => t.Token == Token, null, a => a.BillRequestDetails).FirstOrDefaultAsync();
                if (obj == null)
                {
                    return null;
                }
                return obj;
            }
            catch (Exception ex)

            {

                return null;
            }
        }
        public async Task<BillRequest> GetPayBills(long Token)
        {
            try
            {
                var _repo = this.GetRepository<BillRequest, PaymentDbContext>();
                var obj = await _repo.Get(t => t.Token == Token, null, a => a.BillRequestDetails).FirstOrDefaultAsync();
                if (obj == null)
                {
                    return null;
                }
                return obj;
            }
            catch (Exception ex)

            {

                return null;
            }
        }
        public async Task<bool> updateTollBillPayStatus(long orderId, long WalletReturnId, int status)
        {


            try
            {
                var _repo = this.GetRepository<BillRequest, PaymentDbContext>();
                var obj = await _repo.Get(t => t.OrderID == orderId).FirstOrDefaultAsync();
                if (obj == null)
                {
                    return false;
                }
                else
                {
                    obj.Status = status;
                    obj.RRN = WalletReturnId;
                    _repo.UpdateField(obj, p => p.Status, p => p.RRN);
                    await _repo.SaveChangesAsync();


                    var _requestRepo = this.GetRepository<BillRequestDetail, PaymentDbContext>();
                    var obj2 = await _requestRepo.Get(t => t.BillRequestID == obj.Id).ToListAsync();
                    var date = DateTime.Now;
                    var payDate = date.ToString("YYYY-MM-dd");
                    foreach (var item in obj2)
                    {

                        item.Status = 4;
                        item.PayDate = payDate;
                        _requestRepo.UpdateField(item, p => p.Status);
                        await _repo.SaveChangesAsync();
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {

                return false;
            }
        }
        public async Task<BillRequest> GetBillRequestByOrderId(long order)
        {
            try
            {
                var _repo = this.GetRepository<BillRequest, PaymentDbContext>();
                var obj = await _repo.Get(t => t.OrderID == order, null, a => a.BillRequestDetails).FirstOrDefaultAsync();
                if (obj == null)
                {
                    return null;
                }
                return obj;
            }
            catch (Exception ex)

            {

                return null;
            }
        }
        public async Task<Tuple<int, List<BillRequest>>> GetPaymentHistory(GetBillPaymentHistoryRequestDto getBill)
        {

            try
            {
                var _repo = this.GetRepository<BillRequest, PaymentDbContext>();
                var query = _repo.Get(t => t.UserID == getBill.UserId &&
               t.CreateDate.Value.Date >= getBill.FromDate.Value.Date && t.CreateDate.Value.Date <= getBill.ToDate.Value.Date
               && t.BillRequestDetails.Any(a => a.BillID == getBill.BillId && a.OrganizationID == getBill.OrganizationId),
                null, z => z.BillRequestDetails.Where(b => b.BillID == getBill.BillId && b.OrganizationID == getBill.OrganizationId));

                var count = query.Count();
                query = query.Skip((getBill.PageSize - 1) * getBill.Count)
                .Take(getBill.Count);

                var obj = await query.ToListAsync();

                Tuple<int, List<BillRequest>> retunValue = new Tuple<int, List<BillRequest>>(count, obj);

                return retunValue;
            }
            catch (Exception)
            {

                return null;
            }
        }
        public async Task<Tuple<int, List<BillRequest>>> GetUserPaymentHistory(UserBillPaymentHistoryRequestDto getBill)
        {

            try
            {
                var _repo = this.GetRepository<BillRequest, PaymentDbContext>();
                var query = _repo.Get(t => t.UserID == getBill.UserId &&
               t.CreateDate.Value.Date >= getBill.FromDate.Value.Date && t.CreateDate.Value.Date <= getBill.ToDate.Value.Date
               && t.BillRequestDetails.Any(),
                null, z => z.BillRequestDetails);

                var count = query.Count();
                query = query.Skip((getBill.PageSize - 1) * getBill.Count)
                .Take(getBill.Count);

                var obj = await query.ToListAsync();

                Tuple<int, List<BillRequest>> retunValue = new Tuple<int, List<BillRequest>>(count, obj);

                return retunValue;
            }
            catch (Exception ex)
            {

                return null;
            }
        }
        public async Task<List<WalletPayBill>> GetReadyBillToPayWithWallet()
        {
            try
            {
                var _repo = this.GetRepository<WalletPayBill, PaymentDbContext>();
                var obj = await _repo.Get(t => t.IsPaid.HasValue && !t.IsPaid.Value && t.FinalResult != 0
                    && t.IsRetryable && t.PayId != null && t.BillId != null
                    && t.SourceWallet != null && t.ExternalId != null).ToListAsync();
                if (obj == null)
                {
                    return null;
                }
                return obj;
            }
            catch (Exception ex)
            {

                return null;
            }
        }
        public async Task<bool> UpdatePayBillStatus(long orderId)
        {
            try
            {
                var _repo = this.GetRepository<BillRequest, PaymentDbContext>();
                var obj = await _repo.Get(t => t.OrderID == orderId, null, a => a.BillRequestDetails).FirstOrDefaultAsync();
                obj.Status = 2;
                _repo.UpdateField(obj, p => p.Status);
                await _repo.SaveChangesAsync();

                var _requestRepo = this.GetRepository<BillRequestDetail, PaymentDbContext>();
                var obj2 = await _requestRepo.Get(t => t.BillRequestID == obj.Id).ToListAsync();

                foreach (var item in obj2)
                {
                    item.Status = 2;
                    _requestRepo.UpdateField(item, p => p.Status);
                    await _repo.SaveChangesAsync();
                }
                return true;
            }
            catch (Exception ex)

            {

                return false;
            }
        }



        public async Task<Tuple<UsersBill, bool>> DeActiveAutoBillPayment(UsersBill usersBill)
        {
            try
            {
                var _rep = this.GetRepository<UsersBill, PaymentDbContext>();
                var obj = await _rep.Get(t => t.UserID == usersBill.UserID && t.BillID == usersBill.BillID && t.BillType == usersBill.BillType).FirstOrDefaultAsync();
                if (obj != null)
                {
                    //reopsitory.Remove(obj);
                    obj.AutoPaymentActiveated = false;
                    int m = await reopsitory.SaveChangesAsync();
                    if (m > 0)
                    {
                        return new Tuple<UsersBill, bool>(obj, true);
                    }
                    else
                    {
                        return new Tuple<UsersBill, bool>(obj, false);

                    }
                }
                else
                {

                    return new Tuple<UsersBill, bool>(obj, false);
                }

            }
            catch (Exception ex)
            {
                return new Tuple<UsersBill, bool>(null, false);
            }
        }

        public async Task<Tuple<UsersBill, bool>> ActiveAutoBillPayment(UsersBill usersBill)
        {
            try
            {
                var _rep = this.GetRepository<UsersBill, PaymentDbContext>();
                var obj = await _rep.Get(t => t.UserID == usersBill.UserID && t.BillID == usersBill.BillID && t.BillType == usersBill.BillType).FirstOrDefaultAsync();
                if (obj != null)
                {
                    //reopsitory.Remove(obj);
                    obj.AutoPaymentActiveated = true;
                    int m = await reopsitory.SaveChangesAsync();
                    if (m > 0)
                    {
                        return new Tuple<UsersBill, bool>(obj, true);
                    }
                    else
                    {
                        return new Tuple<UsersBill, bool>(obj, false);

                    }
                }
                else
                {

                    return new Tuple<UsersBill, bool>(obj, false);
                }

            }
            catch (Exception ex)
            {
                return new Tuple<UsersBill, bool>(null, false);
            }
        }

        public async Task<List<UserBillDto>> GetReadyAutoBillPaymentWithWallet()
        {
            try
            {
                var today = DateTime.Now;
                //var fromDate = Convert.ToDateTime($"{today.Year}-{today.Month}-{today.Day} 00:00:00.001");
                //var toDate = Convert.ToDateTime($"{today.Year}-{today.Month}-{today.Day + 1} 00:00:00.001");
                var _repUsersBill = this.GetRepository<UsersBill, PaymentDbContext>();
                var _repAutoBillPayment = this.GetRepository<AutoBillPayment, PaymentDbContext>();
                var innerJoinQuery = from usersBill in _repUsersBill.Get(t => t.AutoPaymentActiveated == true
                                     //&& t.CreateDate >= fromDate
                                     //&& t.CreateDate <= toDate
                                     )
                                     join autoBillPayment in _repAutoBillPayment.Get() on usersBill.Id equals autoBillPayment.UsersBillId /*into sr*/
                                     //from x in sr.DefaultIfEmpty()
                                     where autoBillPayment.DailyPayment == today.Day
                                     select new UserBillDto
                                     {
                                         MaxAmountPayment = autoBillPayment.MaxAmountPayment,
                                         ExpireDatePayment = autoBillPayment.ExpireDatePayment,
                                         BussinesDate = autoBillPayment.BussinesDate,
                                         DailyPayment = autoBillPayment.DailyPayment,
                                         UserID = usersBill.UserID,
                                         BillId = usersBill.BillID,
                                         BillType = usersBill.BillType,
                                         OrganizationId = usersBill.OrganizationID,
                                         Title = usersBill.Title,
                                         ClientId = usersBill.ClientId,
                                         CustomerId = usersBill.CustomerID,
                                         AutoPaymentActiveated = usersBill.AutoPaymentActiveated
                                     };

                innerJoinQuery = innerJoinQuery.Where(x => x.BussinesDate == null ||
                                       (x.BussinesDate.Value.Year != today.Year
                                     && x.BussinesDate.Value.Month != today.Month
                                     && x.BussinesDate.Value.Day != today.Day))
                                               .Where(x => x.ExpireDatePayment > today);
                return await innerJoinQuery.ToListAsync();
            }
            catch (Exception ex)
            {
                return null;
            }
        }


    }

    public interface IBillRepository
    {
        Task<int> UpdateUserBillAutoPayment(UserBillDto dto);
        Task<int> UpdateAutoBillPayment(AutoBillPayment dto);
        Task<Tuple<UsersBill, int>> AddUserBill(UsersBill usersBill);
        Task<Tuple<UsersBill, bool>> GetUserBillBy(Expression<Func<UsersBill, bool>> predicate);
        Task<Tuple<AutoBillPayment, bool>> GetAutoBillBy(Expression<Func<AutoBillPayment, bool>> predicate);
        Task<bool> CheckValidConfirmCodeAutoBillPayment(long userId, int confirmCode, string billId);
        Task<Tuple<AutoBillPaymentAudit, bool>> AddAutoBillPaymentAudit(AutoBillPaymentAudit usersBill);
        Task<Tuple<AutoBillPayment, int>> AddAutoBillPayment(AutoBillPayment autoBillPayment);
        Task<Tuple<AutoBillPayment, bool>> UpdateBussinessDateAutoBillPayment(long userId, string BillId, DateTime newTime);
        Task<List<UserBillDto>> GetUsersBills(long userID);
        Task<List<BillType>> GetBillType();
        Task<List<Organization>> GetOrganization();
        Task<Tuple<UsersBill, bool>> ActiveAutoBillPayment(UsersBill UsersBill);
        Task<Tuple<UsersBill, bool>> DeActiveAutoBillPayment(UsersBill UsersBill);
        Task<Tuple<UsersBill, bool>> DeleteUsersBill(UsersBill usersBill);
        Task<Tuple<UsersBill, bool>> ActiveUsersBill(UsersBill usersBill);
        Task<bool> UpdateBillRequestDetail(string BillId, string PayId, long orderid, long PayOrderId);
        bool InsertBillrequest(BillRequest billrequest);
        Task<bool> UpdateBillRequest(BillRequest billRequest);
        Task<bool> UpdateWalletPayBill(long OrderId, long WalletReturnId, string ServiceMessage, int PayStatus, long payOrderID);
        Task<bool> UpdateBillTable(long orderId, int status, long token, long RRN, string message);
        Task<bool> UpdateBillRequestDetailData(long orderID, long Token, string TransactionDate, int payStatus);
        Task<BillRequest> getPaymentDetail(long Token);
        Task<BillRequest> GetPayBills(long Token);
        bool InsertTollBillData(TollPlateData tollPlate);
        Task<Tuple<int, List<BillRequest>>> GetPaymentHistory(GetBillPaymentHistoryRequestDto getBill);
        Task<BillRequest> GetBillRequestByOrderId(long order);
        Task<List<WalletPayBill>> GetReadyBillToPayWithWallet();
        Task<List<UserBillDto>> GetReadyAutoBillPaymentWithWallet();
        Task<bool> UpdateBillTableForWallet(long orderId, int status, string message, long returnIdWallet, string response);
        Task<long> GetOrganizationId(int utilityCode);
        Task<bool> UpdateTollBill(TollPlateData tollPlate);
        Task<bool> UpdateTollBillSetPayData(int orderId, long RRN);
        Task<TollPlateData> GetToll(int orderId);
        Task<bool> AddWalletPayBillTransaction(WalletPayBillTransaction data);
        Task<Tuple<int, List<BillRequest>>> GetUserPaymentHistory(UserBillPaymentHistoryRequestDto getBill);
    }
}



