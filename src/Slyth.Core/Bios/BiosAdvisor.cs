using System.Text.RegularExpressions;

namespace Slyth.Core.Bios;

public enum AdviceStatus
{
    Ok,
    ActionNeeded,
    CheckManually,
    Info,
}

public sealed record BiosRecommendation(
    string Id,
    string Title,
    AdviceStatus Status,
    string Explanation,
    IReadOnlyList<string> Steps);

public sealed record BiosReport(HardwareInfo Hardware, VendorGuide Vendor, IReadOnlyList<BiosRecommendation> Items);

/// <summary>
/// Analisa o hardware e gera recomendações de BIOS com passo a passo.
/// Nunca altera a BIOS: mudanças automáticas podem impedir o PC de ligar e variam por modelo.
/// </summary>
public static partial class BiosAdvisor
{
    public static readonly TimeSpan OutdatedBiosAge = TimeSpan.FromDays(730);

    public static BiosReport Analyze(HardwareInfo hw, DateTime? now = null)
    {
        var manufacturer = string.IsNullOrWhiteSpace(hw.BoardManufacturer) ? hw.SystemManufacturer : hw.BoardManufacturer;
        var guide = VendorGuide.For(manufacturer);
        var enter = $"Entre na BIOS: use o botão \"Reiniciar na BIOS\" deste app ou aperte {guide.EnterKeys} ao ligar o PC.";
        var save = "Salve e saia (geralmente F10 → Yes). Se o PC não ligar, desligue da tomada e ele volta ao padrão ou use \"Load Optimized Defaults\".";

        var items = new List<BiosRecommendation>();
        AddMemoryProfile(hw, guide, enter, save, items);
        AddDualChannel(hw, items);
        AddBiosUpdate(hw, guide, now ?? DateTime.Now, items);
        AddResizableBar(hw, guide, enter, save, items);
        AddFirmwareMode(hw, items);
        AddVirtualization(hw, guide, enter, save, items);
        return new BiosReport(hw, guide, items);
    }

    private static void AddMemoryProfile(HardwareInfo hw, VendorGuide guide, string enter, string save, List<BiosRecommendation> items)
    {
        const string id = "memory-profile";
        const string title = "Perfil de memória (XMP / EXPO)";
        if (hw.Memory.Count == 0)
        {
            items.Add(new(id, title, AdviceStatus.CheckManually, "Não foi possível ler a velocidade da memória.", []));
            return;
        }

        var configured = hw.Memory.Where(m => m.ConfiguredSpeedMts > 0).Select(m => m.ConfiguredSpeedMts).DefaultIfEmpty(0).Min();
        var rated = hw.Memory.Max(m => m.RatedSpeedMts);
        var generation = hw.Memory.Select(m => m.Generation).FirstOrDefault(g => g.Length > 0) ?? "";

        if (hw.IsLaptop)
        {
            items.Add(new(id, title, AdviceStatus.Info,
                $"Memória a {configured} MT/s. Em notebooks a velocidade é definida pelo fabricante e quase nunca pode ser alterada.", []));
            return;
        }

        var likelyStock = generation switch
        {
            "DDR3" => configured <= 1333,
            "DDR4" => configured <= 2666,
            "DDR5" => configured <= 4800,
            _ => false,
        };

        if (configured > 0 && (likelyStock || rated > configured))
        {
            items.Add(new(id, title, AdviceStatus.ActionNeeded,
                $"Sua memória {generation} está rodando a {configured} MT/s, velocidade padrão de fábrica. " +
                "A maioria dos pentes vendidos para PC gamer suporta bem mais que isso ativando o perfil XMP/EXPO — " +
                "costuma render de 5% a 15% a mais de FPS, principalmente em processadores AMD Ryzen.",
                [
                    enter,
                    guide.MemoryProfilePath + ".",
                    "Se não houver essa opção, a placa-mãe (ex.: chipsets Intel H/B mais antigos) ou o pente não suportam — não há o que fazer.",
                    save,
                    "Depois, abra este app de novo e confira na aba BIOS se a velocidade subiu.",
                ]));
        }
        else
        {
            items.Add(new(id, title, AdviceStatus.Ok, $"Memória {generation} rodando a {configured} MT/s.".Replace("  ", " "), []));
        }
    }

    private static void AddDualChannel(HardwareInfo hw, List<BiosRecommendation> items)
    {
        if (hw.Memory.Count == 1)
        {
            items.Add(new("dual-channel", "Memória em canal único (single channel)", AdviceStatus.ActionNeeded,
                "Há só um pente de memória instalado. Com dois pentes iguais (dual channel) a memória fica com o dobro de banda, " +
                "o que costuma dar um ganho grande em jogos e em PCs com vídeo integrado. Não é uma opção da BIOS: é preciso instalar outro pente igual.",
                []));
        }
    }

    private static void AddBiosUpdate(HardwareInfo hw, VendorGuide guide, DateTime now, List<BiosRecommendation> items)
    {
        const string id = "bios-update";
        const string title = "Versão da BIOS";
        if (hw.BiosReleaseDate is not { } date)
        {
            items.Add(new(id, title, AdviceStatus.CheckManually, $"Versão {hw.BiosVersion}. Não foi possível ler a data.", []));
            return;
        }

        if (now - date > OutdatedBiosAge)
        {
            var model = string.IsNullOrWhiteSpace(hw.BoardProduct) ? hw.SystemModel : hw.BoardProduct;
            items.Add(new(id, title, AdviceStatus.ActionNeeded,
                $"A BIOS {hw.BiosVersion} é de {date:MM/yyyy}. Versões novas trazem correções de estabilidade, segurança e suporte a memórias e processadores.",
                [
                    $"Baixe a versão mais recente para \"{model}\" somente no site oficial do fabricante.",
                    $"Use {guide.UpdateTool} seguindo o manual do fabricante.",
                    "Deixe o PC na tomada (notebook com carregador) e NÃO desligue durante a atualização.",
                    "Este app nunca atualiza a BIOS sozinho, porque uma falha nesse processo pode impedir o PC de ligar.",
                ]));
        }
        else
        {
            items.Add(new(id, title, AdviceStatus.Ok, $"BIOS {hw.BiosVersion} de {date:MM/yyyy}.", []));
        }
    }

    private static void AddResizableBar(HardwareInfo hw, VendorGuide guide, string enter, string save, List<BiosRecommendation> items)
    {
        var gpu = hw.Gpus.FirstOrDefault(g => ModernGpu().IsMatch(g));
        if (gpu is null)
        {
            return;
        }

        var steps = new List<string>();
        if (hw.IsUefi == false)
        {
            steps.Add("Requer Windows instalado em modo UEFI (veja o item \"Modo de inicialização\").");
        }

        steps.AddRange([enter, guide.ResizableBarPath + ".", save,
            "Para conferir: no painel da NVIDIA (Informações do sistema → Resizable BAR) ou no AMD Software (Smart Access Memory)."]);

        items.Add(new("resizable-bar", "Resizable BAR / Smart Access Memory", AdviceStatus.CheckManually,
            $"Sua placa de vídeo ({gpu}) suporta Resizable BAR, que permite ao processador acessar toda a memória da placa de uma vez. " +
            "Dá ganhos em vários jogos" + (gpu.Contains("Arc", StringComparison.OrdinalIgnoreCase) ? " e é essencial nas placas Intel Arc." : "."),
            steps));
    }

    private static void AddFirmwareMode(HardwareInfo hw, List<BiosRecommendation> items)
    {
        if (hw.IsUefi == false)
        {
            items.Add(new("firmware-mode", "Modo de inicialização", AdviceStatus.Info,
                "O Windows está em modo legado (CSM/BIOS antiga). UEFI é necessário para Windows 11, Secure Boot e Resizable BAR.",
                [
                    "IMPORTANTE: não desligue o CSM na BIOS antes de converter o disco para GPT, senão o Windows não inicia.",
                    "A conversão é feita com a ferramenta MBR2GPT da Microsoft. Faça backup dos seus arquivos antes.",
                ]));
        }
    }

    private static void AddVirtualization(HardwareInfo hw, VendorGuide guide, string enter, string save, List<BiosRecommendation> items)
    {
        if (hw.VirtualizationEnabled == false)
        {
            items.Add(new("virtualization", "Virtualização (VT-x / SVM)", AdviceStatus.Info,
                "Desativada. Não muda o desempenho em jogos; só ative se usar emuladores de Android, WSL, Docker ou máquinas virtuais.",
                [enter, guide.VirtualizationPath + " = Enabled.", save]));
        }
    }

    [GeneratedRegex(@"RTX\s*(30|40|50)\d0|RX\s*[6-9]\d{3}|\bArc\b", RegexOptions.IgnoreCase)]
    private static partial Regex ModernGpu();
}
