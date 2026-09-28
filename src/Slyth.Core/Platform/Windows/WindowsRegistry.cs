using System.Globalization;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Slyth.Core.Platform.Windows;

[SupportedOSPlatform("windows")]
public sealed class WindowsRegistry : IRegistry
{
    public RegistryValue? GetValue(RegistryRoot root, string key, string name)
    {
        using var baseKey = OpenBase(root);
        using var subKey = baseKey.OpenSubKey(key);
        if (subKey is null || !subKey.GetValueNames().Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        var kind = subKey.GetValueKind(name);
        var raw = subKey.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        return kind switch
        {
            RegistryValueKind.DWord => new RegistryValue(RegistryValueType.DWord, Convert.ToString(raw, CultureInfo.InvariantCulture)!),
            RegistryValueKind.QWord => new RegistryValue(RegistryValueType.QWord, Convert.ToString(raw, CultureInfo.InvariantCulture)!),
            RegistryValueKind.String => new RegistryValue(RegistryValueType.String, (string)raw!),
            RegistryValueKind.ExpandString => new RegistryValue(RegistryValueType.ExpandString, (string)raw!),
            RegistryValueKind.MultiString => new RegistryValue(RegistryValueType.MultiString, string.Join('\n', (string[])raw!)),
            // Nunca sobrescrevemos um valor que não conseguimos salvar no backup.
            _ => throw new NotSupportedException($"Tipo de registro {kind} em {key}\\{name} não é suportado para backup."),
        };
    }

    public void SetValue(RegistryRoot root, string key, string name, RegistryValue value)
    {
        using var baseKey = OpenBase(root);
        using var subKey = baseKey.CreateSubKey(key, writable: true);
        switch (value.Type)
        {
            case RegistryValueType.DWord:
                subKey.SetValue(name, int.Parse(value.Data, CultureInfo.InvariantCulture), RegistryValueKind.DWord);
                break;
            case RegistryValueType.QWord:
                subKey.SetValue(name, long.Parse(value.Data, CultureInfo.InvariantCulture), RegistryValueKind.QWord);
                break;
            case RegistryValueType.String:
                subKey.SetValue(name, value.Data, RegistryValueKind.String);
                break;
            case RegistryValueType.ExpandString:
                subKey.SetValue(name, value.Data, RegistryValueKind.ExpandString);
                break;
            case RegistryValueType.MultiString:
                subKey.SetValue(name, value.Data.Split('\n'), RegistryValueKind.MultiString);
                break;
        }
    }

    public void DeleteValue(RegistryRoot root, string key, string name)
    {
        using var baseKey = OpenBase(root);
        using var subKey = baseKey.OpenSubKey(key, writable: true);
        subKey?.DeleteValue(name, throwOnMissingValue: false);
    }

    public IReadOnlyList<string> GetSubKeyNames(RegistryRoot root, string key)
    {
        using var baseKey = OpenBase(root);
        using var subKey = baseKey.OpenSubKey(key);
        return subKey?.GetSubKeyNames() ?? [];
    }

    private static RegistryKey OpenBase(RegistryRoot root) => RegistryKey.OpenBaseKey(
        root == RegistryRoot.CurrentUser ? RegistryHive.CurrentUser : RegistryHive.LocalMachine,
        RegistryView.Registry64);
}
