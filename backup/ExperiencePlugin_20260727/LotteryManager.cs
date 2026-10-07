using System;
using System.Collections.Generic;
using System.Linq;

namespace ExperiencePlugin
{
    /// <summary>
    /// 积分抽奖管理器 — 权重概率抽奖
    /// </summary>
    public class LotteryManager
    {
        private readonly Random _rng = new Random();

        /// <summary>单个奖品定义</summary>
        public class Prize
        {
            public string Name { get; set; }
            public string Type { get; set; } // "xp" / "points" / "vip" / "svip"
            public double Weight { get; set; }
            public int Value { get; set; }   // xp数量 / 积分数量
            public int Days { get; set; }    // VIP天数
        }

        /// <summary>一次抽奖的结果</summary>
        public class DrawResult
        {
            public Prize Prize { get; set; }
            public int Index { get; set; } // 连抽时第几次
        }

        public List<Prize> Pool { get; }

        public double TotalWeight { get; private set; }

        private readonly ExperienceConfig _config;

        public LotteryManager(ExperienceConfig config)
        {
            _config = config;
            Pool = BuildPool();
            TotalWeight = Pool.Sum(p => p.Weight);
        }

        private List<Prize> BuildPool()
        {
            return new List<Prize>
            {
                // XP奖品
                new Prize { Name = "10 经验",  Type = "xp",     Weight = 25,  Value = 10 },
                new Prize { Name = "50 经验",  Type = "xp",     Weight = 20,  Value = 50 },
                new Prize { Name = "100 经验", Type = "xp",     Weight = 10,  Value = 100 },
                new Prize { Name = "1000 经验",Type = "xp",     Weight = 1,   Value = 1000 },
                // 积分奖品
                new Prize { Name = "1 积分",   Type = "points", Weight = 50,  Value = 1 },
                new Prize { Name = "10 积分",  Type = "points", Weight = 35,  Value = 10 },
                new Prize { Name = "100 积分", Type = "points", Weight = 20,  Value = 100 },
                new Prize { Name = "1000 积分",Type = "points", Weight = 1,   Value = 1000 },
                // VIP奖品
                new Prize { Name = "VIP 1天",  Type = "vip",    Weight = 15,  Days = 1,  Value = 0 },
                new Prize { Name = "VIP 15天", Type = "vip",    Weight = 10,  Days = 15, Value = 0 },
                new Prize { Name = "VIP 30天", Type = "vip",    Weight = 5,   Days = 30, Value = 0 },
                new Prize { Name = "SVIP 1天", Type = "svip",   Weight = 10,  Days = 1,  Value = 0 },
                new Prize { Name = "SVIP 15天",Type = "svip",   Weight = 5,   Days = 15, Value = 0 },
                new Prize { Name = "SVIP 30天",Type = "svip",   Weight = 0.1, Days = 30, Value = 0 },
            };
        }

        /// <summary>
        /// 执行一次抽奖
        /// </summary>
        public Prize DrawOne()
        {
            double roll = _rng.NextDouble() * TotalWeight;
            double cumulative = 0;
            foreach (var prize in Pool)
            {
                cumulative += prize.Weight;
                if (roll <= cumulative)
                    return prize;
            }
            return Pool[0]; // fallback
        }

        /// <summary>
        /// 执行多次抽奖，返回结果列表
        /// </summary>
        public List<DrawResult> Draw(int count)
        {
            var results = new List<DrawResult>();
            for (int i = 0; i < count; i++)
            {
                results.Add(new DrawResult
                {
                    Prize = DrawOne(),
                    Index = i + 1
                });
            }
            return results;
        }
    }
}
