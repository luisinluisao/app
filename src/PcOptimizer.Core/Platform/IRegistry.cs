namespace PcOptimizer.Core.Platform;

public enum RegistryRoot
{
    CurrentUser,
    LocalMachine,
}

public enum RegistryValueType
{
    DWord,
    QWord,
    String,
    ExpandString,
}

/// <summary>Valor de registro serializável (usado também no backup em JSON).</summary>
public sealed record RegistryValue(RegistryValueType Type, string Data)
{
    public static RegistryValue Dword(int value) => new(RegistryValueType.DWord, value.ToString(System.Globalization.CultureInfo.InvariantCulture));

    public static RegistryValue Str(string value) => new(RegistryValueType.String, value);
}

/// <summary>Acesso ao registro do Windows, abstraído para permitir testes.</summary>
public interface IRegistry
{
    /// <summary>Retorna o valor atual, ou null se o valor (ou a chave) não existe.</summary>
    RegistryValue? GetValue(RegistryRoot root, string key, string name);

    void SetValue(RegistryRoot root, string key, string name, RegistryValue value);

    void DeleteValue(RegistryRoot root, string key, string name);
}
