"""
Live dashboard: consumes the 'predictions' Kafka topic and shows a
continuously updating table + fraud-rate chart. This is what makes the
project demoable in an interview instead of just readable as code.

Run:
    streamlit run streamlit_app.py
"""
import json
import os
from collections import deque

import pandas as pd
import streamlit as st
from kafka import KafkaConsumer

KAFKA_BOOTSTRAP = os.environ.get("KAFKA_BOOTSTRAP_SERVERS", "localhost:9092")
PREDICTIONS_TOPIC = os.environ.get("PREDICTIONS_TOPIC", "predictions")
MAX_ROWS = 200

st.set_page_config(page_title="Fraud Detection — Live", layout="wide")
st.title("Real-Time Fraud Detection Dashboard")
st.caption(f"Consuming '{PREDICTIONS_TOPIC}' from {KAFKA_BOOTSTRAP}")

if "buffer" not in st.session_state:
    st.session_state.buffer = deque(maxlen=MAX_ROWS)

@st.cache_resource
def get_consumer():
    return KafkaConsumer(
        PREDICTIONS_TOPIC,
        bootstrap_servers=KAFKA_BOOTSTRAP,
        auto_offset_reset="latest",
        value_deserializer=lambda v: json.loads(v.decode("utf-8")),
        consumer_timeout_ms=500,
    )

consumer = get_consumer()

placeholder = st.empty()

for message in consumer:
    st.session_state.buffer.append(message.value)

df = pd.DataFrame(list(st.session_state.buffer))

with placeholder.container():
    col1, col2, col3 = st.columns(3)
    if not df.empty:
        col1.metric("Transactions seen", len(df))
        col2.metric("Flagged as fraud", int(df["is_fraud"].fillna(False).sum()))
        col3.metric(
            "Avg fraud probability",
            f"{df['fraud_probability'].dropna().mean():.3f}" if df["fraud_probability"].notna().any() else "n/a",
        )

        st.subheader("Recent transactions")
        st.dataframe(
            df[["transaction_id", "account_id", "amount", "fraud_probability", "is_fraud",
                "recent_txn_count", "recent_avg_amount"]].iloc[::-1],
            use_container_width=True,
        )

        st.subheader("Fraud probability over time")
        st.line_chart(df["fraud_probability"])
    else:
        col1.metric("Transactions seen", 0)
        st.info("Waiting for predictions... make sure the producer and streaming job are running.")

st.button("Refresh")
