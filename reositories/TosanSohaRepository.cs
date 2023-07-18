using AutoMapper;
using CacheRepository.Service;
using Data;
using Dto.Proxy.Response.Tosan;
using Entities.Transportation;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using PEC.CoreCommon.Attribute;
using Pigi.MDbLogging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Repository.reositories
{
    [ScopedService]
    public class TosanSohaRepository : Service<TicketCard, TosanSohaDbContext>, ITosanSohaRepository, ILoggable
    {
        #region Private Variable
        private readonly IServiceProvider _serviceProvider;
        private readonly IMapper _mapper;
        private readonly IMdbLogger<TosanSohaRepository> _logger;
        #endregion

        public TosanSohaRepository(IServiceProvider serviceProvider, IMapper mapper, IMdbLogger<TosanSohaRepository> logger = null) : base(serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _mapper = mapper;
            _logger = logger;
        }

        #region ClientTicketCard

        public async Task<TicketCard> GetTicketCardByCardSerialAsync(string CardSerial)
        {
            try
            {
                var _rep = this.GetRepository<TicketCard, TosanSohaDbContext>();
                var result = await _rep.FirstOrDefaultAsync(c => c.CardSerial == CardSerial);
                return result;
            }
            catch (Exception ex)
            {
                _logger.Log(155, $"📥 AddTicketCard When Got Exception", JsonConvert.SerializeObject(CardSerial), ex);
                return null;
            }
        }
        public bool AddTicketCard(TicketCard myCard)
        {
            try
            {
                var _rep = this.GetRepository<TicketCard, TosanSohaDbContext>();
                _rep.Insert(myCard);
                int x = _rep.SaveChanges();
                if (x > 0)
                {
                    return true;
                }
                else
                    return false;
            }
            catch (Exception ex)
            {
                _logger.Log(155, $"📥 AddTicketCard When Got Exception", JsonConvert.SerializeObject(myCard), ex);
                return false;
            }
        }

        public async Task<bool> UpdateTicketCard(TicketCard myCard)
        {
            try
            {
                var _rep = this.GetRepository<TicketCard, TosanSohaDbContext>();
                var obj = await _rep.Get(e => e.CardSerial == myCard.CardSerial).FirstOrDefaultAsync();
                if (obj == null)
                {
                    return false;
                }
                else
                {
                    obj.Name = myCard.Name;
                    _rep.Update(obj);
                    var result = await _rep.SaveChangesAsync() > 0;
                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.Log(155, $"📥 UpdateMycard When Got Exception", JsonConvert.SerializeObject(myCard), ex);
                return false;
            }
        }

        public async Task<List<TicketCard>> GetTicketCardsAsync(long userId)
        {
            try
            {
                var _repo = this.GetRepository<TicketCard, TosanSohaDbContext>();
                var cards = _repo.Get(n => n.UserId == userId && n.IsDeleted == false);

                return await cards.ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.Log(155, $"📥 GetTicketcardlist When Got Exception", JsonConvert.SerializeObject(userId), ex);
                return null;
            }
        }
        public async Task<TicketCard> GetTicketCardByIdAsync(long CardId)
        {
            try
            {
                var _repo = this.GetRepository<TicketCard, TosanSohaDbContext>();
                var card = await _repo.Get(n => n.Id == CardId).FirstOrDefaultAsync();

                return card;
            }
            catch (Exception ex)
            {
                _logger.Log(155, $"📥 GetTicketcard When Got Exception", JsonConvert.SerializeObject(CardId), ex);
                return null;
            }
        }

        public async Task<bool> DeleteTicketCardAsync(long CardId)
        {
            try
            {
                var _repo = this.GetRepository<TicketCard, TosanSohaDbContext>();
                var card = await _repo.Get(n => n.Id == CardId).FirstOrDefaultAsync();

                card.IsDeleted = true;

                reopsitory.UpdateField(card, a => a.IsDeleted);
                int x = await _repo.SaveChangesAsync();
                if (x > 0)
                {
                    return true;
                }
                else
                    return false;
            }
            catch (Exception ex)
            {
                _logger.Log(155, $"📥 DeleteTicketCardAsync When Got Exception", JsonConvert.SerializeObject(CardId), ex);
                return false;
            }
        }
        #endregion

        #region TicketCardInfo
        public async Task<List<TicketCardInfo>> GetTicketCardsInfoAsync(long userId)
        {
            try
            {
                var _repo = this.GetRepository<TicketCardInfo, TosanSohaDbContext>();
                var cards = _repo.Get(n => n.UserId == userId);

                return await cards.ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.Log(155, $"📥 GetTicketCardInfolist When Got Exception", JsonConvert.SerializeObject(userId), ex);
                return null;
            }
        }

        public async Task<bool> AddTicketCardInfo(TicketCardInfo ticketCardInfo)
        {
            try
            {
                var _rep = this.GetRepository<TicketCardInfo, TosanSohaDbContext>();
                _rep.Insert(ticketCardInfo);
                int x = await _rep.SaveChangesAsync();
                if (x > 0)
                {
                    return true;
                }
                else
                    return false;
            }
            catch (Exception ex)
            {
                _logger.Log(155, $"📥 AddTicketCardInfo When Got Exception", JsonConvert.SerializeObject(ticketCardInfo), ex);
                return false;
            }
        }

        public async Task<TicketCardInfo> GetTicketCardInfoByIdAsync(long CardId)
        {
            try
            {
                var _repo = this.GetRepository<TicketCardInfo, TosanSohaDbContext>();
                var card = await _repo.Get(n => n.Id == CardId).FirstOrDefaultAsync();

                return card;
            }
            catch (Exception ex)
            {
                _logger.Log(155, $"📥 GetTicketcard When Got Exception", JsonConvert.SerializeObject(CardId), ex);
                return null;
            }

        }
        public async Task<List<TicketCardInfo>> GetTicketCardInfoByExperssionAsync(Expression<Func<TicketCardInfo, bool>> predicate)
        {
            try
            {
                var _repo = this.GetRepository<TicketCardInfo, TosanSohaDbContext>();
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
                _logger.Log(177, $"📥 GetTicketCardInfoByExperssionAsync When Got Exception", JsonConvert.SerializeObject(predicate), ex);
                return null;
            }
        }

        public async Task<bool> UpdateTicketCardInfoAsync(TicketCardInfo ticketCardInfo)
        {
            try
            {

                var _repo = this.GetRepository<TicketCardInfo, TosanSohaDbContext>();

                var obj = await _repo.Get(t => t.OrderId == ticketCardInfo.OrderId).FirstOrDefaultAsync();
                if (obj == null)
                {
                    return false;
                }
                else
                {
                    obj.NationalCode = ticketCardInfo.NationalCode;
                    obj.MobileNo = ticketCardInfo.MobileNo;
                    obj.RRN = ticketCardInfo.RRN;
                    obj.Token = ticketCardInfo.Token;
                    //  obj.WalletReturnId = ticketCardInfo.WalletReturnId;
                    obj.OrderId = ticketCardInfo.OrderId;
                    obj.Amount = ticketCardInfo.Amount;
                    //  obj.MaskCardNumber = ticketCardInfo.MaskCardNumber;
                    //   obj.IsSendToSettelment = ticketCardInfo.IsSendToSettelment;
                    obj.Status = ticketCardInfo.Status;
                    obj.PaymentStatus = ticketCardInfo.PaymentStatus;
                    obj.CreateDate = ticketCardInfo.CreateDate;
                    obj.UserId = ticketCardInfo.UserId;
                    obj.MerchantId = ticketCardInfo.MerchantId;
                    obj.TransactionType = ticketCardInfo.TransactionType;
                    obj.WalletId = ticketCardInfo.WalletId;

                    _repo.Update(obj);
                    var result = await _repo.SaveChangesAsync() > 0;

                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.Log(155, $"📥 UpdateticketCardInfoWage When Got Exception", JsonConvert.SerializeObject(ticketCardInfo), ex);
                return false;
            }
        }
        public async Task<bool> UpdateTicketCardInfoRequestAsync(long orderId)
        {
            try
            {
                var _repo = this.GetRepository<TicketCardInfo, TosanSohaDbContext>();
                _logger.Log(124, $"📥 UpdateTicketCardInfo Enter Method ", JsonConvert.SerializeObject(orderId), null);

                var obj = await _repo.Get(t => t.OrderId == orderId).FirstOrDefaultAsync();
                if (obj == null)
                {
                    return false;
                }
                else
                {
                    obj.RequestIsSent = true;
                    _repo.UpdateField(obj, p => p.RequestIsSent);
                    var result = await _repo.SaveChangesAsync() > 0;

                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.Log(155, $"📥 UpdateTicketCardInfoSentRequestAsync When Got Exception", orderId, ex);
                return false;
            }
        }
        #endregion

        #region SingleDirectionTicket
        public async Task<List<SingleDirectionTicket>> GetSingleDirectionTicketsAsync(long userId)
        {
            try
            {
                var _repo = this.GetRepository<SingleDirectionTicket, TosanSohaDbContext>();
                var card = await _repo.Get(n => n.UserId == userId).ToListAsync();

                return card;
            }
            catch (Exception ex)
            {
                _logger.Log(155, $"📥 GetSingleDirectionTicketsAsync When Got Exception", JsonConvert.SerializeObject(userId), ex);
                return null;
            }
        }

        public async Task<bool> AddSingleDirectionTicket(SingleDirectionTicket singleDirectionTicket)
        {
            try
            {
                var _rep = this.GetRepository<SingleDirectionTicket, TosanSohaDbContext>();
                _rep.Insert(singleDirectionTicket);
                int x = await _rep.SaveChangesAsync();
                if (x > 0)
                {
                    return true;
                }
                else
                    return false;
            }
            catch (Exception ex)
            {
                _logger.Log(155, $"📥 AddSingleDirectionTicket When Got Exception", JsonConvert.SerializeObject(singleDirectionTicket), ex);
                return false;
            }
        }
        public async Task<SingleDirectionTicket> GetSingleDirectionTicketByIdAsync(long orderId)
        {
            try
            {
                var _repo = this.GetRepository<SingleDirectionTicket, TosanSohaDbContext>();
                var singleDirectionTicket = await _repo.Get(n => n.OrderId == orderId).FirstOrDefaultAsync();

                return singleDirectionTicket;
            }
            catch (Exception ex)
            {
                _logger.Log(155, $"📥 GetSingleDirectionTicket When Got Exception", JsonConvert.SerializeObject(orderId), ex);
                return null;
            }

        }
        public async Task<List<SingleDirectionTicket>> GetSingleDirectionTicketByExperssionAsync(Expression<Func<SingleDirectionTicket, bool>> predicate)
        {
            try
            {
                var _repo = this.GetRepository<SingleDirectionTicket, TosanSohaDbContext>();
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
                _logger.Log(177, $"📥 GetSingleDirectionTicketByExperssionAsync When Got Exception", JsonConvert.SerializeObject(predicate), ex);
                return null;
            }
        }

        public async Task<List<SingleTicketResponsePaginated>> GetPaginatedSingleTicketAsync(long userId)
        {
            var _repo = this.GetRepository<SingleDirectionTicket, TosanSohaDbContext>();
            var najaWages = from s in _repo.Get(n => n.UserId == userId && (n.TransactionType == 1 || n.TransactionType == 2)).OrderByDescending(o => o.BussinessDate)
                            select new SingleTicketResponsePaginated
                            {
                                RRN = s.RRN,
                                Amount = s.Amount,
                                PgwToken = s.Token,
                                WalletReturnId = s.WalletReturnId,
                                TransactionType = s.TransactionType,
                                Status = s.Status,
                                PaymentStatus = s.PaymentStatus,
                                OrderId = s.OrderId.ToString(),
                                WalletId = s.WalletId,
                                BussinessDate = s.BussinessDate,
                                MunicipalCode = s.voucherMunicipalCode
                            };
          //var data =  najaWages.Where(d => d.BussinessDate >= Convert.ToDateTime("6/3/2023 12:00:00 AM") && d.BussinessDate <= Convert.ToDateTime("6/3/2023 12:00:00 AM")).AsQueryable().ToQueryString();
            return await najaWages.ToListAsync();
        }

        public async Task<bool> UpdateSingleDirectionTicketAsync(SingleDirectionTicket singleDirectionTicket)
        {
            try
            {

                var _repo = this.GetRepository<SingleDirectionTicket, TosanSohaDbContext>();

                var obj = await _repo.Get(t => t.OrderId == singleDirectionTicket.OrderId).FirstOrDefaultAsync();
                if (obj == null)
                {
                    return false;
                }
                else
                {
                    obj.NationalCode = singleDirectionTicket.NationalCode;
                    obj.MobileNo = singleDirectionTicket.MobileNo;
                    obj.RRN = singleDirectionTicket.RRN;
                    obj.Token = singleDirectionTicket.Token;
                    //  obj.WalletReturnId = ticketCardInfo.WalletReturnId;
                    obj.OrderId = singleDirectionTicket.OrderId;
                    obj.Amount = singleDirectionTicket.Amount;
                    //  obj.MaskCardNumber = ticketCardInfo.MaskCardNumber;
                    //   obj.IsSendToSettelment = ticketCardInfo.IsSendToSettelment;
                    obj.Status = singleDirectionTicket.Status;
                    obj.PaymentStatus = singleDirectionTicket.PaymentStatus;
                    obj.CreateDate = singleDirectionTicket.CreateDate;
                    obj.UserId = singleDirectionTicket.UserId;
                    obj.MerchantId = singleDirectionTicket.MerchantId;
                    obj.TransactionType = singleDirectionTicket.TransactionType;
                    obj.WalletId = singleDirectionTicket.WalletId;

                    _repo.Update(obj);
                    var result = await _repo.SaveChangesAsync() > 0;

                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.Log(155, $"📥 UpdateSingleDirectionTicketWage When Got Exception", JsonConvert.SerializeObject(singleDirectionTicket), ex);
                return false;
            }
        }
        public async Task<bool> UpdateSingleDirectionTicketRequestAsync(long orderId)
        {
            try
            {
                var _repo = this.GetRepository<SingleDirectionTicket, TosanSohaDbContext>();
                _logger.Log(124, $"📥 UpdateSingleDirectionTicket Enter Method ", JsonConvert.SerializeObject(orderId), null);

                var obj = await _repo.Get(t => t.OrderId == orderId).FirstOrDefaultAsync();
                if (obj == null)
                {
                    return false;
                }
                else
                {

                    obj.RequestIsSent = true;
                    _repo.UpdateField(obj, p => p.RequestIsSent);
                    var result = await _repo.SaveChangesAsync() > 0;

                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.Log(155, $"📥 UpdateSingleDirectionTicketSentRequestAsync When Got Exception", orderId, ex);
                return false;
            }
        }

        #endregion

        public override IQueryable<TicketCard> Get(Expression<Func<TicketCard, bool>> filter = null, Func<IQueryable<TicketCard>, IOrderedQueryable<TicketCard>> orderBy = null, params Expression<Func<TicketCard, object>>[] includeProperties)
        {
            throw new NotImplementedException();
        }
        public override TicketCard GetById(object Id)
        {
            throw new NotImplementedException();
        }
    }

    public interface ITosanSohaRepository
    {
        #region [ClientTicketCard]
        Task<bool> UpdateTicketCard(TicketCard myCard);
        Task<List<TicketCard>> GetTicketCardsAsync(long userId);
        bool AddTicketCard(TicketCard myCard);
        Task<TicketCard> GetTicketCardByIdAsync(long CardId);
        Task<TicketCard> GetTicketCardByCardSerialAsync(string CardSerial);

        Task<bool> DeleteTicketCardAsync(long CardId);
        #endregion

        #region [TicketCardInfo]

        Task<List<TicketCardInfo>> GetTicketCardsInfoAsync(long userId);
        Task<bool> AddTicketCardInfo(TicketCardInfo ticketCardInfo);
        Task<TicketCardInfo> GetTicketCardInfoByIdAsync(long CardId);
        Task<bool> UpdateTicketCardInfoAsync(TicketCardInfo ticketCardInfo);
        Task<bool> UpdateTicketCardInfoRequestAsync(long orderId);
        Task<List<TicketCardInfo>> GetTicketCardInfoByExperssionAsync(Expression<Func<TicketCardInfo, bool>> predicate);

        #endregion

        #region [SingleDirectionTicket]

        Task<List<SingleTicketResponsePaginated>> GetPaginatedSingleTicketAsync(long userId);

        Task<List<SingleDirectionTicket>> GetSingleDirectionTicketsAsync(long userId);
        Task<bool> AddSingleDirectionTicket(SingleDirectionTicket singleDirectionTicket);
        Task<SingleDirectionTicket> GetSingleDirectionTicketByIdAsync(long CardId);

        Task<bool> UpdateSingleDirectionTicketAsync(SingleDirectionTicket singleDirectionTicket);
        Task<bool> UpdateSingleDirectionTicketRequestAsync(long orderId);
        Task<List<SingleDirectionTicket>> GetSingleDirectionTicketByExperssionAsync(Expression<Func<SingleDirectionTicket, bool>> predicate);

        #endregion  
    }
}
