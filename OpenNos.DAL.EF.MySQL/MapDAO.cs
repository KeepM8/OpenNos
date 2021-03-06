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
    public class MapDAO : IMapDAO
    {
        #region Members

        private IMapper _mapper;

        #endregion

        #region Instantiation

        public MapDAO()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Map, MapDTO>();
                cfg.CreateMap<MapDTO, Map>();
            });

            _mapper = config.CreateMapper();
        }

        #endregion

        #region Methods

        public void Insert(List<MapDTO> maps)
        {
            using (var context = DataAccessHelper.CreateContext())
            {
                foreach (MapDTO Item in maps)
                {
                    Map entity = _mapper.Map<Map>(Item);
                    context.Map.Add(entity);
                }
                context.SaveChanges();
            }
        }

        public MapDTO Insert(MapDTO map)
        {
            using (var context = DataAccessHelper.CreateContext())
            {
                if (context.Map.FirstOrDefault(c => c.MapId.Equals(map.MapId)) == null)
                {
                    Map entity = _mapper.Map<Map>(map);
                    context.Map.Add(entity);
                    context.SaveChanges();
                    return _mapper.Map<MapDTO>(entity);
                }
                else return new MapDTO();
            }
        }

        public IEnumerable<MapDTO> LoadAll()
        {
            using (var context = DataAccessHelper.CreateContext())
            {
                foreach (Map Map in context.Map)
                {
                    yield return _mapper.Map<MapDTO>(Map);
                }
            }
        }

        public MapDTO LoadById(short mapId)
        {
            using (var context = DataAccessHelper.CreateContext())
            {
                return _mapper.Map<MapDTO>(context.Map.FirstOrDefault(c => c.MapId.Equals(mapId)));
            }
        }

        #endregion
    }
}