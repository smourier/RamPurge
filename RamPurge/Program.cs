using System.Reflection;

namespace RamPurge;

internal static class Program
{
    private const string _areasArgumentName = "areas";
    private const string _excludeOptionName = "--exclude";
    private const string _excludeOptionAlias = "/exclude";
    private const string _excludeHelpName = "name,name";
    private const string _areaResourcePrefix = "Area";
    private const char _listSeparator = ',';
    private const ulong _bytesPerMegabyte = 1024 * 1024;

    private static int Main(string[] args)
    {
        Console.WriteLine($"RamPurge - Version {typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion} - Copyright (C) 2015-{DateTime.Now.Year} Simon Mourier. All rights reserved.");
        Console.WriteLine();
        Console.OutputEncoding = Encoding.UTF8;

        var areasArgument = new Argument<MemoryAreas[]>(_areasArgumentName)
        {
            Description = Resources.AreasDescription,
            Arity = ArgumentArity.ZeroOrMore,
        };

        var excludeOption = new Option<string[]>(_excludeOptionName, _excludeOptionAlias)
        {
            Description = Resources.ExcludeDescription,
            HelpName = _excludeHelpName,
        };

        var rootCommand = new RootCommand(Resources.RootDescription) { areasArgument, excludeOption };
        rootCommand.SetAction(parseResult => Purge(parseResult.GetValue(areasArgument) ?? [], parseResult.GetValue(excludeOption) ?? []));
        return rootCommand.Parse(args).Invoke();
    }

    private static int Purge(MemoryAreas[] selectedAreas, string[] excludedNames)
    {
        var areas = selectedAreas.Aggregate(MemoryAreas.None, (all, area) => all | area);
        if (areas == MemoryAreas.None)
        {
            areas = MemoryAreas.All;
        }

        var exclusions = excludedNames
            .SelectMany(names => names.Split(_listSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(name => Path.GetFileNameWithoutExtension(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!MemoryOptimizer.IsElevated)
        {
            Console.WriteLine(Resources.NotElevated);
            Console.WriteLine();
        }

        var before = MemoryOptimizer.GetMemoryStatus();
        Console.WriteLine(Resources.MemoryBefore, before.ullAvailPhys / _bytesPerMegabyte, before.ullTotalPhys / _bytesPerMegabyte);
        var failed = false;
        foreach (var area in Enum.GetValues<MemoryAreas>())
        {
            if (!BitOperations.IsPow2((int)area) || !areas.HasFlag(area))
                continue;

            var name = Resources.ResourceManager.GetString(_areaResourcePrefix + area, Resources.Culture) ?? area.ToString();
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var detail = Optimize(area, exclusions);
                if (detail == null)
                {
                    Console.WriteLine(Resources.AreaSucceeded, name, stopwatch.ElapsedMilliseconds);
                }
                else
                {
                    Console.WriteLine(Resources.AreaSucceededWithDetail, name, detail, stopwatch.ElapsedMilliseconds);
                }
            }
            catch (Exception ex) when (ex is Win32Exception or IOException or UnauthorizedAccessException)
            {
                failed = true;
                Console.Error.WriteLine(Resources.AreaFailed, name, ex.Message);
            }
        }

        var after = MemoryOptimizer.GetMemoryStatus();
        var change = ((long)after.ullAvailPhys - (long)before.ullAvailPhys) / (long)_bytesPerMegabyte;
        Console.WriteLine();
        Console.WriteLine(Resources.MemoryAfter, after.ullAvailPhys / _bytesPerMegabyte, after.ullTotalPhys / _bytesPerMegabyte, change);
        return failed ? 1 : 0;
    }

    private static string? Optimize(MemoryAreas area, HashSet<string> exclusions)
    {
        switch (area)
        {
            case MemoryAreas.WorkingSet:
                // one system wide call is far cheaper than opening every process, but it cannot leave a process out.
                if (exclusions.Count == 0 && MemoryOptimizer.TryEnablePrivilege(SE_PROF_SINGLE_PROCESS_NAME))
                {
                    MemoryOptimizer.EmptySystemWorkingSets();
                    return null;
                }

                var trim = MemoryOptimizer.TrimProcessWorkingSets(exclusions);
                return string.Format(Resources.ProcessesTrimmed, trim.Trimmed, trim.Skipped);

            case MemoryAreas.SystemFileCache:
                MemoryOptimizer.FlushSystemFileCache();
                return null;

            case MemoryAreas.ModifiedPageList:
                MemoryOptimizer.FlushModifiedPageList();
                return null;

            case MemoryAreas.StandbyList:
                MemoryOptimizer.PurgeStandbyList(false);
                return null;

            case MemoryAreas.LowPriorityStandbyList:
                MemoryOptimizer.PurgeStandbyList(true);
                return null;

            case MemoryAreas.CombinedPageList:
                return string.Format(Resources.PagesCombined, MemoryOptimizer.CombinePages());

            case MemoryAreas.RegistryCache:
                MemoryOptimizer.ReconcileRegistry();
                return null;

            case MemoryAreas.VolumeCache:
                return string.Format(Resources.VolumesFlushed, MemoryOptimizer.FlushVolumes());

            default:
                throw new ArgumentOutOfRangeException(nameof(area));
        }
    }
}
