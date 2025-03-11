using System.Text.Json.Serialization;
using static TestAutomation.ApiModel.RecommendedStoresModel;
namespace TestAutomation.ApiModel;
public class NearbyStoresModel
{
    // Model for NearbyStores Response Deserialization
    public class NearbyStoresApiResponseModel
    {
        [JsonPropertyName("paging")]
        public PagingApiResponseModel? Paging { get; set; }

        [JsonPropertyName("stores")]
        public List<StoreContainerApiResponseModel>? Stores { get; set; }
    }
    public class PagingValueApiResponseModel
    {
        [JsonPropertyName("total")]
        public int Total { get; set; }

        [JsonPropertyName("offset")]
        public int Offset { get; set; }

        [JsonPropertyName("limit")]
        public int Limit { get; set; }

        [JsonPropertyName("returned")]
        public int Returned { get; set; }
    }

    public class StoreContainer
    {
        [JsonPropertyName("distance")]
        public double Distance { get; set; }

        [JsonPropertyName("store")]
        public Store? Store { get; set; }
    }

    public class Store
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("brandName")]
        public string? BrandName { get; set; }

        [JsonPropertyName("storeNumber")]
        public string? StoreNumber { get; set; }

        [JsonPropertyName("phoneNumber")]
        public string? PhoneNumber { get; set; }

        [JsonPropertyName("districtId")]
        public int? DistrictId { get; set; }

        [JsonPropertyName("ownershipTypeCode")]
        public string? OwnershipTypeCode { get; set; }

        [JsonPropertyName("market")]
        public string? Market { get; set; }

        [JsonPropertyName("operatingStatus")]
        public OperatingStatus? OperatingStatus { get; set; }

        [JsonPropertyName("features")]
        public List<Feature>? Features { get; set; }

        [JsonPropertyName("timeZoneInfo")]
        public TimeZoneInfo? TimeZoneInfo { get; set; }

        [JsonPropertyName("regularHours")]
        public RegularHours? RegularHours { get; set; }

        [JsonPropertyName("hoursNext7Days")]
        public List<HoursNext7Days>? HoursNext7Days { get; set; }

        [JsonPropertyName("today")]
        public Today? Today { get; set; }

        [JsonPropertyName("serviceTime")]
        public object? ServiceTime { get; set; }

        [JsonPropertyName("xopState")]
        public object? XopState { get; set; }

        [JsonPropertyName("currency")]
        public string? Currency { get; set; }

        [JsonPropertyName("sdState")]
        public object? SdState { get; set; }

        [JsonPropertyName("accessType")]
        public string? AccessType { get; set; }

        [JsonPropertyName("legalEntityCode")]
        public string? LegalEntityCode { get; set; }

        [JsonPropertyName("licenseeStore")]
        public object? LicenseeStore { get; set; }
    }

    public class OperatingStatus
    {
        [JsonPropertyName("operating")]
        public bool Operating { get; set; }

        [JsonPropertyName("openDate")]
        public string? OpenDate { get; set; }

        [JsonPropertyName("closeDate")]
        public string? CloseDate { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }
    }

    public class Feature
    {
        [JsonPropertyName("code")]
        public string? Code { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    public class TimeZoneInfo
    {
        [JsonPropertyName("currentTimeOffset")]
        public int CurrentTimeOffset { get; set; }

        [JsonPropertyName("windowsTimeZoneId")]
        public string? WindowsTimeZoneId { get; set; }

        [JsonPropertyName("olsonTimeZoneId")]
        public string? OlsonTimeZoneId { get; set; }
    }

    public class RegularHours
    {
        [JsonPropertyName("monday")]
        public DayHours? Monday { get; set; }

        [JsonPropertyName("tuesday")]
        public DayHours? Tuesday { get; set; }

        [JsonPropertyName("wednesday")]
        public DayHours? Wednesday { get; set; }

        [JsonPropertyName("thursday")]
        public DayHours? Thursday { get; set; }

        [JsonPropertyName("friday")]
        public DayHours? Friday { get; set; }

        [JsonPropertyName("saturday")]
        public DayHours? Saturday { get; set; }

        [JsonPropertyName("sunday")]
        public DayHours? Sunday { get; set; }

        [JsonPropertyName("open24x7")]
        public bool Open24x7 { get; set; }
    }

    public class DayHours
    {
        [JsonPropertyName("open")]
        public bool Open { get; set; }

        [JsonPropertyName("open24Hours")]
        public bool Open24Hours { get; set; }

        [JsonPropertyName("openTime")]
        public string? OpenTime { get; set; }

        [JsonPropertyName("closeTime")]
        public string? CloseTime { get; set; }
    }

    public class HoursNext7Days
    {
        [JsonPropertyName("open")]
        public bool Open { get; set; }

        [JsonPropertyName("open24Hours")]
        public bool Open24Hours { get; set; }

        [JsonPropertyName("openTime")]
        public string? OpenTime { get; set; }

        [JsonPropertyName("closeTime")]
        public string? CloseTime { get; set; }

        [JsonPropertyName("date")]
        public string? Date { get; set; }

        [JsonPropertyName("holidayCode")]
        public string? HolidayCode { get; set; }
    }

    public class Today
    {
        [JsonPropertyName("open")]
        public bool Open { get; set; }

        [JsonPropertyName("open24Hours")]
        public bool Open24Hours { get; set; }

        [JsonPropertyName("openTime")]
        public string? OpenTime { get; set; }

        [JsonPropertyName("closeTime")]
        public string? CloseTime { get; set; }

        [JsonPropertyName("localTime")]
        public string? LocalTime { get; set; }

        [JsonPropertyName("openAsOfLocalTime")]
        public bool OpenAsOfLocalTime { get; set; }

        [JsonPropertyName("opensIn")]
        public string? OpensIn { get; set; }

        [JsonPropertyName("closesIn")]
        public string? ClosesIn { get; set; }
    }
}
