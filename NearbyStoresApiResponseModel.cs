using System.Text.Json.Serialization;
using static TestAutomation.ApiModel.RecommendedStoresModel;
namespace TestAutomation.ApiModel;

/// <summary>
/// Model classes for deserialization.
/// </summary>

public record NearbyStoresApiResponseModel(
    [property: JsonPropertyName("paging")] PagingValueApiResponseModel? Paging,
    [property: JsonPropertyName("stores")] List<StoreContainer>? Stores)
{
   // public object StoreNumber { get; internal set; }
    public int? StoreNumber { get; set; }
    public int? Id { get; set; }
    public string? BrandName { get; set; }
    public string? PhoneNumber { get; set; }
    public int? DistrictId { get; set; }
    public string? OwnershipTypeCode { get; set; }
    public string? Market { get; set; }
    public string? Name { get; set; }
    public NearbyStoresApiResponseValidationModel? Store { get; internal set; }
}

public record NearbyStoresApiResponseValidationModel
{

    [JsonPropertyName("stores")]
    public int? Stores { get; set; }

    [JsonPropertyName("storeNumber")]
    public int? StoreNumber { get; set; }

    [JsonPropertyName("id")]
    public int? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("brandName")]
    public string? BrandName { get; set; }

    [JsonPropertyName("phoneNumber")]
    public string? PhoneNumber { get; set; }

    [JsonPropertyName("districtId")]
    public int? DistrictId { get; set; }

    [JsonPropertyName("ownershipTypeCode")]
    public string? OwnershipTypeCode { get; set; }

    [JsonPropertyName("market")]
    public string? Market { get; set; }
    public NearbyStoresApiResponseValidationModel? Store { get; internal set; }
}
