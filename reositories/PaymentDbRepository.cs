using CacheRepository.Service;
using Data;
using Entities;
using Microsoft.EntityFrameworkCore;
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
    public class PaymentDbRepository : Service<MerchantProvider, PaymentDbContext>, ILoggable, IPaymentDbRepository
    {
        #region Private Variable
        private readonly IServiceProvider _serviceProvider;
        #endregion

        #region Ctor
        public PaymentDbRepository(IServiceProvider serviceProvider) : base(serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        #endregion

        public async Task<MerchantProvider> GetMerchantProvider(Expression<Func<MerchantProvider, bool>> predicate)
        {
            try
            {
                var _rep = this.GetRepository<MerchantProvider, PaymentDbContext>();
                var obj = await _rep.Get(predicate).FirstOrDefaultAsync();
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


        public override IQueryable<MerchantProvider> Get(Expression<Func<MerchantProvider, bool>> filter = null, Func<IQueryable<MerchantProvider>, IOrderedQueryable<MerchantProvider>> orderBy = null, params Expression<Func<MerchantProvider, object>>[] includeProperties)
        {
            try
            {
                var _rep = this.GetRepository<MerchantProvider, PaymentDbContext>();
                var obj = _rep.Get(filter, orderBy, includeProperties);
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
        public override MerchantProvider GetById(object Id)
        {
            try
            {
                var _rep = this.GetRepository<MerchantProvider, PaymentDbContext>();
                var obj = _rep.GetByID(Id);
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

        public async Task<List<MerchantProvider>> GetAllMerchantProvider(int merchantId)
        {
            try
            {
                var _rep = this.GetRepository<MerchantProvider, PaymentDbContext>();
                var obj = await _rep.Get(t => t.MerchantId == merchantId).ToListAsync();
                if (obj.Count == 0)
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

    public interface IPaymentDbRepository
    {
        Task<MerchantProvider> GetMerchantProvider(Expression<Func<MerchantProvider, bool>> predicate);
        Task<List<MerchantProvider>> GetAllMerchantProvider(int merchantId);
    }
}
