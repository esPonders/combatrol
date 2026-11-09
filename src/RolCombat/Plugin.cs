using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Valmods.TowerGuard
{
    [BepInPlugin(Id, "RolCombat", Version)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Id = "ponders.valmods.towerguard";
        public const string Version = "1.0.1";
        private const string RewardRpc = "Valmods_TowerPreparation_v1";
        internal static Plugin Instance;
        private Harmony harmony;
        internal ConfigEntry<float> EliteHealthBonus;
        internal static Func<Character, float> MaxHealthBase;
        internal static Action<Character, float> SetMaximum, SetCurrentHealth;
        internal sealed class IncomingScale { internal float Multiplier; }
        internal static readonly ConditionalWeakTable<HitData, IncomingScale> IncomingScales = new ConditionalWeakTable<HitData, IncomingScale>();
        internal static Func<Game> CurrentGame;
        internal static Func<Game, Vector3, float> PlayerDamageScale;
        private readonly EquipGate agileGate = new EquipGate();
        private Player gatePlayer;
        private StatusEffect agileTemplate, agileEffect;
        private SEMan agileManager;
        private static Func<Character, SEMan> GetEffects;
        private static Func<Player, Skills> GetSkills;
        private static Func<Skills, Skills.SkillType, Skills.SkillDef> GetSkillDef;
        private static Func<SEMan, StatusEffect, bool, int, float, StatusEffect> AddEffect;
        private static Func<SEMan, StatusEffect, bool, bool> RemoveEffect;
        internal static readonly ConditionalWeakTable<Character, NumbWindow> Numb = new ConditionalWeakTable<Character, NumbWindow>();
        internal ConfigEntry<bool> Enabled;
        internal ConfigEntry<float> StarBonus, Reduction, Preparation, DodgeReduction, DodgeExtension;
        internal static readonly ConditionalWeakTable<Player, DodgeWindow> Dodges = new ConditionalWeakTable<Player, DodgeWindow>();
        private readonly RuntimeState runtime = new RuntimeState();
        public static string RuntimeStatus => Instance == null ? "not_loaded" : Instance.runtime.Status(Instance.Enabled.Value);
        public static string LastError => Instance?.runtime.Error;

        // Resolve once at startup. Never scan assemblies from Update or per hit.
        internal static AccessTools.FieldRef<Attack, Humanoid> AttackCharacter;
        internal static AccessTools.FieldRef<Character, float> Stagger;
        internal static AccessTools.FieldRef<Character, float> StaggerFactor;
        internal static AccessTools.FieldRef<Character, bool> Boss;
        internal static AccessTools.FieldRef<Character, ZNetView> View;
        internal static AccessTools.FieldRef<Humanoid, ItemDrop.ItemData> LeftItem;
        internal static AccessTools.FieldRef<Humanoid, ItemDrop.ItemData> RightItem;
        internal static Func<ItemDrop.ItemData, float, float> BlockPower;
        internal static Func<Character, Skills.SkillType, float> Skill;
        internal static Func<Character, float> Threshold;
        internal static Func<Humanoid, ItemDrop.ItemData> Blocker;

        private void Awake()
        {
            Instance = this;
            Enabled = Config.Bind("General", "Enabled", true, "Activar las reglas. En multijugador todos deben usar la misma versión y configuración.");
            // Always start enabled, including profiles that saved false in an older release.
            Enabled.Value = true;
            StarBonus = Config.Bind("Elites", "DamageBonusPerStar", .25f,
                new ConfigDescription("Incremento aditivo por estrella respecto al daño normal. No altera vida, botín ni criaturas domesticadas.", new AcceptableValueRange<float>(0f, 1f)));
            Reduction = Config.Bind("Tower", "IncomingDamageReduction", .15f,
                new ConfigDescription("Reducción de daño entrante con un torre equipado, antes del bloqueo. No basta llevarlo en el inventario.", new AcceptableValueRange<float>(0f, .9f)));
            Preparation = Config.Bind("Tower", "StaggerFractionPerBlock", .20f,
                new ConfigDescription("Fracción del umbral del atacante añadida por bloqueo cuerpo a cuerpo exitoso. Se omite si alcanzaría o superaría el umbral.", new AcceptableValueRange<float>(0f, .9f)));
            DodgeReduction = Config.Bind("Agile", "DodgeStaminaReduction", .25f,
                new ConfigDescription("Reducción de coste de esquiva con arma de una mano y sin escudo equipado.", new AcceptableValueRange<float>(0f, .9f)));
            DodgeExtension = Config.Bind("Agile", "ExtraInvulnerabilitySeconds", .10f,
                new ConfigDescription("Segundos adicionales de invulnerabilidad con arma de una mano y sin escudo.", new AcceptableValueRange<float>(0f, .3f)));
            EliteHealthBonus = Config.Bind("Elites", "ExtraHealth", .20f,
                new ConfigDescription("Vida adicional sobre la vida original de criaturas con estrellas. El umbral de desequilibrio conserva su valor anterior.", new AcceptableValueRange<float>(0f, 2f)));
            harmony = new Harmony(Id);
            try
            {
                MaxHealthBase = AccessTools.MethodDelegate<Func<Character, float>>(Required(typeof(Character), "GetMaxHealthBase", typeof(float)));
                SetMaximum = AccessTools.MethodDelegate<Action<Character, float>>(Required(typeof(Character), "SetMaxHealth", typeof(void), typeof(float)));
                SetCurrentHealth = AccessTools.MethodDelegate<Action<Character, float>>(Required(typeof(Character), "SetHealth", typeof(void), typeof(float)));
                Patch(Required(typeof(Character), "GetMaxHealthBase", typeof(float)), typeof(EliteHealthPatch), null, nameof(EliteHealthPatch.Postfix));
                Patch(Required(typeof(Character), "GetStaggerTreshold", typeof(float)), typeof(EliteThresholdPatch), null, nameof(EliteThresholdPatch.Postfix));
                CurrentGame = AccessTools.MethodDelegate<Func<Game>>(AccessTools.PropertyGetter(typeof(Game), "instance"));
                PlayerDamageScale = AccessTools.MethodDelegate<Func<Game, Vector3, float>>(Required(typeof(Game), "GetDifficultyDamageScalePlayer", typeof(float), typeof(Vector3)));
                AttackCharacter = AccessTools.FieldRefAccess<Attack, Humanoid>("m_character");
                Stagger = AccessTools.FieldRefAccess<Character, float>("m_staggerDamage");
                StaggerFactor = AccessTools.FieldRefAccess<Character, float>("m_staggerDamageFactor");
                Boss = AccessTools.FieldRefAccess<Character, bool>("m_boss");
                View = AccessTools.FieldRefAccess<Character, ZNetView>("m_nview");
                // GetLeftItem is nonpublic in the installed game. Read its backing field
                // through Harmony's cached access instead of emitting an illegal direct call.
                LeftItem = AccessTools.FieldRefAccess<Humanoid, ItemDrop.ItemData>("m_leftItem");
                GetEffects = AccessTools.MethodDelegate<Func<Character, SEMan>>(Required(typeof(Character), "GetSEMan", typeof(SEMan)));
                GetSkills = AccessTools.MethodDelegate<Func<Player, Skills>>(Required(typeof(Player), "GetSkills", typeof(Skills)));
                GetSkillDef = AccessTools.MethodDelegate<Func<Skills, Skills.SkillType, Skills.SkillDef>>(Required(typeof(Skills), "GetSkillDef", typeof(Skills.SkillDef), typeof(Skills.SkillType)));
                // Valheim 1.0 adds a final Int16 parameter. Resolve both supported shapes
                // once, without referencing an unavailable signature directly in emitted IL.
                var addWithVariant = typeof(SEMan).GetMethod("AddStatusEffect", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, new[] { typeof(StatusEffect), typeof(bool), typeof(int), typeof(float), typeof(short) }, null);
                if (addWithVariant != null && addWithVariant.ReturnType == typeof(StatusEffect))
                {
                    var add = AccessTools.MethodDelegate<Func<SEMan, StatusEffect, bool, int, float, short, StatusEffect>>(addWithVariant);
                    AddEffect = (manager, effect, reset, level, skill) => add(manager, effect, reset, level, skill, 0);
                    Logger.LogInfo("API de buff: AddStatusEffect(StatusEffect, bool, int, float, Int16)");
                }
                else AddEffect = AccessTools.MethodDelegate<Func<SEMan, StatusEffect, bool, int, float, StatusEffect>>(Required(typeof(SEMan), "AddStatusEffect", typeof(StatusEffect), typeof(StatusEffect), typeof(bool), typeof(int), typeof(float)));
                RemoveEffect = AccessTools.MethodDelegate<Func<SEMan, StatusEffect, bool, bool>>(Required(typeof(SEMan), "RemoveStatusEffect", typeof(bool), typeof(StatusEffect), typeof(bool)));
                RightItem = AccessTools.FieldRefAccess<Humanoid, ItemDrop.ItemData>("m_rightItem");
                Skill = AccessTools.MethodDelegate<Func<Character, Skills.SkillType, float>>(Required(typeof(Player), "GetSkillFactor", typeof(float), typeof(Skills.SkillType)));
                BlockPower = AccessTools.MethodDelegate<Func<ItemDrop.ItemData, float, float>>(Required(typeof(ItemDrop.ItemData), "GetBlockPower", typeof(float), typeof(float)));
                Threshold = AccessTools.MethodDelegate<Func<Character, float>>(Required(typeof(Character), "GetStaggerTreshold", typeof(float)));
                Blocker = AccessTools.MethodDelegate<Func<Humanoid, ItemDrop.ItemData>>(Required(typeof(Humanoid), "GetCurrentBlocker", typeof(ItemDrop.ItemData)));
                Patch(Required(typeof(Attack), "GetLevelDamageFactor", typeof(float)), typeof(ElitePatch), null, nameof(ElitePatch.Postfix));
                Patch(Required(typeof(Character), "RPC_Damage", typeof(void), typeof(long), typeof(HitData)), typeof(IncomingPatch), nameof(IncomingPatch.Prefix), nameof(IncomingPatch.Postfix));
                Patch(Required(typeof(Humanoid), "BlockAttack", typeof(bool), typeof(HitData), typeof(Character)), typeof(BlockPatch), nameof(BlockPatch.Prefix), nameof(BlockPatch.Postfix));
                Patch(Required(typeof(Character), "Awake", typeof(void)), typeof(NetworkPatch), null, nameof(NetworkPatch.Postfix));
                Patch(Required(typeof(Character), "UpdateStagger", typeof(void), typeof(float)), typeof(RecoveryPatch), nameof(RecoveryPatch.Prefix), nameof(RecoveryPatch.Postfix));
                Patch(Required(typeof(Character), "Stagger", typeof(void), typeof(Vector3)), typeof(StaggerPatch), null, nameof(StaggerPatch.Postfix));
                foreach (var method in typeof(ItemDrop.ItemData).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                    if (method.Name == "GetTooltip" && method.ReturnType == typeof(string) &&
                        method.GetParameters().Length > 0 && method.GetParameters()[0].ParameterType == typeof(ItemDrop.ItemData))
                        Patch(method, typeof(TooltipPatch), null, nameof(TooltipPatch.Postfix));
                Patch(Required(typeof(Player), "GetDodgeStaminaUse", typeof(float)), typeof(DodgeCostPatch), null, nameof(DodgeCostPatch.Postfix));
                Patch(Required(typeof(Player), "UpdateDodge", typeof(void), typeof(float)), typeof(DodgeUpdatePatch), nameof(DodgeUpdatePatch.Prefix), nameof(DodgeUpdatePatch.Postfix));
                Patch(Required(typeof(Player), "OnDodgeMortal", typeof(void)), typeof(DodgeMortalPatch), nameof(DodgeMortalPatch.Prefix), null);
                Patch(Required(typeof(Player), "IsDodgeInvincible", typeof(bool)), typeof(DodgeInvinciblePatch), null, nameof(DodgeInvinciblePatch.Postfix));
                Patch(Required(typeof(Humanoid), "GetCurrentBlocker", typeof(ItemDrop.ItemData)), typeof(BestBlockerPatch), null, nameof(BestBlockerPatch.Postfix));
                Patch(Required(typeof(Humanoid), "EquipItem", typeof(bool), typeof(ItemDrop.ItemData), typeof(bool)), typeof(EquipmentPatch), null, nameof(EquipmentPatch.Postfix));
                Patch(Required(typeof(Humanoid), "UnequipItem", typeof(void), typeof(ItemDrop.ItemData), typeof(bool)), typeof(EquipmentPatch), null, nameof(EquipmentPatch.Postfix));
                harmony.Patch(Required(typeof(Humanoid), "BlockAttack", typeof(bool), typeof(HitData), typeof(Character)),
                    prefix: new HarmonyMethod(typeof(MultiplayerBlockPatch), nameof(MultiplayerBlockPatch.Prefix)) { priority = Priority.First });
                runtime.Ready = true;
                Logger.LogInfo("RolCombat " + Version + ": " + RuntimeStatus + ". +25% daño/estrella, torre -15% daño y preparación 20% (valores configurables). Entumecimiento: recuperación suspendida 2 s. Sin escudo y con arma de una mano: esquiva -25% vigor y +0,10 s de invulnerabilidad.");
            }
            catch (Exception ex)
            {
                harmony.UnpatchSelf();
                Logger.LogError("RolCombat desactivado: API incompatible. No se aplican reglas parciales. " + ex);
                runtime.Suspend(ex.ToString());
            }
        }

        private static MethodInfo Required(Type type, string name, Type result, params Type[] parameters)
        {
            MethodInfo method = AccessTools.DeclaredMethod(type, name, parameters);
            if (method == null || method.ReturnType != result) throw new MissingMethodException(type.FullName, name);
            return method;
        }

        private void Patch(MethodInfo original, Type patch, string prefix, string postfix)
        {
            harmony.Patch(original,
                prefix: prefix == null ? null : new HarmonyMethod(patch, prefix) { priority = Priority.High },
                postfix: postfix == null ? null : new HarmonyMethod(patch, postfix) { priority = Priority.Low });
            Logger.LogInfo("Hook instalado: " + original.DeclaringType.Name + "." + original.Name);
        }

        internal static bool EliteHealthApplies(Character actor) => Active && actor != null && !actor.IsPlayer() &&
            !actor.IsTamed() && !Boss(actor) && actor.GetLevel() > 1;

        internal static void ReconcileEliteHealth(Character actor)
        {
            if (!EliteHealthApplies(actor) || !Owned(actor) || actor.IsDead()) return;
            float oldMax = actor.GetMaxHealth();
            float desired = MaxHealthBase(actor) * actor.GetLevel();
            if (!Rules.Finite(desired) || desired <= 0f || oldMax <= 0f || Math.Abs(oldMax - desired) < .01f) return;
            float health = Rules.RescaledHealth(actor.GetHealth(), oldMax, desired);
            SetMaximum(actor, desired);
            SetCurrentHealth(actor, health);
        }

        internal static bool Active => Instance != null && Instance.runtime.Running(Instance.Enabled.Value);
        internal static bool Owned(Character c) => c != null && View(c) != null && View(c).IsOwner();
        internal static bool Tower(ItemDrop.ItemData item) => item != null && item.m_shared != null &&
            item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield && item.m_shared.m_timedBlockBonus <= 1f;
        internal static bool Shield(ItemDrop.ItemData item) => item?.m_shared != null && item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield;
        internal static bool OneHanded(ItemDrop.ItemData item) => item?.m_shared != null && item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.OneHandedWeapon;
        private static bool Eligible(Player player) => Active && player != null && !player.IsDead() &&
            OneHanded(RightItem(player)) && !Shield(LeftItem(player));
        internal static bool Agile(Player player)
        {
            if (Instance == null || player != Player.m_localPlayer) return false;
            Instance.ObserveEquipment(player);
            return Instance.agileGate.Ready(Time.time);
        }

        internal void ObserveEquipment(Player player)
        {
            if (player != Player.m_localPlayer) return;
            if (gatePlayer != player)
            {
                RemoveAgileBuff(); agileGate.Reset(); gatePlayer = player;
            }
            agileGate.Observe(Eligible(player), Time.time);
            if (!agileGate.Ready(Time.time))
            {
                RemoveAgileBuff();
                if (player != null && Dodges.TryGetValue(player, out var dodge)) dodge.Clear();
            }
        }

        private void RemoveAgileBuff()
        {
            if (agileEffect != null && agileManager != null) RemoveEffect(agileManager, agileEffect, true);
            agileEffect = null; agileManager = null;
        }

        private void Update()
        {
            try
            {
                Player player = Player.m_localPlayer;
                ObserveEquipment(player);
                if (player == null || !agileGate.Ready(Time.time) || agileEffect != null) return;
                if (agileTemplate == null)
                {
                    var definition = GetSkillDef(GetSkills(player), Skills.SkillType.Dodge);
                    if (definition == null || definition.m_icon == null) return;
                    agileTemplate = ScriptableObject.CreateInstance<StatusEffect>();
                    agileTemplate.name = "RolCombat_Agilidad";
                    agileTemplate.m_name = "Agilidad";
                    agileTemplate.m_icon = definition.m_icon;
                    agileTemplate.m_tooltip = "Con arma de una mano y sin escudo: esquiva con menor coste de vigor y más tiempo de invulnerabilidad.";
                    agileTemplate.m_ttl = 0f;
                }
                agileManager = GetEffects(player);
                if (agileManager != null) agileEffect = AddEffect(agileManager, agileTemplate, false, 0, 0f);
            }
            catch (Exception ex) { Failure(ex); }
        }
        internal static bool EquippedTower(Player player) => player != null && Tower(LeftItem(player));

        internal static void Failure(Exception error)
        {
            // Stop changing gameplay after an unexpected integration failure; never spam every frame.
            var plugin = Instance;
            if (plugin == null || !plugin.runtime.Suspend(error.ToString())) return;
            plugin.Logger.LogError("RolCombat suspendido por un error; conserva el log para diagnosticarlo: " + error);
            try
            {
                var player = Player.m_localPlayer;
                if (player != null)
                    AccessTools.Method(typeof(Player), "Message", new[] { typeof(MessageHud.MessageType), typeof(string), typeof(int), typeof(Sprite) })?
                        .Invoke(player, new object[] { MessageHud.MessageType.Center, "RolCombat suspendido: revisa el log antes de continuar las pruebas.", 0, null });
            }
            catch { /* Keep the original failure in the log even if the HUD is unavailable. */ }
        }

        internal static void ApplyPreparation(Character attacker)
        {
            if (!Active || !Owned(attacker) || attacker.IsPlayer() || attacker.IsTamed() || Boss(attacker) ||
                attacker.IsDead() || attacker.IsStaggering() || StaggerFactor(attacker) <= 0f) return;
            // Direct, bounded addition is intentional: AddStaggerDamage can trigger Stagger and
            // other effects. This path must never stagger or damage health.
            Numb.GetValue(attacker, _ => new NumbWindow()).Refresh(Time.time);
            if (Rules.TryAddPreparation(Stagger(attacker), Threshold(attacker), Instance.Preparation.Value, out float next))
                Stagger(attacker) = next;
        }

        internal static bool NumbActive(Character actor)
        {
            return Active && Owned(actor) && !actor.IsPlayer() && !actor.IsTamed() && !Boss(actor) &&
                !actor.IsDead() && !actor.IsStaggering() && Numb.TryGetValue(actor, out var window) && window.Active(Time.time);
        }

        internal static void Reward(Character attacker, Player defender)
        {
            if (attacker == null || View(attacker) == null) return;
            if (Owned(attacker)) ApplyPreparation(attacker);
            else if (View(defender) != null && View(defender).GetZDO() != null)
                View(attacker).InvokeRPC(RewardRpc, View(defender).GetZDO().m_uid);
        }

        internal static void Register(Character actor)
        {
            ZNetView view = View(actor);
            if (view == null || !view.IsValid()) return;
            view.Register<ZDOID>(RewardRpc, (sender, defenderId) =>
            {
                try
                {
                    if (!Active || !Owned(actor) || ZNetScene.instance == null) return;
                    GameObject obj = ZNetScene.instance.FindInstance(defenderId);
                    Player defender = obj != null ? obj.GetComponent<Player>() : null;
                    if (defender == null || View(defender) == null || View(defender).GetZDO() == null ||
                        View(defender).GetZDO().GetOwner() != sender || defender.IsDead() || !EquippedTower(defender)) return;
                    // A request only identifies the defender. The authoritative owner computes
                    // the fraction and threshold; the sender cannot supply arbitrary damage.
                    if ((actor.transform.position - defender.transform.position).sqrMagnitude > 400f) return;
                    ApplyPreparation(actor);
                }
                catch (Exception ex) { Failure(ex); }
            });
        }

        private void OnDestroy()
        {
            try { RemoveAgileBuff(); } catch (Exception ex) { Logger.LogWarning(ex.Message); }
            if (agileTemplate != null) Destroy(agileTemplate);
            harmony?.UnpatchSelf();
            if (ReferenceEquals(Instance, this)) Instance = null;
        }
    }

    internal static class ElitePatch
    {
        internal static void Postfix(Attack __instance, ref float __result)
        {
            try
            {
                if (!Plugin.Active) return;
                Character attacker = Plugin.AttackCharacter(__instance);
                if (attacker == null || attacker.IsPlayer() || attacker.IsTamed() || Plugin.Boss(attacker)) return;
                __result *= Rules.StarRatio(attacker.GetLevel(), Plugin.Instance.StarBonus.Value);
            }
            catch (Exception ex) { Plugin.Failure(ex); }
        }
    }

    internal static class IncomingPatch
    {
        internal static void Postfix(HitData hit) { if (hit != null) Plugin.IncomingScales.Remove(hit); }

        internal static void Prefix(Character __instance, HitData hit)
        {
            try
            {
                if (!Plugin.Active || hit == null || !Plugin.Owned(__instance)) return;
                var player = __instance as Player;
                Plugin.IncomingScales.Remove(hit);
                if (Plugin.EquippedTower(player))
                {
                    hit.m_damage.Modify(1f - Plugin.Instance.Reduction.Value);
                    Character attacker = hit.GetAttacker();
                    Game game = Plugin.CurrentGame();
                    if (attacker != null && !attacker.IsPlayer() && game != null)
                        Plugin.IncomingScales.Add(hit, new Plugin.IncomingScale { Multiplier = Plugin.PlayerDamageScale(game, player.transform.position) });
                }
            }
            catch (Exception ex) { Plugin.Failure(ex); }
        }
    }

    internal static class MultiplayerBlockPatch
    {
        internal static void Prefix(Humanoid __instance, HitData hit)
        {
            try
            {
                if (!Plugin.Active || hit == null || !hit.m_blockable || !Plugin.Owned(__instance) ||
                    !(__instance is Player player) || !Plugin.EquippedTower(player) || !Plugin.Tower(Plugin.Blocker(player)) ||
                    !player.IsBlocking() || Vector3.Dot(hit.m_dir, player.transform.forward) > 0f) return;
                if (!Plugin.IncomingScales.TryGetValue(hit, out var scale)) return;
                Plugin.IncomingScales.Remove(hit);
                // Native RPC_Damage has already applied multiplayer scaling. Cancel only
                // that multiplier, before armor, stamina and defensive stagger are calculated.
                hit.m_damage.Modify(Rules.MultiplayerCompensation(scale.Multiplier));
            }
            catch (Exception ex) { Plugin.Failure(ex); }
        }
    }

    internal static class BlockPatch
    {
        internal sealed class State
        {
            internal Player Defender;
            internal ItemDrop.ItemData Shield;
            internal float Before;
        }

        internal static void Prefix(Humanoid __instance, HitData hit, Character attacker, out State __state)
        {
            __state = null;
            try
            {
                if (!Plugin.Active || hit == null || !hit.m_blockable || hit.m_ranged || attacker == null || attacker == __instance ||
                    attacker.IsPlayer() || attacker.IsTamed() || Plugin.Boss(attacker) || !Plugin.Owned(__instance)) return;
                var player = __instance as Player;
                if (player == null || player.IsDead() || player.IsStaggering() || !Plugin.EquippedTower(player)) return;
                ItemDrop.ItemData shield = Plugin.Blocker(player);
                if (!Plugin.Tower(shield)) return;
                __state = new State { Defender = player, Shield = shield, Before = hit.GetTotalBlockableDamage() };
            }
            catch (Exception ex) { Plugin.Failure(ex); }
        }

        internal static void Postfix(HitData hit, Character attacker, bool __result, State __state)
        {
            try
            {
                if (!Plugin.Active || __state == null || !__result || hit == null || attacker == null) return;
                Player player = __state.Defender;
                if (!Plugin.Owned(player) || player.IsDead() || player.IsStaggering() ||
                    !ReferenceEquals(Plugin.Blocker(player), __state.Shield)) return;
                // BlockAttack returns true even in some broken-guard cases. Require the actual
                // damage mitigation and remaining defensive resources, not just its bool result.
                if (Rules.Mitigated(__state.Before, hit.GetTotalBlockableDamage(), player.GetStamina(),
                    Plugin.Stagger(player), Plugin.Threshold(player))) Plugin.Reward(attacker, player);
            }
            catch (Exception ex) { Plugin.Failure(ex); }
        }
    }

    internal static class NetworkPatch
    {
        internal static void Postfix(Character __instance)
        {
            try { if (Plugin.Instance != null) { Plugin.Register(__instance); Plugin.ReconcileEliteHealth(__instance); } }
            catch (Exception ex) { Plugin.Failure(ex); }
        }
    }
    internal static class RecoveryPatch
    {
        internal static void Prefix(Character __instance, ref float __0, out float? __state)
        {
            __state = null;
            try
            {
                // Native UpdateStagger decays from max health * factor, not GetStaggerTreshold.
                // Its only use of dt is that decay: compensate dt to retain the pre-bonus rate.
                if (Plugin.EliteHealthApplies(__instance) && Plugin.Owned(__instance))
                    __0 = Rules.RecoveryDelta(__0, Plugin.Instance.EliteHealthBonus.Value);
                if (Plugin.NumbActive(__instance)) __state = Plugin.Stagger(__instance);
            }
            catch (Exception ex) { Plugin.Failure(ex); }
        }
        internal static void Postfix(Character __instance, float? __state)
        {
            try
            {
                // Let native updates run; suppress only decay, preserving new damage and real staggers.
                if (__state.HasValue && Plugin.NumbActive(__instance) && Plugin.Numb.TryGetValue(__instance, out var window))
                    Plugin.Stagger(__instance) = window.Preserve(Time.time, __state.Value, Plugin.Stagger(__instance));
            }
            catch (Exception ex) { Plugin.Failure(ex); }
        }
    }
    internal static class StaggerPatch
    {
        internal static void Postfix(Character __instance) { Plugin.Numb.Remove(__instance); }
    }
    internal static class TooltipPatch
    {
        internal static void Postfix(ItemDrop.ItemData __0, ref string __result)
        {
            if (!Plugin.Active || !Plugin.Tower(__0)) return;
            const string line = "\nEntumecimiento: <color=orange>2s</color>";
            // Native tooltips still contain localization tokens here. Put the effect
            // after block force, in the position normally occupied by the parry bonus.
            int anchor = __result.IndexOf("$item_deflection", StringComparison.Ordinal);
            if (anchor < 0) anchor = __result.IndexOf("$item_blockforce", StringComparison.Ordinal);
            if (anchor < 0) anchor = __result.IndexOf("$item_blockarmor", StringComparison.Ordinal);
            int end = anchor < 0 ? -1 : __result.IndexOf('\n', anchor);
            __result = end < 0 ? __result + line : __result.Insert(end, line);
        }
    }

    internal static class DodgeCostPatch
    {
        internal static void Postfix(Player __instance, ref float __result)
        {
            try { if (Plugin.Active && Plugin.Agile(__instance)) __result *= 1f - Plugin.Instance.DodgeReduction.Value; }
            catch (Exception ex) { Plugin.Failure(ex); }
        }
    }
    internal static class DodgeUpdatePatch
    {
        internal static void Prefix(Player __instance, out bool __state)
        {
            __state = false;
            try { if (Plugin.Active) __state = __instance.InDodge(); }
            catch (Exception ex) { Plugin.Failure(ex); }
        }
        internal static void Postfix(Player __instance, bool __state)
        {
            try
            {
                if (!Plugin.Active || !Plugin.Owned(__instance)) return;
                var state = Plugin.Dodges.GetValue(__instance, _ => new DodgeWindow());
                bool rolling = __instance.InDodge();
                if (!rolling) state.Clear();
                else if (!__state) state.Begin(Plugin.Agile(__instance));
                else if (!Plugin.Agile(__instance)) state.Clear();
            }
            catch (Exception ex) { Plugin.Failure(ex); }
        }
    }
    internal static class DodgeMortalPatch
    {
        internal static void Prefix(Player __instance)
        {
            try
            {
                if (Plugin.Active && Plugin.Owned(__instance) && Plugin.Agile(__instance) &&
                    Plugin.Dodges.TryGetValue(__instance, out var state))
                    state.Mortal(Time.time, Plugin.Instance.DodgeExtension.Value);
            }
            catch (Exception ex) { Plugin.Failure(ex); }
        }
    }
    internal static class DodgeInvinciblePatch
    {
        internal static void Postfix(Player __instance, ref bool __result)
        {
            try
            {
                if (__result || !Plugin.Active || !Plugin.Owned(__instance)) return;
                if (Plugin.Dodges.TryGetValue(__instance, out var state))
                {
                    if (!Plugin.Agile(__instance) || !__instance.InDodge() || __instance.IsDead()) state.Clear();
                    else if (state.Extended(Time.time)) __result = true;
                }
            }
            catch (Exception ex) { Plugin.Failure(ex); }
        }
    }
    internal static class BestBlockerPatch
    {
        internal static void Postfix(Humanoid __instance, ref ItemDrop.ItemData __result)
        {
            try
            {
                if (!Plugin.Active || !(__instance is Player player)) return;
                var weapon = Plugin.RightItem(player);
                var other = Plugin.LeftItem(player);
                if (!Plugin.OneHanded(weapon) || other == null || Plugin.Shield(other)) return;
                float skill = Plugin.Skill(player, Skills.SkillType.Blocking);
                if (__result == null || Plugin.BlockPower(weapon, skill) > Plugin.BlockPower(__result, skill)) __result = weapon;
            }
            catch (Exception ex) { Plugin.Failure(ex); }
        }
    }

    internal static class EquipmentPatch
    {
        internal static void Postfix(Humanoid __instance)
        {
            try { if (Plugin.Instance != null && __instance is Player player) Plugin.Instance.ObserveEquipment(player); }
            catch (Exception ex) { Plugin.Failure(ex); }
        }
    }

    internal static class EliteHealthPatch
    {
        internal static void Postfix(Character __instance, ref float __result)
        {
            try { if (Plugin.EliteHealthApplies(__instance)) __result *= 1f + Plugin.Instance.EliteHealthBonus.Value; }
            catch (Exception ex) { Plugin.Failure(ex); }
        }
    }
    internal static class EliteThresholdPatch
    {
        internal static void Postfix(Character __instance, ref float __result)
        {
            try
            {
                if (Plugin.EliteHealthApplies(__instance))
                    __result /= 1f + Plugin.Instance.EliteHealthBonus.Value;
            }
            catch (Exception ex) { Plugin.Failure(ex); }
        }
    }

}
