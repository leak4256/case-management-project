using CaseManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CaseManagement.Infrastructure.Persistence.Configurations;

public class CaseConfiguration : IEntityTypeConfiguration<Case>
{
    // datetime2 carries no offset, so a value read back from the database arrives as Unspecified
    // even though every write is UTC. Restoring the kind keeps JSON from emitting the same instant
    // once with a Z and once without it.
    private static readonly ValueConverter<DateTime, DateTime> UtcConverter =
        new(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

    public void Configure(EntityTypeBuilder<Case> builder)
    {
        builder.ToTable("Cases");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.OrganizationName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(c => c.Status)
            .HasConversion<byte>()
            .IsRequired();

        builder.Property(c => c.Priority)
            .HasConversion<byte>()
            .IsRequired();

        builder.Property(c => c.CreatedAt)
            .HasColumnType("datetime2(3)")
            .HasConversion(UtcConverter)
            .IsRequired();

        builder.Property(c => c.UpdatedAt)
            .HasColumnType("datetime2(3)")
            .HasConversion(UtcConverter)
            .IsRequired();

        builder.Property(c => c.RowVersion)
            .IsRowVersion();

        ConfigureIndexes(builder);
    }

    /// <summary>
    /// Every index here is covering: filter and sort columns form the key, and the columns the
    /// grid displays ride along in INCLUDE, so a page is answered from the index alone.
    /// </summary>
    private static void ConfigureIndexes(EntityTypeBuilder<Case> builder)
    {
        builder.HasIndex(c => c.CreatedAt)
            .IsDescending(true)
            .IncludeProperties(c => new { c.Title, c.OrganizationName, c.Status, c.Priority, c.UpdatedAt })
            .HasDatabaseName("IX_Cases_CreatedAt");

        builder.HasIndex(c => new { c.Status, c.CreatedAt })
            .IsDescending(false, true)
            .IncludeProperties(c => new { c.Title, c.OrganizationName, c.Priority, c.UpdatedAt })
            .HasDatabaseName("IX_Cases_Status_CreatedAt");

        builder.HasIndex(c => new { c.Priority, c.CreatedAt })
            .IsDescending(false, true)
            .IncludeProperties(c => new { c.Title, c.OrganizationName, c.Status, c.UpdatedAt })
            .HasDatabaseName("IX_Cases_Priority_CreatedAt");

        builder.HasIndex(c => c.OrganizationName)
            .HasDatabaseName("IX_Cases_OrganizationName");
    }
}
