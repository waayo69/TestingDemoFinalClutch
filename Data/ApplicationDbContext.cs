using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System;
using TestingDemo.Models;

namespace TestingDemo.Data
{
    public class ApplicationUser : IdentityUser
    {
        // Add custom user properties
        public string? FullName { get; set; }
        public int? Age { get; set; }
        public DateTime? BirthDate { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? ZipCode { get; set; }
        public string? Country { get; set; }
        public string? ContactPersonNumber { get; set; } // Employee contact number
        public bool IsApproved { get; set; } = true;
    }


    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        // Ensure ApplicationUser is registered
        public DbSet<ApplicationUser> ApplicationUsers { get; set; }

        public DbSet<AcquiringRequestModel> AcquiringRequests { get; set; }

        public DbSet<ClientModel> Clients { get; set; }

        public DbSet<PermitRequirementModel> PermitRequirements { get; set; }

        public DbSet<RetainershipBIRModel> RetainershipBIRs { get; set; }
        public DbSet<RetainershipSPPModel> RetainershipSPPs { get; set; }
        public DbSet<OneTimeTransactionModel> OneTimeTransactions { get; set; }
        public DbSet<ExternalAuditModel> ExternalAudits { get; set; }

        public DbSet<ExpenseModel> Expenses { get; set; }
        public DbSet<RecurringExpense> RecurringExpenses { get; set; }
        public DbSet<ExpensePayment> ExpensePayments { get; set; }
        public DbSet<ExpensePaymentHistory> ExpensePaymentHistories { get; set; }
        public DbSet<RequirementPhoto> RequirementPhotos { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<ExpenseModel>()
                .Property(e => e.Amount)
                .HasColumnType("decimal(18,2)");

            builder.Entity<RecurringExpense>()
                .Property(e => e.Amount)
                .HasColumnType("decimal(18,2)");

            builder.Entity<ExpensePayment>()
                .Property(e => e.AmountPaid)
                .HasColumnType("decimal(18,2)");

            // Configure the relationship between RecurringExpense and ExpensePayment
            builder.Entity<ExpensePayment>()
                .HasOne(ep => ep.RecurringExpense)
                .WithMany(re => re.PaymentHistory)
                .HasForeignKey(ep => ep.RecurringExpenseId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure the relationship between Client and PermitRequirement
            builder.Entity<PermitRequirementModel>()
                .HasOne(pr => pr.Client)
                .WithMany()
                .HasForeignKey(pr => pr.ClientId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure the relationship between PermitRequirementModel and RequirementPhoto
            builder.Entity<RequirementPhoto>()
                .HasOne(rp => rp.Requirement)
                .WithMany(r => r.Photos)
                .HasForeignKey(rp => rp.RequirementId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure the relationship between ExpenseModel and ExpensePaymentHistory
            builder.Entity<ExpensePaymentHistory>()
                .HasOne(eph => eph.Expense)
                .WithMany(e => e.PaymentHistory)
                .HasForeignKey(eph => eph.ExpenseModelId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
