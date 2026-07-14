using k8s;
using Microsoft.Extensions.Logging;
using TrainingOrchestrator.Controller;
using TrainingOrchestrator.Controller.Models;

var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
var logger = loggerFactory.CreateLogger<Program>();

// In-cluster config when running as a pod; falls back to the local kubeconfig
// (~/.kube/config) when running the controller directly on your laptop
// against the kind cluster for faster iteration.
var config = KubernetesClientConfiguration.IsInCluster()
    ? KubernetesClientConfiguration.InClusterConfig()
    : KubernetesClientConfiguration.BuildConfigFromConfigFile();

var client = new Kubernetes(config);
var reconciler = new Reconciler(client, loggerFactory.CreateLogger<Reconciler>());
var @namespace = Environment.GetEnvironmentVariable("WATCH_NAMESPACE") ?? "default";

logger.LogInformation("Training Orchestrator controller starting. Watching namespace: {Namespace}", @namespace);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

// Reconciliation loop: level-triggered polling on a fixed interval, backed by
// a watch for near-real-time reaction to spec changes. Polling alone would be
// simpler but slower to react; a bare watch alone is fragile across restarts
// (missed events). Combining both is the standard controller-runtime pattern.
var pollInterval = TimeSpan.FromSeconds(5);

_ = Task.Run(async () => await WatchTrainingJobsAsync(client, reconciler, @namespace, logger, cts.Token), cts.Token);

while (!cts.Token.IsCancellationRequested)
{
    try
    {
        var jobs = await client.CustomObjects.ListNamespacedCustomObjectAsync(
            TrainingJob.Group, TrainingJob.Version, @namespace, TrainingJob.Plural,
            cancellationToken: cts.Token);

        var items = ((Newtonsoft.Json.Linq.JObject)jobs)["items"]!
            .ToObject<List<TrainingJob>>() ?? new List<TrainingJob>();

        foreach (var job in items)
        {
            try
            {
                await reconciler.ReconcileAsync(job, @namespace, cts.Token);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Reconcile failed for TrainingJob {Name}", job.Metadata.Name);
            }
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Poll loop error");
    }

    await Task.Delay(pollInterval, cts.Token).ContinueWith(_ => { });
}

static async Task WatchTrainingJobsAsync(
    Kubernetes client, Reconciler reconciler, string @namespace, ILogger logger, CancellationToken ct)
{
    // A watch gives sub-second reaction time to `kubectl apply`/`delete`
    // instead of waiting for the next poll tick. Errors here (e.g. watch
    // timeout) just fall back to the poll loop above until the watch
    // reconnects — deliberately not fatal.
    try
    {
        var watchResponse = client.CustomObjects.ListNamespacedCustomObjectWithHttpMessagesAsync(
            TrainingJob.Group, TrainingJob.Version, @namespace, TrainingJob.Plural,
            watch: true, cancellationToken: ct);

        await foreach (var (type, item) in watchResponse.WatchAsync<TrainingJob, object>(
            onError: ex => logger.LogWarning(ex, "Watch error, will rely on poll loop until reconnect")))
        {
            logger.LogInformation("Watch event {Type} for {Name}", type, item.Metadata.Name);
            await reconciler.ReconcileAsync(item, @namespace, ct);
        }
    }
    catch (OperationCanceledException)
    {
        // shutdown
    }
}
