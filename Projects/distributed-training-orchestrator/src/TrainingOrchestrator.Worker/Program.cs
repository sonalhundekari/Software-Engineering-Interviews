using TorchSharp;
using static TorchSharp.torch;
using TrainingOrchestrator.Worker;

// Config comes entirely from env vars set by the controller's Job spec —
// keeps the worker image dumb and stateless; all orchestration decisions
// live in the controller, not baked into the container.
int worldSize = int.Parse(Environment.GetEnvironmentVariable("WORLD_SIZE") ?? "1");
int rank = int.Parse(Environment.GetEnvironmentVariable("RANK") ?? "0");
int epochs = int.Parse(Environment.GetEnvironmentVariable("EPOCHS") ?? "5");
int checkpointInterval = int.Parse(Environment.GetEnvironmentVariable("CHECKPOINT_INTERVAL") ?? "1");
string checkpointDir = Environment.GetEnvironmentVariable("CHECKPOINT_DIR") ?? "/checkpoints";
string dataDir = Environment.GetEnvironmentVariable("DATA_DIR") ?? "/data";

Console.WriteLine($"[rank {rank}/{worldSize}] starting worker. epochs={epochs}, checkpointEvery={checkpointInterval}");

var device = cuda.is_available() ? CUDA : CPU;
Console.WriteLine($"[rank {rank}] using device: {device.type}");

var model = new MnistClassifier("mnist_classifier").to(device);
var optimizer = optim.Adam(model.parameters(), lr: 1e-3);
var checkpointManager = new CheckpointManager(checkpointDir, rank);

// Resume-from-checkpoint: this is the behavior the demo video shows off.
// A worker that gets killed and rescheduled by the controller lands right
// back here and picks up where it left off instead of retraining from
// scratch.
int startEpoch = checkpointManager.TryResume(model, optimizer) + 1;

var (trainX, trainY) = MnistData.LoadShard(dataDir, rank, worldSize);
Console.WriteLine($"[rank {rank}] loaded shard: {trainX.shape[0]} examples (of {worldSize} total shards)");

for (int epoch = startEpoch; epoch <= epochs; epoch++)
{
    model.train();
    double epochLoss = 0;
    int batches = 0;

    foreach (var (batchX, batchY) in MnistData.Batches(trainX, trainY, batchSize: 64, device: device))
    {
        optimizer.zero_grad();
        var predictions = model.forward(batchX);
        var loss = nn.functional.cross_entropy(predictions, batchY);
        loss.backward();
        optimizer.step();

        epochLoss += loss.item<float>();
        batches++;
    }

    var avgLoss = batches == 0 ? 0 : epochLoss / batches;
    Console.WriteLine($"[rank {rank}] epoch {epoch}/{epochs} — avg loss {avgLoss:F4}");

    if (epoch % checkpointInterval == 0 || epoch == epochs)
    {
        checkpointManager.Save(model, optimizer, epoch);
    }
}

Console.WriteLine($"[rank {rank}] training complete.");

/// <summary>
/// Small CNN for MNIST-scale demo data. The point of this project isn't
/// model architecture, so this is intentionally minimal — swap in anything
/// TorchSharp-compatible without touching the orchestration layer.
/// </summary>
class MnistClassifier : nn.Module<Tensor, Tensor>
{
    private readonly nn.Module<Tensor, Tensor> _conv1;
    private readonly nn.Module<Tensor, Tensor> _conv2;
    private readonly nn.Module<Tensor, Tensor> _fc1;
    private readonly nn.Module<Tensor, Tensor> _fc2;
    private readonly nn.Module<Tensor, Tensor> _pool;
    private readonly nn.Module<Tensor, Tensor> _relu;
    private readonly nn.Module<Tensor, Tensor> _flatten;

    public MnistClassifier(string name) : base(name)
    {
        _conv1 = nn.Conv2d(1, 16, kernelSize: 3, padding: 1);
        _conv2 = nn.Conv2d(16, 32, kernelSize: 3, padding: 1);
        _pool = nn.MaxPool2d(kernelSize: 2);
        _relu = nn.ReLU();
        _flatten = nn.Flatten();
        _fc1 = nn.Linear(32 * 7 * 7, 128);
        _fc2 = nn.Linear(128, 10);

        RegisterComponents();
    }

    public override Tensor forward(Tensor input)
    {
        var x = _pool.forward(_relu.forward(_conv1.forward(input)));
        x = _pool.forward(_relu.forward(_conv2.forward(x)));
        x = _flatten.forward(x);
        x = _relu.forward(_fc1.forward(x));
        return _fc2.forward(x);
    }
}

/// <summary>
/// Loads MNIST (expects the standard idx-ubyte files under DATA_DIR) and
/// shards it by rank — a simple contiguous split by index modulo world
/// size. Documented in the README as a "not a real distributed sampler"
/// simplification; a production version would shuffle-then-shard per epoch.
/// </summary>
static class MnistData
{
    public static (Tensor images, Tensor labels) LoadShard(string dataDir, int rank, int worldSize)
    {
        var allImages = torch.zeros(new long[] { 60000, 1, 28, 28 });
        var allLabels = torch.zeros(new long[] { 60000 }, dtype: ScalarType.Int64);
        // NOTE: replace with real idx-ubyte parsing against files in dataDir;
        // omitted here to keep this scaffold dependency-free. See
        // docs/VIDEO_SCRIPT.md for the "point at the real loader" callout.

        var indices = Enumerable.Range(0, 60000).Where(i => i % worldSize == rank).ToArray();
        var idxTensor = torch.tensor(indices, dtype: ScalarType.Int64);
        return (allImages.index_select(0, idxTensor), allLabels.index_select(0, idxTensor));
    }

    public static IEnumerable<(Tensor x, Tensor y)> Batches(Tensor x, Tensor y, int batchSize, Device device)
    {
        var total = x.shape[0];
        for (long i = 0; i < total; i += batchSize)
        {
            var end = Math.Min(i + batchSize, total);
            var idx = torch.arange(i, end, dtype: ScalarType.Int64);
            yield return (x.index_select(0, idx).to(device), y.index_select(0, idx).to(device));
        }
    }
}
