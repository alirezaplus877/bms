using AutoMapper;
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
    public class PgwDbRepository : Service<UserClaim, PgwDbContext>, IPgwDbRepository
    {
        #region Private Variable
        private readonly IServiceProvider _serviceProvider;
        private readonly IMapper _mapper;

        #endregion

        #region Ctor
        public PgwDbRepository(IServiceProvider serviceProvider, IMapper mapper) : base(serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _mapper = mapper;

        }

        public async Task<bool> AddUserClaimAsync(UserClaim userClaim)
        {
            try
            {
                var _rep = this.GetRepository<UserClaim, PgwDbContext>();
                var obj = await _rep.Get(t => t.UserId == userClaim.UserId && t.ClaimId == userClaim.ClaimId).FirstOrDefaultAsync();
                if (obj != null)
                {
                    obj.ClaimValue = userClaim.ClaimValue;
                    obj.UserId = userClaim.UserId;
                    _rep.Update(obj);
                    int m = await _rep.SaveChangesAsync();
                    if (m > 0)
                    {
                        return true;
                    }
                    else
                        return false;
                }
                else
                {
                    _rep.Insert(userClaim);
                    int m = await _rep.SaveChangesAsync();
                    if (m > 0)
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
        #endregion

        public override IQueryable<UserClaim> Get(Expression<Func<UserClaim, bool>> filter = null, Func<IQueryable<UserClaim>, IOrderedQueryable<UserClaim>> orderBy = null, params Expression<Func<UserClaim, object>>[] includeProperties)
        {
            throw new NotImplementedException();
        }

        public async Task<List<UserClaim>> GetAllUserClaimsAsync(long userId)
        {
            try
            {
                var _rep = this.GetRepository<UserClaim, PgwDbContext>();
                var obj = await _rep.Get(t => t.UserId == userId).ToListAsync();
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

        public override UserClaim GetById(object Id)
        {
            throw new NotImplementedException();
        }

        public async Task<User> GetUserByIdAsync(long userId)
        {
            try
            {
                var _rep = this.GetRepository<User, PgwDbContext>();
                var obj = await _rep.Get(u => u.Id == userId).Include(c => c.UserClaims).FirstOrDefaultAsync();
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

        public async Task<List<UserClaim>> GetUserClaimByClaimTypeAsync(long userId)
        {
            try
            {
                var _rep = this.GetRepository<UserClaim, PgwDbContext>();
                var obj = await _rep.Get(t => t.UserId == userId && t.ClaimType == "PecBMSWallet").ToListAsync();
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

        public async Task<bool> UpdateUserClaimAsync(UserClaim userClaim)
        {
            try
            {
                var _repo = this.GetRepository<UserClaim, PgwDbContext>();
                var GetUserClaim = await _repo.Get(t => t.UserId == userClaim.UserId && t.ClaimId == userClaim.ClaimId).FirstOrDefaultAsync();
                if (GetUserClaim == null)
                {
                    return false;
                }
                GetUserClaim.ClaimValue = userClaim.ClaimValue;
                GetUserClaim.ClaimType = userClaim.ClaimType;
                GetUserClaim.UserId = userClaim.UserId;
                GetUserClaim.ClaimId = userClaim.ClaimId;
                _repo.Update(GetUserClaim);
                var result = await _repo.SaveChangesAsync() > 0;
                return result;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
    }

    public interface IPgwDbRepository
    {
        Task<bool> AddUserClaimAsync(UserClaim userClaim);
        Task<bool> UpdateUserClaimAsync(UserClaim userClaim);
        Task<List<UserClaim>> GetAllUserClaimsAsync(long userId);
        Task<User> GetUserByIdAsync(long userId);
        Task<List<UserClaim>> GetUserClaimByClaimTypeAsync(long userId);
    }

}
