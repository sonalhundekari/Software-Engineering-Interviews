using System.Text.Json;

namespace FraudDetection.Model;

/// <summary>
/// Minimal StandardScaler equivalent (mean/std normalization per feature).
/// Serialized to JSON so the exact same normalization parameters computed
/// during training are reused by the serving API -- avoiding train/serve
/// skew, same purpose as scaler.pkl in the Python version.
/// </summary>
public sealed class FeatureScaler
{
    public double[] Mean { get; set; } = Array.Empty<double>();
    public double[] Std { get; set; } = Array.Empty<double>();

    public static FeatureScaler Fit(double[][] rows)
    {
        var nFeatures = rows[0].Length;
        var mean = new double[nFeatures];
        var std = new double[nFeatures];

        for (var j = 0; j < nFeatures; j++)
        {
            var col = rows.Select(r => r[j]).ToArray();
            mean[j] = col.Average();
            var variance = col.Select(v => Math.Pow(v - mean[j], 2)).Average();
            std[j] = Math.Sqrt(variance);
            if (std[j] < 1e-8) std[j] = 1.0; // guard against divide-by-zero on constant columns
        }

        return new FeatureScaler { Mean = mean, Std = std };
    }

    public double[] Transform(double[] row)
    {
        var result = new double[row.Length];
        for (var i = 0; i < row.Length; i++)
            result[i] = (row[i] - Mean[i]) / Std[i];
        return result;
    }

    public double[][] TransformBatch(double[][] rows) => rows.Select(Transform).ToArray();

    public void Save(string path) => File.WriteAllText(path, JsonSerializer.Serialize(this));

    public static FeatureScaler Load(string path) =>
        JsonSerializer.Deserialize<FeatureScaler>(File.ReadAllText(path))
        ?? throw new InvalidOperationException($"Could not load scaler from {path}");
}
