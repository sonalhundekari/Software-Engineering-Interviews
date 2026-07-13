using System.Text.Json.Serialization;

namespace FraudDetection.Producer;

/// <summary>
/// Mirrors the Kaggle creditcard.csv feature schema (V1..V28 are PCA
/// components in the real dataset; here they're synthetic). Shared shape
/// used across producer, stream processor, and serving API.
/// </summary>
public sealed class Transaction
{
    [JsonPropertyName("transaction_id")]
    public string TransactionId { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("account_id")]
    public string AccountId { get; set; } = "";

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("amount")]
    public double Amount { get; set; }

    [JsonPropertyName("merchant_category")]
    public string MerchantCategory { get; set; } = "";

    [JsonPropertyName("features")]
    public double[] Features { get; set; } = new double[28]; // V1..V28

    [JsonPropertyName("_simulated_label")]
    public int SimulatedLabel { get; set; }
}
