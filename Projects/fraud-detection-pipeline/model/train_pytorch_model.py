"""
Offline training script. Expects data/creditcard.csv (Kaggle Credit Card
Fraud Detection dataset: https://www.kaggle.com/mlg-ulb/creditcardfraud).

Handles severe class imbalance (fraud is ~0.17% of the dataset) via a
weighted loss rather than naive oversampling, and saves both the model
weights and the feature scaler so the exact same normalization can be
applied in the online serving path.

Run:
    python train_pytorch_model.py --epochs 15
"""
import argparse
import pickle
from pathlib import Path

import numpy as np
import pandas as pd
import torch
import torch.nn as nn
from sklearn.metrics import average_precision_score, roc_auc_score
from sklearn.model_selection import train_test_split
from sklearn.preprocessing import StandardScaler
from torch.utils.data import DataLoader, TensorDataset

from model import FraudClassifier

DATA_PATH = Path(__file__).parent.parent / "data" / "creditcard.csv"
MODEL_OUT = Path(__file__).parent / "fraud_model.pt"
SCALER_OUT = Path(__file__).parent / "scaler.pkl"

FEATURE_COLS = [f"V{i}" for i in range(1, 29)] + ["Amount"]
LABEL_COL = "Class"


def load_data():
    if not DATA_PATH.exists():
        raise FileNotFoundError(
            f"{DATA_PATH} not found. Download creditcard.csv from Kaggle "
            "(mlg-ulb/creditcardfraud) and place it in data/."
        )
    df = pd.read_csv(DATA_PATH)
    X = df[FEATURE_COLS].values
    y = df[LABEL_COL].values
    return X, y


def train(epochs: int, lr: float, batch_size: int):
    X, y = load_data()
    X_train, X_val, y_train, y_val = train_test_split(
        X, y, test_size=0.2, stratify=y, random_state=42
    )

    scaler = StandardScaler().fit(X_train)
    X_train = scaler.transform(X_train)
    X_val = scaler.transform(X_val)

    train_ds = TensorDataset(
        torch.tensor(X_train, dtype=torch.float32),
        torch.tensor(y_train, dtype=torch.float32).unsqueeze(1),
    )
    train_loader = DataLoader(train_ds, batch_size=batch_size, shuffle=True)

    model = FraudClassifier(input_dim=X_train.shape[1])
    optimizer = torch.optim.Adam(model.parameters(), lr=lr)

    # Fraud is a tiny minority class -- weight positives heavily instead of
    # resampling, so the model sees the true data distribution.
    pos_weight = torch.tensor([(y_train == 0).sum() / max((y_train == 1).sum(), 1)])
    criterion = nn.BCEWithLogitsLoss(pos_weight=pos_weight)

    device = "cuda" if torch.cuda.is_available() else "cpu"
    model.to(device)

    for epoch in range(1, epochs + 1):
        model.train()
        total_loss = 0.0
        for xb, yb in train_loader:
            xb, yb = xb.to(device), yb.to(device)
            optimizer.zero_grad()
            logits = model(xb)
            loss = criterion(logits, yb)
            loss.backward()
            optimizer.step()
            total_loss += loss.item() * xb.size(0)

        model.eval()
        with torch.no_grad():
            val_logits = model(torch.tensor(X_val, dtype=torch.float32).to(device))
            val_probs = torch.sigmoid(val_logits).cpu().numpy().ravel()
            auc = roc_auc_score(y_val, val_probs)
            ap = average_precision_score(y_val, val_probs)  # more informative than AUC given imbalance

        print(
            f"epoch {epoch:02d}  train_loss={total_loss / len(train_ds):.4f}  "
            f"val_auc={auc:.4f}  val_avg_precision={ap:.4f}"
        )

    torch.save(model.state_dict(), MODEL_OUT)
    with open(SCALER_OUT, "wb") as f:
        pickle.dump(scaler, f)
    print(f"\nSaved model -> {MODEL_OUT}")
    print(f"Saved scaler -> {SCALER_OUT}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--epochs", type=int, default=15)
    parser.add_argument("--lr", type=float, default=1e-3)
    parser.add_argument("--batch-size", type=int, default=256)
    args = parser.parse_args()
    train(args.epochs, args.lr, args.batch_size)
