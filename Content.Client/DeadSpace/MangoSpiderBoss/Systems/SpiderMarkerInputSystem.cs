using Content.Shared.DeadSpace.MangoSpiderBoss.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Input;
using Robust.Shared.Map;

namespace Content.Client.DeadSpace.MangoSpiderBoss.Systems;

public sealed class SpiderMarkerInputSystem : EntitySystem
{
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly IInputManager _inputManager = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly InputSystem _inputSystem = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    private EntityUid? _marker;
    private bool _useWasDown;
    private bool _isTurning;

    public override void FrameUpdate(float frameTime)
    {
        var useDown = _inputSystem.CmdStates.GetState(EngineKeyFunctions.Use) == BoundKeyState.Down;
        var pressed = useDown && !_useWasDown;
        _useWasDown = useDown;

        if (_player.LocalEntity is not { } spider ||
            !TryComp<SpiderMarkerFollowComponent>(spider, out var comp))
        {
            ClearMarker();
            UpdateTurn(false);
            return;
        }

        UpdateTurn(true);

        if (pressed)
            TrySetTarget(comp);

        if (_marker is { } marker)
        {
            if (Deleted(marker))
            {
                _marker = null;
                return;
            }

            var dist = (_xform.GetWorldPosition(marker) - _xform.GetWorldPosition(spider)).Length();
            if (dist < comp.StopDistance)
                ClearMarker();
        }
    }

    private void TrySetTarget(SpiderMarkerFollowComponent comp)
    {
        var mousePos = _eyeManager.PixelToMap(_inputManager.MouseScreenPosition);

        if (mousePos.MapId == MapId.Nullspace)
            return;

        EntityCoordinates coordinates;

        if (_mapManager.TryFindGridAt(mousePos, out var gridUid, out _))
            coordinates = _xform.ToCoordinates(gridUid, mousePos);
        else
            coordinates = _xform.ToCoordinates(_map.GetMap(mousePos.MapId), mousePos);

        RaiseNetworkEvent(new SpiderSetTargetEvent(GetNetCoordinates(coordinates)));

        if (_marker is { } marker && !Deleted(marker))
            _xform.SetCoordinates(marker, coordinates);
        else
            _marker = SpawnAtPosition(comp.MarkerPrototype, coordinates);
    }

    private void ClearMarker()
    {
        if (_marker is { } marker && !Deleted(marker))
            QueueDel(marker);

        _marker = null;
    }

    private void UpdateTurn(bool controlling)
    {
        var turnDir = false;
        var turning = false;

        if (controlling)
        {
            if (_inputSystem.CmdStates.GetState(EngineKeyFunctions.MoveRight) == BoundKeyState.Down)
            {
                turnDir = true;
                turning = true;
            }
            if (_inputSystem.CmdStates.GetState(EngineKeyFunctions.MoveLeft) == BoundKeyState.Down)
            {
                turnDir = false;
                turning = true;
            }
        }

        if (turning == _isTurning)
            return;

        _isTurning = turning;

        if (turning)
            RaiseNetworkEvent(new SpiderStartRotationEvent(right: turnDir));
        else
            RaiseNetworkEvent(new SpiderStopRotationEvent());
    }

    public override void Shutdown()
    {
        base.Shutdown();
        ClearMarker();
        UpdateTurn(false);
    }
}