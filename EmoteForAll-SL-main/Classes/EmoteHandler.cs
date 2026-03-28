using EmoteForAll.Types;
using Exiled.API.Features;
using Mirror;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using PlayerRoles.PlayableScps.Scp3114;
using RelativePositioning;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

using GameScp3114Role = PlayerRoles.PlayableScps.Scp3114.Scp3114Role;

namespace EmoteForAll.Classes
{
    public class EmoteHandler : MonoBehaviour
    {
        private static readonly FieldInfo NextDanceIdField =
            typeof(Scp3114Dance).GetField("_nextDanceId", BindingFlags.NonPublic | BindingFlags.Instance);
        // ServerProcessCmd 
        private static readonly MethodInfo ServerProcessCmdMethod =
            typeof(Scp3114Dance).GetMethod("ServerProcessCmd", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, new[] { typeof(NetworkReader) }, null);

        private static readonly PropertyInfo RagdollProp =
            typeof(GameScp3114Role).GetProperty("Ragdoll", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo FreeIdsField =
            typeof(RecyclablePlayerId).GetField("FreeIds", BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly FieldInfo AutoIncrementField =
            typeof(RecyclablePlayerId).GetField("_autoIncrement", BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly FieldInfo IsNpcField =
            typeof(Player).GetField("_isNPC", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? typeof(Player).GetField("isNpc", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? typeof(Player).GetField("IsNpc", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? typeof(Player).GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(f => f.Name.ToLower().Contains("isnpc"));
        
        private static readonly FieldInfo IdentityField =
            typeof(GameScp3114Role).GetField("_identity", BindingFlags.NonPublic | BindingFlags.Instance);
        
        private static readonly FieldInfo CurIdentityField =
            typeof(Scp3114Identity).GetField("CurIdentity", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo StolenIdentityStatusField =
            typeof(Scp3114Identity.StolenIdentity).GetField("Status", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?? typeof(Scp3114Identity.StolenIdentity).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(f => f.Name.ToLower().Contains("status") || f.Name.ToLower().Contains("disguise"));

        private static Scp3114Identity GetIdentity(GameScp3114Role role)
            => IdentityField?.GetValue(role) as Scp3114Identity;
        
        private static Scp3114Dance GetDance(GameScp3114Role role)
        {
            if (role.SubroutineModule.TryGetSubroutine(out Scp3114Dance dance))
                return dance;
            return null;
        }

        private static void SetDisguiseStatus(GameScp3114Role role, object status)
        {
            Scp3114Identity identity = GetIdentity(role);
            if (identity == null) return;
            // CurIdentity.Status
            if (CurIdentityField != null && StolenIdentityStatusField != null)
            {
                object curIdentity = CurIdentityField.GetValue(identity);
                if (curIdentity != null)
                    StolenIdentityStatusField.SetValue(curIdentity, status);
            }
        }

        // Backing fields 

        private static void StartDance(Scp3114Dance dance, Scp3114DanceType danceType)
        {
            // +++++
            NextDanceIdField?.SetValue(dance, (byte)((int)danceType % 256));

            // Command emulate
            NetworkWriterPooled cmdWriter = NetworkWriterPool.Get();
            try
            {
                cmdWriter.WriteBool(true); 
                ArraySegment<byte> segment = cmdWriter.ToArraySegment();
                NetworkReaderPooled cmdReader = NetworkReaderPool.Get(segment);
                try
                {
                    ServerProcessCmdMethod?.Invoke(dance, new object[] { cmdReader });
                }
                finally
                {
                    NetworkReaderPool.Return(cmdReader);
                }
            }
            finally
            {
                NetworkWriterPool.Return(cmdWriter);
            }
        }

        // gamerole
        private static GameScp3114Role GetGameRole(Npc npc)
            => npc.ReferenceHub.roleManager.CurrentRole as GameScp3114Role;

        private static readonly Dictionary<string, Npc> EmoteAttachedNpc = [];

        // Another shit
        // ReSharper disable once InconsistentNaming
        public static Dictionary<string, Npc> emoteAttachedNPC => EmoteAttachedNpc;

        public static bool MakePlayerEmote(Player plr, Scp3114DanceType danceType)
        {
            if (plr.Role.Team == Team.Dead || plr.Role.Team == Team.SCPs) return false;

            if (EmoteAttachedNpc.TryGetValue(plr.UserId, out Npc existingNpc))
            {
                EmoteHandler handler = existingNpc.GameObject.GetComponent<EmoteHandler>();
                GameScp3114Role scpRole = GetGameRole(existingNpc);
                if (scpRole != null)
                {
                    handler.DanceType = danceType;
                    Scp3114Dance dance = GetDance(scpRole);
                    if (dance != null) StartDance(dance, danceType);
                    return true;
                }
                return false;
            }

            Npc emoteNpc = SpawnFix($"{plr.Nickname}-emote", RoleTypeId.Scp3114, position: new Vector3(-9999f, -9999f, -9999f));
            EmoteAttachedNpc.Add(plr.UserId, emoteNpc);

            Round.IgnoredPlayers.Add(emoteNpc.ReferenceHub);

            plr.ShowHint("<size=450>\n</size><color=yellow><size=35>Initializing Emote...</color>\n</size><size=25>Please Wait 5 Seconds...</size>", 5f);

            ReferenceHub referenceHub = plr.ReferenceHub;
            string uid = plr.UserId;

            // corutiner
            CoroutineRunner runner = emoteNpc.GameObject.AddComponent<CoroutineRunner>();

            runner.StartCoroutine(
                DelayedAction(0.5f, () =>
                {
                    emoteNpc.Health = 9999f;
                    emoteNpc.HumeShield = 0f;
                    emoteNpc.Scale = plr.Scale;
                    GameScp3114Role scpRole = GetGameRole(emoteNpc);
                    if (scpRole != null)
                    {
                        Ragdoll ragdoll = Ragdoll.CreateAndSpawn(
                            plr.Role.Type,
                            plr.DisplayNickname,
                            "Placeholder",
                            new Vector3(-9999f, -9999f, -9999f),
                            plr.Rotation);
                        // ragdoll.Base 
                        RagdollProp?.SetValue(scpRole, ragdoll.Base);

                        // DisguiseStatus with StolenIdentity.Status
                        var stolenIdentityStatusType = typeof(Scp3114Identity.StolenIdentity).Assembly
                            .GetType("PlayerRoles.PlayableScps.Scp3114.Scp3114Identity+StolenIdentity+DisguiseStatus")
                            ?? typeof(Scp3114Identity.StolenIdentity).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
                                .FirstOrDefault(t => t.Name.Contains("Status") || t.Name.Contains("Disguise"));
                        if (stolenIdentityStatusType != null && Enum.GetNames(stolenIdentityStatusType).Contains("Equipping"))
                        {
                            object equippingValue = Enum.Parse(stolenIdentityStatusType, "Equipping");
                            SetDisguiseStatus(scpRole, equippingValue);
                        }
                    }
                })
            );

            runner.StartCoroutine(
                DelayedAction(5f, () =>
                {
                    Player owner = Player.Get(uid);
                    Vector3 targetPos = owner != null ? owner.Position : Vector3.zero;
                    emoteNpc.Position = targetPos;

                    // fpc fix
                    var fpc = ((IFpcRole)emoteNpc.ReferenceHub.roleManager.CurrentRole)?.FpcModule;
                    if (fpc != null)
                    {
                        fpc.ServerOverridePosition(targetPos);
                    }

                    EmoteHandler handler = emoteNpc.GameObject.AddComponent<EmoteHandler>();
                    handler.PlayerOwner = referenceHub;
                    handler.NpcOwner = emoteNpc;
                    handler.OwnerUserId = uid;

                    GameScp3114Role scpRole = GetGameRole(emoteNpc);
                    if (scpRole != null)
                    {
                        handler.DanceType = danceType;
                        Scp3114Dance dance = GetDance(scpRole);
                        if (dance != null) StartDance(dance, danceType);
                    }

                    Vector3 realScale = referenceHub.transform.localScale;
                    referenceHub.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
                    foreach (Player item in Player.List)
                    {
                        if (item.UserId != uid)
                            Server.SendSpawnMessage?.Invoke(null, [plr.NetworkIdentity, item.Connection]);
                    }
                    referenceHub.transform.localScale = realScale;
                })
            );

            return true;
        }

        private static IEnumerator DelayedAction(float seconds, Action action)
        {
            yield return new WaitForSeconds(seconds);
            action?.Invoke();
        }

        private static Npc SpawnFix(string name, RoleTypeId role, int id = 0, Vector3? position = null)
        {
            GameObject gameObject = UnityEngine.Object.Instantiate(NetworkManager.singleton.playerPrefab);
            Npc npc = new Npc(gameObject);
            // IsNPC shit
            IsNpcField?.SetValue(npc, true);

            try
            {
                npc.ReferenceHub.roleManager.InitializeNewRole(RoleTypeId.None, RoleChangeReason.None);
            }
            catch (Exception arg)
            {
                Log.Debug($"Ignore: {arg}");
            }

            var freeIds = FreeIdsField?.GetValue(null);
            var containsMethod = freeIds?.GetType().GetMethod("Contains");
            var removeMethod = freeIds?.GetType().GetMethod("RemoveFromQueue");
            bool contained = freeIds != null && (bool)(containsMethod?.Invoke(freeIds, [id]) ?? false);

            if (contained)
            {
                removeMethod?.Invoke(freeIds, [id]);
            }
            else
            {
                int autoIncrement = (int)(AutoIncrementField?.GetValue(null) ?? 0);
                if (autoIncrement >= id)
                {
                    id = ++autoIncrement;
                    AutoIncrementField?.SetValue(null, autoIncrement);
                }
            }

            NetworkServer.AddPlayerForConnection(new Exiled.API.Features.Components.FakeConnection(id), gameObject);
            try
            {
                npc.ReferenceHub.authManager.SyncedUserId = null;
            }
            catch (Exception e)
            {
                Log.Debug($"Ignore: {e}");
            }

            npc.ReferenceHub.nicknameSync.Network_myNickSync = name;
            Player.Dictionary.Add(gameObject, npc);

            npc.GameObject.AddComponent<EmoteSpawnHelper>().Init(npc, role, position);

            return npc;
        }

        private static (ushort horizontal, ushort vertical) ToClientUShorts(Quaternion rotation)
        {
            if (rotation.eulerAngles.z != 0f)
                rotation = Quaternion.LookRotation(rotation * Vector3.forward, Vector3.up);

            float horizontalAngle = rotation.eulerAngles.y;
            float verticalAngle = -rotation.eulerAngles.x;

            if (verticalAngle < -90f)
                verticalAngle += 360f;
            else if (verticalAngle > 270f)
                verticalAngle -= 360f;

            return (ToHorizontal(horizontalAngle), ToVertical(verticalAngle));

            static ushort ToHorizontal(float h)
            {
                const float toHorizontal = 65535f / 360f;
                return (ushort)Mathf.RoundToInt(Mathf.Clamp(h, 0f, 360f) * toHorizontal);
            }

            static ushort ToVertical(float v)
            {
                const float toVertical = 65535f / 176f;
                return (ushort)Mathf.RoundToInt((Mathf.Clamp(v, -88f, 88f) + 88f) * toVertical);
            }
        }

        private static void LookAt(Npc npc, Vector3 position)
        {
            Vector3 direction = position - npc.Position;
            Quaternion quat = Quaternion.LookRotation(direction, Vector3.up);
            var mouseLook = ((IFpcRole)npc.ReferenceHub.roleManager.CurrentRole).FpcModule.MouseLook;
            (ushort horizontal, ushort vertical) = ToClientUShorts(quat);
            mouseLook.ApplySyncValues(horizontal, vertical);
        }

        // ====================================================================================================
        // COMPONENT STUFF
        // ====================================================================================================

        public ReferenceHub PlayerOwner { get; set; }
        public string OwnerUserId { get; set; }
        public Npc NpcOwner { get; set; }
        public Scp3114DanceType DanceType { get; set; }

        private bool _keepRunning = true;
        private long _lastHintTime;

        private void Start()
        {
            StartCoroutine(CheckIfKill());
        }

        private IEnumerator CheckIfKill()
        {
            while (_keepRunning)
            {
                yield return new WaitForSeconds(0.1f);

                try
                {
                    long currentTime = (long)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;

                    Player plr = Player.Get(OwnerUserId);
                    if (plr == null)
                    {
                        KillEmote(true);
                        continue;
                    }
                    if (plr.Role.IsDead)
                    {
                        KillEmote();
                        continue;
                    }
                    if (plr.Scale != NpcOwner.Scale)
                    {
                        Vector3 realScale = plr.ReferenceHub.transform.localScale;
                        plr.ReferenceHub.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
                        foreach (Player item in Player.List)
                        {
                            if (item.UserId != plr.UserId)
                                Server.SendSpawnMessage?.Invoke(null, [plr.NetworkIdentity, item.Connection]);
                        }
                        plr.ReferenceHub.transform.localScale = realScale;
                        NpcOwner.Scale = plr.Scale;
                    }
                    // if move detected
                    if (Vector3.Distance(NpcOwner.Position, PlayerOwner.transform.position) > 0.05f)
                    {
                        KillEmote();
                        continue;
                    }

                    LookAt(NpcOwner, plr.CameraTransform.forward + plr.CameraTransform.position);

                    NpcOwner.Health = 9999f;
                    NpcOwner.HumeShield = 0f;

                    if (currentTime - _lastHintTime > 500)
                    {
                        _lastHintTime = currentTime;
                        plr.ShowHint($"<size=450>\n</size><size=35>Current Emote: <color=yellow>{Enum.GetName(typeof(Scp3114DanceType), DanceType)}</color></size>\n<size=25><color=red>[Cancel]</color> by Moving.\nUse '.emote list' to see Available Emotes.</size>", 0.75f);
                    }
                }
                catch (Exception)
                {
                    // Ignored
                }
            }
        }

        // ReSharper disable once MethodOverloadWithOptionalParameter
        public void KillEmote(bool skipOwner = false, float plrDamage = 0f)
        {
            if (!_keepRunning) return;

            _keepRunning = false;
            NpcOwner.Position = new Vector3(-9999f, -9999f, -9999f);

            if (!skipOwner)
            {
                Player ownerPlr = Player.Get(PlayerOwner);
                ownerPlr?.ShowHint($"<size=450>\n</size><color=red><size=35>Emote Cancelled.</size></color>\n<size=25>{(plrDamage != 0 ? "(You took damage from Someone)" : "(You moved)")}</size>", 3f);

                if (ownerPlr != null)
                {
                    foreach (Player item in Player.List)
                    {
                        if (item.UserId != OwnerUserId)
                            Server.SendSpawnMessage?.Invoke(null, [ownerPlr.NetworkIdentity, item.Connection]);
                    }

                    if (plrDamage != 0)
                    {
                        ownerPlr.Health -= plrDamage;
                        if (ownerPlr.Health <= 0) ownerPlr.Health = 1;
                    }
                }
            }

            StartCoroutine(DelayedKill(0.5f));
        }

        private IEnumerator DelayedKill(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            EmoteAttachedNpc.Remove(OwnerUserId);
            if (NpcOwner?.GameObject != null)
                NetworkServer.Destroy(NpcOwner.GameObject);
        }
    }

    // Простой MonoBehaviour для запуска корутин — добавляется к GameObject сразу при создании
    internal class CoroutineRunner : MonoBehaviour { }

    internal class EmoteSpawnHelper : MonoBehaviour
    {
        private Npc _npc;
        private RoleTypeId _role;
        private Vector3? _position;

        public void Init(Npc npcRef, RoleTypeId roleType, Vector3? pos)
        {
            _npc = npcRef;
            _role = roleType;
            _position = pos;
            StartCoroutine(DoSpawn());
        }

        private IEnumerator DoSpawn()
        {
            yield return new WaitForSeconds(0.3f);
            _npc.Role.Set(_role, Exiled.API.Enums.SpawnReason.ForceClass, _position.HasValue ? RoleSpawnFlags.AssignInventory : RoleSpawnFlags.All);
            _npc.ClearInventory();

            if (_position.HasValue)
            {
                yield return new WaitForSeconds(0.2f);
                _npc.Position = _position.Value;
            }

            Destroy(this);
        }
    }
}