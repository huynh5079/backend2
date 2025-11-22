namespace BusinessLayer.Options;

public class MomoOptions
{
    public string PartnerCode { get; set; } = default!;
    public string AccessKey { get; set; } = default!;
    public string SecretKey { get; set; } = default!;
    public string EndpointCreate { get; set; } = default!;
    public string EndpointQuery { get; set; } = default!;
    public string EndpointRefund { get; set; } = default!;
    public string ReturnUrl { get; set; } = default!;
    public string NotifyUrl { get; set; } = default!;
}

