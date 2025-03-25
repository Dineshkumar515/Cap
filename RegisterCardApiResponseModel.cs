namespace TestAutomation.ApiModel;
public class RegistrationSource
{
    public string? Platform { get; set; }
    public string? Marketing { get; set; }
}

public class ImageUrl
{
    public string? Uri { get; set; }
    public string? ImageType { get; set; }
}

public class CardRegistrationResponse
{
    public string? SvcId { get; set; }
    public string? CardCurrency { get; set; }
    public string? Name { get; set; }
    public DateTime? RegistrationDate { get; set; }
    public DateTime? LastUnregistrationDate { get; set; }
    public RegistrationSource? RegistrationSource { get; set; }
    public string? CardId { get; set; }
    public string? CardNumber { get; set; }
    public string? Nickname { get; set; }
    public string? Class { get; set; }
    public string? Type { get; set; }
    public string? BalanceCurrencyCode { get; set; }
    public string? SubmarketCode { get; set; }
    public double? Balance { get; set; }
    public DateTime BalanceDate { get; set; }
    public List<ImageUrl>? ImageUrls { get; set; }
    public bool Primary { get; set; }
    public bool Partner { get; set; }
    public object? AutoReloadProfile { get; set; }
    public bool? Digital { get; set; }
    public bool? Owner { get; set; }
    public List<string>? Actions { get; set; }
}

public class ErrorResponse
{
    public string? Type { get; set; }
    public string? Title { get; set; }
    public int Status { get; set; }
    public string? Detail { get; set; }
    public string? Instance { get; set; }
    public string? Category { get; set; }
    public string? Code { get; set; }
    public string? Message { get; set; }
    public string? CorrelationId { get; set; }
    public string? ErrorCorrelationId { get; set; }
}
