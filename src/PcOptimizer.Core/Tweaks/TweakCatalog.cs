using PcOptimizer.Core.Platform;
using static PcOptimizer.Core.Platform.RegistryRoot;

namespace PcOptimizer.Core.Tweaks;

/// <summary>
/// Lista de ajustes oferecidos. Só entram ajustes conhecidos, documentados e reversíveis
/// (ou inofensivos, como limpeza) — nada de "tweaks mágicos" sem efeito comprovado.
/// </summary>
public static class TweakCatalog
{
    private const string MultimediaProfile = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";

    public static IReadOnlyList<ITweak> All() =>
    [
        new PowerPlanTweak(),

        new RegistryTweak
        {
            Id = "game-mode",
            Name = "Modo de Jogo do Windows",
            Description = "Prioriza o jogo aberto e evita que o Windows Update instale drivers ou reinicie durante a partida.",
            Category = TweakCategory.Gaming,
            Recommended = true,
            Settings =
            [
                new(CurrentUser, @"Software\Microsoft\GameBar", "AutoGameModeEnabled", RegistryValue.Dword(1)),
                new(CurrentUser, @"Software\Microsoft\GameBar", "AllowAutoGameMode", RegistryValue.Dword(1)),
            ],
        },

        new RegistryTweak
        {
            Id = "game-dvr-off",
            Name = "Desligar gravação em segundo plano (Game DVR)",
            Description = "O Xbox Game Bar grava os últimos minutos de jogo o tempo todo, o que custa FPS. " +
                "Captura manual com Win+Alt+R continua funcionando se reativada.",
            Category = TweakCategory.Gaming,
            Recommended = true,
            Settings =
            [
                new(CurrentUser, @"System\GameConfigStore", "GameDVR_Enabled", RegistryValue.Dword(0)),
                new(CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", RegistryValue.Dword(0)),
            ],
        },

        new RegistryTweak
        {
            Id = "gpu-scheduling",
            Name = "Agendamento de GPU acelerado por hardware",
            Description = "Deixa a placa de vídeo gerenciar a própria memória, reduzindo latência. " +
                "Requer GPU e driver compatíveis (NVIDIA GTX 10+, AMD RX 5000+). Vale após reiniciar.",
            Category = TweakCategory.Gaming,
            Recommended = true,
            RequiresRestart = true,
            Settings =
            [
                new(LocalMachine, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", RegistryValue.Dword(2)),
            ],
        },

        new RegistryTweak
        {
            Id = "multimedia-priority",
            Name = "Prioridade para jogos e multimídia",
            Description = "Reserva menos CPU para tarefas de fundo enquanto jogos e players estão ativos (serviço MMCSS do Windows).",
            Category = TweakCategory.Performance,
            Recommended = true,
            Settings =
            [
                new(LocalMachine, MultimediaProfile, "SystemResponsiveness", RegistryValue.Dword(10)),
                new(LocalMachine, MultimediaProfile + @"\Tasks\Games", "GPU Priority", RegistryValue.Dword(8)),
                new(LocalMachine, MultimediaProfile + @"\Tasks\Games", "Priority", RegistryValue.Dword(6)),
                new(LocalMachine, MultimediaProfile + @"\Tasks\Games", "Scheduling Category", RegistryValue.Str("High")),
                new(LocalMachine, MultimediaProfile + @"\Tasks\Games", "SFIO Priority", RegistryValue.Str("High")),
            ],
        },

        new RegistryTweak
        {
            Id = "telemetry-off",
            Name = "Reduzir telemetria do Windows",
            Description = "Desativa o serviço de coleta de dados (DiagTrack), que roda o tempo todo e usa disco e rede.",
            Category = TweakCategory.Privacy,
            Recommended = true,
            Settings =
            [
                new(LocalMachine, @"SYSTEM\CurrentControlSet\Services\DiagTrack", "Start", RegistryValue.Dword(4)),
                new(LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", RegistryValue.Dword(0)),
            ],
            AfterApply = [("sc", "stop DiagTrack")],
        },

        new TempCleanupTweak(),

        new RegistryTweak
        {
            Id = "background-apps-off",
            Name = "Bloquear apps da Loja em segundo plano",
            Description = "Impede que apps da Microsoft Store rodem escondidos. " +
                "Atenção: apps como WhatsApp da Loja podem parar de notificar com o app fechado.",
            Category = TweakCategory.Privacy,
            Settings =
            [
                new(CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled", RegistryValue.Dword(1)),
            ],
        },

        new RegistryTweak
        {
            Id = "visual-effects",
            Name = "Efeitos visuais para desempenho",
            Description = "Desliga animações e sombras do Windows. Ajuda em PCs fracos, mas deixa o visual mais simples.",
            Category = TweakCategory.Appearance,
            Settings =
            [
                new(CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", RegistryValue.Dword(2)),
            ],
        },

        new RegistryTweak
        {
            Id = "mouse-acceleration-off",
            Name = "Desligar aceleração do mouse",
            Description = "Movimento do mouse fica 1:1 com a mão, preferido em jogos de tiro. Vale após sair e entrar na conta.",
            Category = TweakCategory.Appearance,
            RequiresRestart = true,
            Settings =
            [
                new(CurrentUser, @"Control Panel\Mouse", "MouseSpeed", RegistryValue.Str("0")),
                new(CurrentUser, @"Control Panel\Mouse", "MouseThreshold1", RegistryValue.Str("0")),
                new(CurrentUser, @"Control Panel\Mouse", "MouseThreshold2", RegistryValue.Str("0")),
            ],
        },
    ];
}
