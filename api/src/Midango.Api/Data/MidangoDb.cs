using Microsoft.EntityFrameworkCore;
using Midango.Api.Identity;
using Midango.Api.Venues;

namespace Midango.Api.Data;

public sealed class MidangoDb(DbContextOptions<MidangoDb> options) : DbContext(options)
{
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<StaffMember> Staff => Set<StaffMember>();
    public DbSet<OtpChallenge> OtpChallenges => Set<OtpChallenge>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Venue>(e =>
        {
            e.Property(v => v.NameEn).HasMaxLength(120);
            e.Property(v => v.NameAr).HasMaxLength(120);
        });

        b.Entity<Customer>(e =>
        {
            e.Property(c => c.PhoneE164).HasMaxLength(16);
            e.HasIndex(c => c.PhoneE164).IsUnique();
            e.Property(c => c.Name).HasMaxLength(60);
        });

        b.Entity<StaffMember>(e =>
        {
            e.ToTable("staff", t => t.HasCheckConstraint(
                "ck_staff_venue_matches_role",
                "(role = 'PlatformOperator') = (venue_id IS NULL)"));
            e.Property(s => s.Email).HasMaxLength(254);
            e.HasIndex(s => s.Email).IsUnique();
            e.Property(s => s.DisplayName).HasMaxLength(80);
            e.Property(s => s.Role).HasConversion<string>().HasMaxLength(32);
            e.HasOne<Venue>().WithMany().HasForeignKey(s => s.VenueId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<OtpChallenge>(e =>
        {
            e.Property(c => c.PhoneE164).HasMaxLength(16);
            e.Property(c => c.CodeHash).HasMaxLength(64);
            e.HasIndex(c => new { c.PhoneE164, c.CreatedAt });
        });

        b.Entity<RefreshToken>(e =>
        {
            e.Property(t => t.TokenHash).HasMaxLength(64);
            e.HasIndex(t => t.TokenHash).IsUnique();
            e.Property(t => t.SubjectKind).HasConversion<string>().HasMaxLength(16);
            e.HasIndex(t => new { t.SubjectKind, t.SubjectId });
        });
    }
}
