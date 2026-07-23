using CudaSpirit.Core.Models;

namespace CudaSpirit.Core.Services.Guidance;

public sealed class TransferAdvisor
{
    public TransferResult Evaluate(TransferRequest request)
    {
        var blockers = new List<string>();
        var methods = new List<string>();
        var steps = new List<string>();

        if (request.Binding == ItemBinding.CharacterBound)
            blockers.Add("The item is character-bound. Storage, Central Market, Family Inventory, and normal maid transfer cannot move it to another character.");
        if (request.IsGuildItem) blockers.Add("Guild items have guild-specific storage and transfer restrictions.");
        if (request.IsTradeGood) blockers.Add("Trade and barter goods are commonly blocked from maid and family-character retrieval.");
        if (request.IsTreasureItem) blockers.Add("Certain treasure items cannot be moved through ordinary maid/storage shortcuts.");

        if (request.FamilyInventoryEligible)
        {
            methods.Add("Family Inventory");
            steps.Add("Move the eligible consumable into Family Inventory, then withdraw it on the target character.");
        }
        if (request.HasStorageMaid && request.Binding != ItemBinding.CharacterBound && !request.IsGuildItem && !request.IsTradeGood)
        {
            methods.Add("Storage maid / character inventory retrieval");
            steps.Add("Open the world map or disconnect/character screen, view the other character's inventory, then retrieve the non-character-bound item with a storage maid within the weight limit.");
        }
        if (request.MagnusStorageUnlocked && request.Binding != ItemBinding.CharacterBound)
        {
            methods.Add("Magnus-linked town storage");
            steps.Add("Deposit into a discovered town storage and withdraw from another linked storage or through a storage maid.");
        }
        else if (request.Binding != ItemBinding.CharacterBound)
        {
            methods.Add("Same-town storage handoff");
            steps.Add("Place both characters in the same town, deposit the item in storage, switch characters, and withdraw it.");
        }
        if (request.IsMarketable && request.HasTransactionMaid && request.Binding != ItemBinding.CharacterBound)
        {
            methods.Add("Central Market warehouse handoff");
            steps.Add("Move the item into the Central Market warehouse with a transaction maid, switch characters, then withdraw it. Do not list it for sale.");
        }
        if (request.Binding != ItemBinding.CharacterBound)
        {
            methods.Add("Find My Item (Ctrl+F)");
            steps.Insert(0, "Press Ctrl+F, locate the item across family inventories and storages, then use the retrieval option shown for its location.");
        }

        var can = blockers.All(x => !x.StartsWith("The item is character-bound", StringComparison.Ordinal));
        if (methods.Count == 0 && can)
            steps.Add("Move both characters to the same city and use town storage. Verify that the item can be stored before traveling.");

        return new TransferResult
        {
            CanTransfer = can,
            Summary = can
                ? $"Use {methods.FirstOrDefault() ?? "town storage"} first. Keep the item unopened while moving it."
                : "Normal transfer is blocked. Do not attempt to bypass the binding state.",
            RecommendedMethods = methods.Distinct().ToList(),
            Blockers = blockers,
            Steps = steps.Distinct().ToList()
        };
    }
}
