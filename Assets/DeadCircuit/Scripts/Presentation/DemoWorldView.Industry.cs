namespace DeadCircuit
{
    public sealed partial class DemoWorldView
    {
        // Cargo and inventory resolve the same authored item. Never replace dedicated
        // sprites with runtime diamonds: every current ItemKind has its own atlas cell.
        void LoadItemArt()
        {
            for (int item = 0; item < itemSprites.Length; item++)
                itemSprites[item] = Art("item_" + item);
        }
    }
}
