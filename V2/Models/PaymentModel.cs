using System;

namespace ParkingImporter.Models;   

public enum PaymentStatus
{
    Completed,
    Pending,
    Failed,
    Refunded
    
}

    public class CreatePaymentRequest
    {
        public string? ReservationId { get; set; }
        public string? ParkingSessionId { get; set; }
        public string Method { get; set; } = string.Empty;
        public string? DiscountCode { get; set; }
    }

public class Payment
{
    public string Transaction { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Initiator { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset CompletedAt { get; set; }
    public string Hash { get; set; } = null!;

    // extra info
    public decimal TAmount { get; set; }
    public DateTimeOffset TDate { get; set; }
    public string Method { get; set; } = null!;
    public string Issuer { get; set; } = null!;
    public string Bank { get; set; } = null!;

    public string? ReservationId { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Completed;
}


