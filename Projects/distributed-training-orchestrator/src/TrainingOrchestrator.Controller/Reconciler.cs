using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;
using TrainingOrchestrator.Controller.Models;

namespace TrainingOrchestrator.Controller;

/// <summary>
/// Drives cluster state toward the desired state described by a TrainingJob CR.
///
/// Design note: reconciliation is level-triggered, not edge-triggered. Every
/// call recomputes the full desired state from the CR spec and diffs against
/// what's actually running, rather than reacting to "a pod died" as a
/// standalone event. This is the same principle built-in controllers
/// (Deployment, Job) use — it makes the controller resilient to missed events,
/// restarts, and out-of-order watch delivery.
/// </summary>
public class Reconciler
{
    private const string ApiGroup = "batch";
    private const string ApiVersion = "v1";
    private readonly Kubernetes _client;
    private readonly ILogger<Reconciler> _logger;

    public Reconciler(Kubernetes client, ILogger<Reconciler> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task ReconcileAsync(TrainingJob job, string @namespace, CancellationToken ct)
    {
        _logger.LogInformation(
            "Reconciling TrainingJob {Name} (workers={Workers}, epochs={Epochs})",
            job.Metadata.Name, job.Spec.WorkerCount, job.Spec.Epochs);

        var desiredRanks = Enumerable.Range(0, job.Spec.WorkerCount).ToList();
        var existingJobs = await ListWorkerJobsAsync(job, @namespace, ct);

        var statuses = new List<WorkerStatus>();

        foreach (var rank in desiredRanks)
        {
            var jobName = WorkerJobName(job, rank);
            var existing = existingJobs.FirstOrDefault(j => j.Metadata.Name == jobName);

            if (existing is null)
            {
                _logger.LogInformation("Creating worker Job {JobName} (rank {Rank})", jobName, rank);
                await CreateWorkerJobAsync(job, @namespace, rank, ct);
                statuses.Add(new WorkerStatus { Rank = rank, PodName = jobName, Phase = "Pending" });
                continue;
            }

            var status = InterpretJobStatus(existing);

            // A worker Job that failed out (exceeded backoffLimit) needs to be
            // recreated so training resumes from the last checkpoint rather
            // than being left dead. This is the "self-healing" behavior the
            // demo highlights: delete a pod on camera, watch this branch fire.
            if (status.Phase == "Failed")
            {
                _logger.LogWarning(
                    "Worker rank {Rank} failed — deleting and recreating Job {JobName} to resume from checkpoint",
                    rank, jobName);
                await _client.BatchV1.DeleteNamespacedJobAsync(
                    jobName, @namespace,
                    propagationPolicy: "Background",
                    cancellationToken: ct);
                await CreateWorkerJobAsync(job, @namespace, rank, ct);
                status.Phase = "Resuming";
            }

            statuses.Add(status);
        }

        await UpdateStatusAsync(job, @namespace, statuses, ct);
    }

    private async Task<List<V1Job>> ListWorkerJobsAsync(TrainingJob job, string @namespace, CancellationToken ct)
    {
        var list = await _client.BatchV1.ListNamespacedJobAsync(
            @namespace,
            labelSelector: $"trainingjob={job.Metadata.Name}",
            cancellationToken: ct);
        return list.Items.ToList();
    }

    private async Task CreateWorkerJobAsync(TrainingJob job, string @namespace, int rank, CancellationToken ct)
    {
        var jobName = WorkerJobName(job, rank);
        var resources = job.Spec.Resources ?? new ResourceRequest();

        var k8sJob = new V1Job
        {
            Metadata = new V1ObjectMeta
            {
                Name = jobName,
                Labels = new Dictionary<string, string>
                {
                    ["trainingjob"] = job.Metadata.Name,
                    ["rank"] = rank.ToString(),
                    ["app"] = "training-worker",
                },
                OwnerReferences = new List<V1OwnerReference>
                {
                    new()
                    {
                        ApiVersion = $"{TrainingJob.Group}/{TrainingJob.Version}",
                        Kind = "TrainingJob",
                        Name = job.Metadata.Name,
                        Uid = job.Metadata.Uid,
                        Controller = true,
                        BlockOwnerDeletion = true,
                    },
                },
            },
            Spec = new V1JobSpec
            {
                BackoffLimit = 3,
                Template = new V1PodTemplateSpec
                {
                    Metadata = new V1ObjectMeta
                    {
                        Labels = new Dictionary<string, string>
                        {
                            ["trainingjob"] = job.Metadata.Name,
                            ["rank"] = rank.ToString(),
                        },
                    },
                    Spec = new V1PodSpec
                    {
                        RestartPolicy = "Never", // Job-level backoff handles retries
                        Containers = new List<V1Container>
                        {
                            new()
                            {
                                Name = "worker",
                                Image = job.Spec.Image,
                                Env = new List<V1EnvVar>
                                {
                                    new() { Name = "WORLD_SIZE", Value = job.Spec.WorkerCount.ToString() },
                                    new() { Name = "RANK", Value = rank.ToString() },
                                    new() { Name = "EPOCHS", Value = job.Spec.Epochs.ToString() },
                                    new() { Name = "CHECKPOINT_INTERVAL", Value = job.Spec.CheckpointInterval.ToString() },
                                    new() { Name = "CHECKPOINT_DIR", Value = "/checkpoints" },
                                    new() { Name = "DATA_DIR", Value = "/data" },
                                },
                                Resources = new V1ResourceRequirements
                                {
                                    Requests = new Dictionary<string, ResourceQuantity>
                                    {
                                        ["cpu"] = new ResourceQuantity(resources.Cpu),
                                        ["memory"] = new ResourceQuantity(resources.Memory),
                                    },
                                },
                                VolumeMounts = new List<V1VolumeMount>
                                {
                                    new() { Name = "checkpoints", MountPath = "/checkpoints" },
                                    new() { Name = "dataset", MountPath = "/data" },
                                },
                            },
                        },
                        Volumes = new List<V1Volume>
                        {
                            new()
                            {
                                Name = "checkpoints",
                                PersistentVolumeClaim = new V1PersistentVolumeClaimVolumeSource
                                {
                                    ClaimName = job.Spec.CheckpointPvcName ?? $"{job.Metadata.Name}-checkpoints",
                                },
                            },
                            new()
                            {
                                Name = "dataset",
                                PersistentVolumeClaim = new V1PersistentVolumeClaimVolumeSource
                                {
                                    ClaimName = job.Spec.DatasetPvcName ?? $"{job.Metadata.Name}-dataset",
                                },
                            },
                        },
                    },
                },
            },
        };

        await _client.BatchV1.CreateNamespacedJobAsync(k8sJob, @namespace, cancellationToken: ct);
    }

    private static WorkerStatus InterpretJobStatus(V1Job k8sJob)
    {
        var rank = int.Parse(k8sJob.Metadata.Labels["rank"]);
        var phase = "Running";

        if (k8sJob.Status?.Succeeded is > 0)
            phase = "Succeeded";
        else if (k8sJob.Status?.Failed is >= 3) // matches BackoffLimit
            phase = "Failed";
        else if (k8sJob.Status?.Active is null or 0 && k8sJob.Status?.Succeeded is null or 0)
            phase = "Pending";

        return new WorkerStatus
        {
            Rank = rank,
            PodName = k8sJob.Metadata.Name,
            Phase = phase,
            RestartCount = k8sJob.Status?.Failed ?? 0,
        };
    }

    private async Task UpdateStatusAsync(
        TrainingJob job, string @namespace, List<WorkerStatus> statuses, CancellationToken ct)
    {
        var overallPhase = statuses.All(s => s.Phase == "Succeeded") ? "Succeeded"
            : statuses.Any(s => s.Phase == "Failed") ? "Failed"
            : statuses.Any(s => s.Phase == "Resuming") ? "Resuming"
            : statuses.All(s => s.Phase == "Pending") ? "Pending"
            : "Running";

        var patch = new
        {
            status = new
            {
                phase = overallPhase,
                currentEpoch = statuses.Count == 0 ? 0 : statuses.Min(s => s.LastCheckpointEpoch),
                workerStatuses = statuses,
                lastUpdated = DateTime.UtcNow,
            },
        };

        await _client.CustomObjects.PatchNamespacedCustomObjectStatusAsync(
            new V1Patch(patch, V1Patch.PatchType.MergePatch),
            TrainingJob.Group, TrainingJob.Version, @namespace, TrainingJob.Plural,
            job.Metadata.Name, cancellationToken: ct);
    }

    private static string WorkerJobName(TrainingJob job, int rank) => $"{job.Metadata.Name}-worker-{rank}";
}
