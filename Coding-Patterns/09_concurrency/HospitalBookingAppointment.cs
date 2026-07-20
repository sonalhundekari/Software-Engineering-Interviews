/*
Hospital Appointment Booking API
Design and implement an appointment-booking REST API for a hospital with 1,000 doctors, each working 9 AM–5 PM in 15-minute slots. The API books the first available slot for a given doctor on a given date and must maintain state across calls. 

Requirements

1,000 doctors. Each doctor works 9:00 AM–5:00 PM in 15-minute appointment slots — 32 slots per doctor per day.
API: book the first available slot for a given doctor on a given date.
Return the booked time slot, or an error if no availability remains for that doctor on that date.
Must maintain state across API calls (POST semantics; the booked slot stays booked across subsequent calls).
Implementation expected end-to-end — endpoint handler + storage of bookings.
Notes

Minimal in-memory model: bookings: Map<(doctorId, date), BitSet32> — 32 bits per doctor-day, one per slot. firstFree(bs) = lowest_zero_bit(bs). Book by setting that bit. O(1) per booking.
Endpoint shape: POST /appointments {doctorId, date} → {slotStart, slotEnd} on success, 409 / structured error on no availability.
Concurrency: under any realistic deployment, two POSTs for the same doctor-day must serialise; otherwise two clients can claim the same slot. The cleanest answer in a 45-60-minute onsite is a per-doctor lock (Map<doctorId, Lock>) or a single CAS update on the per-day bitset (atomic compare-and-swap).
Persistence: extending to a DB-backed store, the natural schema is appointments(doctor_id, date, slot_index, patient_id, status) with a unique constraint on (doctor_id, date, slot_index). Idempotency on firstFree becomes: INSERT ... WHERE NOT EXISTS (slot_index) returning the chosen index.
For 1,000 doctors × N days, total state is small. The point of the prompt is to talk through concurrency and persistence, not to scale.
Likely follow-ups: cancellation, rescheduling, patient binding, doctor-side blocked time (vacations), date validation (no booking in the past).
Preparation

Sketch the in-memory bitset implementation in the first 10 minutes; get a working endpoint coded next.
Talk through the concurrency story explicitly — pick one of (per-doctor lock, CAS on bitset, DB unique constraint) and articulate the trade-off.
Be ready to extend to cancellation and date validation in the follow-up window.
*/
using System.Collections;
using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var store = new AppointmentStore(doctorCount: 1000);

// ---------------------------------------------------------------------
// POST /appointments  — book the first available slot for a doctor/date
// ---------------------------------------------------------------------
app.MapPost("/appointments", (BookRequest req) =>
{
    if (req.DoctorId < 1 || req.DoctorId > AppointmentStore.DoctorCount)
        return Results.NotFound(new ErrorResponse($"Doctor {req.DoctorId} does not exist."));

    if (req.Date.Date < DateTime.UtcNow.Date)
        return Results.BadRequest(new ErrorResponse("Cannot book an appointment in the past."));

    var slotIndex = store.BookFirstAvailable(req.DoctorId, req.Date);

    if (slotIndex is null)
        return Results.Conflict(
            new ErrorResponse($"No availability for doctor {req.DoctorId} on {req.Date:yyyy-MM-dd}."));

    var (start, end) = AppointmentStore.SlotTimes(slotIndex.Value);
    return Results.Ok(new BookResponse(req.DoctorId, req.Date.Date, slotIndex.Value, start, end));
});

// ---------------------------------------------------------------------
// GET /appointments/{doctorId}/{date} — list free slots (useful for UI / testing)
// ---------------------------------------------------------------------
app.MapGet("/appointments/{doctorId:int}/{date:datetime}", (int doctorId, DateTime date) =>
{
    if (doctorId < 1 || doctorId > AppointmentStore.DoctorCount)
        return Results.NotFound(new ErrorResponse($"Doctor {doctorId} does not exist."));

    var freeSlots = store.FreeSlots(doctorId, date)
        .Select(i =>
        {
            var (start, end) = AppointmentStore.SlotTimes(i);
            return new { slotIndex = i, start, end };
        });

    return Results.Ok(freeSlots);
});

// ---------------------------------------------------------------------
// DELETE /appointments/{doctorId}/{date}/{slotIndex} — cancel a booking
// ---------------------------------------------------------------------
app.MapDelete("/appointments/{doctorId:int}/{date:datetime}/{slotIndex:int}", (int doctorId, DateTime date, int slotIndex) =>
{
    if (slotIndex < 0 || slotIndex >= AppointmentStore.SlotsPerDay)
        return Results.BadRequest(new ErrorResponse("Invalid slot index."));

    var cancelled = store.Cancel(doctorId, date, slotIndex);

    return cancelled
        ? Results.NoContent()
        : Results.NotFound(new ErrorResponse("That slot is not currently booked."));
});

app.Run();


// =======================================================================
// Storage layer
// =======================================================================
//
// Model: one BitArray(32) per (doctorId, date). 9AM-5PM in 15-min slots
// is exactly 32 slots, one bit each. Bit i set == slot i booked.
//
// Concurrency: BitArray has no atomic compare-and-swap, so this is the
// per-doctor-day-lock approach from the design notes rather than the
// lock-free CAS approach. We lock on the BitArray instance itself —
// ConcurrentDictionary.GetOrAdd guarantees every caller for the same
// (doctorId, date) key gets back the *same* BitArray reference (even if
// the factory races and runs more than once, only one result is ever
// stored and handed out), so locking on it correctly serializes all
// bookings/cancellations for that doctor-day. Different doctor-days
// never contend with each other.
//
public sealed class AppointmentStore
{
    public const int DoctorCount = 1000;
    public const int SlotsPerDay = 32; // (17:00 - 09:00) / 15 min
    private static readonly TimeOnly DayStart = new(9, 0);
    private static readonly TimeSpan SlotLength = TimeSpan.FromMinutes(15);

    // Key: (doctorId, date). Value: 32-bit booked/free mask for that doctor-day.
    private readonly ConcurrentDictionary<(int doctorId, DateTime date), BitArray> bookings = new();

    public AppointmentStore(int doctorCount)
    {
        if (doctorCount != DoctorCount)
            throw new ArgumentException($"This store is sized for {DoctorCount} doctors.");
    }

    /// <summary>
    /// Books the lowest-indexed free slot for a doctor on a date.
    /// Returns the slot index, or null if the day is fully booked.
    /// </summary>
    public int? BookFirstAvailable(int doctorId, DateTime date)
    {
        var bits = bookings.GetOrAdd((doctorId, date.Date), static _ => new BitArray(SlotsPerDay));

        lock (bits)
        {
            for (int i = 0; i < SlotsPerDay; i++)
            {
                if (!bits[i])
                {
                    bits[i] = true;
                    return i;
                }
            }
            return null; // fully booked
        }
    }

    /// <summary>
    /// Frees a previously booked slot. Returns false if it wasn't booked
    /// (already cancelled, or never booked) or the doctor-day has no
    /// bookings at all yet.
    /// </summary>
    public bool Cancel(int doctorId, DateTime date, int slotIndex)
    {
        if (!bookings.TryGetValue((doctorId, date.Date), out var bits))
            return false;

        lock (bits)
        {
            if (!bits[slotIndex])
                return false; // not currently booked

            bits[slotIndex] = false;
            return true;
        }
    }

    /// <summary>Slot indices still free for a doctor-day, in order.</summary>
    public List<int> FreeSlots(int doctorId, DateTime date)
    {
        var bits = bookings.GetOrAdd((doctorId, date.Date), static _ => new BitArray(SlotsPerDay));
        var free = new List<int>(SlotsPerDay);

        lock (bits)
        {
            for (int i = 0; i < SlotsPerDay; i++)
                if (!bits[i])
                    free.Add(i);
        }

        return free;
    }

    public static (TimeOnly Start, TimeOnly End) SlotTimes(int slotIndex)
    {
        var start = DayStart.Add(SlotLength * slotIndex);
        var end = start.Add(SlotLength);
        return (start, end);
    }
}

// =======================================================================
// DTOs
// =======================================================================
public record BookRequest(int DoctorId, DateTime Date);
public record BookResponse(int DoctorId, DateTime Date, int SlotIndex, TimeOnly SlotStart, TimeOnly SlotEnd);
public record ErrorResponse(string Error);