using k8s;
using k8s.Models;

namespace TrainingOrchestrator.Controller.Models;

/// <summary>
/// Strongly-typed wrapper for the ml.sonal.dev/v1 TrainingJob custom resource.
/// Mirrors crds/trainingjob-crd.yaml. Kept hand-written (no codegen) since the
/// schema is small and stable — revisit with a generator if it grows.
/// </summary>
public class TrainingJob : CustomResource<TrainingJobSpec, TrainingJobStatus>
{
    public const string Group = "ml.sonal.dev";
    public const string Version = "v1";
    public const string Plural = "trainingjobs";
}

public class TrainingJobSpec
{
    public string Image { get; set; } = string.Empty;
    public int WorkerCount { get; set; }
    public int Epochs { get; set; }
    public int CheckpointInterval { get; set; } = 1;
    public string? DatasetPvcName { get; set; }
    public string? CheckpointPvcName { get; set; }
    public ResourceRequest? Resources { get; set; }
}

public class ResourceRequest
{
    public string Cpu { get; set; } = "1";
    public string Memory { get; set; } = "2Gi";
}

public class TrainingJobStatus
{
    public string Phase { get; set; } = "Pending";
    public int CurrentEpoch { get; set; }
    public List<WorkerStatus> WorkerStatuses { get; set; } = new();
    public DateTime? LastUpdated { get; set; }
}

public class WorkerStatus
{
    public int Rank { get; set; }
    public string PodName { get; set; } = string.Empty;
    public string Phase { get; set; } = "Pending";
    public int LastCheckpointEpoch { get; set; }
    public int RestartCount { get; set; }
}

/// <summary>
/// Minimal generic base since k8s.CustomResource in the client library only
/// covers the metadata envelope, not spec/status typing ergonomics — this
/// keeps Reconciler.cs free of manual JSON munging.
/// </summary>
public abstract class CustomResource<TSpec, TStatus> : KubernetesObject
{
    public V1ObjectMeta Metadata { get; set; } = new();
    public TSpec Spec { get; set; } = default!;
    public TStatus? Status { get; set; }
}
