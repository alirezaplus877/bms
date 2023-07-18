using Data.TablesMap;
using Entities.Transportation;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data
{
    public class TosanSohaDbContext : DbContext
    {
        public virtual DbSet<TicketCard> TicketCard { get; set; }
        public virtual DbSet<SingleDirectionTicket> SingleDirectionTicket { get; set; }
        public virtual DbSet<TicketCardInfo> TicketCardInfo { get; set; }

        public TosanSohaDbContext(DbContextOptions<TosanSohaDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfiguration(new SingleDirectionTicketMap());
            modelBuilder.ApplyConfiguration(new TicketCardInfoMap());
            modelBuilder.ApplyConfiguration(new TicketCardMap());
           
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {

        }
    }
}
