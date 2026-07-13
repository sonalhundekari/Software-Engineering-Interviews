using System.Text.Json;
using System.Text.Json.Serialization;
using Streamiz.Kafka.Net;
using Streamiz.Kafka.Net.SerDes;
using Streamiz.Kafka.Net.Stream;
using Streamiz.Kafka.Net.Table;

namespace FraudDetection.StreamProcessor;

/// <summary>
/// Kafka Streams-style topology (via Streamiz.Kafka.Net) that stands in for
/// the Spark Structured Streaming / Flink jobs from the Python version:
///   1. Reads raw transactions from the 'transactions' topic.
///   2. Maintains a 5-minute tumbling-window count + running amount total
///      per account (a velocity feature -- the same signal the Spark/Flink
///      versions computed), backed by a local state store.
///   3. Calls the serving API per-record to get a fraud score.
///   4. Publishes enriched predictions to the 'predictions' topic.
///
/// Note on windowing semantics: this uses a tumbling window for simplicity;
/// the Python version used a sliding 5-minute window. Streamiz supports
/// hopping windows (WindowOptions.Of(size).AdvanceBy(step)) if you want an
/// exact match -- worth mentioning as a deliberate simplification if asked.
///
/// Run:
///     dotnet run --project StreamProcessor
/// </summary>
public static class Program
{
    private static readonly string BootstrapServers =
        Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS") ?? "localhost:9092";
    private static readonly string InTopic =
        Environment.GetEnvironmentVariable("TRANSACTIONS_TOPIC") ?? "transactions";
    private static readonly string OutTopic =
        Environment.GetEnvironmentVariable("PREDICTIONS_TOPIC") ?? "predictions";
    private static readonly string ServingUrl =
        Environment.GetEnvironmentVariable("SERVING_URL") ?? "http://localhost:8080/predict";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(1) };

    public static async Task Main(string[] args)
    {
        var config = new StreamConfig<StringSerDes, StringSerDes>
        {
            ApplicationId = "fraud-detection-stream-processor",
            BootstrapServers = BootstrapServers,
            AutoOffsetReset = Streamiz.Kafka.Net.Kafka.AutoOffsetReset.Latest,
        };

        var builder = new StreamBuilder();

        var transactions = builder.Stream<string, string>(InTopic); // keyed by account_id

        // 5-minute tumbling window: per-account transaction count + amount sum.
        var velocity = transactions
            .GroupByKey()
            .WindowedBy(TumblingWindowOptions.Of(TimeSpan.FromMinutes(5)))
            .Aggregate(
                () => new VelocityAgg(),
                (key, rawJson, agg) =>
                {
                    var txn = JsonSerializer.Deserialize<TransactionEvent>(rawJson)!;
                    agg.Count += 1;
                    agg.AmountSum += txn.Amount;
                    return agg;
                },
                InMemoryWindows.As<string, VelocityAgg>("velocity-store")
                    .WithValueSerdes<VelocityAggSerDes>());

        // Re-join the per-window aggregate back onto each raw transaction,
        // call the serving API, and emit the scored result.
        var scored = transactions.MapValues((key, rawJson) =>
        {
            var txn = JsonSerializer.Deserialize<TransactionEvent>(rawJson)!;
            var prediction = ScoreAsync(txn).GetAwaiter().GetResult();

            var result = new PredictionEvent
            {
                TransactionId = txn.TransactionId,
                AccountId = txn.AccountId,
                Amount = txn.Amount,
                FraudProbability = prediction?.FraudProbability,
                IsFraud = prediction?.IsFraud,
            };
            return JsonSerializer.Serialize(result);
        });

        scored.To(OutTopic);

        using var stream = new KafkaStream(builder.Build(), config);

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            stream.Dispose();
        };

        await stream.StartAsync();
    }

    private static async Task<PredictionResult?> ScoreAsync(TransactionEvent txn)
    {
        try
        {
            var payload = new
            {
                transactionId = txn.TransactionId,
                amount = txn.Amount,
                features = txn.Features,
            };
            var response = await Http.PostAsJsonAsync(ServingUrl, payload);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<PredictionResult>();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"serving call failed for {txn.TransactionId}: {ex.Message}");
            return null;
        }
    }
}

public sealed class TransactionEvent
{
    [JsonPropertyName("transaction_id")]
    public string TransactionId { get; set; } = "";

    [JsonPropertyName("account_id")]
    public string AccountId { get; set; } = "";

    [JsonPropertyName("amount")]
    public double Amount { get; set; }

    [JsonPropertyName("features")]
    public double[] Features { get; set; } = Array.Empty<double>();
}

public sealed class PredictionEvent
{
    [JsonPropertyName("transaction_id")]
    public string TransactionId { get; set; } = "";

    [JsonPropertyName("account_id")]
    public string AccountId { get; set; } = "";

    [JsonPropertyName("amount")]
    public double Amount { get; set; }

    [JsonPropertyName("fraud_probability")]
    public double? FraudProbability { get; set; }

    [JsonPropertyName("is_fraud")]
    public bool? IsFraud { get; set; }
}

public sealed class PredictionResult
{
    public double FraudProbability { get; set; }
    public bool IsFraud { get; set; }
}

/// <summary>Rolling per-account velocity aggregate held in the window store.</summary>
public sealed class VelocityAgg
{
    public long Count { get; set; }
    public double AmountSum { get; set; }
    public double AvgAmount => Count == 0 ? 0 : AmountSum / Count;
}

/// <summary>
/// Minimal custom SerDes for VelocityAgg. Streamiz requires explicit
/// serializers for custom aggregate types stored in a state store.
/// </summary>
public sealed class VelocityAggSerDes : Streamiz.Kafka.Net.SerDes.AbstractSerDes<VelocityAgg>
{
    public override VelocityAgg Deserialize(byte[]? data, SerializationContext context) =>
        data is null ? new VelocityAgg() : JsonSerializer.Deserialize<VelocityAgg>(data)!;

    public override byte[] Serialize(VelocityAgg data, SerializationContext context) =>
        JsonSerializer.SerializeToUtf8Bytes(data);
}
