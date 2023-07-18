using AutoMapper;
using CacheRepository.Service;
using Data;
using Dto.repository;
using Entities;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using PEC.CoreCommon.Attribute;
using Pigi.MDbLogging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Repository.reositories
{
    [ScopedService]
    public class NajaRepository : Service<NajaWage, PaymentDbContext>, INajaRepository, ILoggable
    {
        #region Private Variable
        private readonly IServiceProvider _serviceProvider;
        private readonly IMapper _mapper;
        private readonly IMdbLogger<NajaRepository> _logger;

        #endregion

        #region Ctor
        public NajaRepository(IServiceProvider serviceProvider, IMapper mapper, IMdbLogger<NajaRepository> logger = null) : base(serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _mapper = mapper;
            _logger = logger;
        }
        #endregion

        public async Task<NajaWage> GetNajaWageByOrderIdAsync(long orderId)
        {
            try
            {
                var _rep = this.GetRepository<NajaWage, PaymentDbContext>();
                var obj = await _rep.Get(t => t.OrderId == orderId).FirstOrDefaultAsync();
                if (obj == null)
                {
                    return null;
                }
                return obj;
            }
            catch (Exception ex)
            {
                _logger.Log(51, $"📥 GetNajaWageByOrderIdAsync When Got Exception", JsonConvert.SerializeObject(orderId), ex);
                return null;
            }
        }

        public async Task<List<NajaWage>> GetAllNajaWagesAsync()
        {
            try
            {
                var _repo = this.GetRepository<NajaWage, PaymentDbContext>();
                var obj = await _repo.GetAll().ToListAsync();
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

        public async Task<bool> AddNajaWageAsync(NajaWage najaPay)
        {
            try
            {
                var _rep = this.GetRepository<NajaWage, PaymentDbContext>();
                var obj = await _rep.Get(t => t.OrderId == najaPay.OrderId).FirstOrDefaultAsync();
                if (obj != null)
                {
                    return false;
                }
                else
                {
                    _rep.Insert(najaPay);
                    int m = await _rep.SaveChangesAsync();
                    if (m > 0)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Log(100, $"📥 AddNajaWageAsync When Got Exception", JsonConvert.SerializeObject(najaPay), ex);
                return false;
            }
        }

        public async Task<bool> UpdateNajaWageAsync(NajaWage najaPay)
        {
            try
            {

                var _repo = this.GetRepository<NajaWage, PaymentDbContext>();
                //_logger.Log(124, $"📥 UpdateNajaWage Enter Method ", JsonConvert.SerializeObject(najaPay), null);

                var obj = await _repo.Get(t => t.OrderId == najaPay.OrderId).FirstOrDefaultAsync();
                if (obj == null)
                {
                    //  _logger.Log(129, $"📥 UpdateNajaWage When Obj Null ", JsonConvert.SerializeObject(najaPay), null);
                    return false;
                }
                else
                {
                    //_logger.Log(134, $"📥 UpdateNajaWage obj not Null ", JsonConvert.SerializeObject(obj), null);

                    //var _repoDetail = this.GetRepository<NajaWage, Context>();
                    //var objDetail = reopsitory.Get().FirstOrDefault(t => t.Id == obj.Id);
                    obj.NationalCode = najaPay.NationalCode;
                    obj.Mobile = najaPay.Mobile;
                    obj.NajaServiceData = najaPay.NajaServiceData;
                    obj.RRN = najaPay.RRN;
                    obj.PgwToken = najaPay.PgwToken;
                    obj.WalletReturnId = najaPay.WalletReturnId;
                    obj.OrderId = najaPay.OrderId;
                    obj.Amount = najaPay.Amount;
                    obj.MaskCardNumber = najaPay.MaskCardNumber;
                    obj.IsSendToSettelment = najaPay.IsSendToSettelment;
                    obj.Status = najaPay.Status;
                    obj.PaymentStatus = najaPay.PaymentStatus;
                    obj.MIncome = najaPay.MIncome;
                    obj.BussinessDate = najaPay.BussinessDate;
                    obj.CreateDate = najaPay.CreateDate;
                    obj.UserId = najaPay.UserId;
                    obj.MerchantId = najaPay.MerchantId;
                    obj.TransactionType = najaPay.TransactionType;
                    obj.ServiceMessage = najaPay.ServiceMessage;
                    obj.WalletId = najaPay.WalletId;
                    obj.NajaType = najaPay.NajaType;
                    _repo.Update(obj);
                    var result = await _repo.SaveChangesAsync() > 0;
                    //_logger.Log(104, $"📥 UpdateNajaWage When Done {result} With Order Id = {obj.OrderId}", JsonConvert.SerializeObject(obj), null);

                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.Log(155, $"📥 UpdateNajaWage When Got Exception", JsonConvert.SerializeObject(najaPay), ex);
                return false;
            }
        }

        public async Task<List<NajaWage>> GetNajaWageByExperssionAsync(Expression<Func<NajaWage, bool>> predicate)
        {
            try
            {
                var _repo = this.GetRepository<NajaWage, PaymentDbContext>();
                var obj = await _repo.Get(predicate).ToListAsync();
                if (obj != null)
                {
                    return obj;
                }
                else
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.Log(177, $"📥 GetNajaWageByExperssionAsync When Got Exception", JsonConvert.SerializeObject(predicate), ex);
                return null;
            }
        }

        public override IQueryable<NajaWage> Get(Expression<Func<NajaWage, bool>> filter = null, Func<IQueryable<NajaWage>, IOrderedQueryable<NajaWage>> orderBy = null, params Expression<Func<NajaWage, object>>[] includeProperties)
        {
            throw new NotImplementedException();
        }

        public override NajaWage GetById(object Id)
        {
            //var dd= PaginatedList<NajaWage>.CreateAsync(students.AsNoTracking(), pageNumber ?? 1, pageSize));
            throw new NotImplementedException();
        }

        public async Task<List<NajaWageDtoResponsePaginated>> GetPaginatedNajaWageAsync(long userId)
        {
            var _repo = this.GetRepository<NajaWage, PaymentDbContext>();
            var najaWages = from s in _repo.Get(n => n.UserId == userId && (n.TransactionType == 1 || n.TransactionType == 2)).OrderByDescending(o => o.BussinessDate)
                            select new NajaWageDtoResponsePaginated
                            {
                                RRN = s.RRN,
                                Amount = s.Amount,
                                PgwToken = s.PgwToken,
                                NajaType = s.NajaType,
                                WalletReturnId = s.WalletReturnId,
                                TransactionType = s.TransactionType,
                                Status = s.Status,
                                PaymentStatus = s.PaymentStatus,
                                OrderId = s.OrderId.ToString(),
                                WalletId = s.WalletId,
                                BussinessDate = s.BussinessDate
                            };
            return await najaWages.ToListAsync();
        }

        public async Task<NajaWage> GetNajaWageForViolationImageAsync(long orderId, long userId)
        {
            try
            {
                var _repo = this.GetRepository<NajaWage, PaymentDbContext>();
                var obj = await _repo.Get(t => t.OrderId == orderId && t.NajaType == 1 && t.UserId == userId).FirstOrDefaultAsync();
                if (obj == null)
                {
                    return null;
                }
                return obj;
            }
            catch (Exception ex)
            {
                _logger.Log(228, $"📥 GetNajaWageForViolationImageAsync When Got Exception", JsonConvert.SerializeObject(orderId + ":" + userId), ex);
                return null;
            }
        }

        public async Task<bool> UpdateNajaWageSentRequestAsync(long orderId)
        {
            try
            {
                var _repo = this.GetRepository<NajaWage, PaymentDbContext>();
                //_logger.Log(124, $"📥 UpdateNajaWage Enter Method ", JsonConvert.SerializeObject(najaPay), null);

                var obj = await _repo.Get(t => t.OrderId == orderId).FirstOrDefaultAsync();
                if (obj == null)
                {
                    //  _logger.Log(129, $"📥 UpdateNajaWage When Obj Null ", JsonConvert.SerializeObject(najaPay), null);
                    return false;
                }
                else
                {
                    //_logger.Log(134, $"📥 UpdateNajaWage obj not Null ", JsonConvert.SerializeObject(obj), null);

                    //var _repoDetail = this.GetRepository<NajaWage, Context>();
                    //var objDetail = reopsitory.Get().FirstOrDefault(t => t.Id == obj.Id);
                    obj.RequestIsSent = true;
                    _repo.UpdateField(obj, p => p.RequestIsSent);
                    var result = await _repo.SaveChangesAsync() > 0;
                    //_logger.Log(104, $"📥 UpdateNajaWage When Done {result} With Order Id = {obj.OrderId}", JsonConvert.SerializeObject(obj), null);

                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.Log(155, $"📥 UpdateNajaWageSentRequestAsync When Got Exception", orderId, ex);
                return false;
            }
        }
    }

    public interface INajaRepository
    {
        //string GetUserMobileNo(long userId);
        Task<NajaWage> GetNajaWageByOrderIdAsync(long orderId);
        Task<NajaWage> GetNajaWageForViolationImageAsync(long orderId, long userId);
        Task<List<NajaWage>> GetNajaWageByExperssionAsync(Expression<Func<NajaWage, bool>> predicate);
        Task<List<NajaWage>> GetAllNajaWagesAsync();
        Task<bool> AddNajaWageAsync(NajaWage najaPay);
        Task<bool> UpdateNajaWageAsync(NajaWage najaPay);
        Task<bool> UpdateNajaWageSentRequestAsync(long orderId);
        Task<List<NajaWageDtoResponsePaginated>> GetPaginatedNajaWageAsync(long userId);
    }
}



