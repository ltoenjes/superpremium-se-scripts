void Main()
{
    var Assemblers = new List<IMyTerminalBlock>();
    GridTerminalSystem.GetBlocksOfType<IMyAssembler>(Assemblers);
    if (Assemblers == null) return;

    var Cargo = new List<IMyTerminalBlock>();
    GridTerminalSystem.GetBlocksOfType<IMyCargoContainer>(Cargo);
    if (Cargo == null) return;
    IMyInventoryOwner CargoOwner = null;
    IMyCubeBlock CargoCube = null;

    // Find the first working functional non-empty cargo container
    var CargoIndex = 0;
    for (CargoIndex = 0; CargoIndex < Cargo.Count; CargoIndex++)
    {
        CargoOwner = (IMyInventoryOwner)Cargo[CargoIndex];
        CargoCube = (IMyCubeBlock)Cargo[CargoIndex];
        if (CargoCube.IsWorking && CargoCube.IsFunctional && !CargoOwner.GetInventory(0).IsFull) break;
    }
    if (CargoIndex >= Cargo.Count) return; // no empty cargo containers

    // check assemblers for clogging
    for (var Index = 0; Index < Assemblers.Count; Index++)
    {
        if (Assemblers[Index] == null) continue;
        var AssyOwner = (IMyInventoryOwner)Assemblers[Index];
        var Inventory = (IMyInventory)AssyOwner.GetInventory(0);
        var Items = new List<MyInventoryItem>();
        Inventory.GetItems(Items);
        VRage.MyFixedPoint MaxAmount;

        int i = -1;
        while (Inventory.IsItemAt(++i))
        { // set MaxAmount based on what it is.
            if (Items[i].Amount > 1000.0)
                Inventory.TransferItemTo(CargoOwner.GetInventory(0), i, null, true, Items[i].Amount - MaxAmount);
        }
    }
}