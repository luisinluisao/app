namespace Slyth.Core.Platform;

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

    /// <summary>REG_MULTI_SZ; as linhas ficam separadas por '\n' em <see cref="RegistryValue.Data"/>.</summary>
    MultiString,
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

    /// <summary>Nomes das subchaves; vazio se a chave não existe.</summary>
    IReadOnlyList<string> GetSubKeyNames(RegistryRoot root, string key);
}
