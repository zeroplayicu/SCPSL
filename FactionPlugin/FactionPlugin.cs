using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using MEC;

namespace FactionPlugin
{
    public class FactionPlugin : Plugin<FactionConfig>
    {
        public static FactionPlugin Instance { get; private set; }

        /// <summary>
        /// 特殊角色（GOC / SCP-999 / SCP-181 / 阵营人数 HUD）只在 7779 实例启用。
        /// 用户要求（2026-10-02）：7778 不刷特殊角色。
        /// </summary>
        public static bool SpecialRolesEnabled => Server.Port == 7779;

        private FactionEventHandler _eventHandler;
        private SpecialRoleSssSettings _sssSettings;
        private Scp999HudDisplay _hudDisplay;
        private CoroutineHandle _cooldownCoroutine;
        private int _musicTick;
        private CoroutineHandle _gocCoroutine;
        private CoroutineHandle _hsCoroutine;

        public override string Name => "FactionPlugin";
        public override string Author => "Developer";
        public override Version Version => new Version(1, 0, 0);

        public override void OnEnabled()
        {
            Instance = this;
            _hudDisplay = new Scp999HudDisplay(this);
            _eventHandler = new FactionEventHandler(this, _hudDisplay);
            _sssSettings = new SpecialRoleSssSettings(this);
            // GOC 技能按键绑定（技能1 / 技能2）

            Exiled.Events.Handlers.Player.Spawned += _eventHandler.OnSpawned;
            Exiled.Events.Handlers.Player.Hurting += _eventHandler.OnHurting;
            Exiled.Events.Handlers.Player.Hurt += _eventHandler.OnHurt;
            Exiled.Events.Handlers.Player.Died += _eventHandler.OnDied;
            Exiled.Events.Handlers.Player.Left += _eventHandler.OnLeft;
            Exiled.Events.Handlers.Player.InteractingDoor += _eventHandler.OnInteractingDoor;
            Exiled.Events.Handlers.Server.RoundStarted += _eventHandler.OnRoundStarted;
            // 回合开始清空所有广播（用户要求一进去就别一堆提示）
            Exiled.Events.Handlers.Server.RoundStarted += OnRoundStartedClearBroadcasts;
            // GOC 阵营：回合结束清理
            Exiled.Events.Handlers.Server.RoundEnded += OnRoundEndedGoc;
            Exiled.Events.Handlers.Server.RoundEnded += OnRoundEndedNu7;
            Exiled.Events.Handlers.Server.RoundStarted += OnRoundStartedStations;
            // 核弹期间非战斗人员撤离 → 强制转观察者
            Exiled.Events.Handlers.Player.Escaped += OnPlayerEscapedNuke;
            // GOC 核弹任务（轮询方案，无 WarheadStarted/Stopping 事件）
            Exiled.Events.Handlers.Player.ActivatingWarheadPanel += GocNukeMission.OnActivatingWarheadPanel;
            // SCP-181 幸运儿
            Exiled.Events.Handlers.Player.InteractingDoor += Scp181Manager.OnInteractingDoor;
            Exiled.Events.Handlers.Player.InteractingLocker += Scp181Manager.OnInteractingLocker;
            Exiled.Events.Handlers.Player.PickingUpItem += Scp181Manager.OnPickingUpItem;
            Exiled.Events.Handlers.Player.Hurting += Scp181Manager.OnHurting;
            Exiled.Events.Handlers.Player.Verified += OnVerified;
            Exiled.Events.Handlers.Player.PickingUpItem += OnPickingUpItem;

            _cooldownCoroutine = Timing.RunCoroutine(CooldownRoutine());
            // GOC 阵营：对局中期刷新检查
            _gocCoroutine = Timing.RunCoroutine(GocSpawnRoutine());
            // GOC 护盾高频恢复（0.2 秒一次，对抗人类角色 Hume Shield 的原生衰减）
            _hsCoroutine = Timing.RunCoroutine(HumeShieldRoutine());
            // 奇术师特制 A7
            Exiled.Events.Handlers.Player.Shooting += _eventHandler.OnShooting;
            // 特战囚鸟：无法蓄力 / 打不爆
            Exiled.Events.Handlers.Item.ChargingJailbird += _eventHandler.OnChargingJailbird;
            Exiled.Events.Handlers.Item.JailbirdChangingWearState += _eventHandler.OnJailbirdChangingWear;

            Log.Info("FactionPlugin 已启用");
            base.OnEnabled();
        }

        /// <summary>回合结束：清理 GOC 阵营记录</summary>
        /// <summary>回合开始：按配置放置点歌台（2026-10-06）</summary>
        private void OnRoundStartedStations()
        {
            try
            {
                if (!FactionPlugin.SpecialRolesEnabled) return;
                MusicStation.ClearMarkers();
                var list = Config.MusicStationPositions;
                if (list == null) return;
                foreach (var entry in list)
                {
                    var parts = entry.Split(',');
                    if (parts.Length != 3) continue;
                    if (float.TryParse(parts[0].Trim(), out float x) &&
                        float.TryParse(parts[1].Trim(), out float y) &&
                        float.TryParse(parts[2].Trim(), out float z))
                    {
                        MusicStation.SpawnMarker(new UnityEngine.Vector3(x, y, z));
                    }
                }
            }
            catch (Exception ex) { Log.Warn($"[点歌台] 自动放置失败: {ex.Message}"); }
        }

        /// <summary>核弹任务期间有人撤离 → 强制转观察者（GocNukeMission 内部判断任务是否进行中）</summary>
        private void OnPlayerEscapedNuke(EscapedEventArgs ev)
        {
            if (ev.Player == null) return;
            GocNukeMission.OnPlayerEscaped(ev.Player);
        }

        /// <summary>回合结束：清理 NU7-A 状态</summary>
        private void OnRoundEndedNu7(Exiled.Events.EventArgs.Server.RoundEndedEventArgs ev)
        {
            try { Nu7Manager.OnRoundEnded(); } catch { }
        }

        private void OnRoundEndedGoc(Exiled.Events.EventArgs.Server.RoundEndedEventArgs ev)
        {
            GocManager.OnRoundEnded();
            GocNukeMission.Reset();
            Scp181Manager.OnRoundEnded();
        }

        /// <summary>回合开始：清掉所有累积的广播/字幕（用户要求"一进去就出现好多提示"全部不显示）</summary>
        private void OnRoundStartedClearBroadcasts()
        {
            try
            {
                foreach (var p in Player.List)
                {
                    if (p == null || !p.IsConnected) continue;
                    p.ClearBroadcasts();
                }
                Map.ClearBroadcasts();
            }
            catch { }
        }

        public override void OnDisabled()
        {
            Exiled.Events.Handlers.Player.Spawned -= _eventHandler.OnSpawned;
            Exiled.Events.Handlers.Player.Shooting -= _eventHandler.OnShooting;
            Exiled.Events.Handlers.Item.ChargingJailbird -= _eventHandler.OnChargingJailbird;
            Exiled.Events.Handlers.Item.JailbirdChangingWearState -= _eventHandler.OnJailbirdChangingWear;
            Exiled.Events.Handlers.Player.Hurting -= _eventHandler.OnHurting;
            Exiled.Events.Handlers.Player.Died -= _eventHandler.OnDied;
            Exiled.Events.Handlers.Player.Left -= _eventHandler.OnLeft;
            Exiled.Events.Handlers.Player.InteractingDoor -= _eventHandler.OnInteractingDoor;
            Exiled.Events.Handlers.Server.RoundStarted -= _eventHandler.OnRoundStarted;
            Exiled.Events.Handlers.Server.RoundStarted -= OnRoundStartedClearBroadcasts;
            Exiled.Events.Handlers.Server.RoundEnded -= OnRoundEndedGoc;
            Exiled.Events.Handlers.Server.RoundEnded -= OnRoundEndedNu7;
            Exiled.Events.Handlers.Server.RoundStarted -= OnRoundStartedStations;
            Exiled.Events.Handlers.Player.Escaped -= OnPlayerEscapedNuke;
            Exiled.Events.Handlers.Player.ActivatingWarheadPanel -= GocNukeMission.OnActivatingWarheadPanel;
            Exiled.Events.Handlers.Player.InteractingDoor -= Scp181Manager.OnInteractingDoor;
            Exiled.Events.Handlers.Player.InteractingLocker -= Scp181Manager.OnInteractingLocker;
            Exiled.Events.Handlers.Player.PickingUpItem -= Scp181Manager.OnPickingUpItem;
            Exiled.Events.Handlers.Player.Hurting -= Scp181Manager.OnHurting;
            Exiled.Events.Handlers.Player.Verified -= OnVerified;
            Exiled.Events.Handlers.Player.PickingUpItem -= OnPickingUpItem;

            _sssSettings?.RemoveFromAll();
            _hudDisplay?.ClearAllCache();
            GocSkillManager.Reset();

            if (_cooldownCoroutine.IsRunning)
                Timing.KillCoroutines(_cooldownCoroutine);
            if (_gocCoroutine.IsRunning)
                Timing.KillCoroutines(_gocCoroutine);
            if (_hsCoroutine.IsRunning)
                Timing.KillCoroutines(_hsCoroutine);

            Scp999Manager.Reset();
            Scp999SkillManager.Reset();

            Instance = null;
            Log.Info("FactionPlugin 已禁用");
            base.OnDisabled();
        }

        private void OnVerified(VerifiedEventArgs ev)
        {
            // 特殊角色技能按键只在 7779 注册
            if (ev.Player != null && ev.Player.IsConnected && SpecialRolesEnabled)
                _sssSettings?.SendToPlayer(ev.Player);
        }

        private void OnPickingUpItem(PickingUpItemEventArgs ev)
        {
            if (ev.Player == null || ev.Pickup == null) return;

            // SCP-999 只允许拾取药品
            // SCP-999 可以拾取灯和武器（武器攻击转为治疗）
            if (Scp999Manager.IsScp999(ev.Player) && !Scp999Manager.IsAllowedItem(ev.Pickup.Type) && !_eventHandler.IsScp999Weapon(ev.Pickup.Type))
            {
                ev.IsAllowed = false;
                ev.Player.ShowHint("<color=#FF4444>[SCP-999] 你只能拾取灯和武器</color>", 1.5f);
            }
        }

        private IEnumerator<float> CooldownRoutine()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(1f);

                // 刷新所有SCP-999玩家的技能冷却倒计时
                foreach (var player in Player.List.ToList())
                {
                    if (player == null || !player.IsConnected) continue;
                    if (!Scp999Manager.IsScp999(player)) continue;
                    _sssSettings?.UpdateCooldown(player);
                    _hudDisplay?.Refresh(player);
                }

                // SCP-999 治疗周围队友（主线程协程内执行，替代原 System.Timers.Timer 线程池回调）
                Scp999Manager.HealNearbyAllies();

                // SCP-999 持灯生命恢复（每秒 +3 HP，离开灯后 3 秒宽限）
                Scp999Manager.UpdateLanternHeal();

                // GOC 核弹任务监控（启动检测 / 关闭拦截 / 撤离计时）
                GocNukeMission.MonitorTick();

                // 特殊角色（GOC / SCP-999）身份与技能介绍（显示在等级状态栏正下方）
                foreach (var p in Player.List.ToList())
                {
                    SpecialRoleInfoDisplay.Refresh(p);
                    FactionCountDisplay.Refresh(p);   // 右下角阵营存活人数
                }
            }
        }

        /// <summary>GOC 护盾恢复：0.1 秒 +0.5（每秒 5），需站立不动 5 秒以上才开始回盾</summary>
        private IEnumerator<float> HumeShieldRoutine()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(0.1f);
                GocManager.TickShieldRegen(0.5f);
                // 奇术师 A7 直接开火检测（弹匣轮询）
                GocSkillManager.TickThaumaturgeFire();
                // GOC 枪械开火检测（Tutorial 原生伤害不可靠，射线补伤害）
                GocGunFireWatcher.Tick();
                // 点歌台靠近检测（每 5 拍 = 0.5 秒一次）
                if (++_musicTick >= 5) { _musicTick = 0; MusicStation.Tick(); }
            }
        }

        /// <summary>GOC 阵营刷新检查：对局中期自动刷一次（不检查存活人数，可被 sx goc 重复触发）</summary>
        private IEnumerator<float> GocSpawnRoutine()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(5f);
                try
                {
                    if (!Config.GocEnabled) continue;
                    if (!Round.IsStarted) continue;
                    if (GocManager.Members.Count > 0) continue;      // 场上已有 GOC 就不自动补刷
                    if (Round.ElapsedTime.TotalSeconds < Config.GocSpawnDelay) continue;

                    GocManager.TrySpawnGoc();
                }
                catch (Exception ex)
                {
                    Log.Debug($"[GOC] 刷新检查失败: {ex.Message}");
                }
            }
        }
    }
}
