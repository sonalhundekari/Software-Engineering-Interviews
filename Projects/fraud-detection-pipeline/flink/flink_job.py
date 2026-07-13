"""
PyFlink alternative to consumer/spark_streaming_job.py -- same logic
(rolling per-account velocity features + call out to the serving API),
implemented on Flink's DataStream API instead of Spark Structured Streaming.

Worth building both: interviewers care about *why* you'd pick one over the
other (true low-latency event-at-a-time processing with Flink vs. Spark's
micro-batch model), not just that you can use either.

Run:
    python flink_job.py
"""
import json
import os
from collections import defaultdict, deque

import requests
from pyflink.common import Types, WatermarkStrategy
from pyflink.common.serialization import SimpleStringSchema
from pyflink.datastream import StreamExecutionEnvironment
from pyflink.datastream.connectors.kafka import (
    KafkaSource,
    KafkaSink,
    KafkaOffsetsInitializer,
    KafkaRecordSerializationSchema,
)
from pyflink.datastream.functions import KeyedProcessFunction, RuntimeContext
from pyflink.datastream.state import ListStateDescriptor

KAFKA_BOOTSTRAP = os.environ.get("KAFKA_BOOTSTRAP_SERVERS", "localhost:9092")
IN_TOPIC = os.environ.get("TRANSACTIONS_TOPIC", "transactions")
OUT_TOPIC = os.environ.get("PREDICTIONS_TOPIC", "predictions")
SERVING_URL = os.environ.get("SERVING_URL", "http://localhost:8000/predict")

WINDOW_SECONDS = 300  # 5-minute rolling velocity window, mirrors the Spark job


class VelocityScorer(KeyedProcessFunction):
    """Keyed by account_id. Maintains a per-key rolling deque of recent
    (timestamp, amount) pairs in Flink managed state, computes velocity
    features, then calls the serving API and emits the scored result."""

    def open(self, runtime_context: RuntimeContext):
        descriptor = ListStateDescriptor("recent_txns", Types.STRING())
        self.recent_state = runtime_context.get_list_state(descriptor)

    def process_element(self, value, ctx):
        txn = json.loads(value)
        now = txn["_event_time_epoch"]

        history = [json.loads(x) for x in self.recent_state.get()] if self.recent_state.get() else []
        history = [h for h in history if now - h["t"] <= WINDOW_SECONDS]
        history.append({"t": now, "amount": txn["amount"]})

        self.recent_state.clear()
        for h in history:
            self.recent_state.add(json.dumps(h))

        recent_txn_count = len(history)
        recent_avg_amount = sum(h["amount"] for h in history) / recent_txn_count

        payload = {
            "transaction_id": txn["transaction_id"],
            "Amount": txn["amount"],
            "features": {f"V{i}": txn.get(f"V{i}", 0.0) for i in range(1, 29)},
        }
        try:
            resp = requests.post(SERVING_URL, json=payload, timeout=1.0)
            resp.raise_for_status()
            result = resp.json()
        except requests.RequestException as e:
            result = {"fraud_probability": None, "is_fraud": None, "error": str(e)}

        yield json.dumps(
            {
                "transaction_id": txn["transaction_id"],
                "account_id": txn["account_id"],
                "amount": txn["amount"],
                "fraud_probability": result.get("fraud_probability"),
                "is_fraud": result.get("is_fraud"),
                "recent_txn_count": recent_txn_count,
                "recent_avg_amount": recent_avg_amount,
            }
        )


def main():
    env = StreamExecutionEnvironment.get_execution_environment()
    env.set_parallelism(2)

    source = (
        KafkaSource.builder()
        .set_bootstrap_servers(KAFKA_BOOTSTRAP)
        .set_topics(IN_TOPIC)
        .set_group_id("flink-fraud-scorer")
        .set_starting_offsets(KafkaOffsetsInitializer.latest())
        .set_value_only_deserializer(SimpleStringSchema())
        .build()
    )

    sink = (
        KafkaSink.builder()
        .set_bootstrap_servers(KAFKA_BOOTSTRAP)
        .set_record_serializer(
            KafkaRecordSerializationSchema.builder()
            .set_topic(OUT_TOPIC)
            .set_value_serialization_schema(SimpleStringSchema())
            .build()
        )
        .build()
    )

    stream = env.from_source(source, WatermarkStrategy.no_watermarks(), "kafka-source")

    def add_epoch(raw_json: str) -> str:
        txn = json.loads(raw_json)
        from datetime import datetime

        txn["_event_time_epoch"] = datetime.fromisoformat(txn["timestamp"]).timestamp()
        return json.dumps(txn)

    keyed = (
        stream.map(add_epoch, output_type=Types.STRING())
        .key_by(lambda raw: json.loads(raw)["account_id"])
        .process(VelocityScorer(), output_type=Types.STRING())
    )

    keyed.sink_to(sink)
    env.execute("fraud-detection-flink-job")


if __name__ == "__main__":
    main()
