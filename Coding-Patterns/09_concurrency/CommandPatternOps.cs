// Command Pattern: apply a list of operations to an initial state.
/*
Given a list of operations and an initial state, apply all operations to derive the final state. Recognising and using the Command pattern is the explicit signal.

Requirements

Input: an initial state and a list of operations to apply.
Output: the final state after all operations are applied in order.
The interviewer flags that the round is intentionally not a traditional LeetCode problem — it is testing whether the candidate organises the implementation around the Command pattern (encapsulating each operation as a self-contained object with an apply(state) method).
Follow-up: identify cases where operations do not need to be applied sequentially — operations that commute can be reordered or batched.
Notes

Core skeleton:
interface Operation {
    State apply(State current);
    // Optionally: boolean commutesWith(Operation other);
    // Optionally: Optional<Operation> mergeWith(Operation other);
}
Each concrete operation (e.g. AddItemOp, RemoveItemOp, UpdateMetadataOp) implements apply and exposes its inputs as fields. The driver is a tight loop: for (op : operations) state = op.apply(state);
Optimisation follow-up: introduce a commutesWith (or domain-specific equivalent) that allows the driver to reorder pure-add operations ahead of pure-remove operations, or to merge two consecutive Updates on the same key.
Real-world parallel: this is the same shape as Redux reducers, event-sourced state machines, or operational transformation. Mentioning one of these analogies signals senior understanding without name-dropping unnecessary jargon.
Common over-engineering trap: pre-defining undo() / redo() before they're requested. Skip — the round is about composing forward application, not maintaining an undo stack.
Common under-engineering trap: writing one giant applyAll(ops) function with a switch on operation type. The whole point of this round is moving the switch into per-operation polymorphism.
Preparation

For the optimisation follow-up, list of 5-6 operations on a sample state and identify which pairs commute by hand. This builds the intuition for what commutesWith should encode.
Compare against a non-Command alternative (e.g. event sourcing, reducers) and articulate why polymorphism beats the giant switch.
*/
// Interview framing: the switch-on-type lives *inside* each operation via
// polymorphism, not in one giant applyAll(). Follow-up adds CommutesWith /
// TryMergeWith so the driver can reorder or batch operations.
//
// dotnet run — Program.Main at the bottom demonstrates everything.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace CommandPatternDemo
{
    // ---------------------------------------------------------------------
    // State: immutable so Apply returns a new state (Redux-reducer shape).
    // Items = a set of item ids; Metadata = key/value map.
    // ---------------------------------------------------------------------
    public sealed record State(
        ImmutableHashSet<string> Items,
        ImmutableDictionary<string, string> Metadata)
    {
        public static State Empty => new(
            ImmutableHashSet<string>.Empty,
            ImmutableDictionary<string, string>.Empty);

        public override string ToString() =>
            $"Items=[{string.Join(", ", Items.OrderBy(x => x))}] " +
            $"Meta={{{string.Join(", ", Metadata.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"))}}}";
    }

    // ---------------------------------------------------------------------
    // The Command interface. Apply is the whole contract; the two optional
    // members exist only for the optimization follow-up.
    // ---------------------------------------------------------------------
    public interface IOperation
    {
        State Apply(State current);

        // Follow-up 1: can this op be swapped with `other` without changing
        // the final state? Conservative default: false (never wrong, just
        // misses optimizations).
        bool CommutesWith(IOperation other) => false;

        // Follow-up 2: can this op and the *immediately following* op be
        // collapsed into one? Returns null if not mergeable.
        IOperation? TryMergeWith(IOperation next) => null;
    }

    // ---------------------------------------------------------------------
    // Concrete operations. Each exposes its inputs as properties (records
    // give us that for free) and owns its own Apply logic.
    // ---------------------------------------------------------------------
    public sealed record AddItemOp(string ItemId) : IOperation
    {
        public State Apply(State s) => s with { Items = s.Items.Add(ItemId) };

        public bool CommutesWith(IOperation other) => other switch
        {
            // Two adds always commute (set semantics).
            AddItemOp => true,
            // Add(x) and Remove(y) commute iff they touch different items.
            RemoveItemOp r => r.ItemId != ItemId,
            // Item ops never touch metadata → always commute with it.
            UpdateMetadataOp => true,
            _ => false
        };
    }

    public sealed record RemoveItemOp(string ItemId) : IOperation
    {
        public State Apply(State s) => s with { Items = s.Items.Remove(ItemId) };

        public bool CommutesWith(IOperation other) => other switch
        {
            RemoveItemOp => true,                       // removes commute
            AddItemOp a => a.ItemId != ItemId,          // disjoint ids only
            UpdateMetadataOp => true,
            _ => false
        };
    }

    public sealed record UpdateMetadataOp(string Key, string Value) : IOperation
    {
        public State Apply(State s) =>
            s with { Metadata = s.Metadata.SetItem(Key, Value) };

        public bool CommutesWith(IOperation other) => other switch
        {
            // Updates on *different* keys commute; same key does not
            // (last-writer-wins means order matters).
            UpdateMetadataOp u => u.Key != Key,
            AddItemOp => true,
            RemoveItemOp => true,
            _ => false
        };

        // Two consecutive updates to the SAME key collapse to the second.
        public IOperation? TryMergeWith(IOperation next) =>
            next is UpdateMetadataOp u && u.Key == Key ? u : null;
    }

    // ---------------------------------------------------------------------
    // Driver. Core loop is three lines; everything else is the follow-up.
    // ---------------------------------------------------------------------
    public static class OperationEngine
    {
        // The whole "main" answer:
        public static State ApplyAll(State initial, IEnumerable<IOperation> ops)
        {
            var state = initial;
            foreach (var op in ops)
                state = op.Apply(state);
            return state;
        }

        // Follow-up: merge adjacent ops where possible, then bubble ops that
        // commute (e.g. reorder pure adds ahead of unrelated removes).
        public static List<IOperation> Optimize(IReadOnlyList<IOperation> ops)
        {
            // Pass 1: merge consecutive mergeable ops (e.g. Update("k",a) then
            // Update("k",b) → Update("k",b)).
            var merged = new List<IOperation>();
            foreach (var op in ops)
            {
                if (merged.Count > 0 &&
                    merged[^1].TryMergeWith(op) is IOperation combined)
                {
                    merged[^1] = combined;
                }
                else
                {
                    merged.Add(op);
                }
            }

            // Pass 2: stable "bubble" reorder — move AddItemOp earlier while
            // every op it hops over commutes with it. (Batching adds enables
            // e.g. one bulk insert instead of N single inserts.)
            var result = new List<IOperation>(merged);
            for (int i = 1; i < result.Count; i++)
            {
                if (result[i] is not AddItemOp) continue;
                int j = i;
                while (j > 0 && result[j].CommutesWith(result[j - 1]))
                {
                    (result[j], result[j - 1]) = (result[j - 1], result[j]);
                    j--;
                }
            }
            return result;
        }
    }

    // ---------------------------------------------------------------------
    // Demo: shows correctness, then shows Optimize preserving the result.
    // ---------------------------------------------------------------------
    public static class Program
    {
        public static void Main()
        {
            var ops = new List<IOperation>
            {
                new UpdateMetadataOp("owner", "alice"),
                new AddItemOp("A"),
                new RemoveItemOp("B"),          // no-op remove, still valid
                new UpdateMetadataOp("owner", "bob"),   // merges over alice? no — not adjacent
                new UpdateMetadataOp("owner", "carol"), // adjacent → merges with bob
                new AddItemOp("C"),
                new RemoveItemOp("A"),
            };

            var initial = State.Empty with { Items = ImmutableHashSet.Create("B") };

            var plain = OperationEngine.ApplyAll(initial, ops);
            Console.WriteLine($"Sequential : {plain}");

            var optimized = OperationEngine.Optimize(ops);
            var optResult = OperationEngine.ApplyAll(initial, optimized);
            Console.WriteLine($"Optimized  : {optResult}");
            Console.WriteLine($"Same result: {plain == optResult}");

            Console.WriteLine("\nOptimized op order:");
            foreach (var op in optimized) Console.WriteLine($"  {op}");
        }
    }
}
