using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartOps.Web.Models;
using SmartOps.Web.Models.CustomerApp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Threading.Tasks;

namespace SmartOps.Web.Data
{
    public partial class AppDBContext : IdentityDbContext<AppUser>
    {
        public bool BridgeImporting { get; set; }
        private readonly ITenantContext _tenantContext;
        private readonly IHttpContextAccessor? _auditHttp;
        public int? CurrentTenantId =>
        _tenantContext.TenantId;

        public AppDBContext(
            DbContextOptions<AppDBContext> options,
            ITenantContext tenantContext, IHttpContextAccessor? auditHttp = null)
            : base(options)
        {
            _tenantContext = tenantContext;
            _auditHttp = auditHttp;
        }
        public DbSet<TenantQuestionSet> TenantQuestionSets { get; set; } = default!;
        public DbSet<SystemSetting> SystemSettings { get; set; }
        public DbSet<NewsArticle> NewsArticles { get; set; } = default!;
        public DbSet<PageHit> PageHits { get; set; }
        public DbSet<SurveyAppRequest> SurveyAppRequests { get; set; }
        public DbSet<Insurer> Insurers { get; set; }
        public DbSet<InsuranceClaim> InsuranceClaims { get; set; }
        public DbSet<InsuranceClaimPhoto> InsuranceClaimPhotos { get; set; }
        public DbSet<InsurerMessage> InsurerMessages { get; set; } = default!;
        public DbSet<AgentMessage> AgentMessages { get; set; } = default!;
        public DbSet<AgentMessageRecipient> AgentMessageRecipients { get; set; } = default!;

        public DbSet<InsuranceClaimUpdate> InsuranceClaimUpdates { get; set; }
        public DbSet<InsuranceClaimRequest> InsuranceClaimRequests { get; set; }

        public DbSet<Tenant> Tenant { get; set; } = default!;

        public DbSet<SupportTicket> SupportTickets { get; set; }
        public DbSet<SupportTicketMessage> SupportTicketMessages { get; set; }
        public DbSet<BookingReservation> BookingReservations { get; set; } = default!;
        public DbSet<Job> Job { get; set; } = default!;
        public DbSet<Customer> Customer { get; set; } = default!;
        public DbSet<Contract> Contract { get; set; } = default!;

        public DbSet<Invoice> Invoices { get; set; } = default!;
        public DbSet<InvoiceLine> InvoiceLines { get; set; } = default!;
        public DbSet<Estimate> Estimates { get; set; } = default!;
        public DbSet<EstimateLine> EstimateLines { get; set; } = default!;

        public DbSet<ContractUpdate> ContractUpdates { get; set; } = default!;

        public DbSet<ContractNote> ContractNotes { get; set; } = default!;
        public DbSet<Agent> Agent { get; set; } = default!;
        public DbSet<JobHeader> JobHeader { get; set; } = default!;
        public DbSet<GarageDoorMotor> GarageDoorMotor { get; set; } = default!;

        public DbSet<SecuritySurvey> SecuritySurveys { get; set; } = default!;

        public DbSet<CustomItem> CustomItems { get; set; } = default!;
        public DbSet<PhotoImage> Images { get; set; } = default!;

        public DbSet<Branch> Branches { get; set; } = default!;
        public DbSet<Vehicle> Vehicles { get; set; } = default!;

        public DbSet<VehicleCheckHeader> VehicleCheckHeaders { get; set; } = default!;
        public DbSet<VehicleCheck> VehicleChecks { get; set; } = default!;
        public DbSet<VAccident> VAccidents { get; set; } = default!;

        public DbSet<FAccident> FAccidents { get; set; } = default!;
        public DbSet<ToolCheck> ToolChecks { get; set; } = default!;
        public DbSet<MileageSheet> MileageSheets { get; set; } = default!;
        public DbSet<LadderCheck> LadderChecks { get; set; } = default!;
        public DbSet<SpotCheck> SpotChecks { get; set; } = default;
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.Entity<BookingReservation>().HasIndex(r => new { r.TenantId, r.AgentId, r.Start, r.ExpiresUtc });
            builder.Entity<Job>().HasIndex(j => new { j.TenantId, j.AgentId, j.DiaryDate });

            ConfigureAuditLog(builder);
            builder.Entity<Invoice>().HasOne(i => i.Contract).WithMany()
                .HasForeignKey(i => i.ContractId).OnDelete(DeleteBehavior.NoAction);
            builder.Entity<Invoice>().HasOne(i => i.Estimate).WithMany()
                .HasForeignKey(i => i.EstimateId).OnDelete(DeleteBehavior.NoAction);
            builder.Entity<Invoice>().HasIndex(i => i.ContractId).IsUnique();
            builder.Entity<Invoice>().HasIndex(i => new { i.TenantId, i.InvoiceDate, i.Id });
            builder.Entity<InvoiceLine>().HasOne(l => l.Invoice).WithMany(i => i.Lines)
                .HasForeignKey(l => l.InvoiceId).OnDelete(DeleteBehavior.Cascade);
            builder.Entity<InvoiceLine>().HasIndex(l => new { l.TenantId, l.InvoiceId });
            builder.Entity<Estimate>().HasOne(e => e.Contract).WithMany()
                .HasForeignKey(e => e.ContractId).OnDelete(DeleteBehavior.NoAction);
            builder.Entity<Estimate>().HasIndex(e => new { e.TenantId, e.ContractId, e.CreatedUtc });
            builder.Entity<EstimateLine>().HasOne(e => e.Estimate).WithMany(e => e.Lines)
                .HasForeignKey(e => e.EstimateId).OnDelete(DeleteBehavior.Cascade);
            builder.Entity<EstimateLine>().HasIndex(e => new { e.TenantId, e.EstimateId });
            builder.Entity<Contract>().HasOne<Estimate>().WithMany()
                .HasForeignKey(c => c.SelectedEstimateId).OnDelete(DeleteBehavior.NoAction);
            builder.Entity<TenantQuestionSet>(entity =>
            {
                entity.HasKey(x => x.TenantId);
                entity.Property(x => x.QuestionsJson).HasColumnType("nvarchar(max)").IsRequired();
                entity.Property(x => x.DraftQuestionsJson).HasColumnType("nvarchar(max)");
                entity.HasOne(x => x.Tenant).WithMany()
                    .HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            });
            // Customize the ASP.NET Identity model and override the defaults if needed.
            // For example, you can rename the ASP.NET Identity table names and more.
            // Add your customizations after calling base.OnModelCreating(builder);

            var decimalProps = builder.Model
            .GetEntityTypes()
            .SelectMany(t => t.GetProperties())
            .Where(p => (System.Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType) == typeof(decimal));

            //builder.Entity<DeliveryVan>();
            //builder.Entity<DeliveryHGV>();
            //builder.Entity<FitterVan>();
            //builder.Entity<SalesCar>();

            foreach (var property in decimalProps)
            {
                property.SetPrecision(18);
                property.SetScale(2);
            }

            builder.Entity<NewsArticle>()
                .HasIndex(x => new { x.IsPublished, x.PublishedDate });

            builder.Entity<AgentMessageRecipient>()
                .HasOne(x => x.AgentMessage)
                .WithMany(x => x.Recipients)
                .HasForeignKey(x => x.AgentMessageId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<AgentMessageRecipient>()
                .HasOne(x => x.Agent)
                .WithMany()
                .HasForeignKey(x => x.AgentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<AgentMessageRecipient>()
                .HasIndex(x => new { x.AgentMessageId, x.AgentId })
                .IsUnique();

            builder.Entity<AgentMessageRecipient>()
                .HasIndex(x => new { x.AgentId, x.IsRead });

            builder.Entity<JobHeader>()
                    .HasOne(h => h.GarageDoorMotor)
                    .WithOne()
                    .HasForeignKey<GarageDoorMotor>(g => g.Id)
                    .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<GarageDoorMotor>()
                .Property(g => g.Id)
                .ValueGeneratedNever();

            builder.Entity<JobHeader>()
                .HasOne(h => h.SecuritySurvey)
                .WithOne()
                .HasForeignKey<SecuritySurvey>(g => g.Id)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<SecuritySurvey>()
                .Property(g => g.Id)
                .ValueGeneratedNever();

            // Applies tennancy filter to all classes that inherit TennantEntity
            foreach (var entityType in builder.Model.GetEntityTypes())
            {
                if (typeof(TenantEntity)
                    .IsAssignableFrom(entityType.ClrType))
                {
                    var method = typeof(AppDBContext)
                        .GetMethod(
                            nameof(ApplyTenantFilter),
                            System.Reflection.BindingFlags.NonPublic |
                            System.Reflection.BindingFlags.Instance)!
                        .MakeGenericMethod(entityType.ClrType);

                    method.Invoke(this, new object[] { builder });
                }
            }

            builder.Entity<Contract>()
                .HasOne(c => c.Customer)
                .WithMany()
                .HasForeignKey(c => c.CustomerId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<Job>()
                .HasOne(j => j.Contract)
                .WithMany()
                .HasForeignKey(j => j.ContractId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<Job>()
                .HasOne(j => j.Customer)
                .WithMany()
                .HasForeignKey(j => j.CustomerId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<Job>()
                .HasOne(j => j.Agent)
                .WithMany()
                .HasForeignKey(j => j.AgentId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<SupportTicket>()
                .HasOne(x => x.Contract)
                .WithMany()
                .HasForeignKey(x => x.ContractId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<SupportTicket>()
                .HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<SupportTicketMessage>()
                .HasOne(x => x.SupportTicket)
                .WithMany(x => x.Messages)
                .HasForeignKey(x => x.SupportTicketId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<SupportTicket>()
                .HasIndex(x => x.ContractId);

            builder.Entity<SupportTicket>()
                .HasIndex(x => x.CustomerId);

            builder.Entity<SupportTicketMessage>()
                .HasIndex(x => x.SupportTicketId);

            builder.Entity<SupportTicket>()
    .           HasQueryFilter(x => x.TenantId == CurrentTenantId);

            builder.Entity<SupportTicketMessage>()
                .HasQueryFilter(x => x.TenantId == CurrentTenantId);

            builder.Entity<Contract>()
                .HasOne(c => c.Insurer)
                .WithMany(i => i.Contracts)
                .HasForeignKey(c => c.InsurerId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<Contract>()
                .HasIndex(c => c.ContractNumber)
                .IsUnique();

            builder.Entity<Contract>()
                .HasOne(c => c.Tenant)
                .WithMany()
                .HasForeignKey(c => c.TenantId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<InsurerMessage>()
                .HasOne(x => x.Contract)
                .WithMany()
                .HasForeignKey(x => x.ContractId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<InsurerMessage>()
                .HasOne(x => x.Insurer)
                .WithMany()
                .HasForeignKey(x => x.InsurerId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<InsurerMessage>()
                .HasOne(x => x.SentByUser)
                .WithMany()
                .HasForeignKey(x => x.SentByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<InsurerMessage>()
                .HasIndex(x => x.ContractId);

            builder.Entity<InsurerMessage>()
                .HasIndex(x => x.InsurerId);

            builder.Entity<InsuranceClaim>()
                .HasOne(x => x.Insurer)
                .WithMany()
                .HasForeignKey(x => x.InsurerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Contract>()
                .HasOne(x => x.InsuranceClaim)
                .WithOne()
                .HasForeignKey<Contract>(x => x.InsuranceClaimId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<InsuranceClaimRequest>()
                .HasIndex(x => new
                {
                    x.InsuranceClaimId,
                    x.TenantId
                })
                .IsUnique();

            builder.ApplyConfiguration(new ApplicationUserEntityConfiguration());
            // 4D web: SQLite is only the request-local LINQ workspace.
            foreach (var entity in builder.Model.GetEntityTypes())
                foreach (var property in entity.GetProperties())
                {
                    property.SetColumnType(null);
                    property.SetDefaultValueSql(null);
                    property.SetComputedColumnSql(null);
                    if (property.ClrType == typeof(decimal))
                        property.SetValueConverter(new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<decimal, double>(value => (double)value, value => (decimal)value));
                    if (property.ClrType == typeof(DateTimeOffset))
                        property.SetValueConverter(new Microsoft.EntityFrameworkCore.Storage.ValueConversion.DateTimeOffsetToStringConverter());
                }
        }

        private void ApplyTenantFilter<TEntity>(ModelBuilder builder)
                                        where TEntity : TenantEntity
        {
            builder.Entity<TEntity>()
                .HasQueryFilter(x =>
                    x.TenantId == CurrentTenantId);
        }

        public override int SaveChanges() => SaveChanges(true);

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => SaveChangesAsync(true, cancellationToken);

        private void SetTenantIds()
        {
            if (CurrentTenantId == null)
                return;

            foreach (var entry in ChangeTracker
                .Entries<TenantEntity>()
                .Where(x => x.State == EntityState.Added))
            {
                entry.Entity.TenantId =
                    CurrentTenantId.Value;
            }
        }

        public class ApplicationUserEntityConfiguration : IEntityTypeConfiguration<AppUser>
        {
            public void Configure(EntityTypeBuilder<AppUser> builder)
            {
                builder.Property(u => u.FirstName).HasMaxLength(255);
                builder.Property(u => u.LastName).HasMaxLength(255);

                builder.HasOne(u => u.Insurer)
                                    .WithMany()
                                    .HasForeignKey(u => u.InsurerId)
                                    .OnDelete(DeleteBehavior.SetNull);
            }
        }

    }
}
