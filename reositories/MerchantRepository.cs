using CacheRepository.Service;
using Data;
using Entities;
using Microsoft.EntityFrameworkCore;
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
    public class MerchantRepository : Service<MerchantTopUp, MerchantContext>, IMerchantRepository
    {
        #region Private Variable
        private readonly IServiceProvider _serviceProvider;
        #endregion

        #region Ctor
        public MerchantRepository(IServiceProvider serviceProvider) : base(serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }
        #endregion

        public override IQueryable<MerchantTopUp> Get(Expression<Func<MerchantTopUp, bool>> filter = null, Func<IQueryable<MerchantTopUp>, IOrderedQueryable<MerchantTopUp>> orderBy = null, params Expression<Func<MerchantTopUp, object>>[] includeProperties)
        {
            throw new NotImplementedException();
        }

        public override MerchantTopUp GetById(object Id)
        {
            throw new NotImplementedException();
        }

        public async Task<MerchantTopUp> GetMerchant(int Id)
        {
            try
            {
                var _rep = this.GetRepository<MerchantTopUp, MerchantContext>();
                var obj = await _rep.Get(t => t.Id == Id && t.IsActive==true).OrderByDescending(ot => ot.Id).FirstOrDefaultAsync();
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
        public async Task<List<MerchantTopUpBaner>> GetMerchantBaner(int merchantId)
        {
            try
            {
                var _rep =  this.GetRepository<MerchantTopUpBaner, MerchantContext>();
                var obj = await _rep.Get(t => t.MerchantId == merchantId).OrderByDescending(ot => ot.Id).ToListAsync();
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
    public interface IMerchantRepository
    {
        Task<MerchantTopUp> GetMerchant(int Id);
        Task<List<MerchantTopUpBaner>> GetMerchantBaner(int merchantId);
    }
}
