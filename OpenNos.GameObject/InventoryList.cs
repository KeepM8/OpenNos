﻿/*
 * This file is part of the OpenNos Emulator Project. See AUTHORS file for Copyright information
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 */

using AutoMapper;
using OpenNos.Data;
using OpenNos.Domain;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenNos.GameObject
{
    public class InventoryList
    {
        #region Instantiation

        public InventoryList(Character Character)
        {
            Inventory = new List<Inventory>();
            Owner = Character;
        }

        #endregion

        #region Properties
        public Character Owner { get; set; }
        public List<Inventory> Inventory { get; set; }

        #endregion

        #region Methods

        public int CountItem(int v)
        {
            int count = 0;
            foreach (Inventory inv in Inventory.Where(s => s.ItemInstance.ItemVNum == v))
            {
                count += inv.ItemInstance.Amount;
            }
            return count;
        }

        public Inventory CreateInventory<T>(short vnum)
                     where T : ItemInstance
        {
            short Slot = -1;
            IEnumerable<ItemInstance> slotfree = null;
            Inventory inv = null;
            ItemInstance newItem = CreateItemInstance<ItemInstance>(vnum);
            if (newItem.Item.Type != 0)
            {
                slotfree = Owner.LoadBySlotAllowed(newItem.ItemVNum, newItem.Amount);
                inv = GetFirstSlot(slotfree);
            }
            if (inv != null)
            {
                inv.ItemInstance.Amount = (byte)(newItem.Amount + inv.ItemInstance.Amount);
            }
            else
            {
                Slot = GetFirstPlace(newItem.Item.Type, Owner.BackPack);
                if (Slot != -1)
                {
                    inv = AddToInventory(newItem, newItem.Item.Type, Slot);
                }
            }
            return inv;
        }

        public Inventory CreateInventory<T>(T newItem)
            where T : ItemInstance
        {
            short Slot = -1;
            IEnumerable<ItemInstance> slotfree = null;
            Inventory inv = null;
            if (newItem.Item.Type != 0)
            {
                slotfree = Owner.LoadBySlotAllowed(newItem.ItemVNum, newItem.Amount);
                inv = GetFirstSlot(slotfree);
            }
            if (inv != null)
            {
                inv.ItemInstance.Amount = (byte)(newItem.Amount + inv.ItemInstance.Amount);
            }
            else
            {
                Slot = GetFirstPlace(newItem.Item.Type, Owner.BackPack);
                if (Slot != -1)
                {
                    inv = AddToInventory(newItem, newItem.Item.Type, Slot);
                }
            }
            return inv;
        }

        public Tuple<short, byte> DeleteByInventoryItemId(long inventoryItemId)
        {
            Tuple<short, byte> removedPlace = new Tuple<short, byte>(0, 0);
            Inventory inv = Inventory.FirstOrDefault(i => i.ItemInstance.ItemInstanceId.Equals(inventoryItemId));

            if (inv != null)
            {
                removedPlace = new Tuple<short, byte>(inv.Slot, inv.Type);
                Inventory.Remove(inv);
            }

            return removedPlace;
        }

        public void DeleteFromSlotAndType(short slot, byte type)
        {
            Inventory inv = Inventory.FirstOrDefault(i => i.Slot.Equals(slot) && i.Type.Equals(type));

            if (inv != null)
            {
                Inventory.Remove(inv);
            }
        }

        public long GenerateInventoryId()
        {
            Random r = new Random();
            bool boolean = true;
            long inventoryId = -1;
            while (boolean)
            {
                boolean = false;
                inventoryId = (long)((r.NextDouble() * 2.0 - 1.0) * long.MaxValue);
                foreach (Inventory inv in Inventory)
                {
                    if (inv.InventoryId == inventoryId)
                    { boolean = true; break; }
                }
            }
            return inventoryId;
        }

        public long GenerateInventoryItemId()
        {
            Random r = new Random();
            bool boolean = true;
            int inventoryitemId = -1;
            while (boolean)
            {
                boolean = false;
                inventoryitemId = (int)((r.NextDouble() * 2.0 - 1.0) * int.MaxValue);
                foreach (Inventory inv in Inventory)
                {
                    if (inv.ItemInstance.ItemInstanceId == inventoryitemId)
                    { boolean = true; break; }
                }
            }
            return inventoryitemId;
        }

        public short GetFirstPlace(byte type, int backPack)
        {
            Inventory result;
            for (short i = 0; i < 48 + (backPack * 12); i++)
            {
                result = Inventory.FirstOrDefault(c => c.Type.Equals(type) && c.Slot.Equals(i));
                if (result == null)
                    return i;
            }
            return -1;
        }

        public Inventory GetFirstSlot(IEnumerable<ItemInstance> slotfree)
        {
            List<long> inventoryitemids = new List<long>();
            foreach (ItemInstance itemfree in slotfree)
            {
                inventoryitemids.Add(itemfree.ItemInstanceId);
            }
            return Inventory.Where(i => inventoryitemids.Contains(i.ItemInstance.ItemInstanceId)).OrderBy(i => i.Slot).FirstOrDefault();
        }

        public bool GetFreePlaceAmount(List<ItemInstance> item, int backPack)
        {
            short[] place = new short[10];
            for (byte k = 0; k < place.Count(); k++)
            {
                place[k] = (byte)(48 + (backPack * 12));
                for (short i = 0; i < 48 + (backPack * 12); i++)
                {
                    Inventory result = LoadInventoryBySlotAndType(i, k);
                    if (result != null && result.Type == 0)
                        place[k]--;
                    else if (result != null)
                    {
                        bool check = false;
                        // If an item stuck
                        foreach (ItemInstance itemins in item)
                        {
                            if (itemins.Item.Type != 0 && itemins.Amount + result.ItemInstance.Amount <= 99)
                                check = true;
                        }
                        if (!check)
                            place[k]--;
                    }
                }
            }
            bool test2 = true;
            foreach (ItemInstance itemins in item)
            {
                if (place[itemins.Item.Type] == 0)
                    test2 = false;
            }
            return test2;
        }

        public Inventory GetInventoryByInventoryItemId(long inventoryItemId)
        {
            return Inventory.FirstOrDefault(i => i.ItemInstance.ItemInstanceId.Equals(inventoryItemId));
        }

        public bool IsEmpty()
        {
            return !Inventory.Any();
        }

        public T LoadByInventoryItem<T>(long InventoryItemId)
            where T : ItemInstanceDTO
        {
            return (T)Inventory.FirstOrDefault(i => i.ItemInstance.ItemInstanceId.Equals(InventoryItemId))?.ItemInstance;
        }

        public Inventory LoadInventoryBySlotAndType(short slot, byte type)
        {
            return Inventory.SingleOrDefault(i => i.Slot.Equals(slot) && i.Type.Equals(type));
        }

        public T LoadBySlotAndType<T>(short slot, byte type)
            where T : ItemInstance
        {
            return (T)Inventory.FirstOrDefault(i => i.Type.Equals(typeof(T)) && i.Slot.Equals(slot) && i.Type.Equals(type))?.ItemInstance;
        }

        public Inventory MoveInventory(Inventory inv,  byte desttype, short destslot)
        {
            if (inv != null)
            {
                Item iteminfo = (inv.ItemInstance as ItemInstance).Item;
                Inventory invdest = LoadInventoryBySlotAndType(destslot, desttype);

                if (invdest == null && ((desttype == 6 && iteminfo.ItemType == 4) || (desttype == 7 && iteminfo.ItemType == 2) || desttype == 0))
                {
                    inv.Slot = destslot;
                    inv.Type = desttype;
                    return inv;
                }
            }
            return null;
        }

        public void MoveItem(Character character, byte type, short slot, byte amount, short destslot, out Inventory inv, out Inventory invdest)
        {
            inv = LoadInventoryBySlotAndType(slot, type);
            invdest = LoadInventoryBySlotAndType(destslot, type);
            if (inv != null && amount <= inv.ItemInstance.Amount)
            {
                if (invdest == null)
                {
                    if (inv.ItemInstance.Amount == amount)
                    {
                        inv.Slot = destslot;
                        Update(ref inv);
                    }
                    else
                    {
                        inv.ItemInstance.Amount = (byte)(inv.ItemInstance.Amount - amount);
                       
                        ItemInstance itemDest = inv.ItemInstance as ItemInstance;
                        Update(ref inv);

                        invdest = AddToInventory(itemDest, inv.Type, destslot);
                    }
                }
                else
                {
                    if (invdest.ItemInstance.ItemVNum == inv.ItemInstance.ItemVNum && inv.Type != 0)
                    {
                        if (invdest.ItemInstance.Amount + amount > 99)
                        {
                            int saveItemCount = invdest.ItemInstance.Amount;
                            invdest.ItemInstance.Amount = 99;
                            inv.ItemInstance.Amount = (byte)(saveItemCount + inv.ItemInstance.Amount - 99);

                            Update(ref inv);
                            Update(ref invdest);
                        }
                        else
                        {
                            int saveItemCount = invdest.ItemInstance.Amount;
                            invdest.ItemInstance.Amount = (byte)(saveItemCount + amount);
                            inv.ItemInstance.Amount = (byte)(inv.ItemInstance.Amount - amount);
                            Update(ref inv);
                            Update(ref invdest);
                        }
                    }
                    else
                    {
                        invdest.Slot = slot;
                        inv.Slot = destslot;
                        Update(ref inv);
                        Update(ref invdest);
                    }
                }
            }
            inv = LoadInventoryBySlotAndType(slot, type);
            invdest = LoadInventoryBySlotAndType(destslot, type);
        }

        public MapItem PutItem(ClientSession Session, byte type, short slot, byte amount, out Inventory inv)
        {
            Random rnd = new Random();
            int random = 0;
            int i = 0;
            inv = Session.Character.InventoryList.LoadInventoryBySlotAndType(slot, type);
            MapItem droppedItem = null;
            short MapX = (short)(rnd.Next(Session.Character.MapX - 1, Session.Character.MapX + 1));
            short MapY = (short)(rnd.Next(Session.Character.MapY - 1, Session.Character.MapY + 1));
            while (Session.CurrentMap.IsBlockedZone(MapX, MapY) && i < 5)
            {
                MapX = (short)(rnd.Next(Session.Character.MapX - 1, Session.Character.MapX + 1));
                MapY = (short)(rnd.Next(Session.Character.MapY - 1, Session.Character.MapY + 1));
                i++;
            }
            if (i == 5)
                return null;
            if (amount > 0 && amount <= inv.ItemInstance.Amount)
            {
                droppedItem = new MapItem(MapX, MapY)
                {
                    ItemInstance = inv.ItemInstance as ItemInstance
                };
                while (Session.CurrentMap.DroppedList.ContainsKey(random = rnd.Next(1, 999999)))
                { }
                droppedItem.ItemInstance.ItemInstanceId = random;
                Session.CurrentMap.DroppedList.Add(random, droppedItem);
                inv.ItemInstance.Amount = (byte)(inv.ItemInstance.Amount - amount);
                Session.Character.InventoryList.Update(ref inv);
            }
            return droppedItem;
        }

        public void RemoveItemAmount(int v, int amount)
        {
            for (int i = 0; i < Inventory.Where(s => s.ItemInstance.ItemVNum == v).OrderBy(s => s.Slot).Count(); i++)
            {
                Inventory inv = Inventory.Where(s => s.ItemInstance.ItemVNum == v).OrderBy(s => s.Slot).ElementAt(i);
                if (inv.ItemInstance.Amount > amount)
                {
                    inv.ItemInstance.Amount -= (byte)amount;
                    amount = 0;
                }
                else
                {
                    amount -= inv.ItemInstance.Amount;
                    DeleteByInventoryItemId(inv.ItemInstance.ItemInstanceId);
                }
            }
        }

        internal Inventory RemoveItemAmountFromInventory(byte amount, long InventoryId)
        {
            Inventory inv = Inventory.FirstOrDefault(i => i.InventoryId.Equals(InventoryId));

            if (inv != null)
            {
                inv.ItemInstance.Amount -= amount;
                if (inv.ItemInstance.Amount <= 0)
                {
                    Inventory.Remove(inv);
                    return null;
                }
            }

            return inv;
        }

        public ItemInstance CreateItemInstance<T>(short vnum)
        {
            ItemInstance iteminstance = new ItemInstance();
            if (iteminstance.Item != null)
            {
                switch (iteminstance.Item.Type)
                {
                    case (byte)InventoryType.Wear:
                        if (iteminstance.Item.ItemType == (byte)ItemType.Specialist)
                            iteminstance = new SpecialistInstance() { ItemVNum = vnum, SpLevel = 1 };
                        else
                            iteminstance = new WearableInstance() { ItemVNum = vnum };
                        break;
                    default:
                        iteminstance = new ItemInstance() { ItemVNum = vnum };
                        break;

                }
            }
            return iteminstance;
        }

        public void Update(ref Inventory newInventory)
        {
            short SLOT = newInventory.Slot;
            byte TYPE = newInventory.Type;

            Inventory entity = Inventory.FirstOrDefault(c => c.Slot.Equals(SLOT) && c.Type.Equals(TYPE));

            if (entity != null) 
            {
                long id = newInventory.InventoryId;
                var result = Inventory.FirstOrDefault(c => c.InventoryId == id);
                if (result != null)
                {
                    Inventory.Remove(result);
                    Inventory.Add(newInventory);
                }
                
            }
        }

        public Inventory AddToInventory(ItemInstance iteminstance, byte Type, short Slot)
        {
            Inventory inv = new Inventory() { Type = Type, Slot = Slot, ItemInstance = iteminstance, CharacterId = Owner.CharacterId,InventoryId= GenerateInventoryId() };
            if (Inventory.Any(s => s.Slot == Slot && s.Type == Type))
                return null;
            Inventory.Add(inv);
            return inv;
        }
        #endregion
    }
}