using Slyth.Core.Platform;
using static Slyth.Core.Platform.RegistryRoot;
using static Slyth.Core.Tweaks.TweakLevel;

namespace Slyth.Core.Tweaks;

/// <summary>
/// Todos os ajustes do Slyth. Só entram ajustes conhecidos e documentados, reversíveis
/// (ou inofensivos, como limpezas) — nada de "tweaks mágicos" sem efeito comprovado.
/// </summary>
public static class TweakCatalog
{
    private const string Multimedia = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
    private const string ContentDelivery = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
    private const string Desktop = @"Control Panel\Desktop";
    private const string GameConfig = @"System\GameConfigStore";
    private const string UsbSubgroup = "2a737441-1930-4402-8d77-b2bebba308a3";
    private const string UsbSelectiveSuspend = "48e6b7a6-50f5-4782-a5d4-53bb8f07e226";

    private static RegistryValue D(int value) => RegistryValue.Dword(value);

    private static RegistryValue S(string value) => RegistryValue.Str(value);

    public static IReadOnlyList<ITweak> All() =>
    [
        .. Performance(),
        .. Gaming(),
        .. Network(),
        .. Privacy(),
        .. Services(),
        .. Cleanup(),
        .. Appearance(),
    ];

    private static IEnumerable<ITweak> Performance() =>
    [
        new PowerPlanTweak(),
        new RegistryTweak
        {
            Id = "multimedia-priority", Level = Safe, Category = TweakCategory.Performance,
            Name = "Prioridade para jogos e multimídia",
            Description = "Reserva menos CPU para tarefas de fundo enquanto jogos e players estão ativos (serviço MMCSS).",
            Settings =
            [
                new(LocalMachine, Multimedia, "SystemResponsiveness", D(10)),
                new(LocalMachine, Multimedia + @"\Tasks\Games", "GPU Priority", D(8)),
                new(LocalMachine, Multimedia + @"\Tasks\Games", "Priority", D(6)),
                new(LocalMachine, Multimedia + @"\Tasks\Games", "Scheduling Category", S("High")),
                new(LocalMachine, Multimedia + @"\Tasks\Games", "SFIO Priority", S("High")),
            ],
        },
        new RegistryTweak
        {
            Id = "startup-delay-off", Level = Safe, Category = TweakCategory.Performance,
            Name = "Inicialização sem atraso",
            Description = "O Windows espera alguns segundos antes de abrir os programas de inicialização. Isso remove a espera.",
            Settings =
            [
                new(CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize", "StartupDelayInMSec", D(0)),
                new(CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize", "WaitForIdleState", D(0)),
            ],
        },
        new RegistryTweak
        {
            Id = "ntfs-last-access-off", Level = Safe, Category = TweakCategory.Performance,
            Name = "Menos escrita no disco (último acesso)",
            Description = "Para de gravar a data de último acesso toda vez que um arquivo é lido. Menos escrita, disco mais rápido.",
            Settings = [new(LocalMachine, @"SYSTEM\CurrentControlSet\Control\FileSystem", "NtfsDisableLastAccessUpdate", D(unchecked((int)0x80000001)))],
        },
        new RegistryTweak
        {
            Id = "foreground-priority", Level = Gamer, Category = TweakCategory.Performance,
            Name = "Prioridade para o programa em primeiro plano",
            Description = "Dá fatias de tempo de CPU maiores para a janela ativa (jogo) do que para processos de fundo.",
            Settings = [new(LocalMachine, @"SYSTEM\CurrentControlSet\Control\PriorityControl", "Win32PrioritySeparation", D(38))],
        },
        new RegistryTweak
        {
            Id = "power-throttling-off", Level = Gamer, Category = TweakCategory.Performance,
            Name = "Desligar limitação de energia (Power Throttling)",
            Description = "Impede o Windows de reduzir a velocidade de programas em segundo plano. Em notebooks, gasta mais bateria.",
            Settings = [new(LocalMachine, @"SYSTEM\CurrentControlSet\Control\Power\PowerThrottling", "PowerThrottlingOff", D(1))],
        },
        new RegistryTweak
        {
            Id = "background-apps-off", Level = Gamer, Category = TweakCategory.Performance,
            Name = "Bloquear apps da Loja em segundo plano",
            Description = "Impede que apps da Microsoft Store rodem escondidos. Apps da Loja podem parar de notificar com o app fechado.",
            Settings = [new(CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled", D(1))],
        },
        new RegistryTweak
        {
            Id = "fast-shutdown", Level = Gamer, Category = TweakCategory.Performance,
            Name = "Desligamento mais rápido",
            Description = "Reduz o tempo que o Windows espera por programas e serviços travados ao desligar ou reiniciar.",
            Settings =
            [
                new(LocalMachine, @"SYSTEM\CurrentControlSet\Control", "WaitToKillServiceTimeout", S("2000")),
                new(CurrentUser, Desktop, "HungAppTimeout", S("2000")),
                new(CurrentUser, Desktop, "WaitToKillAppTimeout", S("2000")),
            ],
        },
        new CommandTweak
        {
            Id = "hibernation-off", Level = Extreme, Category = TweakCategory.Performance,
            Name = "Desligar hibernação",
            Description = "Apaga o arquivo hiberfil.sys (libera vários GB do disco do sistema). Desativa a Inicialização Rápida e a hibernação.",
            Command = ("powercfg", "/hibernate off"),
            Revert = ("powercfg", "/hibernate on"),
            AppliedCheck = c => c.Registry.GetValue(LocalMachine, @"SYSTEM\CurrentControlSet\Control\Power", "HibernateEnabled") == D(0),
        },
        new RegistryTweak
        {
            Id = "folder-discovery-off", Level = Safe, Category = TweakCategory.Performance,
            Name = "Pastas abrem na hora",
            Description = "O Explorer analisa o conteúdo de cada pasta para adivinhar o tipo (fotos, músicas...). " +
                "Em pastas grandes isso deixa a abertura lenta. Desligar deixa tudo instantâneo.",
            Settings = [new(CurrentUser, @"Software\Classes\Local Settings\Software\Microsoft\Windows\Shell\Bags\AllFolders\Shell", "FolderType", S("NotSpecified"))],
        },
        new RegistryTweak
        {
            Id = "storage-sense", Level = Safe, Category = TweakCategory.Performance,
            Name = "Limpeza automática do Windows (Sensor de Armazenamento)",
            Description = "Liga a limpeza automática de temporários e da Lixeira antiga, para o disco não encher de novo com o tempo.",
            Settings = [new(CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy", "01", D(1))],
        },
        new CommandTweak
        {
            Id = "usb-suspend-off", Level = Gamer, Category = TweakCategory.Performance,
            Name = "USB sem suspensão seletiva",
            Description = "Impede o Windows de \"adormecer\" portas USB para economizar energia. " +
                "Evita mouse, teclado e headset travando ou desconectando por um instante.",
            Command = ("cmd", $"/c powercfg /setacvalueindex scheme_current {UsbSubgroup} {UsbSelectiveSuspend} 0 && powercfg /setactive scheme_current"),
            Revert = ("cmd", $"/c powercfg /setacvalueindex scheme_current {UsbSubgroup} {UsbSelectiveSuspend} 1 && powercfg /setactive scheme_current"),
        },
        new RegistryTweak
        {
            Id = "browser-background-off", Level = Gamer, Category = TweakCategory.Performance,
            Name = "Navegadores sem rodar escondidos",
            Description = "Edge e Chrome continuam rodando depois de fechados e o Edge pré-carrega ao ligar o PC. " +
                "Isso libera memória e CPU. Os navegadores passam a mostrar \"gerenciado pela organização\" (normal).",
            Settings =
            [
                new(LocalMachine, @"SOFTWARE\Policies\Microsoft\Edge", "StartupBoostEnabled", D(0)),
                new(LocalMachine, @"SOFTWARE\Policies\Microsoft\Edge", "BackgroundModeEnabled", D(0)),
                new(LocalMachine, @"SOFTWARE\Policies\Google\Chrome", "BackgroundModeEnabled", D(0)),
            ],
        },
        new RegistryTweak
        {
            Id = "ntfs-memory", Level = Extreme, Category = TweakCategory.Performance,
            Name = "Mais memória para o cache de arquivos",
            Description = "Aumenta a memória que o Windows reserva para lembrar onde estão os arquivos (opção documentada do NTFS). " +
                "Acelera jogos e programas com milhares de arquivos. Recomendado com 16 GB de RAM ou mais.",
            RequiresRestart = true,
            Settings = [new(LocalMachine, @"SYSTEM\CurrentControlSet\Control\FileSystem", "NtfsMemoryUsage", D(2))],
        },
        new CommandTweak
        {
            Id = "reserved-storage-off", Level = Extreme, Category = TweakCategory.Performance, Timeout = TimeSpan.FromMinutes(5),
            Name = "Liberar armazenamento reservado",
            Description = "O Windows reserva cerca de 7 GB do disco para atualizações. Desligar libera esse espaço (recurso oficial da Microsoft).",
            Command = ("dism", "/Online /Set-ReservedStorageState /State:Disabled /NoRestart"),
            Revert = ("dism", "/Online /Set-ReservedStorageState /State:Enabled /NoRestart"),
        },
        new RegistryTweak
        {
            Id = "ntfs-8dot3-off", Level = Extreme, Category = TweakCategory.Performance,
            Name = "Desligar nomes curtos 8.3",
            Description = "Para de criar nomes no estilo antigo (ARQUIV~1.TXT) para cada arquivo novo, acelerando pastas grandes.",
            Settings = [new(LocalMachine, @"SYSTEM\CurrentControlSet\Control\FileSystem", "NtfsDisable8dot3NameCreation", D(1))],
        },
    ];

    private static IEnumerable<ITweak> Gaming() =>
    [
        new RegistryTweak
        {
            Id = "game-mode", Level = Safe, Category = TweakCategory.Gaming,
            Name = "Modo de Jogo do Windows",
            Description = "Prioriza o jogo aberto e impede o Windows Update de instalar drivers ou reiniciar durante a partida.",
            Settings =
            [
                new(CurrentUser, @"Software\Microsoft\GameBar", "AutoGameModeEnabled", D(1)),
                new(CurrentUser, @"Software\Microsoft\GameBar", "AllowAutoGameMode", D(1)),
            ],
        },
        new RegistryTweak
        {
            Id = "game-dvr-off", Level = Safe, Category = TweakCategory.Gaming,
            Name = "Desligar gravação em segundo plano (Game DVR)",
            Description = "O Xbox Game Bar grava os últimos minutos de jogo o tempo todo, custando FPS.",
            Settings =
            [
                new(CurrentUser, GameConfig, "GameDVR_Enabled", D(0)),
                new(CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", D(0)),
                new(LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\GameDVR", "AllowGameDVR", D(0)),
            ],
        },
        new RegistryTweak
        {
            Id = "gpu-scheduling", Level = Safe, Category = TweakCategory.Gaming, RequiresRestart = true,
            Name = "Agendamento de GPU por hardware",
            Description = "A placa de vídeo gerencia a própria memória, reduzindo latência. Requer GPU compatível (GTX 10+, RX 5000+).",
            Settings = [new(LocalMachine, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", D(2))],
        },
        new RegistryTweak
        {
            Id = "windowed-optimizations", Level = Gamer, Category = TweakCategory.Gaming,
            Name = "Otimizações para jogos em janela",
            Description = "Jogos em janela/sem borda passam a usar o modo de apresentação moderno, com menos latência (Windows 11).",
            Settings = [new(CurrentUser, @"Software\Microsoft\DirectX\UserGpuPreferences", "DirectXUserGlobalSettings", S("SwapEffectUpgradeEnable=1;"))],
        },
        new RegistryTweak
        {
            Id = "gamebar-tips-off", Level = Gamer, Category = TweakCategory.Gaming,
            Name = "Sem pop-ups da Game Bar",
            Description = "Remove o aviso da Game Bar que aparece ao abrir jogos.",
            Settings = [new(CurrentUser, @"Software\Microsoft\GameBar", "ShowStartupPanel", D(0))],
        },
        new RegistryTweak
        {
            Id = "audio-ducking-off", Level = Safe, Category = TweakCategory.Gaming,
            Name = "Som do jogo não abaixa sozinho",
            Description = "Quando detecta uma chamada de voz (Discord, WhatsApp), o Windows abaixa o volume do resto — inclusive o jogo. Isso desliga.",
            Settings = [new(CurrentUser, @"Software\Microsoft\Multimedia\Audio", "UserDuckingPreference", D(3))],
        },
        new RegistryTweak
        {
            Id = "sticky-keys-off", Level = Gamer, Category = TweakCategory.Gaming,
            Name = "Sem janela de Teclas de Aderência no jogo",
            Description = "Apertar Shift 5 vezes (comum em jogos) abre a janela de acessibilidade e minimiza o jogo. Desliga só o atalho.",
            Settings =
            [
                new(CurrentUser, @"Control Panel\Accessibility\StickyKeys", "Flags", S("506")),
                new(CurrentUser, @"Control Panel\Accessibility\ToggleKeys", "Flags", S("58")),
                new(CurrentUser, @"Control Panel\Accessibility\Keyboard Response", "Flags", S("122")),
            ],
        },
        new RegistryTweak
        {
            Id = "wu-drivers-off", Level = Gamer, Category = TweakCategory.Gaming,
            Name = "Windows Update não troca seu driver de vídeo",
            Description = "O Windows Update às vezes substitui o driver da NVIDIA/AMD por uma versão antiga, derrubando o FPS. " +
                "As atualizações do Windows continuam normais; só os drivers deixam de vir por ele.",
            Settings = [new(LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate", "ExcludeWUDriversInQualityUpdate", D(1))],
        },
        new RegistryTweak
        {
            Id = "mpo-off", Level = Manual, Category = TweakCategory.Gaming, RequiresRestart = true,
            Name = "Corrigir tela piscando e travadinhas (MPO)",
            Description = "Desliga o Multi-Plane Overlay, correção documentada pela NVIDIA para tela piscando, travadinhas e tela preta " +
                "com dois monitores. Use só se tiver esses sintomas.",
            Settings = [new(LocalMachine, @"SOFTWARE\Microsoft\Windows\Dwm", "OverlayTestMode", D(5))],
        },
        new RegistryTweak
        {
            Id = "fullscreen-optimizations-off", Level = Extreme, Category = TweakCategory.Gaming,
            Name = "Tela cheia exclusiva de verdade",
            Description = "Desliga as \"otimizações de tela cheia\" do Windows. Alguns jogos antigos ganham FPS; o Alt+Tab fica mais lento.",
            Settings =
            [
                new(CurrentUser, GameConfig, "GameDVR_FSEBehaviorMode", D(2)),
                new(CurrentUser, GameConfig, "GameDVR_FSEBehavior", D(2)),
                new(CurrentUser, GameConfig, "GameDVR_HonorUserFSEBehaviorMode", D(1)),
                new(CurrentUser, GameConfig, "GameDVR_DXGIHonorFSEWindowsCompatible", D(1)),
            ],
        },
    ];

    private static IEnumerable<ITweak> Network() =>
    [
        new RegistryTweak
        {
            Id = "delivery-optimization-p2p-off", Level = Safe, Category = TweakCategory.Network,
            Name = "Parar de enviar atualizações para outros PCs",
            Description = "Por padrão o Windows usa sua internet para enviar atualizações a outros computadores. Isso desliga.",
            Settings = [new(LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization", "DODownloadMode", D(0))],
        },
        new CommandTweak
        {
            Id = "flush-dns", Level = Safe, Category = TweakCategory.Network,
            Name = "Limpar cache de DNS",
            Description = "Apaga endereços de sites guardados, resolvendo sites que não abrem ou abrem lentos.",
            Command = ("ipconfig", "/flushdns"),
            SuccessDetail = "cache de DNS limpo",
        },
        new RegistryTweak
        {
            Id = "network-throttling-off", Level = Gamer, Category = TweakCategory.Network,
            Name = "Desligar limitação de rede do Windows",
            Description = "O Windows limita pacotes de rede enquanto toca mídia. Removendo o limite, a conexão fica estável em jogos.",
            Settings = [new(LocalMachine, Multimedia, "NetworkThrottlingIndex", D(-1))],
        },
        new NetworkInterfaceTweak
        {
            Id = "nagle-off", Level = Gamer,
            Name = "Menor latência em jogos online (Nagle)",
            Description = "Envia pacotes pequenos na hora, sem agrupar. Reduz o ping percebido em jogos que usam TCP.",
            Values = [("TcpAckFrequency", D(1)), ("TCPNoDelay", D(1))],
        },
        new NetworkAdapterPowerTweak(),
    ];

    private static IEnumerable<ITweak> Privacy() =>
    [
        new ServiceTweak
        {
            Id = "telemetry-off", Level = Safe, Category = TweakCategory.Privacy,
            Name = "Desligar telemetria do Windows",
            Description = "Desativa os serviços de coleta de dados (DiagTrack), que rodam o tempo todo usando disco e rede.",
            Services = ["DiagTrack", "dmwappushservice"],
            AlsoSet = [new(LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", D(0))],
        },
        new RegistryTweak
        {
            Id = "advertising-id-off", Level = Safe, Category = TweakCategory.Privacy,
            Name = "Desligar ID de anúncios",
            Description = "Impede que apps usem um identificador seu para mostrar propaganda personalizada.",
            Settings = [new(CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", D(0))],
        },
        new RegistryTweak
        {
            Id = "suggestions-off", Level = Safe, Category = TweakCategory.Privacy,
            Name = "Sem propagandas e apps instalados sozinhos",
            Description = "Remove sugestões no Iniciar, dicas na tela de bloqueio e para o Windows de instalar apps promocionais.",
            Settings =
            [
                new(CurrentUser, ContentDelivery, "SilentInstalledAppsEnabled", D(0)),
                new(CurrentUser, ContentDelivery, "SystemPaneSuggestionsEnabled", D(0)),
                new(CurrentUser, ContentDelivery, "SoftLandingEnabled", D(0)),
                new(CurrentUser, ContentDelivery, "SubscribedContent-338388Enabled", D(0)),
                new(CurrentUser, ContentDelivery, "SubscribedContent-338389Enabled", D(0)),
                new(CurrentUser, ContentDelivery, "SubscribedContent-353694Enabled", D(0)),
                new(CurrentUser, ContentDelivery, "SubscribedContent-353696Enabled", D(0)),
            ],
        },
        new RegistryTweak
        {
            Id = "bing-search-off", Level = Safe, Category = TweakCategory.Privacy,
            Name = "Pesquisa do Iniciar sem Bing",
            Description = "A busca do menu Iniciar procura só no seu PC: mais rápida e sem enviar o que você digita.",
            Settings =
            [
                new(CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Search", "BingSearchEnabled", D(0)),
                new(CurrentUser, @"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", D(1)),
            ],
        },
        new RegistryTweak
        {
            Id = "activity-history-off", Level = Safe, Category = TweakCategory.Privacy,
            Name = "Desligar histórico de atividades",
            Description = "O Windows para de registrar e enviar quais apps e arquivos você usa.",
            Settings =
            [
                new(LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\System", "EnableActivityFeed", D(0)),
                new(LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities", D(0)),
                new(LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\System", "UploadUserActivities", D(0)),
            ],
        },
        new RegistryTweak
        {
            Id = "feedback-off", Level = Safe, Category = TweakCategory.Privacy,
            Name = "Sem pedidos de feedback",
            Description = "Para as notificações pedindo avaliação do Windows e experiências baseadas nos seus dados de diagnóstico.",
            Settings =
            [
                new(CurrentUser, @"Software\Microsoft\Siuf\Rules", "NumberOfSIUFInPeriod", D(0)),
                new(CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", D(0)),
            ],
        },
        new RegistryTweak
        {
            Id = "recall-off", Level = Safe, Category = TweakCategory.Privacy,
            Name = "Desligar Recall (capturas de tela da IA)",
            Description = "Impede o Windows 11 de tirar capturas periódicas da sua tela para o recurso Recall.",
            Settings = [new(CurrentUser, @"Software\Policies\Microsoft\Windows\WindowsAI", "DisableAIDataAnalysis", D(1))],
        },
        new ScheduledTaskTweak
        {
            Id = "telemetry-tasks-off", Level = Safe,
            Name = "Tarefas escondidas de telemetria",
            Description = "Tarefas agendadas que acordam sozinhas para coletar dados do PC e causam picos de disco e CPU do nada " +
                "(ex.: Compatibility Appraiser). Ninguém vê, mas travam o jogo por um instante.",
            Tasks =
            [
                @"\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser",
                @"\Microsoft\Windows\Application Experience\ProgramDataUpdater",
                @"\Microsoft\Windows\Autochk\Proxy",
                @"\Microsoft\Windows\Customer Experience Improvement Program\Consolidator",
                @"\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip",
                @"\Microsoft\Windows\DiskDiagnostic\Microsoft-Windows-DiskDiagnosticDataCollector",
                @"\Microsoft\Windows\Feedback\Siuf\DmClient",
                @"\Microsoft\Windows\Feedback\Siuf\DmClientOnScenarioDownload",
                @"\Microsoft\Windows\Windows Error Reporting\QueueReporting",
            ],
        },
        new RegistryTweak
        {
            Id = "tips-notifications-off", Level = Safe, Category = TweakCategory.Privacy,
            Name = "Sem notificações de dicas",
            Description = "Para as notificações de \"dicas e sugestões\" e a tela de boas-vindas depois de atualizações.",
            Settings =
            [
                new(CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\Windows.SystemToast.Suggested", "Enabled", D(0)),
                new(CurrentUser, ContentDelivery, "SubscribedContent-310093Enabled", D(0)),
                new(CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\UserProfileEngagement", "ScoobeSystemSettingEnabled", D(0)),
            ],
        },
        new RegistryTweak
        {
            Id = "copilot-off", Level = Gamer, Category = TweakCategory.Privacy,
            Name = "Desligar Copilot",
            Description = "Remove o assistente Copilot do Windows, que fica carregado em segundo plano.",
            Settings = [new(CurrentUser, @"Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", D(1))],
        },
        new RegistryTweak
        {
            Id = "widgets-off", Level = Gamer, Category = TweakCategory.Privacy,
            Name = "Desligar Widgets e notícias",
            Description = "Remove o painel de widgets/notícias, que consome memória e internet em segundo plano.",
            Settings = [new(LocalMachine, @"SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests", D(0))],
        },
    ];

    private static IEnumerable<ITweak> Services() =>
    [
        new ServiceTweak
        {
            Id = "svc-retail-demo", Level = Safe,
            Name = "Modo demonstração de loja",
            Description = "Serviço usado só em PCs de vitrine.",
            Services = ["RetailDemo"],
        },
        new ServiceTweak
        {
            Id = "svc-remote-registry", Level = Safe,
            Name = "Registro remoto",
            Description = "Permite que outros computadores alterem seu registro pela rede. Desligar é mais seguro.",
            Services = ["RemoteRegistry"],
        },
        new ServiceTweak
        {
            Id = "svc-maps", Level = Gamer,
            Name = "Mapas offline",
            Description = "Baixa e atualiza mapas do app Mapas em segundo plano.",
            Services = ["MapsBroker"],
        },
        new ServiceTweak
        {
            Id = "svc-error-reporting", Level = Gamer,
            Name = "Relatório de erros do Windows",
            Description = "Coleta e envia relatórios de travamentos para a Microsoft.",
            Services = ["WerSvc"],
        },
        new ServiceTweak
        {
            Id = "svc-fax", Level = Gamer,
            Name = "Fax",
            Description = "Serviço de fax, que praticamente ninguém usa mais.",
            Services = ["Fax"],
        },
        new ServiceTweak
        {
            Id = "svc-nvidia-telemetry", Level = Gamer,
            Name = "Telemetria da NVIDIA",
            Description = "Serviço de coleta de dados instalado junto com o driver de vídeo. O driver e o painel continuam funcionando.",
            Services = ["NvTelemetryContainer"],
        },
        new ServiceTweak
        {
            Id = "svc-link-tracking", Level = Gamer,
            Name = "Rastreamento de links distribuídos",
            Description = "Acompanha atalhos para arquivos em outros PCs da rede corporativa. Inútil em PC doméstico.",
            Services = ["TrkWks"],
        },
        new ServiceTweak
        {
            Id = "svc-media-sharing", Level = Gamer,
            Name = "Compartilhamento do Windows Media Player",
            Description = "Compartilha sua biblioteca de mídia na rede local em segundo plano.",
            Services = ["WMPNetworkSvc"],
        },
        new ServiceTweak
        {
            Id = "svc-insider", Level = Gamer,
            Name = "Programa Windows Insider",
            Description = "Só é usado por quem testa versões beta do Windows.",
            Services = ["wisvc"],
        },
        new ServiceTweak
        {
            Id = "svc-compat-assistant", Level = Extreme,
            Name = "Assistente de compatibilidade de programas",
            Description = "Monitora cada programa aberto para sugerir modos de compatibilidade. Consome recursos a cada execução.",
            Services = ["PcaSvc"],
        },
        new ServiceTweak
        {
            Id = "svc-sysmain", Level = Extreme,
            Name = "SysMain (Superfetch)",
            Description = "Pré-carrega programas na memória. Em SSD o ganho é pequeno e ele pode causar uso alto de disco.",
            Services = ["SysMain"],
        },
        new ServiceTweak
        {
            Id = "svc-search", Level = Extreme,
            Name = "Indexação da Pesquisa do Windows",
            Description = "Para de indexar arquivos o tempo todo. A busca por arquivos no Explorer fica mais lenta.",
            Services = ["WSearch"],
        },
        new ServiceTweak
        {
            Id = "svc-print", Level = Manual,
            Name = "Spooler de impressão",
            Description = "Só desligue se não usa impressora. Também fecha uma falha de segurança conhecida (PrintNightmare).",
            Services = ["Spooler"],
        },
        new ServiceTweak
        {
            Id = "svc-xbox", Level = Manual,
            Name = "Serviços do Xbox",
            Description = "Só desligue se não usa Xbox app, Game Pass ou controle Xbox: esses recursos param de funcionar.",
            Services = ["XblAuthManager", "XblGameSave", "XboxNetApiSvc", "XboxGipSvc"],
        },
    ];

    private static IEnumerable<ITweak> Cleanup() =>
    [
        new CleanupTweak
        {
            Id = "temp-cleanup", Level = Safe,
            Name = "Arquivos temporários",
            Description = "Temporários do Windows e do usuário com mais de 1 dia. Arquivos em uso são ignorados.",
            Paths = ["%TEMP%", @"%WINDIR%\Temp"],
        },
        new CleanupTweak
        {
            Id = "windows-update-cache", Level = Safe,
            Name = "Cache do Windows Update",
            Description = "Instaladores de atualizações que já foram aplicadas.",
            Paths = [@"%WINDIR%\SoftwareDistribution\Download"],
        },
        new CleanupTweak
        {
            Id = "delivery-optimization-cache", Level = Safe,
            Name = "Cache de otimização de entrega",
            Description = "Pedaços de atualizações guardados para compartilhar com outros PCs.",
            Paths = [@"%WINDIR%\ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization\Cache"],
        },
        new CleanupTweak
        {
            Id = "crash-dumps", Level = Safe,
            Name = "Relatórios de erro e dumps de memória",
            Description = "Arquivos gerados quando programas ou o Windows travam. Podem ocupar vários GB.",
            Paths =
            [
                @"%WINDIR%\Minidump",
                @"%WINDIR%\MEMORY.DMP",
                @"%LOCALAPPDATA%\CrashDumps",
                @"%PROGRAMDATA%\Microsoft\Windows\WER\ReportArchive",
                @"%PROGRAMDATA%\Microsoft\Windows\WER\ReportQueue",
            ],
        },
        new CleanupTweak
        {
            Id = "browser-cache", Level = Gamer, MinimumAge = TimeSpan.FromHours(1),
            Name = "Cache dos navegadores",
            Description = "Chrome, Edge, Brave, Opera GX e Firefox. Senhas, logins e histórico NÃO são apagados.",
            Paths =
            [
                @"%LOCALAPPDATA%\Google\Chrome\User Data\*\Cache",
                @"%LOCALAPPDATA%\Google\Chrome\User Data\*\Code Cache",
                @"%LOCALAPPDATA%\Microsoft\Edge\User Data\*\Cache",
                @"%LOCALAPPDATA%\Microsoft\Edge\User Data\*\Code Cache",
                @"%LOCALAPPDATA%\BraveSoftware\Brave-Browser\User Data\*\Cache",
                @"%LOCALAPPDATA%\Opera Software\Opera GX Stable\Cache",
                @"%LOCALAPPDATA%\Mozilla\Firefox\Profiles\*\cache2",
            ],
        },
        new CleanupTweak
        {
            Id = "app-cache", Level = Gamer, MinimumAge = TimeSpan.FromHours(1),
            Name = "Cache de Discord, Steam e Spotify",
            Description = "Imagens e arquivos baixados que esses apps guardam e baixam de novo quando precisam.",
            Paths =
            [
                @"%APPDATA%\discord\Cache",
                @"%APPDATA%\discord\Code Cache",
                @"%APPDATA%\discord\GPUCache",
                @"%LOCALAPPDATA%\Steam\htmlcache",
                @"%LOCALAPPDATA%\Spotify\Data",
            ],
        },
        new CleanupTweak
        {
            Id = "shader-cache", Level = Extreme, MinimumAge = TimeSpan.Zero,
            Name = "Cache de shaders da placa de vídeo",
            Description = "Resolve travadinhas causadas por cache corrompido após atualizar driver. Na primeira partida os jogos recompilam os shaders.",
            Paths =
            [
                @"%LOCALAPPDATA%\D3DSCache",
                @"%LOCALAPPDATA%\NVIDIA\DXCache",
                @"%LOCALAPPDATA%\NVIDIA\GLCache",
                @"%LOCALAPPDATA%\AMD\DxCache",
                @"%LOCALAPPDATA%\AMD\DxcCache",
                @"%LOCALAPPDATA%\AMD\GLCache",
                @"%LOCALAPPDATA%\Intel\ShaderCache",
            ],
        },
        new CommandTweak
        {
            Id = "recycle-bin", Level = Extreme, Category = TweakCategory.Cleanup,
            Name = "Esvaziar Lixeira",
            Description = "Apaga definitivamente tudo o que está na Lixeira.",
            Command = ("powershell", "-NoProfile -Command \"Clear-RecycleBin -Force -ErrorAction SilentlyContinue\""),
            SuccessDetail = "Lixeira esvaziada",
        },
        new CommandTweak
        {
            Id = "component-store", Level = Extreme, Category = TweakCategory.Cleanup, Timeout = TimeSpan.FromMinutes(30),
            Name = "Limpeza profunda do Windows (WinSxS)",
            Description = "Remove versões antigas de componentes substituídas por atualizações. Pode levar vários minutos.",
            Command = ("dism", "/online /Cleanup-Image /StartComponentCleanup"),
            SuccessDetail = "componentes antigos removidos",
        },
        new CommandTweak
        {
            Id = "drive-optimize", Level = Extreme, Category = TweakCategory.Cleanup, Timeout = TimeSpan.FromMinutes(60),
            Name = "Otimizar unidades (TRIM / desfragmentar)",
            Description = "Executa TRIM nos SSDs e desfragmenta HDs. Em HDs grandes pode demorar.",
            Command = ("defrag", "/C /O /H"),
            SuccessDetail = "unidades otimizadas",
        },
    ];

    private static IEnumerable<ITweak> Appearance() =>
    [
        new RegistryTweak
        {
            Id = "menu-delay", Level = Safe, Category = TweakCategory.Appearance,
            Name = "Menus instantâneos",
            Description = "Menus e submenus abrem sem o atraso padrão de 400 ms.",
            Settings = [new(CurrentUser, Desktop, "MenuShowDelay", S("100"))],
        },
        new RegistryTweak
        {
            Id = "mouse-acceleration-off", Level = Gamer, Category = TweakCategory.Appearance, RequiresRestart = true,
            Name = "Desligar aceleração do mouse",
            Description = "Movimento 1:1 com a mão, preferido em jogos de tiro. Vale após sair e entrar na conta.",
            Settings =
            [
                new(CurrentUser, @"Control Panel\Mouse", "MouseSpeed", S("0")),
                new(CurrentUser, @"Control Panel\Mouse", "MouseThreshold1", S("0")),
                new(CurrentUser, @"Control Panel\Mouse", "MouseThreshold2", S("0")),
            ],
        },
        new RegistryTweak
        {
            Id = "keyboard-response", Level = Gamer, Category = TweakCategory.Appearance, RequiresRestart = true,
            Name = "Teclado mais responsivo",
            Description = "Menor atraso antes de repetir a tecla e repetição mais rápida. Vale após sair e entrar na conta.",
            Settings =
            [
                new(CurrentUser, @"Control Panel\Keyboard", "KeyboardDelay", S("0")),
                new(CurrentUser, @"Control Panel\Keyboard", "KeyboardSpeed", S("31")),
            ],
        },
        new RegistryTweak
        {
            Id = "visual-effects", Level = Extreme, Category = TweakCategory.Appearance,
            Name = "Efeitos visuais para desempenho",
            Description = "Desliga animações e sombras do Windows. Ajuda em PCs fracos; o visual fica mais simples.",
            Settings = [new(CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", D(2))],
        },
        new RegistryTweak
        {
            Id = "transparency-off", Level = Extreme, Category = TweakCategory.Appearance,
            Name = "Desligar transparência",
            Description = "Remove o efeito de vidro da barra de tarefas e do Iniciar, poupando a placa de vídeo.",
            Settings = [new(CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", D(0))],
        },
    ];
}
