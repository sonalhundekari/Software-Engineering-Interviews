using TorchSharp;
using static TorchSharp.torch;

namespace TrainingOrchestrator.Worker;

/// <summary>
/// Handles checkpoint save/load for a single worker rank against the shared
/// PVC mounted at CHECKPOINT_DIR. Each rank writes its own file so ranks
/// never contend for a write lock; the controller/reconciler only needs to
/// know the highest epoch each rank has persisted.
///
/// File naming: {rank}/epoch-{N}.ts, plus a small manifest file so a resumed
/// worker doesn't need to list-and-parse the directory to find its latest
/// checkpoint.
/// </summary>
public class CheckpointManager
{
    private readonly string _checkpointDir;
    private readonly int _rank;

    public CheckpointManager(string checkpointDir, int rank)
    {
        _checkpointDir = checkpointDir;
        _rank = rank;
        Directory.CreateDirectory(RankDir);
    }

    private string RankDir => Path.Combine(_checkpointDir, $"rank-{_rank}");
    private string ManifestPath => Path.Combine(RankDir, "manifest.txt");

    public void Save(nn.Module model, optim.Optimizer optimizer, int epoch)
    {
        var modelPath = Path.Combine(RankDir, $"epoch-{epoch}.model.ts");
        var optimPath = Path.Combine(RankDir, $"epoch-{epoch}.optim.ts");

        model.save(modelPath);
        optimizer.save_state_dict(optimPath);

        // Manifest write is last and atomic-ish (single small file) so a
        // crash mid-save never leaves the manifest pointing at a partial
        // checkpoint file.
        File.WriteAllText(ManifestPath, epoch.ToString());

        Console.WriteLine($"[rank {_rank}] checkpoint saved: epoch {epoch}");
    }

    /// <summary>
    /// Returns the epoch to resume from (0 if no checkpoint exists), having
    /// already loaded model/optimizer state in place if one was found.
    /// </summary>
    public int TryResume(nn.Module model, optim.Optimizer optimizer)
    {
        if (!File.Exists(ManifestPath))
        {
            Console.WriteLine($"[rank {_rank}] no checkpoint found, starting from epoch 0");
            return 0;
        }

        var lastEpoch = int.Parse(File.ReadAllText(ManifestPath).Trim());
        var modelPath = Path.Combine(RankDir, $"epoch-{lastEpoch}.model.ts");
        var optimPath = Path.Combine(RankDir, $"epoch-{lastEpoch}.optim.ts");

        if (!File.Exists(modelPath) || !File.Exists(optimPath))
        {
            Console.WriteLine(
                $"[rank {_rank}] manifest points to epoch {lastEpoch} but files are missing — starting fresh");
            return 0;
        }

        model.load(modelPath);
        optimizer.load_state_dict(optimPath);
        Console.WriteLine($"[rank {_rank}] resumed from checkpoint at epoch {lastEpoch}");
        return lastEpoch;
    }
}
