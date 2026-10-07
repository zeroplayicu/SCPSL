using System;
using System.Collections.Generic;
using Exiled.API.Features;
using MEC;

namespace SCPplaus
{
    public class SCPplaus : Plugin<SCPplausConfig>
    {
        public static SCPplaus Instance;

        public override string Author => "Developer";
        public override string Name => "SCPplaus";
        public override string Prefix => "scpp";
        public override Version Version => new Version(1, 0, 0);
        public override Version RequiredExiledVersion => new Version(9, 0, 0);

        public SCPplausEventHandler EventHandler { get; private set; }
        public Scp079Manager Scp079 { get; private set; }

        private CoroutineHandle _scp079Coroutine;
        private CoroutineHandle _scp939StaminaCoroutine;

        public override void OnEnabled()
        {
            Instance = this;
            EventHandler = new SCPplausEventHandler(this);
            Scp079 = new Scp079Manager(this);
            Exiled.Events.Handlers.Player.Hurt += EventHandler.OnHurt;
            Exiled.Events.Handlers.Player.Hurting += EventHandler.OnHurting;
            Exiled.Events.Handlers.Player.Hurting += EventHandler.OnHurtingEnsureHp;
            Exiled.Events.Handlers.Player.Spawned += EventHandler.OnSpawned;
            Exiled.Events.Handlers.Player.ChangingRole += EventHandler.OnChangingRole;
            // SCP-079 升级系统：击杀获得经验 / 回合结束清空
            // （079 生成初始化已由 OnSpawned 统一处理，原 OnSpawnedWithScp079 重复订阅导致每次生成执行两次）
            Exiled.Events.Handlers.Player.Died += EventHandler.OnDiedWithScp079Exp;
            Exiled.Events.Handlers.Server.RoundEnded += EventHandler.OnRoundEndedWithScp079;

            // 每 1 秒持续应用 SCP-079 的 AP 配置（MEC 主线程执行，防止原生逻辑每帧重置覆盖）
            _scp079Coroutine = Timing.RunCoroutine(Scp079Routine());

            // SCP-3114 掐喉无冷却补丁
            if (Config.Scp3114NoStrangleCooldown)
                Scp3114StranglePatch.Enable();

            // SCP-127 掉落物异常刷屏修复（柜子每帧重试导致卡顿）
            if (Config.Scp127CrashFix)
                Scp127FixPatch.Enable();

            // SCP-939 无限耐力：0.2 秒高频补满，保证耐力条始终满格可一直奔跑
            if (Config.Scp939InfiniteStamina)
                _scp939StaminaCoroutine = Timing.RunCoroutine(Scp939StaminaRoutine());

            base.OnEnabled();
        }

        public override void OnDisabled()
        {
            Scp3114StranglePatch.Disable();
            Scp127FixPatch.Disable();
            if (_scp939StaminaCoroutine.IsRunning)
                Timing.KillCoroutines(_scp939StaminaCoroutine);
            Timing.KillCoroutines(_scp079Coroutine);

            Exiled.Events.Handlers.Player.Hurt -= EventHandler.OnHurt;
            Exiled.Events.Handlers.Player.Hurting -= EventHandler.OnHurting;
            Exiled.Events.Handlers.Player.Hurting -= EventHandler.OnHurtingEnsureHp;
            Exiled.Events.Handlers.Player.Spawned -= EventHandler.OnSpawned;
            Exiled.Events.Handlers.Player.ChangingRole -= EventHandler.OnChangingRole;
            Exiled.Events.Handlers.Player.Died -= EventHandler.OnDiedWithScp079Exp;
            Exiled.Events.Handlers.Server.RoundEnded -= EventHandler.OnRoundEndedWithScp079;
            EventHandler = null;
            Scp079 = null;
            Instance = null;
            base.OnDisabled();
        }

        private IEnumerator<float> Scp079Routine()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(1f);
                Scp079?.RefreshAllScp079();
                // SCP-1509 手持加速（每秒刷新）
                Scp1509SpeedBoost.Tick();
            }
        }

        /// <summary>SCP-939 无限耐力：0.2 秒轮询补满耐力（比 1 秒更平滑，不会出现短暂减速）</summary>
        private IEnumerator<float> Scp939StaminaRoutine()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(0.2f);
                try
                {
                    foreach (var player in Player.List)
                    {
                        if (player == null || !player.IsConnected || !player.IsAlive) continue;
                        if (player.Role.Type != PlayerRoles.RoleTypeId.Scp939) continue;
                        if (player.Stamina < 100f)
                            player.Stamina = 100f;
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug($"[SCPplaus] SCP-939 耐力刷新失败: {ex.Message}");
                }
            }
        }
    }
}
