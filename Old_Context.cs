using Data.TablesMap;
using Entities;
using Microsoft.EntityFrameworkCore;
namespace Data
{
    public class Old_Context : DbContext // this context not use in project after 1 month deleted well 
    {
        //public virtual DbSet<Organization> Organization { get; set; }
        public Old_Context(DbContextOptions<Old_Context> options) : base(options) { }

        public virtual DbSet<BillRequest> BillRequest { get; set; }
        public virtual DbSet<NajaWage> NajaWage { get; set; }
        public virtual DbSet<BillRequestDetail> BillRequestDetail { get; set; }
        public virtual DbSet<BillType> BillType { get; set; }
        public virtual DbSet<UsersBill> UsersBill { get; set; }
        public virtual DbSet<TransactionType> TransactionType { get; set; }
        public virtual DbSet<Organization> Organization { get; set; }
       


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfiguration(new BillRequestDetailMap());
            modelBuilder.ApplyConfiguration(new BillRequestMap());
            modelBuilder.ApplyConfiguration(new BillTypeMap());
            modelBuilder.ApplyConfiguration(new UsersBillMap());
            modelBuilder.ApplyConfiguration(new TransactionTypeMap());
            modelBuilder.ApplyConfiguration(new OrganizationMap());
            modelBuilder.ApplyConfiguration(new NajaWageMap());
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            
        }
        
    }

}
