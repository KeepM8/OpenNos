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

using OpenNos.DAL.EF.MySQL.Helpers;
using OpenNos.DAL.Interface;
using OpenNos.Data;
using System.Collections.Generic;
using System.Linq;

namespace OpenNos.DAL.EF.MySQL
{
    public class MapNpcDAO : IMapNpcDAO
    {
        #region Members

        private IMapper _mapper;

        #endregion

        #region Instantiation

        public MapNpcDAO()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<MapNpc, MapNpcDTO>();
                cfg.CreateMap<MapNpcDTO, MapNpc>();
            });

            _mapper = config.CreateMapper();
        }

        #endregion

        #region Methods

        public void Insert(List<MapNpcDTO> npcs)
        {
            using (var context = DataAccessHelper.CreateContext())
            {
                foreach (MapNpcDTO Item in npcs)
                {
                    MapNpc entity = _mapper.Map<MapNpc>(Item);
                    context.MapNpc.Add(entity);
                }
                context.SaveChanges();
            }
        }

        public MapNpcDTO Insert(MapNpcDTO npc)
        {
            using (var context = DataAccessHelper.CreateContext())
            {
                MapNpc entity = _mapper.Map<MapNpc>(npc);
                context.MapNpc.Add(entity);
                context.SaveChanges();
                return _mapper.Map<MapNpcDTO>(entity);
            }
        }

        public MapNpcDTO LoadById(int id)
        {
            using (var context = DataAccessHelper.CreateContext())
            {
                return _mapper.Map<MapNpcDTO>(context.MapNpc.FirstOrDefault(i => i.MapNpcId.Equals(id)));
            }
        }

        public IEnumerable<MapNpcDTO> LoadFromMap(short mapId)
        {
            using (var context = DataAccessHelper.CreateContext())
            {
                foreach (MapNpc npcobject in context.MapNpc.Where(c => c.MapId.Equals(mapId)))
                {
                    yield return _mapper.Map<MapNpcDTO>(npcobject);
                }
            }
        }

        #endregion
    }
}