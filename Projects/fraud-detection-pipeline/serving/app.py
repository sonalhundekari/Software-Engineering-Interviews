"""
Model-serving microservice. Loads the PyTorch model + scaler trained by
model/train_pytorch_model.py and exposes a low-latency /predict endpoint.

This is what the Spark/Flink streaming job (or a direct HTTP client) calls
per-transaction or per-micro-batch to get a fraud score.

Run:
    uvicorn app:app --host 0.0.0.0 --port 8000
"""
import pickle
import sys
from pathlib import Path
from typing import List

import numpy as np
import torch
from fastapi import FastAPI
from pydantic import BaseModel, Field

sys.path.append(str(Path(__file__).parent.parent / "model"))
from model import FraudClassifier  # noqa: E402

MODEL_PATH = Path(__file__).parent.parent / "model" / "fraud_model.pt"
SCALER_PATH = Path(__file__).parent.parent / "model" / "scaler.pkl"
FEATURE_ORDER = [f"V{i}" for i in range(1, 29)] + ["Amount"]

app = FastAPI(title="Fraud Detection Serving API")

_model: FraudClassifier | None = None
_scaler = None


@app.on_event("startup")
def load_artifacts():
    global _model, _scaler
    _model = FraudClassifier(input_dim=len(FEATURE_ORDER))
    _model.load_state_dict(torch.load(MODEL_PATH, map_location="cpu"))
    _model.eval()
    with open(SCALER_PATH, "rb") as f:
        _scaler = pickle.load(f)


class Transaction(BaseModel):
    transaction_id: str
    amount: float = Field(..., alias="Amount")
    features: dict = Field(..., description="V1..V28 PCA feature values")

    class Config:
        populate_by_name = True


class PredictionResponse(BaseModel):
    transaction_id: str
    fraud_probability: float
    is_fraud: bool


class BatchRequest(BaseModel):
    transactions: List[Transaction]


def _vectorize(txn: Transaction) -> np.ndarray:
    row = [txn.features.get(f"V{i}", 0.0) for i in range(1, 29)] + [txn.amount]
    return np.array(row, dtype=np.float32).reshape(1, -1)


@app.post("/predict", response_model=PredictionResponse)
def predict(txn: Transaction):
    x = _scaler.transform(_vectorize(txn))
    with torch.no_grad():
        logit = _model(torch.tensor(x, dtype=torch.float32))
        prob = torch.sigmoid(logit).item()
    return PredictionResponse(
        transaction_id=txn.transaction_id,
        fraud_probability=round(prob, 6),
        is_fraud=prob > 0.5,
    )


@app.post("/predict_batch", response_model=List[PredictionResponse])
def predict_batch(req: BatchRequest):
    if not req.transactions:
        return []
    X = np.vstack([_vectorize(t) for t in req.transactions])
    X = _scaler.transform(X)
    with torch.no_grad():
        logits = _model(torch.tensor(X, dtype=torch.float32))
        probs = torch.sigmoid(logits).numpy().ravel()
    return [
        PredictionResponse(
            transaction_id=t.transaction_id,
            fraud_probability=round(float(p), 6),
            is_fraud=bool(p > 0.5),
        )
        for t, p in zip(req.transactions, probs)
    ]


@app.get("/health")
def health():
    return {"status": "ok", "model_loaded": _model is not None}
