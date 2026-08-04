using Celeste.Mod;

namespace Celeste.Mod.AndroidPort;

public sealed class AndroidPortSettings : EverestModuleSettings {
    public bool TouchControls { get; set; } = true;
    public bool JoystickMode { get; set; } = true;
    public bool JoystickSnap8Way { get; set; } = true;
    public bool HapticFeedback { get; set; } = true;
    public bool CameraCentering { get; set; } = true;
}
