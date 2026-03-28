using Exiled.API.Features;
using HarmonyLib;
using PlayerRoles;
using PlayerRoles.PlayableScps.Scp3114;
using PlayerStatsSystem;
using EmoteForAll.Classes;
using System.Linq;
using Mirror;
using System.Collections.Generic;
using NorthwoodLib.Pools;
using System.Reflection.Emit;
using EmoteForAll.Types;
using PlayerRoles.PlayableScps.Scp079;
using UnityEngine;

namespace EmoteForAll.Patches
{
    // ServerPlayConditionally
    [HarmonyPatch(typeof(Scp3114VoiceLines), "ServerPlayConditionally")]
    internal static class ServerPlayConditionally
    {
        [HarmonyPrefix]
        private static bool Prefix(Scp3114VoiceLines __instance)
        {
            return !EmoteHandler.emoteAttachedNPC.Values.Contains(Npc.Get(__instance.Owner));
        }
    }

    // HumeShieldStat.Update
    [HarmonyPatch(typeof(HumeShieldStat), "Update")]
    internal static class HumeShieldUpdate
    {
        [HarmonyPrefix]
        private static bool Prefix(HumeShieldStat __instance)
        {
            return !EmoteHandler.emoteAttachedNPC.Values.Contains(Npc.Get(__instance.Hub));
        }
    }

    [HarmonyPatch(typeof(Scp3114Role), nameof(Scp3114Role.TryPreventHitmarker))]
    internal static class TryPreventHitmarker
    {
        [HarmonyPostfix]
        private static void Postfix(Scp3114Role __instance, AttackerDamageHandler adh, ref bool __result)
        {
            __instance.TryGetOwner(out ReferenceHub rhub);
            Npc npc = Npc.Get(rhub);
            if (EmoteHandler.emoteAttachedNPC.Values.Contains(npc))
            {
                __result = !HitboxIdentity.IsDamageable(adh.Attacker.Role, __instance.CurIdentity.StolenRole);
                if (__result)
                {
                    Player attacker = Player.Get(adh.Attacker.Hub);
                    attacker?.ShowHint("<size=40><color=red>STOP!</color></size>\n<size=25>This isn't the Skeleton. It's just an emote. Don't Try Killing it.</size>");
                }
            }
        }
    }

    [HarmonyPatch(typeof(Scp3114Dance), nameof(Scp3114Dance.ServerWriteRpc))]
    internal static class ServerWriteRpc
    {
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            List<CodeInstruction> newInstructions = ListPool<CodeInstruction>.Shared.Rent(instructions);

            Label skip = generator.DefineLabel();

            newInstructions.Add(new CodeInstruction(OpCodes.Ret));
            newInstructions[newInstructions.Count - 1].labels.Add(skip);

            newInstructions.InsertRange(7, new List<CodeInstruction>()
            {
                new CodeInstruction(OpCodes.Br_S, skip),
            });

            foreach (CodeInstruction instruction in newInstructions)
                yield return instruction;

            ListPool<CodeInstruction>.Shared.Return(newInstructions);
        }

        [HarmonyPostfix]
        private static void Postfix(Scp3114Dance __instance, NetworkWriter writer)
        {
            Npc npc = Npc.Get(__instance.Owner);
            if (npc != null && EmoteHandler.emoteAttachedNPC.Values.Contains(npc))
            {
                EmoteHandler handle = npc.GameObject.GetComponent<EmoteHandler>();
                writer.WriteByte((byte)handle.DanceType);
                return;
            }
            writer.WriteByte((byte)Random.Range(0, 255));
        }
    }

    [HarmonyPatch(typeof(PlayerRolesUtils), nameof(PlayerRolesUtils.GetTeam), new[] { typeof(ReferenceHub) })]
    internal static class GetTeam
    {
        [HarmonyPostfix]
        private static void Postfix(ReferenceHub hub, ref Team __result)
        {
            Npc npc = Npc.Get(hub);
            __result = EmoteHandler.emoteAttachedNPC.Values.Contains(npc) ? Team.Dead : __result;
        }
    }

    // IsScpButNot079
    [HarmonyPatch(typeof(Scp079Recontainer), "IsScpButNot079")]
    internal static class IsScpButNot079
    {
        [HarmonyPostfix]
        private static void Postfix(PlayerRoleBase prb, ref bool __result)
        {
            prb.TryGetOwner(out ReferenceHub rhub);
            Npc npc = Npc.Get(rhub);
            if (EmoteHandler.emoteAttachedNPC.Values.Contains(npc))
                __result = false;
        }
    }
}