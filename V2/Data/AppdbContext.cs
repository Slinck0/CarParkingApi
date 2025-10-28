using Microsoft.EntityFrameworkCore;
using ParkingImporter.Models;

namespace ParkingImporter.Data;

public class AppDbContext : DbContext
{
    public DbSet<ParkingLot> ParkingLots => Set<ParkingLot>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<Payment> Payments => Set<Payment>();

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
        e.HasKey(x => x.Transaction); // unieke sleutel
        e.Property(x => x.Amount).HasColumnType("decimal(10,2)");
        e.Property(x => x.TAmount).HasColumnType("decimal(10,2)");
        e.Property(x => x.Initiator).HasMaxLength(64);
        e.Property(x => x.Method).HasMaxLength(32);
        e.Property(x => x.Issuer).HasMaxLength(64);
        e.Property(x => x.Bank).HasMaxLength(64);
        e.Property(x => x.Hash).HasMaxLength(64);
    });
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
            optionsBuilder.UseSqlite("Data Source=parking.db");
    }

}
