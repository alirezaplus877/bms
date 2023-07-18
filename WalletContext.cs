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
    public class WalletContext : DbContext
    {
        public virtual DbSet<Charge> Charges { get; set; }

        public WalletContext(DbContextOptions<WalletContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfiguration(new WalletChargeMap());
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {

        }
    }
}

