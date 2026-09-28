namespace Slyth.Core.Tweaks;

public enum Preset
{
    Safe,
    Gamer,
    Extreme,
}

public static class Presets
{
    /// <summary>Um modo inclui todos os níveis até ele; ajustes Manual nunca entram.</summary>
    public static bool Includes(this Preset preset, TweakLevel level) => level switch
    {
        TweakLevel.Safe => true,
        TweakLevel.Gamer => preset >= Preset.Gamer,
        TweakLevel.Extreme => preset == Preset.Extreme,
        _ => false,
    };

    public static string DisplayName(this Preset preset) => preset switch
    {
        Preset.Safe => "Seguro",
        Preset.Gamer => "Gamer",
        _ => "Extremo",
    };

    public static string Description(this Preset preset) => preset switch
    {
        Preset.Safe => "Só ajustes sem efeito colateral. Ideal para qualquer PC.",
        Preset.Gamer => "Tudo do Seguro + ajustes de jogos, rede e entrada para máximo FPS e menor latência.",
        _ => "Tudo do Gamer + desliga indexação, hibernação, efeitos visuais e faz limpeza profunda.",
    };
}
