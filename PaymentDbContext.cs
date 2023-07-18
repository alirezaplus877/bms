using Data.TablesMap;
using Entities;
using Microsoft.EntityFrameworkCore;

namespace Data
{
    public class PaymentDbContext : DbContext
    {
        public virtual DbSet<MerchantProvider> MerchantProvider { get; set; }
        public virtual DbSet<BillRequest> BillRequest { get; set; }
        public virtual DbSet<NajaWage> NajaWage { get; set; }
        public virtual DbSet<BillRequestDetail> BillRequestDetail { get; set; }
        public virtual DbSet<BillType> BillType { get; set; }
        public virtual DbSet<UsersBill> UsersBill { get; set; }
        public virtual DbSet<AutoBillPayment> AutoBillPayment { get; set; }
        public virtual DbSet<AutoBillPaymentAudit> AutoBillPaymentAudit { get; set; }
        public virtual DbSet<TransactionType> TransactionType { get; set; }
        public virtual DbSet<Organization> Organization { get; set; }
        public virtual DbSet<WalletPayBill> WalletPayBill { get; set; }
        public virtual DbSet<WalletPayBillTransaction> WalletPayBillTransaction { get; set; }
        public virtual DbSet<TollPlateBill> TollPlateBill { get; set; }
        public virtual DbSet<TollPlateData> TollPlateData { get; set; }
        public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfiguration(new MerchantProviderMap());
            modelBuilder.ApplyConfiguration(new BillRequestDetailMap());
            modelBuilder.ApplyConfiguration(new BillRequestMap());
            modelBuilder.ApplyConfiguration(new BillTypeMap());
            //modelBuilder.ApplyConfiguration(new UsersBillMap());
            modelBuilder.ApplyConfiguration(new TransactionTypeMap());
            modelBuilder.ApplyConfiguration(new OrganizationMap());
            modelBuilder.ApplyConfiguration(new NajaWageMap());
            modelBuilder.ApplyConfiguration(new WalletPayBillMap());
            modelBuilder.ApplyConfiguration(new WalletPayBillTransactionMap());
            //modelBuilder.ApplyConfiguration(new AutoBillPaymentMap());
            //modelBuilder.ApplyConfiguration(new AutoBillPaymentAuditMap());
            modelBuilder.ApplyConfiguration(new TollPlateBillMap());
            modelBuilder.ApplyConfiguration(new TollPlateDataMap());

            modelBuilder.Entity<UsersBill>(entity =>
            {
                entity.ToTable("UsersBill", "pecbms");

                entity.Property(e => e.BillID)
                    .HasMaxLength(50)
                    .HasColumnName("BillID");

                entity.Property(e => e.ClientId).HasMaxLength(100);

                entity.Property(e => e.CreateDate)
                    .HasColumnType("datetime")
                    .HasComputedColumnSql("(getdate())", false);

                entity.Property(e => e.CustomerID)
                    .HasMaxLength(50)
                    .HasColumnName("CustomerID");

                entity.Property(e => e.OrganizationID).HasColumnName("OrganizationID");

                entity.Property(e => e.Title).HasMaxLength(50);

                entity.Property(e => e.UserID).HasColumnName("UserID");
            });

            modelBuilder.Entity<AutoBillPayment>(entity =>
            {
                entity.ToTable("AutoBillPayment", "pecbms");

                entity.Property(e => e.ExpireDatePayment).HasColumnType("datetime");

                entity.Property(e => e.MaxAmountPayment).HasColumnType("decimal(18, 0)");

                entity.HasOne(d => d.UsersBill)
                    .WithMany(p => p.AutoBillPayments)
                    .HasForeignKey(d => d.UsersBillId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_AutoBillPayment_UsersBill");
            });

            modelBuilder.Entity<AutoBillPaymentAudit>(entity =>
            {
                entity.ToTable("AutoBillPaymentAudit", "pecbms");

                entity.Property(e => e.DateActivity).HasColumnType("datetime");

                entity.HasOne(d => d.AutoBillPayment)
                    .WithMany(p => p.AutoBillPaymentAudits)
                    .HasForeignKey(d => d.AutoBillPaymentId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_AutoBillPaymentAudit_AutoBillPayment");
            });
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {

        }
    }
}