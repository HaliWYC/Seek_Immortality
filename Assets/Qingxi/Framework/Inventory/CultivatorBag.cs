using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class CultivatorBag
{
    public const int StandardCapacity = 8;
    public const int CarrySlotCount = 2;

    [Serializable]
    struct Holding
    {
        public string id;
        public int count;
    }

    // A row stores all interchangeable units of one item. Stacks are derived from
    // their definition, so importing a very large old stash never allocates per unit.
    [SerializeField] List<Holding> holdings = new List<Holding>();
    [SerializeField] int capacity = StandardCapacity;
    [SerializeField] string equippedWeapon, equippedClothing, equippedAccessory;
    [SerializeField] string carriedFirst, carriedSecond;
    [SerializeField] bool startingGearClaimed, battleKitClaimed;

    public int Capacity => Math.Max(StandardCapacity, capacity);
    public bool StartingGearClaimed => startingGearClaimed;
    public bool BattleKitClaimed => battleKitClaimed;
    public int UsedSlots
    {
        get
        {
            int used = 0;
            foreach (var holding in holdings)
            {
                var item = FirstVolumeItems.Find(holding.id);
                // Keep a removed definition's serialized row intact for later versions.
                used += item == null ? 1 : item.SlotsFor(holding.count);
            }
            return used;
        }
    }

    public int ReservedSlots
    {
        get
        {
            int empty = 0;
            foreach (var item in FirstVolumeItems.All)
                if (item.ReservedPocket && Count(item.Id) == 0) empty++;
            return empty;
        }
    }

    public int Count(string id)
    {
        foreach (var holding in holdings)
            if (holding.id == id) return holding.count;
        return 0;
    }

    string EquippedId(CultivationGearSlot slot) => slot switch
    {
        CultivationGearSlot.Weapon => equippedWeapon,
        CultivationGearSlot.Clothing => equippedClothing,
        CultivationGearSlot.Accessory => equippedAccessory,
        _ => null
    };

    public CultivationItem Equipped(CultivationGearSlot slot)
    {
        var id = EquippedId(slot);
        if (string.IsNullOrEmpty(id)) return null;
        var item = FirstVolumeItems.Find(id);
        return item?.Kind == CultivationItemKind.Equipment && item.GearSlot == slot ? item : null;
    }

    void SetEquipment(CultivationGearSlot slot, string id)
    {
        if (slot == CultivationGearSlot.Weapon) equippedWeapon = id;
        else if (slot == CultivationGearSlot.Clothing) equippedClothing = id;
        else if (slot == CultivationGearSlot.Accessory) equippedAccessory = id;
    }

    public bool TryEquip(string id)
    {
        var item = FirstVolumeItems.Find(id);
        if (item == null || item.Kind != CultivationItemKind.Equipment ||
            (item.GearSlot != CultivationGearSlot.Weapon && item.GearSlot != CultivationGearSlot.Clothing &&
             item.GearSlot != CultivationGearSlot.Accessory)) return false;
        var previous = EquippedId(item.GearSlot);
        if (previous == id) return false;
        bool transferred = string.IsNullOrEmpty(previous) ? TryRemove(id, 1) : TryExchange(id, 1, previous, 1);
        if (!transferred) return false;
        SetEquipment(item.GearSlot, id);
        return true;
    }

    public bool TryUnequip(CultivationGearSlot slot)
    {
        var id = EquippedId(slot);
        if (string.IsNullOrEmpty(id) || !TryAdd(id, 1)) return false;
        SetEquipment(slot, null);
        return true;
    }

    public bool TryClaimStartingGear()
    {
        if (startingGearClaimed || !TryAddPair(FirstVolumeItems.TravelBlade, 1, FirstVolumeItems.TravelCoat, 1))
            return false;
        startingGearClaimed = true;
        return true;
    }

    public bool TryClaimBattleKit()
    {
        if (battleKitClaimed || !TryAddPair(FirstVolumeItems.SpiritPendant, 1, FirstVolumeItems.SpringPill, 2))
            return false;
        battleKitClaimed = true;
        return true;
    }

    bool TryAddPair(string firstId, int firstCount, string secondId, int secondCount)
    {
        var first = FirstVolumeItems.Find(firstId);
        var second = FirstVolumeItems.Find(secondId);
        int firstBefore = Count(firstId), secondBefore = Count(secondId);
        if (firstBefore > int.MaxValue - firstCount || secondBefore > int.MaxValue - secondCount) return false;
        long slots = (long)first.SlotsWithReservation(firstBefore + firstCount) - first.SlotsWithReservation(firstBefore) +
            second.SlotsWithReservation(secondBefore + secondCount) - second.SlotsWithReservation(secondBefore);
        if (!CanGrowBy(slots)) return false;

        SetCount(firstId, firstBefore + firstCount);
        SetCount(secondId, secondBefore + secondCount);
        return true;
    }

    string CarriedId(int slot) => slot switch { 0 => carriedFirst, 1 => carriedSecond, _ => null };

    void SetCarried(int slot, string id)
    {
        if (slot == 0) carriedFirst = id;
        else if (slot == 1) carriedSecond = id;
    }

    public CultivationItem Carried(int slot)
    {
        var id = CarriedId(slot);
        if (string.IsNullOrEmpty(id)) return null;
        var item = FirstVolumeItems.Find(id);
        return item != null && item.CanCarry ? item : null;
    }

    public bool TryPack(int slot, string id)
    {
        if (slot < 0 || slot >= CarrySlotCount) return false;
        var item = FirstVolumeItems.Find(id);
        if (item == null || !item.CanCarry) return false;

        string previous = CarriedId(slot);
        if (previous == id) return false;
        bool transferred = string.IsNullOrEmpty(previous) ? TryRemove(id, 1) : TryExchange(id, 1, previous, 1);
        if (!transferred) return false;
        SetCarried(slot, id);
        return true;
    }

    public bool TryUnpack(int slot)
    {
        var item = Carried(slot);
        if (item == null || !TryAdd(item.Id, 1)) return false;
        SetCarried(slot, null);
        return true;
    }

    public bool TryConsumeCarried(int slot, string id)
    {
        var item = Carried(slot);
        if (item == null || item.Id != id) return false;
        SetCarried(slot, null);
        return true;
    }

    bool CanGrowBy(long slots) => slots <= 0 || (long)UsedSlots + ReservedSlots + slots <= Capacity;

    void SetCount(string id, int count)
    {
        var next = new Holding { id = id, count = count };
        for (int i = 0; i < holdings.Count; i++)
        {
            if (holdings[i].id != id) continue;
            if (count == 0) holdings.RemoveAt(i);
            else holdings[i] = next;
            return;
        }
        if (count > 0) holdings.Add(next);
    }

    public bool TryAdd(string id, int count) => TryExchange(null, 0, id, count);

    public bool TryRemove(string id, int count)
    {
        if (count < 0 || FirstVolumeItems.Find(id) == null || Count(id) < count) return false;
        SetCount(id, Count(id) - count);
        return true;
    }

    // Test the final contents, including any stack emptied by payment, before
    // changing either side. A failed purchase or recipe leaves the bag untouched.
    public bool TryExchange(string paymentId, int cost, string rewardId, int reward)
    {
        var income = FirstVolumeItems.Find(rewardId);
        if (income == null || reward < 0 || cost < 0) return false;
        var payment = cost == 0 ? null : FirstVolumeItems.Find(paymentId);
        int paidBefore = Count(paymentId);
        if (cost > 0 && (payment == null || paidBefore < cost)) return false;

        int held = Count(rewardId);
        bool sameItem = paymentId == rewardId;
        long next = (long)held + reward - (sameItem ? cost : 0);
        if (next > int.MaxValue) return false;
        int change = income.SlotsWithReservation((int)next) - income.SlotsWithReservation(held);
        if (payment != null && !sameItem)
            change += payment.SlotsWithReservation(paidBefore - cost) - payment.SlotsWithReservation(paidBefore);
        if (!CanGrowBy(change)) return false;

        if (payment != null && !sameItem) SetCount(paymentId, paidBefore - cost);
        SetCount(rewardId, (int)next);
        return true;
    }

    internal static CultivatorBag FromFirstVolume(int stones, int herbs, int pills, bool dew)
    {
        var bag = new CultivatorBag();
        bag.SetCount(FirstVolumeItems.SpiritStone, Math.Max(0, stones));
        bag.SetCount(FirstVolumeItems.JadeHerb, Math.Max(0, herbs));
        bag.SetCount(FirstVolumeItems.FoundationPill, Math.Max(0, pills));
        if (dew) bag.SetCount(FirstVolumeItems.MeridianDew, 1);
        bag.capacity = Math.Max(StandardCapacity, bag.UsedSlots + bag.ReservedSlots);
        return bag;
    }
}
