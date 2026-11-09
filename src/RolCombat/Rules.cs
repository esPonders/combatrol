using System;

namespace Valmods.TowerGuard
{
    internal sealed class EquipGate
    {
        private bool eligible;
        private float since;
        internal void Observe(bool allowed, float now)
        {
            if (!allowed) { Reset(); return; }
            if (!eligible) { eligible = true; since = now; }
        }
        internal bool Ready(float now) => eligible && now - since >= 2f;
        internal void Reset() { eligible = false; since = 0f; }
    }

    internal sealed class DodgeWindow
    {
        private bool eligible, mortal;
        private float expires;
        internal void Begin(bool allowed) { eligible = allowed; mortal = false; expires = 0f; }
        internal void Clear() { eligible = false; mortal = false; expires = 0f; }
        internal void Mortal(float now, float seconds)
        {
            if (!eligible || mortal) return;
            mortal = true; expires = now + seconds;
        }
        internal bool Extended(float now) => eligible && mortal && now < expires;
    }

    internal sealed class NumbWindow
    {
        private float expires;
        internal void Refresh(float now) { expires = now + 2f; }
        internal bool Active(float now) => now < expires;
        internal float Preserve(float now, float before, float after) => Active(now) && after < before ? before : after;
    }

    // Pure rules, also executed by the offline test suite.
    internal static class Rules
    {
        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        internal static float RescaledHealth(float health, float oldMax, float newMax) =>
            (float)(Math.Min(1d, Math.Max(0d, (double)health / oldMax)) * newMax);

        internal static float MultiplayerCompensation(float scale) => Finite(scale) && scale > 1f ? 1f / scale : 1f;

        internal static float StarRatio(int level, float bonusPerStar)
        {
            if (level <= 1) return 1f;
            double stars = (double)level - 1;
            return (float)((1d + stars * bonusPerStar) / (1d + stars * .5d));
        }

        internal static bool TryAddPreparation(float current, float threshold, float fraction, out float next)
        {
            next = current;
            if (!Finite(current) || !Finite(threshold) || !Finite(fraction) ||
                current < 0f || threshold <= 0f || fraction <= 0f || fraction >= 1f) return false;
            double candidate = current + (double)threshold * fraction;
            // Reject the entire increment, including exact equality. Never clamp to threshold-1.
            if (candidate >= threshold) return false;
            float rounded = (float)candidate;
            if (rounded >= threshold || rounded <= current) return false;
            next = rounded;
            return true;
        }

        internal static bool Mitigated(float before, float after, float stamina, float stagger, float threshold)
        {
            return Finite(before) && Finite(after) && Finite(stamina) && Finite(stagger) && Finite(threshold) &&
                before > after + .0001f && after >= 0f && stamina > 0f && threshold > 0f && stagger < threshold;
        }
    }
}
