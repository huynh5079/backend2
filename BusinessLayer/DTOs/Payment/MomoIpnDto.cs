namespace BusinessLayer.DTOs.Payment;

public class MomoIpnRequestDto
{
    public string PartnerCode { get; set; } = default!;
    public string OrderId { get; set; } = default!;
    public string RequestId { get; set; } = default!;
    public long Amount { get; set; }
    public string OrderInfo { get; set; } = default!;
    public string OrderType { get; set; } = default!;
    public string TransId { get; set; } = default!;
    public int ResultCode { get; set; }
    public string Message { get; set; } = default!;
    public string PayType { get; set; } = default!;
    public long ResponseTime { get; set; }
    public string ExtraData { get; set; } = string.Empty;
    public string Signature { get; set; } = default!;
    public string AccessKey { get; set; } = default!;
}

public class MomoIpnResponseDto
{
    public int ResultCode { get; set; }
    public string Message { get; set; } = string.Empty;
}

