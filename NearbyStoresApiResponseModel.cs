using System.Text.Json.Serialization;
namespace TestAutomation.ApiModel;

/// <summary>
/// Model classes for deserialization.
/// </summary>
public class NearbyStoresApiResponseModel
{
    [JsonPropertyName("paging")]
    public NearbyStoresModel.PagingValueApiResponseModel? Paging { get; set; }

    [JsonPropertyName("stores")]
    public List<StoreContainerApiResponseModel>? Stores { get; set; }

    internal class StoreContainer
    {
    }
}


