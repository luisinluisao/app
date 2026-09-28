using System.Globalization;
using System.Text.RegularExpressions;

namespace Slyth.Core.Performance;

/// <summary>Lê o tempo de inicialização que o próprio Windows registra (evento 100 do Diagnostics-Performance).</summary>
public static partial class BootTime
{
    public const string WevtutilArgs =
        "qe Microsoft-Windows-Diagnostics-Performance/Operational /q:\"*[System[(EventID=100)]]\" /c:1 /rd:true /f:xml";

    /// <summary>Segundos, ou null se o evento não existe ou é de uma inicialização anterior à atual.</summary>
    public static double? Parse(string eventXml, DateTime lastBootUtc)
    {
        var ms = BootTimeData().Match(eventXml);
        var created = TimeCreated().Match(eventXml);
        if (!ms.Success || !created.Success ||
            !DateTime.TryParse(created.Groups[1].Value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var createdUtc))
        {
            return null;
        }

        // O Windows grava o evento alguns minutos depois de ligar; antes disso o último evento é da inicialização anterior.
        return createdUtc < lastBootUtc ? null : double.Parse(ms.Groups[1].Value, CultureInfo.InvariantCulture) / 1000;
    }

    [GeneratedRegex(@"<Data Name=['""]BootTime['""]>(\d+)</Data>")]
    private static partial Regex BootTimeData();

    [GeneratedRegex(@"<TimeCreated SystemTime=['""]([^'""]+)['""]")]
    private static partial Regex TimeCreated();
}
