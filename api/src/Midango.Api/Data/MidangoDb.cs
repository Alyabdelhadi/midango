using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Midango.Api.Bookings;
using Midango.Api.Catalog;
using Midango.Api.Identity;
using Midango.Api.Loyalty;
using Midango.Api.Notifications;
using Midango.Api.Platform;
using Midango.Api.Tournaments;
using Midango.Api.Venues;

namespace Midango.Api.Data;

public sealed class MidangoDb(DbContextOptions<MidangoDb> options) : DbContext(options)
{
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<VenuePhoto> VenuePhotos => Set<VenuePhoto>();
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
    public DbSet<PlatformSettings> PlatformSettings => Set<PlatformSettings>();
    public DbSet<CustomerDevice> CustomerDevices => Set<CustomerDevice>();
    public DbSet<VenueLead> VenueLeads => Set<VenueLead>();
    public DbSet<OperatorAction> OperatorActions => Set<OperatorAction>();
    public DbSet<LegalDocument> LegalDocuments => Set<LegalDocument>();
    public DbSet<CustomerLevel> CustomerLevels => Set<CustomerLevel>();
    public DbSet<MatchPlayer> MatchPlayers => Set<MatchPlayer>();
    public DbSet<PointsEntry> PointsEntries => Set<PointsEntry>();
    public DbSet<Tournament> Tournaments => Set<Tournament>();
    public DbSet<TournamentTeam> TournamentTeams => Set<TournamentTeam>();
    public DbSet<TournamentMatch> TournamentMatches => Set<TournamentMatch>();

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
            e.Property(v => v.LogoPath).HasMaxLength(200);
        });

        b.Entity<VenuePhoto>(e =>
        {
            e.Property(p => p.Path).HasMaxLength(200);
            e.HasIndex(p => new { p.VenueId, p.SortOrder });
            e.HasOne<Venue>().WithMany().HasForeignKey(p => p.VenueId).OnDelete(DeleteBehavior.Cascade);
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
            e.ToTable("sport_categories", t =>
            {
                t.HasCheckConstraint("ck_sport_categories_durations", "cardinality(durations_minutes) BETWEEN 1 AND 4");
                t.HasCheckConstraint("ck_sport_categories_players_per_match", "players_per_match BETWEEN 2 AND 22");
            });
            e.Property(c => c.PlayersPerMatch).HasDefaultValue(4);
            e.Property(c => c.NameEn).HasMaxLength(60);
            e.Property(c => c.NameAr).HasMaxLength(60);
            e.Property(c => c.IconKey).HasMaxLength(40);
            e.Property(c => c.IconPath).HasMaxLength(200);
            e.HasMany(c => c.Features).WithOne().HasForeignKey(f => f.CategoryId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<CourtFeature>(e =>
        {
            e.Property(f => f.NameEn).HasMaxLength(60);
            e.Property(f => f.NameAr).HasMaxLength(60);
            e.Property(f => f.IconPath).HasMaxLength(200);
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
                // A booking is for someone (an account, a name typed at the desk, or both). A block is for nobody.
                t.HasCheckConstraint("ck_bookings_party",
                    "(kind = 'Block' AND customer_id IS NULL AND guest_name IS NULL) OR " +
                    "(kind = 'Booking' AND (customer_id IS NOT NULL OR guest_name IS NOT NULL))");
                // Cash taken stays on a live booking: undo the check-in before cancelling or marking a no-show.
                t.HasCheckConstraint("ck_bookings_checked_in_confirmed",
                    "checked_in_at IS NULL OR (status = 'Confirmed' AND kind = 'Booking' AND cash_collected IS NOT NULL)");
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
            e.Property(x => x.CashCollected).HasPrecision(10, 2);
            e.Property(x => x.MatchLevel).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.PointsDiscount).HasPrecision(10, 2);
            e.HasOne<Venue>().WithMany().HasForeignKey(x => x.VenueId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Court>().WithMany().HasForeignKey(x => x.CourtId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.CourtId, x.StartsAt });
            e.HasIndex(x => new { x.CustomerId, x.StartsAt });
            e.HasIndex(x => new { x.Status, x.StartsAt });
            e.HasOne<Tournament>().WithMany().HasForeignKey(x => x.TournamentId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.TournamentId);
        });

        b.Entity<Tournament>(e =>
        {
            e.ToTable("tournaments", t =>
            {
                t.HasCheckConstraint("ck_tournaments_ends_after_start", "ends_at > starts_at");
                t.HasCheckConstraint("ck_tournaments_registration_closes", "registration_closes_at <= starts_at");
            });
            e.Property(x => x.NameEn).HasMaxLength(120);
            e.Property(x => x.NameAr).HasMaxLength(120);
            e.Property(x => x.DescriptionEn).HasMaxLength(2000);
            e.Property(x => x.DescriptionAr).HasMaxLength(2000);
            e.Property(x => x.Format).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Level).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.EntryFee).HasPrecision(10, 2);
            e.Property(x => x.Currency).HasMaxLength(3);
            e.Property(x => x.CourtIds).HasColumnType("uuid[]");
            e.HasOne<Venue>().WithMany().HasForeignKey(x => x.VenueId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<SportCategory>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.VenueId, x.CreatedAt });
            e.HasIndex(x => new { x.Status, x.StartsAt });
        });

        b.Entity<TournamentTeam>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(40);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.CashCollected).HasPrecision(10, 2);
            e.HasOne<Tournament>().WithMany().HasForeignKey(x => x.TournamentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Customer>().WithMany().HasForeignKey(x => x.CaptainCustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Members).WithOne().HasForeignKey(m => m.TeamId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.TournamentId, x.RegisteredAt });
            e.HasIndex(x => x.CaptainCustomerId);
        });

        b.Entity<TournamentTeamMember>(e =>
        {
            e.HasKey(x => new { x.TeamId, x.Position });
            e.Property(x => x.Name).HasMaxLength(60);
            e.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<TournamentMatch>(e =>
        {
            e.Property(x => x.Stage).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.NextSlot).HasMaxLength(1);
            e.HasOne<Tournament>().WithMany().HasForeignKey(x => x.TournamentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<TournamentTeam>().WithMany().HasForeignKey(x => x.TeamAId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<TournamentTeam>().WithMany().HasForeignKey(x => x.TeamBId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<TournamentTeam>().WithMany().HasForeignKey(x => x.WinnerTeamId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Court>().WithMany().HasForeignKey(x => x.CourtId).OnDelete(DeleteBehavior.Restrict);
            // NO ACTION, not RESTRICT: a redraw deletes a whole bracket in one statement, checked at its end.
            e.HasOne<TournamentMatch>().WithMany().HasForeignKey(x => x.NextMatchId).OnDelete(DeleteBehavior.NoAction);
            e.HasIndex(x => new { x.TournamentId, x.Round, x.Position });
        });

        b.Entity<CustomerLevel>(e =>
        {
            e.HasKey(x => new { x.CustomerId, x.CategoryId });
            e.Property(x => x.Level).HasConversion<string>().HasMaxLength(16);
            e.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<SportCategory>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<MatchPlayer>(e =>
        {
            e.HasKey(x => new { x.BookingId, x.CustomerId });
            e.Property(x => x.CashCollected).HasPrecision(10, 2);
            e.Property(x => x.PointsDiscount).HasPrecision(10, 2);
            e.HasOne<Booking>().WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.CustomerId);
        });

        b.Entity<PlatformSettings>(e =>
        {
            e.ToTable("platform_settings", t => t.HasCheckConstraint("ck_platform_settings_singleton", "id = 1"));
            e.Property(s => s.Id).ValueGeneratedNever();
            e.Property(s => s.PointsPerSar).HasPrecision(6, 2);
            e.Property(s => s.SilverMultiplier).HasPrecision(4, 2);
            e.Property(s => s.GoldMultiplier).HasPrecision(4, 2);
            e.Property(s => s.SarPerRedeemStep).HasPrecision(10, 2);
            e.HasData(new PlatformSettings());
        });

        b.Entity<CustomerDevice>(e =>
        {
            e.Property(d => d.Token).HasMaxLength(512);
            e.HasIndex(d => d.Token).IsUnique();
            e.Property(d => d.Platform).HasMaxLength(16);
            e.Property(d => d.Language).HasMaxLength(2);
            e.HasOne<Customer>().WithMany().HasForeignKey(d => d.CustomerId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(d => d.CustomerId);
        });

        b.Entity<VenueLead>(e =>
        {
            e.Property(x => x.VenueName).HasMaxLength(120);
            e.Property(x => x.ContactName).HasMaxLength(80);
            e.Property(x => x.Phone).HasMaxLength(32);
            e.Property(x => x.City).HasMaxLength(80);
            e.Property(x => x.Message).HasMaxLength(1000);
            e.Property(x => x.Language).HasMaxLength(2);
            e.HasIndex(x => x.CreatedAt);
        });

        // Id is a long database identity; the Guid-only loop below leaves it generated on add.
        b.Entity<OperatorAction>(e =>
        {
            e.Property(x => x.Method).HasMaxLength(8);
            e.Property(x => x.Path).HasMaxLength(300);
            e.HasIndex(x => new { x.VenueId, x.At });
            e.HasOne<StaffMember>().WithMany().HasForeignKey(x => x.OperatorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Venue>().WithMany().HasForeignKey(x => x.VenueId).OnDelete(DeleteBehavior.Cascade);
        });

        // The points ledger. Id is a long identity, like OperatorAction. Balance = SUM(points) per customer.
        b.Entity<PointsEntry>(e =>
        {
            e.ToTable("points_entries");
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Note).HasMaxLength(200);
            e.HasIndex(x => new { x.CustomerId, x.At });
            e.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Booking>().WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<LegalDocument>(e =>
        {
            e.HasKey(x => x.Kind);
            e.Property(x => x.Kind).HasMaxLength(16);
            e.Property(x => x.BodyEn).HasMaxLength(LegalDocument.MaxBodyLength);
            e.Property(x => x.BodyAr).HasMaxLength(LegalDocument.MaxBodyLength);
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
