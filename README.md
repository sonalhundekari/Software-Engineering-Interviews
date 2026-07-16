# ml-infra-systems-portfolio

A small collection of production-style engineering projects alongside some interview preparation material, implemented in Python/C#. Built to learn informational level distributed systems, ML infrastructure, and operating systems fundamentals.

## Repo Structure

### `Projects/` — Flagship engineering work

Real, end-to-end systems built to demonstrate infrastructure and ML systems depth.

- **`distributed-training-orchestrator/`** — A Kubernetes-based orchestrator for distributed model training. Includes a custom C# controller, a TorchSharp-based worker, fault-tolerant checkpoint/resume logic, and a local `kind` cluster setup for end-to-end testing.
- **`fraud-detection-pipeline/`** — A real-time fraud detection pipeline combining Kafka for event streaming, TorchSharp for inference, Streamiz for stream processing, and a Blazor front end for monitoring/visualization.
- **`MiniOS/`** — A from-scratch mini operating system project covering memory paging, a simple filesystem implementation, and concurrency primitives, with an accompanying technical write-up.

### `Coding-Patterns/` — Algorithmic problem-solving reference

Organized by pattern covering the core categories tested in technical interviews:

| Folder | Focus |
|---|---|
| `01_arrays_strings` | Array/string manipulation, two pointers, sliding window |
| `02_trees` | Tree traversal, BST operations, recursion |
| `03_graphs` | BFS/DFS, topological sort, graph traversal |
| `04_dynamic_programming` | Classic DP patterns (knapsack, sequences, stock trading, etc.) |
| `05_intervals_greedy` | Interval merging, greedy scheduling problems |
| `06_stack_queue` | Stack/queue-based problems, monotonic stacks |
| `07_binary_search` | Binary search variants and search-space problems |
| `08_linked_lists` | Linked list manipulation and traversal patterns |


### `System-Design-CheatSheet/` — Classic system design breakdowns

25 concise write-ups covering canonical system design interview questions:

`url_shortener`, `rate_limiter`, `whatsapp`, `instagram`, `youtube_netflix`, `uber_lyft`, `twitter_feed`, `key_value_store`, `notification_system`, `google_drive`, `payment_system`, `web_crawler`, `distributed_cache`, `top_k_trending`, `autocomplete`, `amazon_ecommerce`, `flash_sale`, `movie_booking`, `auction_system`, `shopify_multitenant`, `message_queue`, `job_scheduler`, `cicd_pipeline`, `ad_click_aggregator`, `realtime_leaderboard`.

### `System-Design-DeepDive/` — Extended system design walkthroughs

A smaller set of more detailed designs that go beyond the cheat sheet versions:

- Google News System Design
- Ride-Sharing System Design
- Web Crawler System Design

## Tech Stack

C#, .NET, Kubernetes, TorchSharp, Kafka, Streamiz, Blazor

## About

This repo has a portfolio of hands-on infrastructure/ML systems projects and a structured reference for algorithm and system design interview preparation.
