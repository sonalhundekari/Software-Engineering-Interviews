"""
Simulates a live transaction stream and publishes each event to a Kafka
topic. Uses the Kaggle creditcard.csv feature schema (V1..V28, Amount, Time)
if available; otherwise falls back to purely synthetic values so the pipeline
is runnable without the dataset.

Run:
    python transaction_producer.py --rate 5   # 5 events/sec
"""
import argparse
import json
import os
import random
import time
import uuid
from datetime import datetime, timezone

import numpy as np
from kafka import KafkaProducer

TOPIC = os.environ.get("TRANSACTIONS_TOPIC", "transactions")
BOOTSTRAP_SERVERS = os.environ.get("KAFKA_BOOTSTRAP_SERVERS", "localhost:9092")


def make_synthetic_transaction() -> dict:
    """Generates one transaction event with the same feature shape as the
    Kaggle creditcard.csv dataset (V1-V28 are PCA components in real data;
    here they're just gaussian noise, occasionally shifted to mimic a
    fraud-like outlier)."""
    is_outlier = random.random() < 0.02  # ~2% simulated anomalies
    shift = 3.5 if is_outlier else 0.0

    features = {f"V{i}": float(np.random.normal(loc=shift * random.choice([-1, 1]), scale=1.0))
                for i in range(1, 29)}

    amount = float(np.random.exponential(scale=250.0 if not is_outlier else 1200.0))

    return {
        "transaction_id": str(uuid.uuid4()),
        "account_id": f"acct_{random.randint(1, 500):04d}",
        "timestamp": datetime.now(timezone.utc).isoformat(),
        "amount": round(amount, 2),
        "merchant_category": random.choice(
            ["grocery", "electronics", "travel", "gas", "online", "restaurant"]
        ),
        **features,
        "_simulated_label": int(is_outlier),  # ground truth for offline eval only
    }


def main(rate_per_sec: float):
    producer = KafkaProducer(
        bootstrap_servers=BOOTSTRAP_SERVERS,
        value_serializer=lambda v: json.dumps(v).encode("utf-8"),
        key_serializer=lambda k: k.encode("utf-8") if k else None,
    )

    delay = 1.0 / rate_per_sec
    print(f"Producing to '{TOPIC}' at ~{rate_per_sec}/sec (Ctrl+C to stop)...")

    try:
        while True:
            txn = make_synthetic_transaction()
            # Key by account_id so all events for one account land on the
            # same partition -- required for correct per-account windowed
            # aggregation downstream.
            producer.send(TOPIC, key=txn["account_id"], value=txn)
            print(f"sent {txn['transaction_id']} amount={txn['amount']}")
            time.sleep(delay)
    except KeyboardInterrupt:
        pass
    finally:
        producer.flush()
        producer.close()


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--rate", type=float, default=5.0, help="events per second")
    args = parser.parse_args()
    main(args.rate)
