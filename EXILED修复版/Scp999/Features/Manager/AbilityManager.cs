using System;
using System.Collections.Generic;
using System.Reflection;
using Exiled.API.Features;
using Scp999.Interfaces;

namespace Scp999.Features.Manager;
public static class AbilityManager
{
    private static List<IAbility> _abilityList = new();
    public static void RegisterAbilities()
    {
        foreach (Type type in Assembly.GetExecutingAssembly().GetTypes())
        {
            try
            {
                if (type.IsInterface || type.IsAbstract || !type.GetInterfaces().Contains(typeof(IAbility)))
                    continue;

                var activator = Activator.CreateInstance(type) as IAbility;
                if (activator != null)
                {
                    _abilityList.Add(activator);
                    
                    Log.Debug($"Register the {activator.Name} ability.");
                    activator.Register();
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error in RegisterAbilities:" + ex.Message);
            }
        }
    }

    public static void UnregisterAbilities()
    {
        foreach (IAbility ability in _abilityList)
        {
            try
            {
                ability.Unregister();
            }
            catch (Exception ex)
            {
                Log.Error("Error in UnregisterAbilities:" + ex.Message);
            }
        }

        // 本轮修复: 必须清空能力列表。否则插件禁用后重新启用时 RegisterAbilities
        // 会把新实例再次 Add 进来，列表中同一能力出现多个实例：
        //  - CooldownController.Awake 的 ToDictionary(a => a.Name) 遇重复键抛 ArgumentException，
        //    冷却系统对所有 SCP-999 玩家直接失效；
        //  - KeybindManager.RegisterKeybinds 会用重复 KeyId 注册重复的键位设置。
        _abilityList.Clear();
    }

    public static List<IAbility> GetAbilities => _abilityList;
}