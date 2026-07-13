FROM python:3.11-slim

WORKDIR /app

COPY serving/requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY model/model.py /app/model/model.py
COPY model/fraud_model.pt /app/model/fraud_model.pt
COPY model/scaler.pkl /app/model/scaler.pkl
COPY serving/app.py .

EXPOSE 8000
CMD ["uvicorn", "app:app", "--host", "0.0.0.0", "--port", "8000"]
