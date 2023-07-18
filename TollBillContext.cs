using Data.TablesMap;
using Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data
{
    public class TollBillContext_Old : DbContext
    {
        public virtual DbSet<TollPlateBill> TollPlateBill { get; set; }
        public virtual DbSet<TollPlateData> TollPlateData { get; set; }

        public TollBillContext_Old(DbContextOptions<TollBillContext_Old> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfiguration(new TollPlateBillMap());
            modelBuilder.ApplyConfiguration(new TollPlateDataMap());
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {

        }
    }
}
