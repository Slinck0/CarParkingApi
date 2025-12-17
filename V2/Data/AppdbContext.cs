using Microsoft.EntityFrameworkCore;
using ParkingImporter.Models;

namespace ParkingImporter.Data;

public class AppDbContext : DbContext
{
    public DbSet<ParkingLot> ParkingLots => Set<ParkingLot>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<ParkingSessions> ParkingSessions => Set<ParkingSessions>();
    public DbSet<DiscountModel> Discounts => Set<DiscountModel>();
    


    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) {}

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<ParkingLot>(e =>
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
        });

        mb.Entity<Reservation>(e =>
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

        mb.Entity<Payment>(e =>
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
        });
        mb.Entity<User>(e =>
        {
            e.ToTable("user");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(255).IsRequired();
            e.Property(x => x.Email).HasMaxLength(255).IsRequired();
            e.HasIndex(x => x.Email);
        });
        mb.Entity<Vehicle>(e =>
        {
            e.ToTable("vehicle");
            e.HasKey(x => x.Id);

            e.Property(x => x.LicensePlate).HasMaxLength(32).IsRequired();
            e.Property(x => x.Make).HasMaxLength(64).IsRequired();
            e.Property(x => x.Model).HasMaxLength(64).IsRequired();
            e.Property(x => x.Color).HasMaxLength(32).IsRequired();

            e.HasIndex(x => x.LicensePlate).IsUnique();
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.CreatedAt);
        });
        mb.Entity<ParkingSessions>(e =>
        {
            e.ToTable("parking_sessions");
            e.HasKey(x => x.Id);
            e.Property(x => x.UserId).IsRequired();
            e.Property(x => x.VehicleId).IsRequired();
            e.Property(x => x.LicensePlate).HasMaxLength(32);
            e.Property(x => x.StartTime).IsRequired();
            e.Property(x => x.EndTime);
            e.Property(x => x.ParkingLotId).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Cost).HasColumnType("decimal(10,2)");
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.VehicleId);
            e.HasIndex(x => x.StartTime);
            e.HasIndex(x => x.EndTime);
        });
        mb.Entity<DiscountModel>(e =>
        {
            e.ToTable("discount");
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(64).IsRequired();
            e.Property(x => x.Percentage).HasColumnType("decimal(5,2)").IsRequired();
            e.Property(x => x.ValidUntil).IsRequired();
            e.HasIndex(x => x.Code).IsUnique();
        });
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
            optionsBuilder.UseSqlite("Data Source=parking.db");
    }

}
