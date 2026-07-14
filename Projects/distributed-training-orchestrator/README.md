# Distributed Training Orchestrator

A Kubernetes-native controller (C#) that schedules, checkpoints, and resumes
distributed TorchSharp training jobs. Built to demonstrate ML **infrastructure**
engineering — scheduling, fault tolerance, resource isolation — rather than
model quality itself.

Runs entirely on your laptop via `kind` (Kubernetes-in-Docker). No cloud
account needed to develop or record a demo; the same manifests deploy to AKS
unchanged (just swap the image registry).

## Why this project exists

Most "ML portfolio" projects show off a model. This one shows off the platform
underneath it: a custom resource (`TrainingJob`), a reconciliation loop that
watches for changes and drives cluster state toward the desired spec, worker
pods that checkpoint to a shared volume, and automatic resume-from-checkpoint
on pod failure. That's the muscle that transfers directly to Kubernetes
Platform / infra-adjacent Staff roles.

## Architecture

```
                 ┌─────────────────────────┐
   kubectl apply │   TrainingJob (CRD)      │
   -f sample.yaml│   spec: epochs, workers, │
                 │   checkpointInterval     │
                 └────────────┬─────────────┘
                              │ watch
                              ▼
                 ┌─────────────────────────┐
                 │  Controller (C#)         │
                 │  - Reconciler.cs         │
                 │  - creates/patches       │
                 │    K8s Jobs per worker   │
                 │  - watches Job status    │
                 │  - updates CR .status    │
                 └────────────┬─────────────┘
                              │ creates
                              ▼
        ┌───────────────┐ ┌───────────────┐
        │ Worker Pod 0  │ │ Worker Pod N  │   (K8s Job, restartPolicy=OnFailure)
        │ TorchSharp    │ │ TorchSharp    │
        │ training loop │ │ training loop │
        └───────┬───────┘ └───────┬───────┘
                │                 │
                ▼                 ▼
        ┌─────────────────────────────┐
        │  Shared PVC: /checkpoints    │
        │  epoch-N.ts per worker rank  │
        └─────────────────────────────┘
```

Each worker is a plain Kubernetes `Job` (rank baked in via env var), so the
controller doesn't reinvent scheduling — it composes primitives Kubernetes
already gives you. That's a deliberate design choice worth calling out in an
interview: prefer composing built-in primitives (Jobs, PVCs, ConfigMaps) over
reimplementing them in the CRD controller.

## Repo layout

```
crds/trainingjob-crd.yaml          CustomResourceDefinition
samples/sample-trainingjob.yaml    Example TrainingJob CR
src/TrainingOrchestrator.Controller/  Reconciliation controller (.NET 8)
src/TrainingOrchestrator.Worker/      TorchSharp training worker (.NET 8)
deploy/                            Dockerfiles + kind cluster config
scripts/                           One-shot setup scripts
docs/VIDEO_SCRIPT.md               Narration script for a walkthrough video
```

## Quickstart (local)

Prerequisites: Docker Desktop, `kind`, `kubectl`, .NET 8 SDK.

```bash
# 1. Spin up a local 3-node cluster
./scripts/setup-local-cluster.sh

# 2. Build and load the controller + worker images into kind
./scripts/build-and-load-images.sh

# 3. Install the CRD and RBAC
kubectl apply -f crds/trainingjob-crd.yaml
kubectl apply -f deploy/rbac.yaml

# 4. Deploy the controller
kubectl apply -f deploy/controller-deployment.yaml

# 5. Submit a training job
kubectl apply -f samples/sample-trainingjob.yaml

# 6. Watch it work
kubectl get trainingjobs -w
kubectl get pods -w
kubectl logs -f deploy/training-orchestrator-controller

# 7. Kill a worker mid-run to prove resume-from-checkpoint works
kubectl delete pod -l trainingjob=mnist-demo,rank=0
# ...watch the controller recreate it and resume from the last checkpoint
```

## What to show in the demo video

See `docs/VIDEO_SCRIPT.md` for a full narration script. Short version:
submit a job, show workers training in parallel, kill a worker pod on
camera, show it resume from checkpoint instead of restarting from epoch 0,
then show the CR status reflecting real-time progress via `kubectl get
trainingjob -o yaml`.

## Status of this scaffold

This is a first working slice, not a finished platform. Deliberately out of
scope for v1 (call these out as "future work" in interviews — it shows you
know the difference between a demo and a production system):

- Gang scheduling / all-or-nothing worker admission
- Elastic worker count (currently fixed at job submission)
- Multi-tenant resource quotas and priority classes
- Metrics export (Prometheus) for loss/throughput per rank
- A real distributed data loader (this demo shards MNIST by rank, not a
  general-purpose sampler)
