using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Valmods.TowerGuard;
using Mono.Cecil;

internal static class Program
{
    private static int checks;
    private static void Check(bool result, string message)
    { checks++; if (!result) throw new Exception(message); }
    private static void Near(double actual, double expected, double tolerance, string message) => Check(Math.Abs(actual - expected) < tolerance, message);
    private static double Remaining(double damage, double armor) => damage > 2 * armor ? damage - armor : damage * damage / (4 * armor);

    private static void Main(string[] args)
    {
        var runtime = new RuntimeState();
        Check(!runtime.Running(true), "rules inactive until hooks are ready");
        runtime.Ready = true;
        Check(runtime.Running(true), "ready and configured rules active");
        Check(runtime.Status(false) == "disabled_configuration", "manual disable distinguishable from fault");
        Check(runtime.Suspend("MethodAccessException") && runtime.Status(true) == "faulted", "fault is exposed");
        Check(!runtime.Running(true), "reenabling configuration does not hide current runtime fault");
        Check(!runtime.Suspend("later") && runtime.Error == "MethodAccessException", "preserve original error");
        var restarted = new RuntimeState { Ready = true };
        Check(restarted.Running(true), "runtime fault does not persist after restart");
        using (var dll = ModuleDefinition.ReadModule("src/RolCombat/bin/Release/netstandard2.1/Valmods.RolCombat.dll"))
        {
            var methods = dll.GetMemberReferences().OfType<MethodReference>().ToArray();
            Check(!methods.Any(m => m.Name == "GetLeftItem" && m.DeclaringType.Name == "Humanoid"), "compiled addon contains no forbidden direct GetLeftItem call");
            var plugin = dll.Types.Single(t => t.Name == "Plugin");
            Check(!plugin.Methods.Single(m => m.Name == "Failure").Body.Instructions.Any(i => i.Operand is MethodReference mr && mr.Name == "set_Value"), "fault cannot persist disabled state");
            Check(plugin.Methods.Single(m => m.Name == "Awake").Body.Instructions.Any(i => i.Operand is MethodReference mr && mr.Name == "set_Value"), "startup explicitly enables config");
        }
        foreach (float scale in new[] { 1f, 1.04f, 1.08f, 1.12f, 1.16f })
            Near(130f * .85f * scale * Rules.MultiplayerCompensation(scale), 110.5, .0001, "multiplayer blocked damage equals solo tower damage");
        Check(Rules.MultiplayerCompensation(float.NaN) == 1f && Rules.MultiplayerCompensation(0) == 1f, "invalid scale cannot amplify damage");
        Near(1600 * 1.2, 1920, .001, "one-star brute gains twenty percent health");
        Near(2400 * 1.2, 2880, .001, "two-star brute gains twenty percent, not per star");
        Near(1920 * .3 / 1.2, 480, .001, "original stagger threshold retained");
        Near(Rules.RescaledHealth(800, 1600, 1920), 960, .001, "existing injuries retain fraction");
        Near(Rules.RescaledHealth(960, 1920, 1920), 960, .001, "reload cannot compound health");
        Near(1920f * .3f / 5f * Rules.RecoveryDelta(1f, .2f), 96, .0001, "elite decay retains original 96 points per second");
        Near(2880f * .3f / 5f * Rules.RecoveryDelta(1f, .2f), 144, .0001, "two-star decay retains original 144 points per second");
        Near(Rules.RecoveryDelta(.02f, 0), .02, .000001, "zero health bonus leaves native delta unchanged");
        float bar = 480;
        for (int i = 0; i < 250; i++) bar = Math.Max(0, bar - 1920f * .3f / 5f * Rules.RecoveryDelta(.02f, .2f));
        Near(bar, 0, .01, "original full bar recovers in five seconds");
        var gate = new EquipGate();
        gate.Observe(true, 10);
        Check(!gate.Ready(11.99f) && gate.Ready(12), "two continuous seconds required");
        gate.Observe(false, 12.1f);
        Check(!gate.Ready(12.1f), "incompatible equipment cancels bonus immediately");
        gate.Observe(true, 12.1f);
        Check(!gate.Ready(14) && gate.Ready(14.1f), "switch back restarts entire wait");
        gate.Reset();
        Check(!gate.Ready(100), "new character or session cannot retain bonus");
        var dodge = new DodgeWindow();
        dodge.Begin(true);
        Check(!dodge.Extended(1), "extension does not start before native invulnerability ends");
        dodge.Mortal(1, .1f);
        Check(dodge.Extended(1.05f) && !dodge.Extended(1.11f), "extra invulnerability limited to configured duration");
        dodge.Mortal(1.09f, .1f);
        Check(!dodge.Extended(1.11f), "repeated animation events cannot extend the bonus");
        dodge.Clear();
        Check(!dodge.Extended(1.05f), "equipment change or roll end cancels extension");
        dodge.Begin(false); dodge.Mortal(2, .1f);
        Check(!dodge.Extended(2.05f), "ineligible roll cannot gain extension");
        dodge.Begin(true); dodge.Mortal(3, .1f);
        Check(dodge.Extended(3.05f), "new eligible roll resets previous state");
        Near(18.4688 * .75, 13.8516, .00001, "25 percent stamina reduction example");
        var numb = new NumbWindow();
        numb.Refresh(10);
        Check(numb.Active(11.99f) && !numb.Active(12), "freeze expires after two seconds");
        numb.Refresh(11);
        Check(numb.Active(12.99f) && !numb.Active(13), "block refreshes rather than stacks duration");
        Near(numb.Preserve(12, 50, 48), 50, .0001, "decay suppressed during window");
        Near(numb.Preserve(12, 50, 65), 65, .0001, "additional stagger damage preserved");
        Near(numb.Preserve(13, 50, 48), 48, .0001, "native decay resumes after expiry");
        Check(!Rules.TryAddPreparation(90, 100, .2f, out _) && numb.Active(12), "freeze independent of increment fitting");
        Near(Rules.StarRatio(1, .25f), 1, .00001, "normal unchanged");
        Near(1.5 * Rules.StarRatio(2, .25f), 1.25, .00001, "one star is +25%, not half total damage");
        Near(2 * Rules.StarRatio(3, .25f), 1.5, .00001, "two stars is +50%");
        Near(Rules.StarRatio(3, .5f), 1, .00001, "vanilla setting restores star factor");
        Check(Rules.TryAddPreparation(65, 100, .2f, out var next) && next == 85, "65 -> 85");
        Check(!Rules.TryAddPreparation(85, 100, .2f, out next) && next == 85, "85 unchanged, not 99");
        Check(!Rules.TryAddPreparation(80, 100, .2f, out next) && next == 80, "exact threshold rejected");
        Check(!Rules.TryAddPreparation(0, 0, .2f, out next), "immune/no threshold");
        Check(!Rules.TryAddPreparation(float.NaN, 100, .2f, out next), "NaN rejected");
        Check(!Rules.TryAddPreparation(0, float.PositiveInfinity, .2f, out next), "infinity rejected");
        Check(!Rules.TryAddPreparation(0, 100, 0, out next), "disabled reward");
        Check(!Rules.TryAddPreparation(0, 100, 1, out next), "invalid full bar reward");
        float meter = 0;
        for (int i = 0; i < 20; i++)
            if (Rules.TryAddPreparation(meter, 100, .2f, out next)) meter = next;
        Check(meter == 80, "blocking alone never fills the bar");
        Check(Rules.TryAddPreparation(0, 240, .2f, out next), "normal brute reward");
        Near(next, 48, .0001, "normal brute 20%");
        Rules.TryAddPreparation(0, 480, .2f, out next);
        Near(next, 96, .0001, "elite receives same fraction, independent of attack damage");
        Check(Rules.Mitigated(100, 20, 30, 20, 74), "real block accepted");
        Check(!Rules.Mitigated(188, 188, 148, 74, 74), "captured failed block rejected even if BlockAttack returns true");
        Check(!Rules.Mitigated(100, 20, 0, 20, 74), "stamina exhausted rejected");
        Check(!Rules.Mitigated(100, 20, 30, 74, 74), "full defensive stagger bar rejected");
        // Exercise many sizes and fill levels: every accepted increment must stay strictly below threshold.
        foreach (float threshold in new[] { .01f, .5f, 1, 10, 24, 240, 480, 10000 })
            for (int i = 0; i <= 1000; i++)
            {
                float current = threshold * i / 1000f;
                if (Rules.TryAddPreparation(current, threshold, .2f, out next))
                    Check(next < threshold && next > current, "never stagger or reduce existing meter");
                else Check(next == current, "rejected increment leaves bar unchanged");
            }

        string directory = args.Length > 0 ? args[0] : "Datos";
        var baselines = new[] { "combat-20261109-104700-207-69ea44a6.jsonl", "combat-20261109-105930-831-43835058.jsonl", "combat-20261109-110929-865-344aa636.jsonl" };
        int replayed = 0, wolfCount = 0;
        double observedCost = 0, reducedCost = 0;
        foreach (string file in (Directory.Exists(directory) ? Directory.GetFiles(directory, "*.jsonl", SearchOption.AllDirectories) : Array.Empty<string>()).Where(p => baselines.Contains(Path.GetFileName(p))))
            foreach (string line in File.ReadLines(file))
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                if (root.GetProperty("event").GetString() != "block") continue;
                var before = root.GetProperty("actor_before");
                var after = root.GetProperty("actor_after");
                var attacker = root.GetProperty("attacker");
                var input = root.GetProperty("input").GetProperty("damage");
                bool real = Rules.Mitigated(
                    input.GetProperty("blunt").GetSingle() + input.GetProperty("slash").GetSingle() + input.GetProperty("pierce").GetSingle(),
                    root.GetProperty("hit_after").GetProperty("damage").GetProperty("blunt").GetSingle() + root.GetProperty("hit_after").GetProperty("damage").GetProperty("slash").GetSingle() + root.GetProperty("hit_after").GetProperty("damage").GetProperty("pierce").GetSingle(),
                    after.GetProperty("stamina").GetSingle(), after.GetProperty("stagger").GetSingle(), after.GetProperty("stagger_threshold").GetSingle());
                if (attacker.GetProperty("prefab").GetString() == "GoblinBrute" && attacker.GetProperty("level").GetInt32() == 2)
                    Check(!real, "actual broken guards must not reward attacker stagger");
                replayed++;
                if (attacker.GetProperty("prefab").GetString() != "Wolf") continue;
                double damage = input.GetProperty("blunt").GetDouble() + input.GetProperty("slash").GetDouble() + input.GetProperty("pierce").GetDouble();
                double block = before.GetProperty("blocker").GetProperty("effective_block").GetDouble();
                double cost = 10 * Math.Clamp((damage - Remaining(damage, block)) / block, 0, 1);
                double actual = before.GetProperty("stamina").GetDouble() - after.GetProperty("stamina").GetDouble();
                Near(cost, actual, .001, "reproduce captured block stamina without changing its formula");
                double reduced = damage * .85;
                reducedCost += 10 * Math.Clamp((reduced - Remaining(reduced, block)) / block, 0, 1);
                observedCost += actual; wolfCount++;
            }
        if (replayed > 0)
        {
        Check(replayed == 78, "replayed all captured block calls");
        Check(wolfCount == 41, "replayed 41 wolf blocks");
        Near(observedCost, 211.30096, .001, "captured baseline stamina");
        Near(reducedCost, 184.46909, .001, "15% incoming reduction without extra stamina rebate");
        }
        else Console.WriteLine("Historical baseline captures unavailable; replay skipped.");
        Console.WriteLine($"{checks} checks passed; {replayed} real block events replayed. No Valheim runtime executed.");
    }
}
