using System.Text.Json;
using Confluent.Kafka;

namespace FraudDetection.Producer;

public static class Program
{
    private static readonly string BootstrapServers =
        Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS") ?? "localhost:9092";

    private static readonly string Topic =
        Environment.GetEnvironmentVariable("TRANSACTIONS_TOPIC") ?? "transactions";

    public static async Task Main(string[] args)
    {
        var ratePerSecond = args.Length > 0 && double.TryParse(args[0], out var r) ? r : 5.0;
        var delay = TimeSpan.FromSeconds(1.0 / ratePerSecond);

        var config = new ProducerConfig { BootstrapServers = BootstrapServers };
        using var producer = new ProducerBuilder<string, string>(config).Build();

        Console.WriteLine($"Producing to '{Topic}' at ~{ratePerSecond}/sec (Ctrl+C to stop)...");
        var random = new Random();

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            while (!cts.IsCancellationRequested)
            {
                var txn = MakeSyntheticTransaction(random);
                var json = JsonSerializer.Serialize(txn);

                var result = await producer.ProduceAsync(
                    Topic,
                    new Message<string, string> { Key = txn.AccountId, Value = json },
                    cts.Token);

                Console.WriteLine($"sent {txn.TransactionId} amount={txn.Amount:F2} -> partition {result.Partition.Value}");
                await Task.Delay(delay, cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // graceful shutdown
        }
        finally
        {
            producer.Flush(TimeSpan.FromSeconds(5));
        }
    }

    /// <summary>
    /// Generates one synthetic transaction. ~2% are shifted to mimic a
    /// fraud-like outlier, mirroring the Python version's behavior.
    /// </summary>
    private static Transaction MakeSyntheticTransaction(Random random)
    {
        var isOutlier = random.NextDouble() < 0.02;
        var shift = isOutlier ? 3.5 : 0.0;

        var features = new double[28];
        for (var i = 0; i < 28; i++)
        {
            var sign = random.NextDouble() < 0.5 ? -1 : 1;
            features[i] = NextGaussian(random) + shift * sign;
        }

        var scale = isOutlier ? 1200.0 : 250.0;
        var amount = -scale * Math.Log(1.0 - random.NextDouble()); // exponential draw

        var categories = new[] { "grocery", "electronics", "travel", "gas", "online", "restaurant" };

        return new Transaction
        {
            AccountId = $"acct_{random.Next(1, 501):D4}",
            Amount = Math.Round(amount, 2),
            MerchantCategory = categories[random.Next(categories.Length)],
            Features = features,
            SimulatedLabel = isOutlier ? 1 : 0,
        };
    }

    /// <summary>Box-Muller transform since .NET has no built-in Gaussian sampler.</summary>
    private static double NextGaussian(Random random)
    {
        var u1 = 1.0 - random.NextDouble();
        var u2 = random.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }
}
