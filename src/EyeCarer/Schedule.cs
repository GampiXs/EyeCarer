using System;

namespace EyeCarer;

// The caller supplies monotonic elapsed seconds, never a UI tick count or wall clock.
public sealed class Schedule
{
    public double Interval { get; private set; }
    public void Reconfigure(double interval, double now) { Interval = interval; Next = Math.Max(now, PausedUntil) + interval; }
    public double Next { get; private set; }
    public double PausedUntil { get; private set; }
    public bool Suspended { get; private set; }
    public Schedule(double interval, double now, bool immediately = false)
    { Interval = interval; Next = immediately ? now : now + interval; }
    public void Reset(double now) { PausedUntil = 0; Suspended = false; Next = now + Interval; }
    public void Suspend() { Suspended = true; }
    public void Pause(double now) { PausedUntil = now + 3600; Next = PausedUntil + Interval; }
    public bool IsPaused(double now) => now < PausedUntil;
    public bool TakeDue(double now)
    {
        if (Suspended || IsPaused(now) || now < Next) return false;
        // Missed deadlines are discarded; there is never a catch-up queue.
        Next = now + Interval;
        return true;
    }
    public double Delay(double now) => Suspended ? double.PositiveInfinity : Math.Max(.01, Next - now);
}
