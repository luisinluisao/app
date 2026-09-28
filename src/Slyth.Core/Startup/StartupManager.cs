using System.Buffers.Binary;
using Slyth.Core.Platform;

namespace Slyth.Core.Startup;

public enum StartupSource
{
    UserRegistry,
    MachineRegistry,
    MachineRegistry32,
    UserFolder,
    CommonFolder,
}

public sealed record StartupEntry(string Name, string Command, StartupSource Source, bool Enabled)
{
    public string SourceName => Source switch
    {
        StartupSource.UserRegistry => "Registro · usuário",
        StartupSource.MachineRegistry or StartupSource.MachineRegistry32 => "Registro · todos os usuários",
        StartupSource.UserFolder => "Pasta Inicializar · usuário",
        _ => "Pasta Inicializar · todos os usuários",
    };

    /// <summary>Caminho do executável extraído da linha de comando (com ou sem aspas e argumentos).</summary>
    public string ExecutablePath
    {
        get
        {
            var command = Environment.ExpandEnvironmentVariables(Command.Trim());
            if (command.StartsWith('"'))
            {
                var end = command.IndexOf('"', 1);
                return end > 1 ? command[1..end] : command.Trim('"');
            }

            var exe = command.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            return exe > 0 ? command[..(exe + 4)] : command.Split(' ')[0];
        }
    }
}

/// <summary>
/// Lista e liga/desliga programas que abrem com o Windows, usando o mesmo mecanismo do
/// Gerenciador de Tarefas (StartupApproved): nada é apagado, então religar é sempre possível.
/// </summary>
public sealed class StartupManager(IRegistry registry, string userFolder, string commonFolder)
{
    private const string Run = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Run32 = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run";
    private const string Approved = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved";

    public static StartupManager ForCurrentUser(IRegistry registry) => new(
        registry,
        Environment.GetFolderPath(Environment.SpecialFolder.Startup),
        Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup));

    public IReadOnlyList<StartupEntry> List()
    {
        var entries = new List<StartupEntry>();
        AddRegistry(entries, RegistryRoot.CurrentUser, Run, StartupSource.UserRegistry);
        AddRegistry(entries, RegistryRoot.LocalMachine, Run, StartupSource.MachineRegistry);
        AddRegistry(entries, RegistryRoot.LocalMachine, Run32, StartupSource.MachineRegistry32);
        AddFolder(entries, userFolder, StartupSource.UserFolder);
        AddFolder(entries, commonFolder, StartupSource.CommonFolder);
        return entries.OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public void SetEnabled(StartupEntry entry, bool enabled)
    {
        var (root, key) = ApprovedLocation(entry.Source);
        var bytes = new byte[12];
        bytes[0] = enabled ? (byte)0x02 : (byte)0x03;
        if (!enabled)
        {
            BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(4), DateTime.UtcNow.ToFileTimeUtc());
        }

        registry.SetValue(root, key, entry.Name, new RegistryValue(RegistryValueType.Binary, Convert.ToHexString(bytes)));
    }

    private void AddRegistry(List<StartupEntry> entries, RegistryRoot root, string key, StartupSource source)
    {
        foreach (var name in registry.GetValueNames(root, key).Where(n => n.Length > 0))
        {
            try
            {
                if (registry.GetValue(root, key, name) is { Type: RegistryValueType.String or RegistryValueType.ExpandString } value)
                {
                    entries.Add(new StartupEntry(name, value.Data, source, IsEnabled(source, name)));
                }
            }
            catch (NotSupportedException)
            {
            }
        }
    }

    private void AddFolder(List<StartupEntry> entries, string folder, StartupSource source)
    {
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(folder).Where(f => !Path.GetFileName(f).Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)))
        {
            var name = Path.GetFileName(file);
            entries.Add(new StartupEntry(name, file, source, IsEnabled(source, name)));
        }
    }

    /// <summary>Sem registro em StartupApproved = ligado; primeiro byte ímpar (0x03, 0x07...) = desligado.</summary>
    private bool IsEnabled(StartupSource source, string name)
    {
        var (root, key) = ApprovedLocation(source);
        var value = registry.GetValue(root, key, name);
        if (value is not { Type: RegistryValueType.Binary } || value.Data.Length < 2)
        {
            return true;
        }

        return (Convert.ToByte(value.Data[..2], 16) & 1) == 0;
    }

    private static (RegistryRoot Root, string Key) ApprovedLocation(StartupSource source) => source switch
    {
        StartupSource.UserRegistry => (RegistryRoot.CurrentUser, $@"{Approved}\Run"),
        StartupSource.MachineRegistry => (RegistryRoot.LocalMachine, $@"{Approved}\Run"),
        StartupSource.MachineRegistry32 => (RegistryRoot.LocalMachine, $@"{Approved}\Run32"),
        StartupSource.UserFolder => (RegistryRoot.CurrentUser, $@"{Approved}\StartupFolder"),
        _ => (RegistryRoot.LocalMachine, $@"{Approved}\StartupFolder"),
    };
}
