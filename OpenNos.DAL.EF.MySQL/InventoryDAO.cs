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
using OpenNos.DAL.EF.MySQL.DB;
using OpenNos.DAL.Interface;
using OpenNos.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenNos.Data;
using AutoMapper;
using OpenNos.Core;

namespace OpenNos.DAL.EF.MySQL
{
    public class InventoryDAO : IInventoryDAO
    {
        public InventoryDTO LoadBySlotAndType(long characterId, short slot,short type)
        {
            using (var context = DataAccessHelper.CreateContext())
            {
                return Mapper.Map<InventoryDTO>(context.inventory.SingleOrDefault(i => i.Slot.Equals(slot) && i.Type.Equals(type) && i.CharacterId.Equals(characterId)));
            }
        }
        public IEnumerable<InventoryDTO> Load(long characterId)
        {
            using (var context = DataAccessHelper.CreateContext())
            {
                foreach (Inventory inventoryobject in context.inventory.Where(i => i.CharacterId.Equals(characterId)))
                {
                    yield return Mapper.Map<InventoryDTO>(inventoryobject);
                }
            }
        }
        public IEnumerable<InventoryDTO> LoadBySlot(long characterId, short slot)
        {
            using (var context = DataAccessHelper.CreateContext())
            {
                foreach (Inventory inventoryobject in context.inventory.Where(i => i.Slot.Equals(slot) && i.CharacterId.Equals(characterId)))
                {
                    yield return Mapper.Map<InventoryDTO>(inventoryobject);
                }
            }
        }
        public IEnumerable<InventoryDTO> LoadByType(long characterId, short type)
        {
            using (var context = DataAccessHelper.CreateContext())
            {
                foreach (Inventory inventoryobject in context.inventory.Where(i => i.Type.Equals(type) && i.CharacterId.Equals(characterId)))
                {
                    yield return Mapper.Map<InventoryDTO>(inventoryobject);
                }
            }
        }
    }
}