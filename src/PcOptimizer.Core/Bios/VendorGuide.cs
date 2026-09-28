namespace PcOptimizer.Core.Bios;

/// <summary>Onde ficam as opções em cada fabricante. Os nomes podem variar um pouco conforme o modelo.</summary>
public sealed record VendorGuide(
    string Name,
    string EnterKeys,
    string MemoryProfilePath,
    string ResizableBarPath,
    string VirtualizationPath,
    string UpdateTool)
{
    public static readonly VendorGuide Generic = new(
        "Genérico",
        "Del, F2, F10 ou Esc logo ao ligar (ou use o botão \"Reiniciar na BIOS\")",
        "Procure por XMP, EXPO, DOCP ou \"Memory Profile\" nas abas de Overclock/Tweaker e escolha o Perfil 1",
        "Procure por \"Above 4G Decoding\" e \"Re-Size BAR\" na aba Advanced/PCI e ative os dois",
        "Procure por \"Intel Virtualization Technology\", \"VT-x\" ou \"SVM Mode\" na aba de CPU",
        "a ferramenta de atualização da própria BIOS ou o site de suporte do fabricante");

    private static readonly VendorGuide[] Known =
    [
        new("ASUS",
            "Del ou F2",
            "F7 para o modo avançado → Ai Tweaker → Ai Overclock Tuner → XMP I (Intel) ou EXPO / D.O.C.P. (AMD)",
            "Advanced → PCI Subsystem Settings → Above 4G Decoding = Enabled e Re-Size BAR Support = Auto",
            "Advanced → CPU Configuration → Intel Virtualization Technology (Intel) ou SVM Mode (AMD)",
            "ASUS EZ Flash 3 (menu Tool dentro da BIOS)"),
        new("MSI",
            "Del",
            "Na tela inicial (EZ Mode), clique no botão XMP / A-XMP / EXPO e escolha o Perfil 1",
            "Settings → Advanced → PCI Subsystem Settings → Above 4G Memory = Enabled e Re-Size BAR Support = Enabled",
            "OC → CPU Features → Intel Virtualization Tech (Intel) ou SVM Mode (AMD)",
            "M-FLASH (botão na tela da BIOS)"),
        new("Gigabyte",
            "Del",
            "Tweaker → Extreme Memory Profile (X.M.P.) ou EXPO → Profile 1",
            "Settings → IO Ports → Above 4G Decoding = Enabled e Re-Size BAR Support = Auto",
            "Tweaker → Advanced CPU Settings → SVM Mode (AMD) ou Intel Virtualization Technology",
            "Q-Flash (tecla F8 dentro da BIOS)"),
        new("ASRock",
            "F2 ou Del",
            "OC Tweaker → DRAM Configuration → Load XMP Setting (ou EXPO) → XMP 2.0 Profile 1",
            "Advanced → Chipset Configuration → Above 4G Decoding = Enabled e Re-Size BAR Support = Enabled",
            "Advanced → CPU Configuration → SVM Mode (AMD) ou Intel Virtualization Technology",
            "Instant Flash (menu Tool dentro da BIOS)"),
        new("Dell",
            "F2 (F12 abre o menu de boot)",
            "Performance → Memory / Intel XMP (disponível só em Alienware e alguns XPS/G-Series)",
            "Normalmente já vem ativo quando suportado; atualize a BIOS para receber o suporte",
            "Virtualization Support → Virtualization",
            "Dell Command | Update ou SupportAssist"),
        new("HP",
            "F10 (ou Esc e depois F10)",
            "Em OMEN/Victus: Advanced → Memory Overclocking; nos demais modelos a opção não existe",
            "Normalmente já vem ativo quando suportado; atualize a BIOS para receber o suporte",
            "Advanced → System Options → Virtualization Technology (VTx)",
            "HP Support Assistant"),
        new("Lenovo",
            "F1 (desktops) ou F2 (notebooks); em alguns modelos use o botão Novo",
            "Em Legion/LOQ: Advanced → Memory Overclocking; nos demais modelos a opção não existe",
            "Normalmente já vem ativo quando suportado; atualize a BIOS para receber o suporte",
            "Configuration → Intel Virtual Technology / AMD SVM Technology",
            "Lenovo Vantage (ou Lenovo System Update)"),
        new("Acer",
            "F2 ou Del",
            "Em Predator/Nitro: Main → Memory Profile / XMP; nos demais modelos a opção não existe",
            "Normalmente já vem ativo quando suportado; atualize a BIOS para receber o suporte",
            "Advanced → Intel VT / AMD-V",
            "o site de suporte da Acer (baixe pelo número de série)"),
    ];

    public static VendorGuide For(string manufacturer)
    {
        var m = manufacturer.ToUpperInvariant();
        string? name = m switch
        {
            _ when m.Contains("ASUS") => "ASUS",
            _ when m.Contains("MICRO-STAR") || m.StartsWith("MSI", StringComparison.Ordinal) => "MSI",
            _ when m.Contains("GIGABYTE") => "Gigabyte",
            _ when m.Contains("ASROCK") => "ASRock",
            _ when m.Contains("DELL") || m.Contains("ALIENWARE") => "Dell",
            _ when m.StartsWith("HP", StringComparison.Ordinal) || m.Contains("HEWLETT") => "HP",
            _ when m.Contains("LENOVO") => "Lenovo",
            _ when m.Contains("ACER") => "Acer",
            _ => null,
        };
        return Known.FirstOrDefault(g => g.Name == name) ?? Generic;
    }
}
