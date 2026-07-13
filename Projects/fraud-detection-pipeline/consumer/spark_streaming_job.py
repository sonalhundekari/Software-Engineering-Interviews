"""
Spark Structured Streaming job:
  1. Reads raw transaction events from the 'transactions' Kafka topic.
  2. Computes a rolling per-account feature: count and average amount over a
     5-minute event-time window (velocity check -- a classic fraud signal
     independent of the model itself).
  3. Calls the FastAPI serving endpoint per micro-batch to get a model score.
  4. Writes enriched predictions to the 'predictions' Kafka topic.

Run:
    spark-submit \
      --packages org.apache.spark:spark-sql-kafka-0-10_2.12:3.5.0 \
      spark_streaming_job.py
"""
import json
import os

import requests
from pyspark.sql import SparkSession
from pyspark.sql.functions import (
    avg,
    col,
    count,
    from_json,
    struct,
    to_json,
    window,
)
from pyspark.sql.types import DoubleType, FloatType, StringType, StructField, StructType

KAFKA_BOOTSTRAP = os.environ.get("KAFKA_BOOTSTRAP_SERVERS", "localhost:9092")
IN_TOPIC = os.environ.get("TRANSACTIONS_TOPIC", "transactions")
OUT_TOPIC = os.environ.get("PREDICTIONS_TOPIC", "predictions")
SERVING_URL = os.environ.get("SERVING_URL", "http://localhost:8000/predict")

# Schema for the JSON payload produced by producer/transaction_producer.py
FEATURE_FIELDS = [StructField(f"V{i}", DoubleType()) for i in range(1, 29)]
TXN_SCHEMA = StructType(
    [
        StructField("transaction_id", StringType()),
        StructField("account_id", StringType()),
        StructField("timestamp", StringType()),
        StructField("amount", DoubleType()),
        StructField("merchant_category", StringType()),
        *FEATURE_FIELDS,
    ]
)


def score_partition(rows):
    """Calls the serving API for each row in a partition. In production this
    would batch requests and use connection pooling; kept simple here for
    readability."""
    for row in rows:
        payload = {
            "transaction_id": row["transaction_id"],
            "Amount": row["amount"],
            "features": {f"V{i}": row[f"V{i}"] for i in range(1, 29)},
        }
        try:
            resp = requests.post(SERVING_URL, json=payload, timeout=1.0)
            resp.raise_for_status()
            result = resp.json()
        except requests.RequestException as e:
            result = {"fraud_probability": None, "is_fraud": None, "error": str(e)}

        yield {
            "transaction_id": row["transaction_id"],
            "account_id": row["account_id"],
            "amount": row["amount"],
            "fraud_probability": result.get("fraud_probability"),
            "is_fraud": result.get("is_fraud"),
            "recent_txn_count": row["recent_txn_count"],
            "recent_avg_amount": row["recent_avg_amount"],
        }


def main():
    spark = (
        SparkSession.builder.appName("FraudDetectionStreaming")
        .config("spark.sql.shuffle.partitions", "4")
        .getOrCreate()
    )
    spark.sparkContext.setLogLevel("WARN")

    raw = (
        spark.readStream.format("kafka")
        .option("kafka.bootstrap.servers", KAFKA_BOOTSTRAP)
        .option("subscribe", IN_TOPIC)
        .option("startingOffsets", "latest")
        .load()
    )

    parsed = (
        raw.selectExpr("CAST(value AS STRING) as json_str")
        .select(from_json(col("json_str"), TXN_SCHEMA).alias("data"))
        .select("data.*")
        .withColumn("event_time", col("timestamp").cast("timestamp"))
    )

    # Velocity feature: rolling count + avg amount per account over a 5-min
    # event-time window with a 1-min watermark for late data.
    windowed = (
        parsed.withWatermark("event_time", "1 minute")
        .groupBy(col("account_id"), window(col("event_time"), "5 minutes"))
        .agg(
            count("*").alias("recent_txn_count"),
            avg("amount").alias("recent_avg_amount"),
        )
    )

    # Join windowed features back onto individual transactions for scoring.
    enriched = parsed.join(windowed, on="account_id", how="inner").select(
        parsed["transaction_id"],
        parsed["account_id"],
        parsed["amount"],
        *[parsed[f"V{i}"] for i in range(1, 29)],
        windowed["recent_txn_count"],
        windowed["recent_avg_amount"],
    )

    def process_batch(batch_df, batch_id):
        if batch_df.rdd.isEmpty():
            return
        scored = batch_df.rdd.mapPartitions(score_partition)
        scored_df = spark.createDataFrame(scored)
        (
            scored_df.select(to_json(struct("*")).alias("value"))
            .write.format("kafka")
            .option("kafka.bootstrap.servers", KAFKA_BOOTSTRAP)
            .option("topic", OUT_TOPIC)
            .save()
        )
        print(f"batch {batch_id}: scored {scored_df.count()} transactions")

    query = enriched.writeStream.foreachBatch(process_batch).outputMode("update").start()
    query.awaitTermination()


if __name__ == "__main__":
    main()
