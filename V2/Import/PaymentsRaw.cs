using System;
using System.Diagnostics.CodeAnalysis;

namespace V2.Import;

[ExcludeFromCodeCoverage]
public class PaymentsRaw
{
    public string transaction { get; set; } = null!;
    public decimal amount { get; set; }
    public string initiator { get; set; } = null!;
    public string created_at { get; set; } = null!;
    public string completed { get; set; } = null!;
    public string hash { get; set; } = null!;
    public TDataRaw t_data { get; set; } = null!;
}

public class TDataRaw
{
    public decimal amount { get; set; }
    public string date { get; set; } = null!;
    public string method { get; set; } = null!;
    public string issuer { get; set; } = null!;
    public string bank { get; set; } = null!;
}
