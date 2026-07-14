#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."

echo "==> Creating kind cluster (2 workers + control plane)"
kind create cluster --config deploy/kind-config.yaml

echo "==> Waiting for nodes to be Ready"
kubectl wait --for=condition=Ready nodes --all --timeout=120s

echo "==> Creating local-path PVCs for dataset and checkpoints"
cat <<'EOF' | kubectl apply -f -
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: mnist-dataset-pvc
spec:
  accessModes: ["ReadWriteOnce"]
  resources:
    requests:
      storage: 200Mi
---
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: mnist-checkpoints-pvc
spec:
  accessModes: ["ReadWriteOnce"]
  resources:
    requests:
      storage: 500Mi
EOF

echo "==> Cluster ready. Next: ./scripts/build-and-load-images.sh"
