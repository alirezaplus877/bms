using AutoMapper;
using CacheRepository.Service;
using Data;
using Dto.Request;
using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEC.CoreCommon.Attribute;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Repository.reositories
{
    [ScopedService]
    public class WalletRepository : Service<Charge, WalletContext>, IWalletRepository
    {
        #region Private Variable
        private readonly IServiceProvider _serviceProvider;
        private readonly IMapper _mapper;
        #endregion

        public WalletRepository(IServiceProvider serviceProvider, IMapper mapper) : base(serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _mapper = mapper;
        }
        public async Task<Charge> GetChargeRow(string PGWToken)
        {
            try
            {
                var _rep = this.GetRepository<Charge, WalletContext>();
                var obj = await _rep.Get(t => t.PGWToken == PGWToken).FirstOrDefaultAsync();
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
        public async Task<bool> InsertChargeData(Charge charge)
        {
            try
            {
                var _rep = this.GetRepository<Charge, WalletContext>();
                _rep.Insert(charge);
                int x = await reopsitory.SaveChangesAsync();
                if (x > 0)
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

        public async Task<bool> UpdateChargeTable(Charge charge)
        {
            try
            {
                var _rep = this.GetRepository<Charge, WalletContext>();
                var obj = await _rep.Get(t => t.PGWToken == charge.PGWToken).FirstOrDefaultAsync();
                if (obj == null)
                {
                    return false;
                }
                else
                {
                    obj.RRN = charge.RRN;
                    obj.PaymentStatus = charge.PaymentStatus;
                    reopsitory.UpdateField(obj, a => a.RRN, a => a.PaymentStatus);
                    int x = await reopsitory.SaveChangesAsync();
                    if (x > 0)
                    {
                        return true;
                    }
                    else
                        return false;
                }
            }
            catch (Exception ex)
            {
                return false;
            }
        }


        public override IQueryable<Charge> Get(Expression<Func<Charge, bool>> filter = null, Func<IQueryable<Charge>, IOrderedQueryable<Charge>> orderBy = null, params Expression<Func<Charge, object>>[] includeProperties)
        {
            throw new NotImplementedException();
        }

        public override Charge GetById(object Id)
        {
            throw new NotImplementedException();
        }
        public async Task<Tuple<int, List<Charge>>> GetChargeHistory(ChargeHistoryRequestDo chargeHistory)
        {
            try
            {
                var _repo = this.GetRepository<Charge, WalletContext>();
                var query = _repo.Get(t => t.UserId == chargeHistory.UserId &&
                                                                               t.DestinationWalletId == chargeHistory.WalletCode &&
                                                                               t.WalletType == chargeHistory.WalletType &&
                                                                               t.CreateDate.Date >= chargeHistory.FromDate.Value.Date &&
                                                                               t.CreateDate.Date <= chargeHistory.ToDate.Value.Date);

                var count = query.Count();
                query = query.Skip((chargeHistory.PageSize - 1) * chargeHistory.Count)
                .Take(chargeHistory.Count);
                var obj = await query.ToListAsync();
                Tuple<int, List<Charge>> retunValue = new Tuple<int, List<Charge>>(count, obj);
                return retunValue;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public async Task<Charge> GetChargeViaOrderId(long orderId)
        {
            try
            {
                var _rep = this.GetRepository<Charge, WalletContext>();
                var obj = await _rep.Get(t => t.WalletOrderId == orderId).FirstOrDefaultAsync();
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


    }
    public interface IWalletRepository
    {
        Task<Charge> GetChargeRow(string PGWToken);
        Task<bool> InsertChargeData(Charge charge);
        Task<bool> UpdateChargeTable(Charge charge);
        Task<Charge> GetChargeViaOrderId(long orderId);
        Task<Tuple<int, List<Charge>>> GetChargeHistory(ChargeHistoryRequestDo chargeHistory);
    }
}
