#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."

echo "==> Building controller image"
docker build -t training-orchestrator/controller:local -f deploy/Dockerfile.controller .

echo "==> Building worker image"
docker build -t training-orchestrator/worker:local -f deploy/Dockerfile.worker .

echo "==> Loading images into kind cluster"
kind load docker-image training-orchestrator/controller:local --name training-orchestrator-demo
kind load docker-image training-orchestrator/worker:local --name training-orchestrator-demo

echo "==> Images loaded. Next:"
echo "    kubectl apply -f crds/trainingjob-crd.yaml"
echo "    kubectl apply -f deploy/rbac.yaml"
echo "    kubectl apply -f deploy/controller-deployment.yaml"
