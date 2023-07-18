

using Data.TablesMap;
using Entities;
using Microsoft.EntityFrameworkCore;

namespace Data
{
    public class MerchantContext : DbContext
    {
        public virtual DbSet<MerchantTopUp> MerchantTopUp { get; set; }
        public virtual DbSet<MerchantTopUpBaner> MerchantTopUpBaner { get; set; }
        public MerchantContext(DbContextOptions<MerchantContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfiguration(new MerchantTopUpMap());
            modelBuilder.ApplyConfiguration(new MerchantTopUpBanerMap());
         
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {

        }
    }
}