using Microsoft.EntityFrameworkCore;
using PadesSign.Domain.Entities;

namespace PadesSign.Infrastructure.Data;

public class PadesSignDbContext : DbContext
{
    public PadesSignDbContext(DbContextOptions<PadesSignDbContext> options) : base(options) { }

    public DbSet<WorkflowTemplate> WorkflowTemplates => Set<WorkflowTemplate>();
    public DbSet<WorkflowStep>     WorkflowSteps     => Set<WorkflowStep>();
    public DbSet<DocumentEnvelope> Envelopes         => Set<DocumentEnvelope>();
    public DbSet<SignatureRecord>  SignatureRecords   => Set<SignatureRecord>();
    public DbSet<AuditEntry>       AuditLog           => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<WorkflowTemplate>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.HasMany(x => x.Steps).WithOne()
             .HasForeignKey(s => s.TemplateId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<WorkflowStep>(e =>
        {
            e.HasKey(x => x.Id);
            e.OwnsOne(x => x.Field, f =>
            {
                f.Property(p => p.Reason).HasMaxLength(500);
                f.Property(p => p.Location).HasMaxLength(200);
            });
        });

        b.Entity<DocumentEnvelope>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.OriginalFileName).HasMaxLength(500);
            e.HasMany(x => x.Signatures).WithOne()
             .HasForeignKey(s => s.EnvelopeId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<SignatureRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.OwnsOne(x => x.Field);
        });

        b.Entity<AuditEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Action).HasMaxLength(100);
            e.Property(x => x.IpAddress).HasMaxLength(45);
        });
    }
}