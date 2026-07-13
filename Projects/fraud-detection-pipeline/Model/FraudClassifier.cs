using TorchSharp;
using TorchSharp.Modules;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace FraudDetection.Model;

/// <summary>
/// Small feed-forward classifier, structurally identical to the PyTorch
/// version (model/model.py) so behavior transfers directly. Kept in its own
/// class so both TrainModel.cs and the serving API reference exactly one
/// definition -- avoids train/serve skew on the architecture itself.
/// </summary>
public sealed class FraudClassifier : Module<Tensor, Tensor>
{
    public const int InputDim = 29; // V1..V28 + Amount

    private readonly Sequential _net;

    public FraudClassifier(int inputDim = InputDim, int hiddenDim = 64) : base(nameof(FraudClassifier))
    {
        _net = Sequential(
            ("linear1", Linear(inputDim, hiddenDim)),
            ("relu1", ReLU()),
            ("dropout", Dropout(0.2)),
            ("linear2", Linear(hiddenDim, hiddenDim / 2)),
            ("relu2", ReLU()),
            ("linear3", Linear(hiddenDim / 2, 1))
        );

        RegisterComponents();
    }

    /// <summary>Returns raw logits; apply sigmoid outside for a probability.</summary>
    public override Tensor forward(Tensor x) => _net.forward(x);
}
