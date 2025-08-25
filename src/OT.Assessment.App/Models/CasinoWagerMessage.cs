using System.Text.Json.Serialization;

namespace OT.Assessment.App.Models;

public class CasinoWagerMessage
{
    [JsonPropertyName("wagerId")] public Guid WagerId { get; set; }
    [JsonPropertyName("theme")] public string Theme { get; set; } = "";
    [JsonPropertyName("provider")] public string Provider { get; set; } = "";
    [JsonPropertyName("gameName")] public string GameName { get; set; } = "";
    [JsonPropertyName("transactionId")] public string TransactionId { get; set; } = "";
    [JsonPropertyName("brandId")] public Guid BrandId { get; set; }
    [JsonPropertyName("accountId")] public Guid AccountId { get; set; }
    [JsonPropertyName("Username")] public string Username { get; set; } = "";
    [JsonPropertyName("externalReferenceId")] public string ExternalReferenceId { get; set; } = "";
    [JsonPropertyName("transactionTypeId")] public Guid TransactionTypeId { get; set; }
    [JsonPropertyName("amount")] public decimal Amount { get; set; }
    [JsonPropertyName("createdDateTime")] public DateTimeOffset CreatedDateTime { get; set; }
    [JsonPropertyName("numberOfBets")] public int NumberOfBets { get; set; }
    [JsonPropertyName("countryCode")] public string CountryCode { get; set; } = "";
    [JsonPropertyName("sessionData")] public string SessionData { get; set; } = "";
    [JsonPropertyName("duration")] public long? duration { get; set; }
}