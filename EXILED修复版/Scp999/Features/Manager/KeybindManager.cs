using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features.Core.UserSettings;

namespace Scp999.Features.Manager;
public static class KeybindManager
{
    private static IEnumerable<SettingBase> _settings;
    
    public static void RegisterKeybinds()
    {
        var settings = new List<SettingBase>();
            
        var header = new HeaderSetting(
            name: "SCP-999 技能按键",
            hintDescription: "请在下方为每个技能绑定按键",
            paddling: true
        );

        settings.Add(header);

        foreach (var ability in AbilityManager.GetAbilities.OrderBy(r => r.KeyId))
        {
            var keybindSetting = new KeybindSetting(
                id: ability.KeyId,
                label: ability.Name,
                suggested: ability.SuggestedKey,
                hintDescription: ability.Description,
                preventInteractionOnGUI: true
                //header: header
            );

            settings.Add(keybindSetting);
        }

        _settings = settings;
        
        SettingBase.Register(_settings);
        SettingBase.SendToAll();
    }

    public static void UnregisterKeybinds()
    {
        SettingBase.Unregister(settings: _settings);
    }
}