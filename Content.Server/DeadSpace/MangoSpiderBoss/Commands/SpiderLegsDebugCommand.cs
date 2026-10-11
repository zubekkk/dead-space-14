using Content.Server.Administration;
using Content.Server.DeadSpace.MangoSpiderBoss.Systems;
using Content.Shared.Administration;
using Content.Shared.DeadSpace.MangoSpiderBoss.Components;
using Robust.Shared.Console;

namespace Content.Server.DeadSpace.MangoSpiderBoss.Commands;

[AdminCommand(AdminFlags.Debug)]
public sealed class SpiderLegsDebugCommand : LocalizedEntityCommands
{
    [Dependency] private readonly SpiderLegsSystem _legs = default!;

    public override string Command => "spiderlegsdebug";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length > 1)
        {
            shell.WriteError("Usage: spiderlegsdebug | spiderlegsdebug all | spiderlegsdebug <uid>");
            return;
        }

        if (args.Length == 0)
        {
            if (shell.Player?.AttachedEntity is not { } player)
            {
                shell.WriteError("You need to be attached to an entity.");
                return;
            }

            Toggle(shell, player, player.ToString());
            return;
        }

        if (args[0] == "all")
        {
            var spiders = new List<EntityUid>();
            var query = EntityManager.EntityQueryEnumerator<SpiderLegsComponent>();
            while (query.MoveNext(out var spider, out _))
                spiders.Add(spider);

            if (spiders.Count == 0)
            {
                shell.WriteError("No spiders with legs found.");
                return;
            }

            var allEnabled = true;
            foreach (var spider in spiders)
            {
                if (!_legs.IsLegDebugEnabled(spider))
                {
                    allEnabled = false;
                    break;
                }
            }
            foreach (var spider in spiders)
                _legs.SetDebugLegPoints(spider, !allEnabled);

            shell.WriteLine(allEnabled
                ? $"Disabled leg points debug for {spiders.Count} spiders."
                : $"Enabled leg points debug for {spiders.Count} spiders.");
            return;
        }

        if (!NetEntity.TryParse(args[0], out var net) || !EntityManager.TryGetEntity(net, out var entity))
        {
            shell.WriteError($"Failed parsing entity uid '{args[0]}'.");
            return;
        }

        Toggle(shell, entity.Value, net.ToString());
    }

    private void Toggle(IConsoleShell shell, EntityUid uid, string label)
    {
        _legs.SetDebugLegPoints(uid, !_legs.IsLegDebugEnabled(uid));
        shell.WriteLine($"Leg points debug {(_legs.IsLegDebugEnabled(uid) ? "enabled" : "disabled")} for {label}.");
    }
}