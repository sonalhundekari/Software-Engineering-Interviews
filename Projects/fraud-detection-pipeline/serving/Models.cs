namespace FraudDetection.Serving;

public sealed class TransactionRequest
{
    public string TransactionId { get; set; } = "";
    public double Amount { get; set; }
    public double[] Features { get; set; } = Array.Empty<double>(); // V1..V28
}

public sealed class BatchRequest
{
    public List<TransactionRequest> Transactions { get; set; } = new();
}

public sealed class PredictionResponse
{
    public string TransactionId { get; set; } = "";
    public double FraudProbability { get; set; }
    public bool IsFraud { get; set; }
}
