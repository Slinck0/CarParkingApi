using System;

namespace V2.Models;

public enum PaymentStatus
{
    Completed,
    Pending,
    Failed,
    Refunded
}

public record CreatePaymentRequest(
    string ReservationId,
    string Method
);

public class PaymentModel
{
    public string Transaction { get; set; } = null!;
    public decimal Amount { get; set; }
    public string? Initiator { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset CompletedAt { get; set; }
    public string? Hash { get; set; }

    // extra info
    public decimal TAmount { get; set; }
    public DateTimeOffset TDate { get; set; }
    public string? Method { get; set; }
    public string? Issuer { get; set; }
    public string? Bank { get; set; }

    // link to reservation (string, because Reservation.Id is string)
    public string? ReservationId { get; set; }

    // default Completed so old imported rows are treated as completed
    public PaymentStatus Status { get; set; } = PaymentStatus.Completed;
}
