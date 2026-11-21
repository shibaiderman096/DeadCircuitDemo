namespace DeadCircuit
{
    public sealed partial class WorldSimulator
    {
        private bool TryFloorInteraction(PlayerState player, int x, int y)
        {
            if (!FloorWorld.IsStairCell(State, x, y)) return false;
            if (FloorWorld.TryTravel(State, player, x, y, out string notice))
            {
                if (inputs.TryGetValue(player.id, out var input))
                { ClearPlayerControl(player, input); input.x = input.y = 0f; input.sprint = false; }
                player.aiming = player.moving = false;
                CancelReload(player);
                RefreshSpatial(); flowRevision = -1;
                MapExploration.Reveal(State, player);
            }
            if (!string.IsNullOrEmpty(notice)) Notice(player, notice);
            return true;
        }
    }
}
