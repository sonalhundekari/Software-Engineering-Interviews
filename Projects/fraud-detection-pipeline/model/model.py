"""
Model architecture shared by the training script and the serving API.
Keeping this in one file (imported by both) avoids train/serve skew on the
model definition itself.
"""
import torch
import torch.nn as nn

# V1..V28 + Amount = 29 input features
INPUT_DIM = 29


class FraudClassifier(nn.Module):
    """Small feed-forward classifier. Deliberately simple -- the point of
    this project is the surrounding infrastructure, not model complexity."""

    def __init__(self, input_dim: int = INPUT_DIM, hidden_dim: int = 64):
        super().__init__()
        self.net = nn.Sequential(
            nn.Linear(input_dim, hidden_dim),
            nn.ReLU(),
            nn.Dropout(0.2),
            nn.Linear(hidden_dim, hidden_dim // 2),
            nn.ReLU(),
            nn.Linear(hidden_dim // 2, 1),
        )

    def forward(self, x: torch.Tensor) -> torch.Tensor:
        return self.net(x)  # raw logits; apply sigmoid outside for probability
