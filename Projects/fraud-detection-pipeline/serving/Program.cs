using FraudDetection.Model;
using FraudDetection.Serving;
using TorchSharp;
using static TorchSharp.torch;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// --- Load model + scaler once at startup -----------------------------------
var modelPath = Environment.GetEnvironmentVariable("MODEL_PATH")
    ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Model", "bin", "Debug", "net8.0", "fraud_model.dat");
var scalerPath = Environment.GetEnvironmentVariable("SCALER_PATH")
    ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Model", "bin", "Debug", "net8.0", "scaler.json");

var model = new FraudClassifier();
model.load(modelPath);
model.eval();

var scaler = FeatureScaler.Load(scalerPath);

// --- Endpoints ---------------------------------------------------------------
app.MapGet("/health", () => Results.Ok(new { status = "ok", modelLoaded = true }));

app.MapPost("/predict", (TransactionRequest txn) =>
{
    var prob = Score(txn);
    return Results.Ok(new PredictionResponse
    {
        TransactionId = txn.TransactionId,
        FraudProbability = Math.Round(prob, 6),
        IsFraud = prob > 0.5,
    });
});

app.MapPost("/predict_batch", (BatchRequest req) =>
{
    if (req.Transactions.Count == 0)
        return Results.Ok(Array.Empty<PredictionResponse>());

    var results = req.Transactions.Select(txn =>
    {
        var prob = Score(txn);
        return new PredictionResponse
        {
            TransactionId = txn.TransactionId,
            FraudProbability = Math.Round(prob, 6),
            IsFraud = prob > 0.5,
        };
    });

    return Results.Ok(results);
});

app.Run();

double Score(TransactionRequest txn)
{
    var row = new double[29];
    Array.Copy(txn.Features, row, Math.Min(28, txn.Features.Length));
    row[28] = txn.Amount;

    var scaled = scaler.Transform(row);
    var input = tensor(scaled.Select(v => (float)v).ToArray(), dtype: ScalarType.Float32).reshape(1, 29);

    using var _ = torch.no_grad();
    var logit = model.forward(input);
    var prob = torch.sigmoid(logit).item<float>();
    return prob;
}
