using Microsoft.EntityFrameworkCore;
using V2.Models;

namespace V2.Data;

public class AppDbContext : DbContext
{
    public DbSet<ParkingLotModel> ParkingLots => Set<ParkingLotModel>();
    public DbSet<ReservationModel> Reservations => Set<ReservationModel>();
    public DbSet<PaymentModel> Payments => Set<PaymentModel>();
    public DbSet<UserModel> Users => Set<UserModel>();
    public DbSet<VehicleModel> Vehicles => Set<VehicleModel>();
    public DbSet<ParkingSessionModel> ParkingSessions => Set<ParkingSessionModel>();
    public DbSet<OrganizationModel> Organizations { get; set; } = null!;
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<ParkingLotModel>(e =>
        {
            e.ToTable("parking_lot");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(255).IsRequired();
            e.Property(x => x.Location).HasMaxLength(100).IsRequired();
            e.Property(x => x.Address).HasMaxLength(255).IsRequired();
            e.Property(x => x.Tariff).HasColumnType("decimal(10,2)");
            e.Property(x => x.DayTariff).HasColumnType("decimal(10,2)");
            e.Property(x => x.Status).HasMaxLength(32);
            e.Property(x => x.ClosedReason).HasMaxLength(255);
            e.HasIndex(x => x.Location);
            e.HasIndex(x => x.CreatedAt);
            e.HasIndex(x => x.OrganizationId);
            e.HasOne<OrganizationModel>()
                .WithMany()
                .HasForeignKey(x => x.OrganizationId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        mb.Entity<ReservationModel>(e =>
        {
            e.ToTable("reservation");
            e.HasKey(x => x.Id);
            e.Property(x => x.Cost).HasColumnType("decimal(10, 2)");
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.HasIndex(x => x.ParkingLotId);
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.VehicleId);
            e.HasIndex(x => new { x.StartTime, x.EndTime });
        });

        mb.Entity<PaymentModel>(e =>
        {
            e.ToTable("payment");
            e.HasKey(x => x.Transaction);

            e.Property(x => x.Amount).HasColumnType("decimal(10,2)");
            e.Property(x => x.TAmount).HasColumnType("decimal(10,2)");

            e.Property(x => x.Initiator).HasMaxLength(64);
            e.Property(x => x.Method).HasMaxLength(32);
            e.Property(x => x.Issuer).HasMaxLength(64);
            e.Property(x => x.Bank).HasMaxLength(64);
            e.Property(x => x.Hash).HasMaxLength(64);

            // store enum as text (optional but consistent with Reservation.Status)
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);

            e.HasIndex(x => x.ReservationId);
        });

        mb.Entity<UserModel>(e =>
        {
            e.ToTable("user");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(255).IsRequired();
            e.Property(x => x.Email).HasMaxLength(255).IsRequired();
            e.HasIndex(x => x.Email);
            e.Property(x => x.OrganizationRole).HasMaxLength(32);
            e.HasIndex(x => x.OrganizationId);
            e.HasOne<OrganizationModel>()
                .WithMany()
                .HasForeignKey(x => x.OrganizationId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        mb.Entity<VehicleModel>(e =>
        {
            e.ToTable("vehicle");
            e.HasKey(x => x.Id);

            e.Property(x => x.LicensePlate).HasMaxLength(32).IsRequired();
            e.Property(x => x.Make).HasMaxLength(64).IsRequired();
            e.Property(x => x.Model).HasMaxLength(64).IsRequired();
            e.Property(x => x.Color).HasMaxLength(32).IsRequired();
            e.HasIndex(x => x.OrganizationId);
            e.HasOne<OrganizationModel>()
                .WithMany()
                .HasForeignKey(x => x.OrganizationId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => x.LicensePlate).IsUnique();
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.CreatedAt);
        });

        mb.Entity<ParkingSessionModel>(e =>
        {
            e.ToTable("parking_sessions");
            e.HasKey(x => x.Id);
            e.Property(x => x.UserId).IsRequired();
            e.Property(x => x.VehicleId).IsRequired();
            e.Property(x => x.LicensePlate).HasMaxLength(32);
            e.Property(x => x.StartTime).IsRequired();
            e.Property(x => x.EndTime);
            e.Property(x => x.Cost).HasColumnType("decimal(10,2)");
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.VehicleId);
            e.HasIndex(x => x.StartTime);
            e.HasIndex(x => x.EndTime);
        });

        mb.Entity<OrganizationModel>(e =>
        {
            e.ToTable("organization");
            e.HasKey(x => x.Id);

            e.Property(x => x.Name).HasMaxLength(255).IsRequired();
            e.Property(x => x.Email).HasMaxLength(255);
            e.Property(x => x.Phone).HasMaxLength(64);

            e.Property(x => x.Address).HasMaxLength(255);
            e.Property(x => x.City).HasMaxLength(128);
            e.Property(x => x.Country).HasMaxLength(128);

            e.HasIndex(x => x.Name).IsUnique();
            e.HasIndex(x => x.CreatedAt);
        });

    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
            optionsBuilder.UseSqlite("Data Source=parking.db");
    }
}
