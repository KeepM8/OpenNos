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

using OpenNos.Core;
using OpenNos.Domain;
using OpenNos.GameObject;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenNos.Handler
{
    public class NpcPacketHandler : IPacketHandler
    {
        #region Members

        private readonly ClientSession _session;

        #endregion

        #region Instantiation

        public NpcPacketHandler(ClientSession session)
        {
            _session = session;
        }

        #endregion

        #region Properties

        public ClientSession Session { get { return _session; } }

        #endregion

        #region Methods

        [Packet("buy")]
        public void BuyShop(string packet)
        {
            string[] packetsplit = packet.Split(' ');
            if (packetsplit.Length < 5)
                return;
            long owner; long.TryParse(packetsplit[3], out owner);
            byte type; byte.TryParse(packetsplit[2], out type);
            short slot; short.TryParse(packetsplit[4], out slot);
            byte amount = 0;
            if (packetsplit.Length == 6)
                byte.TryParse(packetsplit[5], out amount);

            if (type == 1) // User shop
            {
                KeyValuePair<long, MapShop> shop = Session.CurrentMap.ShopUserList.FirstOrDefault(mapshop => mapshop.Value.OwnerId.Equals(owner));
                PersonalShopItem item = shop.Value.Items.FirstOrDefault(i => i.Slot.Equals(slot));
                if (item == null || amount <= 0) return;

                if (amount > item.Amount)
                    amount = item.Amount;

                if (item.Price * amount + ClientLinkManager.Instance.GetProperty<long>(shop.Value.OwnerId, "Gold") > 1000000000)
                {
                    Session.Client.SendPacket(Session.Character.GenerateShopMemo(3,
                        Language.Instance.GetMessageFromKey("MAX_GOLD")));
                    return;
                }

                if (item.Price * amount >= Session.Character.Gold)
                {
                    Session.Client.SendPacket(Session.Character.GenerateShopMemo(3,
                        Language.Instance.GetMessageFromKey("NOT_ENOUGH_MONEY")));
                    return;
                }

                ItemInstance item2 = (item.ItemInstance as ItemInstance).DeepCopy();
                item2.Amount = amount;
                item2.ItemInstanceId = Session.Character.InventoryList.GenerateItemInstanceId();
                Inventory inv = Session.Character.InventoryList.AddToInventory(item2);

                if (inv != null)
                {
                    Session.Client.SendPacket(Session.Character.GenerateInventoryAdd(inv.ItemInstance.ItemVNum,
                        inv.ItemInstance.Amount, inv.Type, inv.Slot, inv.ItemInstance.Rare, inv.ItemInstance.Design, inv.ItemInstance.Upgrade));
                    Session.Character.Gold -= item.Price * amount;
                    Session.Client.SendPacket(Session.Character.GenerateGold());
                }
                else
                    Session.Client.SendPacket(Session.Character.GenerateMsg(Language.Instance.GetMessageFromKey("NOT_ENOUGH_PLACE"), 0));
                ClientLinkManager.Instance.BuyValidate(Session, shop, slot, amount);
                KeyValuePair<long, MapShop> shop2 = Session.CurrentMap.ShopUserList.FirstOrDefault(s => s.Value.OwnerId.Equals(owner));
                LoadShopItem(owner, shop2);
            }
            else if (packetsplit.Length == 5) // skill shop
            {
                if (Session.Character.UseSp)
                {
                    Session.Client.SendPacket(Session.Character.GenerateMsg(Language.Instance.GetMessageFromKey("REMOVE_SP"), 0));
                    return;
                }
                Skill skillinfo = ServerManager.GetSkill(slot);

                if (skillinfo == null)
                    return;
                if (Session.Character.Gold >= skillinfo.Price && Session.Character.GetCP() >= skillinfo.CPCost && Session.Character.Level >= skillinfo.LevelMinimum)
                {
                    switch (Session.Character.Class)
                    {
                        case (byte)ClassType.Adventurer:
                            if (Session.Character.Level < skillinfo.MinimumAdventurerLevel)
                                return;
                            break;

                        case (byte)ClassType.Swordman:
                            if (Session.Character.Level < (skillinfo.MinimumSwordmanLevel == 0 ? skillinfo.MinimumAdventurerLevel : skillinfo.MinimumSwordmanLevel))
                                return;
                            break;

                        case (byte)ClassType.Archer:
                            if (Session.Character.Level < (skillinfo.MinimumArcherLevel == 0 ? skillinfo.MinimumAdventurerLevel : skillinfo.MinimumArcherLevel))
                                return;
                            break;

                        case (byte)ClassType.Magician:
                            if (Session.Character.Level < (skillinfo.MinimumMagicianLevel == 0 ? skillinfo.MinimumAdventurerLevel : skillinfo.MinimumMagicianLevel))
                                return;
                            break;
                    }

                    if (Session.Character.Skills.FirstOrDefault(s => s.SkillVNum == slot) != null)
                        return;

                    Session.Character.Gold -= skillinfo.Price;
                    Session.Client.SendPacket(Session.Character.GenerateGold());
                    Skill ski = ServerManager.GetSkill(slot);

                    if (ski == null || !(ski.Class == Session.Character.Class || (ski.Class == 0 && ski.SkillVNum < 100)))
                        return;

                    Session.Character.Skills.Add(new CharacterSkill() { SkillVNum = slot, CharacterId = Session.Character.CharacterId });
                    Session.Client.SendPacket(Session.Character.GenerateSki());
                    string[] quicklistpackets = Session.Character.GenerateQuicklist();
                    foreach (string quicklist in quicklistpackets)
                        Session.Client.SendPacket(quicklist);

                    Session.Client.SendPacket(Session.Character.GenerateMsg(Language.Instance.GetMessageFromKey("SKILL_LEARNED"), 0));
                    Session.Client.SendPacket(Session.Character.GenerateLev());
                }
                else
                {
                    Session.Client.SendPacket(Session.Character.GenerateMsg(Language.Instance.GetMessageFromKey("NOT_ENOUGH_CP"), 0));
                }
            }
            else
            {
                MapNpc npc = Session.CurrentMap.Npcs.FirstOrDefault(n => n.MapNpcId.Equals((short)owner));

                ShopItem item = npc?.Shop.ShopItems.FirstOrDefault(it => it.Slot == slot);
                if (item == null) return;
                Item iteminfo = ServerManager.GetItem(item.ItemVNum);
                long price = iteminfo.Price * amount;
                long Reputprice = iteminfo.ReputPrice * amount;
                double pourcent = 1;
                if (Session.Character.GetDigniteIco() == 3)
                    pourcent = 1.10;
                else if (Session.Character.GetDigniteIco() == 4)
                    pourcent = 1.20;
                else if (Session.Character.GetDigniteIco() == 5 || Session.Character.GetDigniteIco() == 6)
                    pourcent = 1.5;
                byte rare = item.Rare;
                if (iteminfo.ReputPrice == 0)
                {
                    if (price < 0 || price * pourcent > Session.Character.Gold)
                    {
                        Session.Client.SendPacket(Session.Character.GenerateShopMemo(3, Language.Instance.GetMessageFromKey("NOT_ENOUGH_MONEY")));
                        return;
                    }
                }
                else
                {
                    if (Reputprice <= 0 || Reputprice > Session.Character.Reput)
                    {
                        Session.Client.SendPacket(Session.Character.GenerateShopMemo(3, Language.Instance.GetMessageFromKey("NOT_ENOUGH_REPUT")));
                        return;
                    }
                    Random rnd = new Random();
                    byte ra = (byte)rnd.Next(0, 100);

                    int[] rareprob = { 100, 100, 70, 50, 30, 15, 5, 1 };
                    if (iteminfo.ReputPrice != 0)
                        for (int i = 0; i < rareprob.Length; i++)
                        {
                            if (ra <= rareprob[i])
                                rare = (byte)i;
                        }
                }

                Inventory newItem = Session.Character.InventoryList.AddNewItemToInventory(item.ItemVNum, amount);
                newItem.ItemInstance.Rare = rare;
                newItem.ItemInstance.Upgrade = item.Upgrade;
                newItem.ItemInstance.Design = item.Color;

                if (newItem != null && newItem.Slot != -1)
                {
                    Session.Client.SendPacket(Session.Character.GenerateInventoryAdd(newItem.ItemInstance.ItemVNum,
                        newItem.ItemInstance.Amount, newItem.Type, newItem.Slot, newItem.ItemInstance.Rare, newItem.ItemInstance.Design, newItem.ItemInstance.Upgrade));
                    if (iteminfo.ReputPrice == 0)
                    {
                        Session.Client.SendPacket(Session.Character.GenerateShopMemo(1, string.Format(Language.Instance.GetMessageFromKey("BUY_ITEM_VALIDE"), ServerManager.GetItem(item.ItemVNum).Name, amount)));
                        Session.Character.Gold -= (long)(price * pourcent);
                        Session.Client.SendPacket(Session.Character.GenerateGold());
                    }
                    else
                    {
                        Session.Client.SendPacket(Session.Character.GenerateShopMemo(1, string.Format(Language.Instance.GetMessageFromKey("BUY_ITEM_VALIDE"), ServerManager.GetItem(item.ItemVNum).Name, amount)));
                        Session.Character.Reput -= (long)(Reputprice);
                        Session.Client.SendPacket(Session.Character.GenerateFd());
                        Session.Client.SendPacket(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("REPUT_DECREASED"), 11));
                    }
                }
                else
                    Session.Client.SendPacket(Session.Character.GenerateMsg(Language.Instance.GetMessageFromKey("NOT_ENOUGH_PLACE"), 0));
            }
        }

        [Packet("m_shop")]
        public void CreateShop(string packet)
        {
            string[] packetsplit = packet.Split(' ');
            byte[] type = new byte[20];
            long[] gold = new long[20];
            short[] slot = new short[20];
            byte[] qty = new byte[20];

            string shopname = "";
            if (packetsplit.Length > 2)
            {
                foreach (Portal por in Session.CurrentMap.Portals)
                {
                    if (Session.Character.MapX < por.SourceX + 6 && Session.Character.MapX > por.SourceX - 6 && Session.Character.MapY < por.SourceY + 6 && Session.Character.MapY > por.SourceY - 6)
                    {
                        Session.Client.SendPacket(Session.Character.GenerateMsg(Language.Instance.GetMessageFromKey("SHOP_NEAR_PORTAL"), 0));
                        return;
                    }
                }
                short typePacket; short.TryParse(packetsplit[2], out typePacket);
                if (typePacket == 2)
                {
                    Session.Client.SendPacket("ishop");
                }
                else if (typePacket == 0)
                {
                    if (Session.CurrentMap.ShopUserList.Where(s => s.Value.OwnerId == Session.Character.CharacterId).Count() != 0)
                    {
                        return;
                    }
                    MapShop myShop = new MapShop();

                    if (packetsplit.Length > 2)
                        for (short j = 3, i = 0; j <= packetsplit.Length - 5; j += 4, i++)
                        {
                            byte.TryParse(packetsplit[j], out type[i]);
                            short.TryParse(packetsplit[j + 1], out slot[i]);
                            byte.TryParse(packetsplit[j + 2], out qty[i]);
                            long.TryParse(packetsplit[j + 3], out gold[i]);
                            if (qty[i] != 0)
                            {
                                Inventory inv = Session.Character.InventoryList.LoadInventoryBySlotAndType(slot[i], type[i]);

                                PersonalShopItem personalshopitem = new PersonalShopItem()
                                {
                                    Slot = slot[i],
                                    Type = type[i],
                                    Price = gold[i],
                                    InventoryId = inv.InventoryId,
                                    CharacterId = inv.CharacterId,
                                    Amount = qty[i],
                                    ItemInstance = inv.ItemInstance
                                };
                                myShop.Items.Add(personalshopitem);
                            }
                        }
                    if (myShop.Items.Count != 0)
                    {
                        for (int i = 83; i < packetsplit.Length; i++)
                            shopname += $"{packetsplit[i]} ";

                        shopname.TrimEnd(' ');

                        myShop.OwnerId = Session.Character.CharacterId;
                        myShop.Name = shopname;

                        Session.CurrentMap.ShopUserList.Add(Session.CurrentMap.ShopUserList.Count(), myShop);

                        ClientLinkManager.Instance.Broadcast(Session, Session.Character.GeneratePlayerFlag(Session.CurrentMap.ShopUserList.Count()), ReceiverType.AllOnMapExceptMe);
                        ClientLinkManager.Instance.Broadcast(Session, Session.Character.GenerateShop(shopname), ReceiverType.AllOnMap);

                        Session.Client.SendPacket(Session.Character.GenerateInfo(Language.Instance.GetMessageFromKey("SHOP_OPEN")));
                        Session.Character.IsSitting = true;
                        Session.Character.LastSpeed = Session.Character.Speed;
                        Session.Character.Speed = 0;
                        Session.Client.SendPacket(Session.Character.GenerateCond());

                        ClientLinkManager.Instance.Broadcast(Session, Session.Character.GenerateRest(), ReceiverType.AllOnMap);
                    }
                    else
                    {
                        Session.Client.SendPacket("shop_end 0");
                        Session.Client.SendPacket(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("SHOP_EMPTY"), 10));
                    }
                }
                else if (typePacket == 1)
                {
                    KeyValuePair<long, MapShop> shop = Session.CurrentMap.ShopUserList.FirstOrDefault(mapshop => mapshop.Value.OwnerId.Equals(Session.Character.CharacterId));
                    Session.CurrentMap.ShopUserList.Remove(shop.Key);

                    ClientLinkManager.Instance.Broadcast(Session, Session.Character.GenerateShopEnd(), ReceiverType.AllOnMap);
                    ClientLinkManager.Instance.Broadcast(Session, Session.Character.GeneratePlayerFlag(0), ReceiverType.AllOnMapExceptMe);
                    Session.Character.Speed = Session.Character.LastSpeed != 0 ? Session.Character.LastSpeed : Session.Character.Speed;
                    Session.Character.IsSitting = false;
                    Session.Client.SendPacket(Session.Character.GenerateCond());
                    ClientLinkManager.Instance.Broadcast(Session, Session.Character.GenerateRest(), ReceiverType.AllOnMap);
                }
            }
        }

        [Packet("n_run")]
        public void NpcRunFunction(string packet)
        {
            string[] packetsplit = packet.Split(' ');
            if (packetsplit.Length <= 5) return;

            byte type; byte.TryParse(packetsplit[3], out type);
            short runner; short.TryParse(packetsplit[2], out runner);
            short data3; short.TryParse(packetsplit[4], out data3);
            short npcid; short.TryParse(packetsplit[5], out npcid);
            Session.Character.LastNRunId = npcid;
            if(Session.Character.Hp > 0)
            NRunHandler.NRun(Session, type, runner, data3, npcid);
        }

        [Packet("pdtse")]
        public void Pdtse(string packet)
        {
            string[] packetsplit = packet.Split(' ');
            if (packetsplit.Count() < 4)
                return;

            byte type = 0;
            byte.TryParse(packetsplit[2], out type);
            if (type == 1)
            {
                MapNpc npc = Session.CurrentMap.Npcs.FirstOrDefault(s => s.MapNpcId == Session.Character.LastNRunId);
                if (npc != null)
                {
                    Recipe rec = npc.Recipes.FirstOrDefault(s => s.ItemVNum == short.Parse(packetsplit[3]));
                    if (rec != null && rec.Amount > 0)
                    {
                        String rece = $"m_list 3 {rec.Amount}";
                        foreach (RecipeItem ite in rec.Items)
                        {
                            if (ite.Amount > 0)
                                rece += String.Format($" {ite.ItemVNum} {ite.Amount}");
                        }
                        rece += " -1";
                        Session.Client.SendPacket(rece);
                    }
                }
            }
            else
            {
                MapNpc npc = Session.CurrentMap.Npcs.FirstOrDefault(s => s.MapNpcId == Session.Character.LastNRunId);
                if (npc != null)
                {
                    Recipe rec = npc.Recipes.FirstOrDefault(s => s.ItemVNum == short.Parse(packetsplit[3]));
                    if (rec != null)
                    {
                        if (rec.Amount <= 0)
                            return;
                        foreach (RecipeItem ite in rec.Items)
                        {
                            if (Session.Character.InventoryList.CountItem(ite.ItemVNum) < ite.Amount)
                                return;
                        }

                        Inventory inv = Session.Character.InventoryList.AddNewItemToInventory(rec.ItemVNum, rec.Amount);
                        if (inv.ItemInstance.GetType().Equals(typeof(WearableInstance)))
                        {
                            WearableInstance item = inv.ItemInstance as WearableInstance;
                            item.SetRarityPoint();
                        }

                        if (inv != null)
                        {
                            short Slot = inv.Slot;
                            if (Slot != -1)
                            {
                                foreach (RecipeItem ite in rec.Items)
                                {
                                    Session.Character.InventoryList.RemoveItemAmount(ite.ItemVNum, ite.Amount);
                                }
                                Session.Character.GenerateStartupInventory();

                                Session.Client.SendPacket($"pdti 11 {inv.ItemInstance.ItemVNum} {rec.Amount} 29 {inv.ItemInstance.Upgrade} 0");
                                Session.Client.SendPacket($"guri 19 1 {Session.Character.CharacterId} 1324");

                                Session.Client.SendPacket(Session.Character.GenerateMsg(String.Format(Language.Instance.GetMessageFromKey("CRAFTED_OBJECT"), (inv.ItemInstance as ItemInstance).Item.Name, rec.Amount), 0));
                            }
                        }
                        else
                        {
                            Session.Client.SendPacket(Session.Character.GenerateMsg(Language.Instance.GetMessageFromKey("NOT_ENOUGH_PLACE"), 0));
                        }
                    }
                }
            }
        }

        [Packet("sell")]
        public void SellShop(string packet)
        {
            string[] packetsplit = packet.Split(' ');
            if (packetsplit.Length > 6)
            {
                byte type, amount, slot;
                if (!byte.TryParse(packetsplit[4], out type) || !byte.TryParse(packetsplit[5], out slot) || !byte.TryParse(packetsplit[6], out amount)) return;

                Inventory inv = Session.Character.InventoryList.LoadInventoryBySlotAndType(slot, type);
                if (inv == null || amount > inv.ItemInstance.Amount) return;

                if ((inv.ItemInstance as ItemInstance).Item.IsSoldable != true)
                {
                    Session.Client.SendPacket(Session.Character.GenerateShopMemo(2, string.Format(Language.Instance.GetMessageFromKey("ITEM_NOT_SOLDABLE"))));
                    return;
                }

                if (Session.Character.Gold + (inv.ItemInstance as ItemInstance).Item.Price * amount > 1000000000)
                {
                    string message = Session.Character.GenerateMsg(Language.Instance.GetMessageFromKey("MAX_GOLD"), 0);
                    Session.Client.SendPacket(message);
                    return;
                }
                Session.Character.Gold += ((inv.ItemInstance as ItemInstance).Item.Price / 20) * amount;
                Session.Character.DeleteItem(type, slot);
                Session.Client.SendPacket(Session.Character.GenerateGold());
                Session.Client.SendPacket(Session.Character.GenerateShopMemo(1, string.Format(Language.Instance.GetMessageFromKey("SELL_ITEM_VALIDE"), (inv.ItemInstance as ItemInstance).Item.Name, amount)));
            }
            else if (packetsplit.Length == 5)
            {
                short vnum = -1;
                short.TryParse(packetsplit[4], out vnum);
                Skill skillinfo = ServerManager.GetSkill(vnum);
                CharacterSkill skill = Session.Character.Skills.FirstOrDefault(s => s.SkillVNum == vnum);
                if (skill == null || skillinfo == null || skill == Session.Character.Skills.ElementAt(0) || skill == Session.Character.Skills.ElementAt(1))
                    return;
                Session.Character.Gold -= skillinfo.Price;
                Session.Client.SendPacket(Session.Character.GenerateGold());

                for (int i = Session.Character.Skills.Count - 1; i >= 0; i--)
                {
                    Skill skinfo = ServerManager.GetSkill(Session.Character.Skills[i].SkillVNum);
                    if (skillinfo.UpgradeSkill == skinfo.UpgradeSkill)
                        Session.Character.Skills.Remove(Session.Character.Skills[i]);
                }

                Session.Character.Skills.Remove(skill);
                Session.Client.SendPacket(Session.Character.GenerateSki());
                string[] quicklistpackets = Session.Character.GenerateQuicklist();
                foreach (string quicklist in quicklistpackets)
                    Session.Client.SendPacket(quicklist);
                Session.Client.SendPacket(Session.Character.GenerateLev());
            }
        }

        [Packet("shopping")]
        public void Shopping(string packet)
        {
            // n_inv 2 1834 0 100 0.13.13.0.0.330 0.14.15.0.0.2299 0.18.120.0.0.3795 0.19.107.0.0.3795 0.20.94.0.0.3795 0.37.95.0.0.5643 0.38.97.0.0.11340 0.39.99.0.0.18564 0.48.108.0.0.5643 0.49.110.0.0.11340 0.50.112.0.0.18564 0.59.121.0.0.5643 0.60.123.0.0.11340 0.61.125.0.0.18564
            string[] packetsplit = packet.Split(' ');
            byte type;
            int NpcId;
            byte typeshop = 0;
            if (!int.TryParse(packetsplit[5], out NpcId) || !byte.TryParse(packetsplit[2], out type)) return;
            if (Session.Character.Speed == 0)
                return;
            MapNpc mapnpc = Session.CurrentMap.Npcs.FirstOrDefault(n => n.MapNpcId.Equals(NpcId));
            NpcMonster npc = ServerManager.GetNpc(mapnpc.NpcVNum);
            if (mapnpc?.Shop == null) return;

            string shoplist = "";
            foreach (ShopItem item in mapnpc.Shop.ShopItems.Where(s => s.Type.Equals(type)))
            {
                Item iteminfo = ServerManager.GetItem(item.ItemVNum);
                double pourcent = 1;
                if (Session.Character.GetDigniteIco() == 3)
                    pourcent = 1.10;
                else if (Session.Character.GetDigniteIco() == 4)
                    pourcent = 1.20;
                else if (Session.Character.GetDigniteIco() == 5 || Session.Character.GetDigniteIco() == 6)
                    pourcent = 1.5;

                if (iteminfo.ReputPrice > 0 && iteminfo.Type == 0)
                    shoplist += $" {iteminfo.Type}.{item.Slot}.{item.ItemVNum}.{item.Rare}.{(iteminfo.IsColored ? item.Color : item.Upgrade)}.{ServerManager.GetItem(item.ItemVNum).ReputPrice}";
                else if (iteminfo.ReputPrice > 0 && iteminfo.Type != 0)
                    shoplist += $" {iteminfo.Type}.{item.Slot}.{item.ItemVNum}.{-1}.{ServerManager.GetItem(item.ItemVNum).ReputPrice}";
                else if (iteminfo.Type != 0)
                    shoplist += $" {iteminfo.Type}.{item.Slot}.{item.ItemVNum}.{-1}.{ServerManager.GetItem(item.ItemVNum).Price * pourcent}";
                else
                    shoplist += $" {iteminfo.Type}.{item.Slot}.{item.ItemVNum}.{item.Rare}.{(iteminfo.IsColored ? item.Color : item.Upgrade)}.{ServerManager.GetItem(item.ItemVNum).Price * pourcent}";
            }

            foreach (ShopSkill skill in mapnpc.Shop.ShopSkills.Where(s => s.Type.Equals(type)))
            {
                Skill skillinfo = ServerManager.GetSkill(skill.SkillVNum);

                if (skill.Type != 0)
                {
                    typeshop = 1;
                    if (skillinfo.Class == Session.Character.Class)
                        shoplist += $" {skillinfo.SkillVNum}";
                }
                else
                    shoplist += $" {skillinfo.SkillVNum}";
            }

            Session.Client.SendPacket($"n_inv 2 {mapnpc.MapNpcId} 0 {typeshop}{shoplist}");
        }

        [Packet("npc_req")]
        public void ShowShop(string packet)
        {
            // n_inv 1 2 0 0 0.0.302.7.0.990000. 0.1.264.5.6.2500000. 0.2.69.7.0.650000. 0.3.4106.0.0.4200000. -1 0.5.4240.0.0.11200000. 0.6.4240.0.5.24000000. 0.7.4801.0.0.6200000. 0.8.4240.0.10.32000000. 0.9.712.0.3.250000. 0.10.997.0.4.250000. 1.11.1895.4.16000.-1.-1 1.12.1897.6.18000.-1.-1 -1 1.14.1902.3.35000.-1.-1 1.15.1237.2.12000.-1.-1 -1 -1 1.18.1249.3.92000.-1.-1 0.19.4240.0.1.10500000. -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1
            string[] packetsplit = packet.Split(' ');
            if (packetsplit.Length > 2)
            {
                int mode;
                if (!int.TryParse(packetsplit[2], out mode)) return;

                if (mode == 1)
                {
                    // User Shop
                    if (packetsplit.Length <= 3) return;

                    long owner;
                    if (!long.TryParse(packetsplit[3], out owner)) return;

                    KeyValuePair<long, MapShop> shopList = Session.CurrentMap.ShopUserList.FirstOrDefault(s => s.Value.OwnerId.Equals(owner));
                    LoadShopItem(owner, shopList);
                }
                else
                {
                    // Npc Shop
                    MapNpc npc = ServerManager.GetMap(Session.Character.MapId).Npcs.FirstOrDefault(n => n.MapNpcId.Equals(Convert.ToInt16(packetsplit[3])));
                    if (!string.IsNullOrEmpty(npc?.GetNpcDialog()))
                        Session.Client.SendPacket(npc.GetNpcDialog());
                }
            }
        }

        private void LoadShopItem(long owner, KeyValuePair<long, MapShop> shop)
        {
            string packetToSend = $"n_inv 1 {owner} 0 0";
            for (short i = 0; i < 20; i++)
            {
                PersonalShopItem item = shop.Value.Items.Count() > i ? shop.Value.Items.ElementAt(i) : null;
                if (item != null)
                {
                    if ((item.ItemInstance as ItemInstance).Item.Type == 0)
                        packetToSend += $" 0.{i}.{item.ItemInstance.ItemVNum}.{item.ItemInstance.Rare}.{item.ItemInstance.Upgrade}.{item.Price}.";
                    else
                        packetToSend += $" {(item.ItemInstance as ItemInstance).Item.Type}.{i}.{item.ItemInstance.ItemVNum}.{item.Amount}.{item.Price}.-1.";
                }
                else
                {
                    packetToSend += " -1";
                }
            }
            packetToSend += " -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1 -1";

            Session.Client.SendPacket(packetToSend);
        }

        #endregion
    }
}
