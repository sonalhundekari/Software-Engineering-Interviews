using System.Collections.Concurrent;
using System.Text.Json;
using Confluent.Kafka;

namespace FraudDetection.Dashboard;

public sealed class PredictionRecord
{
    public string TransactionId { get; set; } = "";
    public string AccountId { get; set; } = "";
    public double Amount { get; set; }
    public double? FraudProbability { get; set; }
    public bool? IsFraud { get; set; }
}

/// <summary>
/// In-memory ring buffer of recent predictions, updated by the background
/// consumer and read by the Razor page. Bounded to avoid unbounded growth
/// during a long-running demo.
/// </summary>
public sealed class PredictionStore
{
    private const int MaxRecords = 200;
    private readonly ConcurrentQueue<PredictionRecord> _records = new();

    public void Add(PredictionRecord record)
    {
        _records.Enqueue(record);
        while (_records.Count > MaxRecords)
            _records.TryDequeue(out _);
    }

    public IReadOnlyList<PredictionRecord> Snapshot() => _records.ToArray();
}

/// <summary>Background hosted service that consumes 'predictions' and feeds PredictionStore.</summary>
public sealed class KafkaConsumerService : BackgroundService
{
    private readonly PredictionStore _store;
    private readonly string _bootstrapServers;
    private readonly string _topic;

    public KafkaConsumerService(PredictionStore store, IConfiguration config)
    {
        _store = store;
        _bootstrapServers = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS") ?? "localhost:9092";
        _topic = Environment.GetEnvironmentVariable("PREDICTIONS_TOPIC") ?? "predictions";
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() =>
        {
            var consumerConfig = new ConsumerConfig
            {
                BootstrapServers = _bootstrapServers,
                GroupId = "fraud-dashboard",
                AutoOffsetReset = AutoOffsetReset.Latest,
            };

            using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
            consumer.Subscribe(_topic);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = consumer.Consume(TimeSpan.FromSeconds(1));
                    if (result is null) continue;

                    var prediction = JsonSerializer.Deserialize<PredictionRecordDto>(result.Message.Value);
                    if (prediction is null) continue;

                    _store.Add(new PredictionRecord
                    {
                        TransactionId = prediction.TransactionId,
                        AccountId = prediction.AccountId,
                        Amount = prediction.Amount,
                        FraudProbability = prediction.FraudProbability,
                        IsFraud = prediction.IsFraud,
                    });
                }
                catch (ConsumeException)
                {
                    // transient -- keep polling
                }
            }

            consumer.Close();
        }, stoppingToken);
    }

    private sealed class PredictionRecordDto
    {
        public string TransactionId { get; set; } = "";
        public string AccountId { get; set; } = "";
        public double Amount { get; set; }
        public double? FraudProbability { get; set; }
        public bool? IsFraud { get; set; }
    }
}
