# Video Walkthrough Script

Target length: 4-6 minutes. Structure mirrors your other system-design
walkthroughs — problem framing, architecture, live demo, tradeoffs. Written
so you can feed section by section into ElevenLabs/OpenAI TTS, or read live
over a screen recording.

Suggested screen layout: terminal on one side (kubectl commands), the
Mermaid architecture diagram from the README on the other for the first
90 seconds, then full-screen terminal for the demo.

---

## 1. Framing (30s)

> "Most ML portfolio projects show off a model. This one shows off the
> platform underneath it — the part that decides where training runs,
> what happens when a worker dies mid-epoch, and how you get back to
> making progress without losing hours of compute. That's the Kubernetes
> Platform problem, not the machine learning problem, and it's the piece
> I've spent most of my career on."

## 2. Architecture (60-90s)

> "The core abstraction is a custom Kubernetes resource — a TrainingJob —
> that describes a training run declaratively: how many workers, how many
> epochs, how often to checkpoint. A controller I wrote in C# watches for
> these resources and reconciles cluster state to match: it creates one
> Kubernetes Job per worker rank, mounts a shared volume for checkpoints,
> and — this is the part worth paying attention to — if a worker Job
> fails out, the controller deletes and recreates it, and the worker
> resumes from its last checkpoint instead of retraining from scratch."

[Show architecture diagram here]

> "I deliberately didn't reimplement scheduling logic inside the
> controller. Each worker is just a plain Kubernetes Job — the
> controller composes primitives Kubernetes already gives you instead of
> fighting them. That's a design choice, not a shortcut, and it's the
> kind of tradeoff I'd want to talk through in an interview."

## 3. Live demo (2-3 min)

Talking points to say while running each command:

```bash
kubectl apply -f samples/sample-trainingjob.yaml
```
> "This submits a TrainingJob asking for 3 workers, 10 epochs. Watch the
> controller pick it up."

```bash
kubectl get trainingjobs -w
```
> "The status subresource updates in real time as workers report
> progress — phase, current epoch, per-rank checkpoint state."

```bash
kubectl get pods
```
> "Three worker pods, one per rank, training in parallel on their own
> shard of the dataset."

```bash
kubectl logs -f mnist-demo-worker-0-xxxxx
```
> "Here's rank 0 actually training — loss dropping epoch over epoch,
> checkpointing every epoch to the shared volume."

**The key moment — kill a worker on camera:**
```bash
kubectl delete pod -l trainingjob=mnist-demo,rank=1
```
> "I'm killing rank 1 mid-run right now, simulating a node failure or
> preemption — the kind of thing that happens constantly on shared
> clusters. Watch what happens."

```bash
kubectl get pods -w
```
> "The controller notices the Job failed, recreates it, and—"

```bash
kubectl logs -f mnist-demo-worker-1-yyyyy
```
> "—the new pod comes up, finds its last checkpoint, and resumes from
> epoch 6 instead of epoch 0. That's the whole point of this project:
> failure is routine at scale, and the platform needs to make it boring."

## 4. Tradeoffs and what's next (30-45s)

> "A few things I scoped out deliberately for v1: gang scheduling — right
> now workers start independently rather than all-or-nothing; elastic
> worker counts; and Prometheus metrics per rank. I'd treat all three as
> the natural next milestones, and I can talk through how I'd sequence
> them."

## Closing line

> "The bigger point: I didn't build this to prove I can train a model —
> I built it to show how I think about the infrastructure that makes
> training reliable at scale, which is the same muscle behind the AKS
> and Kubernetes work I've been doing at Microsoft."

---

## Production notes

- Record the demo terminal session first, unscripted, so timing of
  `kubectl get pods -w` output is real — don't fake the resume behavior.
- If using TTS: split into the 4 numbered sections above as separate
  generations, so a flubbed line doesn't force a full re-render.
- Keep the architecture diagram on screen only during section 2; full
  terminal for the demo keeps it feeling like a real walkthrough, not a
  slide deck.
