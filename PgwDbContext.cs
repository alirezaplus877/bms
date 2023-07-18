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
    public class PgwDbContext : DbContext
    {
        public virtual DbSet<UserClaim> UserClaim { get; set; }
        public virtual DbSet<Claim> Claim { get; set; }
        public virtual DbSet<User> User { get; set; }

        public PgwDbContext(DbContextOptions<PgwDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfiguration(new UserClaimMap());
            modelBuilder.ApplyConfiguration(new ClaimMap());
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {

        }
    }
}
