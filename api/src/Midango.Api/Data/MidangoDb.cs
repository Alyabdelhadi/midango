using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Midango.Api.Bookings;
using Midango.Api.Catalog;
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
    public DbSet<SportCategory> SportCategories => Set<SportCategory>();
    public DbSet<CourtFeature> CourtFeatures => Set<CourtFeature>();
    public DbSet<Court> Courts => Set<Court>();
    public DbSet<OpeningHours> OpeningHours => Set<OpeningHours>();
    public DbSet<PriceBand> PriceBands => Set<PriceBand>();
    public DbSet<Booking> Bookings => Set<Booking>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasPostgresExtension("btree_gist");

        b.Entity<Venue>(e =>
        {
            e.Property(v => v.NameEn).HasMaxLength(120);
            e.Property(v => v.NameAr).HasMaxLength(120);
            e.Property(v => v.AddressEn).HasMaxLength(200);
            e.Property(v => v.AddressAr).HasMaxLength(200);
            e.Property(v => v.TimeZoneId).HasMaxLength(64);
            e.Property(v => v.CurrencyCode).HasMaxLength(3);
            e.Property(v => v.VatPercent).HasPrecision(5, 2);
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

        b.Entity<SportCategory>(e =>
        {
            e.ToTable("sport_categories", t => t.HasCheckConstraint(
                "ck_sport_categories_durations", "cardinality(durations_minutes) BETWEEN 1 AND 4"));
            e.Property(c => c.NameEn).HasMaxLength(60);
            e.Property(c => c.NameAr).HasMaxLength(60);
            e.Property(c => c.IconKey).HasMaxLength(40);
            e.HasMany(c => c.Features).WithOne().HasForeignKey(f => f.CategoryId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<CourtFeature>(e =>
        {
            e.Property(f => f.NameEn).HasMaxLength(60);
            e.Property(f => f.NameAr).HasMaxLength(60);
        });

        b.Entity<Court>(e =>
        {
            e.Property(c => c.NameEn).HasMaxLength(60);
            e.Property(c => c.NameAr).HasMaxLength(60);
            e.Property(c => c.HourlyPrice).HasPrecision(10, 2);
            e.HasOne<Venue>().WithMany().HasForeignKey(c => c.VenueId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<SportCategory>().WithMany().HasForeignKey(c => c.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(c => c.Features).WithMany().UsingEntity(j => j.ToTable("court_feature_links"));
            e.HasIndex(c => new { c.VenueId, c.SortOrder });
        });

        b.Entity<OpeningHours>(e =>
        {
            e.ToTable("opening_hours", t => t.HasCheckConstraint("ck_opening_hours_not_empty", "opens <> closes"));
            e.HasKey(h => new { h.VenueId, h.Day });
            e.HasOne<Venue>().WithMany().HasForeignKey(h => h.VenueId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<PriceBand>(e =>
        {
            e.Property(p => p.HourlyPrice).HasPrecision(10, 2);
            e.HasOne<Court>().WithMany().HasForeignKey(p => p.CourtId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(p => new { p.CourtId, p.Day });
        });

        b.Entity<Booking>(e =>
        {
            // The no-overlap exclusion constraint is added by hand in the Bookings migration (EF can't express it).
            e.ToTable("bookings", t =>
            {
                t.HasCheckConstraint("ck_bookings_ends_after_start", "ends_at > starts_at");
                // A booking is for exactly one of: an account or a walk-in. A block is for nobody.
                t.HasCheckConstraint("ck_bookings_party",
                    "(kind = 'Block' AND customer_id IS NULL AND guest_name IS NULL) OR " +
                    "(kind = 'Booking' AND (customer_id IS NULL) <> (guest_name IS NULL))");
            });
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Source).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.GuestName).HasMaxLength(60);
            e.Property(x => x.Note).HasMaxLength(200);
            e.HasIndex(x => new { x.VenueId, x.StartsAt });
            e.Property(x => x.Reference).HasMaxLength(12);
            e.HasIndex(x => x.Reference).IsUnique();
            e.Property(x => x.Currency).HasMaxLength(3);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Total).HasPrecision(10, 2);
            e.Property(x => x.Net).HasPrecision(10, 2);
            e.Property(x => x.Vat).HasPrecision(10, 2);
            e.HasOne<Venue>().WithMany().HasForeignKey(x => x.VenueId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Court>().WithMany().HasForeignKey(x => x.CourtId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.CourtId, x.StartsAt });
            e.HasIndex(x => new { x.CustomerId, x.StartsAt });
        });

        // Ids are created in code (Guid.CreateVersion7). Left as "generated on add", EF treats a new entity
        // reached through a navigation with its id already set as an existing row, sends an UPDATE that
        // matches nothing, and throws a concurrency error. Never-generated makes EF insert it.
        foreach (var entity in b.Model.GetEntityTypes())
        {
            if (entity.FindPrimaryKey()?.Properties is [{ Name: "Id" } key] && key.ClrType == typeof(Guid))
                key.ValueGenerated = ValueGenerated.Never;
        }
    }
}
