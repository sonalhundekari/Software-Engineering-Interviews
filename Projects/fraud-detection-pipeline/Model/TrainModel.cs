using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using TorchSharp;
using static TorchSharp.torch;

namespace FraudDetection.Model;

/// <summary>
/// Offline training entry point. Expects data/creditcard.csv (Kaggle Credit
/// Card Fraud Detection dataset). Handles severe class imbalance via a
/// weighted loss (fraud is ~0.17% of rows) and saves both model weights and
/// the feature scaler, mirroring model/train_pytorch_model.py.
///
/// Run:
///     dotnet run --project Model -- --epochs 15
/// </summary>
public static class TrainModel
{
    private static readonly string DataPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "data", "creditcard.csv");
    private static readonly string ModelOut = Path.Combine(AppContext.BaseDirectory, "fraud_model.dat");
    private static readonly string ScalerOut = Path.Combine(AppContext.BaseDirectory, "scaler.json");

    public static void Main(string[] args)
    {
        var epochs = GetArg(args, "--epochs", 15);
        var lr = GetArg(args, "--lr", 1e-3);
        var batchSize = GetArg(args, "--batch-size", 256);

        Console.WriteLine("Loading data...");
        var (features, labels) = LoadData();

        var (trainX, trainY, valX, valY) = TrainTestSplit(features, labels, testFraction: 0.2, seed: 42);

        Console.WriteLine("Fitting scaler...");
        var scaler = FeatureScaler.Fit(trainX);
        var trainXScaled = scaler.TransformBatch(trainX);
        var valXScaled = scaler.TransformBatch(valX);

        var model = new FraudClassifier();
        var optimizer = torch.optim.Adam(model.parameters(), lr: lr);

        // Weight positives heavily instead of resampling, so the model sees
        // the true data distribution -- same approach as the Python version.
        var posCount = Math.Max(trainY.Count(y => y == 1), 1);
        var negCount = trainY.Count(y => y == 0);
        var posWeight = tensor((float)negCount / posCount);
        var criterion = torch.nn.BCEWithLogitsLoss(pos_weight: posWeight);

        var trainXTensor = tensor(Flatten(trainXScaled), dtype: ScalarType.Float32).reshape(trainXScaled.Length, trainXScaled[0].Length);
        var trainYTensor = tensor(trainY.Select(y => (float)y).ToArray(), dtype: ScalarType.Float32).reshape(trainY.Length, 1);
        var valXTensor = tensor(Flatten(valXScaled), dtype: ScalarType.Float32).reshape(valXScaled.Length, valXScaled[0].Length);

        var nSamples = trainXTensor.shape[0];

        for (var epoch = 1; epoch <= epochs; epoch++)
        {
            model.train();
            var permutation = torch.randperm(nSamples);
            double totalLoss = 0;

            for (long start = 0; start < nSamples; start += batchSize)
            {
                var end = Math.Min(start + batchSize, nSamples);
                var idx = permutation[start..end];
                var xb = trainXTensor.index_select(0, idx);
                var yb = trainYTensor.index_select(0, idx);

                optimizer.zero_grad();
                var logits = model.forward(xb);
                var loss = criterion.forward(logits, yb);
                loss.backward();
                optimizer.step();

                totalLoss += loss.item<float>() * (end - start);
            }

            model.eval();
            using (torch.no_grad())
            {
                var valLogits = model.forward(valXTensor);
                var valProbs = torch.sigmoid(valLogits);
                var auc = ApproxRocAuc(valProbs.data<float>().ToArray(), valY);
                Console.WriteLine($"epoch {epoch:D2}  train_loss={totalLoss / nSamples:F4}  val_auc_approx={auc:F4}");
            }
        }

        model.save(ModelOut);
        scaler.Save(ScalerOut);
        Console.WriteLine($"\nSaved model -> {ModelOut}");
        Console.WriteLine($"Saved scaler -> {ScalerOut}");
    }

    private static (double[][] Features, int[] Labels) LoadData()
    {
        if (!File.Exists(DataPath))
            throw new FileNotFoundException(
                $"{DataPath} not found. Download creditcard.csv from Kaggle (mlg-ulb/creditcardfraud) and place it in data/.");

        using var reader = new StreamReader(DataPath);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture));
        csv.Read();
        csv.ReadHeader();

        var featuresList = new List<double[]>();
        var labelsList = new List<int>();

        while (csv.Read())
        {
            var row = new double[29];
            for (var i = 1; i <= 28; i++)
                row[i - 1] = csv.GetField<double>($"V{i}");
            row[28] = csv.GetField<double>("Amount");

            featuresList.Add(row);
            labelsList.Add(csv.GetField<int>("Class"));
        }

        return (featuresList.ToArray(), labelsList.ToArray());
    }

    private static (double[][], int[], double[][], int[]) TrainTestSplit(
        double[][] features, int[] labels, double testFraction, int seed)
    {
        var random = new Random(seed);
        var indices = Enumerable.Range(0, features.Length).OrderBy(_ => random.Next()).ToArray();
        var testSize = (int)(features.Length * testFraction);

        var testIdx = indices[..testSize];
        var trainIdx = indices[testSize..];

        return (
            trainIdx.Select(i => features[i]).ToArray(),
            trainIdx.Select(i => labels[i]).ToArray(),
            testIdx.Select(i => features[i]).ToArray(),
            testIdx.Select(i => labels[i]).ToArray()
        );
    }

    private static double ApproxRocAuc(float[] scores, int[] labels)
    {
        // Simple rank-based AUC approximation (Mann-Whitney U) -- good enough
        // for training-loop monitoring; not a substitute for sklearn's exact
        // implementation if you need a reportable metric.
        var positives = scores.Where((_, i) => labels[i] == 1).ToArray();
        var negatives = scores.Where((_, i) => labels[i] == 0).ToArray();
        if (positives.Length == 0 || negatives.Length == 0) return double.NaN;

        long concordant = 0;
        foreach (var p in positives)
            foreach (var n in negatives)
                if (p > n) concordant++;
                else if (p == n) concordant += 1; // count ties as 0.5 each side; approximated as 1 total below

        return (double)concordant / (positives.Length * (long)negatives.Length);
    }

    private static float[] Flatten(double[][] matrix) =>
        matrix.SelectMany(row => row.Select(v => (float)v)).ToArray();

    private static int GetArg(string[] args, string name, int fallback)
    {
        var idx = Array.IndexOf(args, name);
        return idx >= 0 && idx + 1 < args.Length && int.TryParse(args[idx + 1], out var v) ? v : fallback;
    }

    private static double GetArg(string[] args, string name, double fallback)
    {
        var idx = Array.IndexOf(args, name);
        return idx >= 0 && idx + 1 < args.Length && double.TryParse(args[idx + 1], out var v) ? v : fallback;
    }
}
