using System;
using Exiled.API.Features;
using Exiled.API.Enums;
using Player = Exiled.API.Features.Player;

namespace ExpWebBate
{
    public class ExpWebBate : Plugin<ExpWebConfig>
    {
        public static ExpWebBate Instance;
        public WebServer WebSrv;
        public DataStore Store;
        public AuthManager Auth;
        public PlayerDataManager ExpData;
        public ForumManager Forum;

        public override string Author => "Developer";
        public override string Name => "ExpWebBate";
        public override string Prefix => "expweb";
        public override PluginPriority Priority => PluginPriority.Low;
        public override Version Version => new Version(1, 0, 0);
        public override Version RequiredExiledVersion => new Version(9, 0, 0);

        public override void OnEnabled()
        {
            Instance = this;
            try
            {
                // 初始化数据存储（使用 JSON 文件）
                Store = new DataStore(Config);
                Store.Initialize();

                // 初始化管理员认证
                Auth = new AuthManager(Store, Config);

                // 获取经验插件数据管理器（通过反射）
                ExpData = new PlayerDataManager();

                // 启动 Web 服务器
                Forum = new ForumManager(Paths.Configs);

                WebSrv = new WebServer(Config, Store, Auth, ExpData, Forum);
                WebSrv.Start();
            }
            catch (Exception ex)
            {
                Log.Error("[ExpWebBate] 初始化失败: " + ex.Message + "\n" + ex.StackTrace);
            }

            base.OnEnabled();
        }

        public override void OnDisabled()
        {
            if (WebSrv != null)
            {
                WebSrv.Stop();
                WebSrv = null;
            }
            Instance = null;
            base.OnDisabled();
        }
    }
}
