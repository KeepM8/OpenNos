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
using OpenNos.DAL;
using OpenNos.Data;
using OpenNos.Data.Enums;
using OpenNos.Domain;
using OpenNos.GameObject;
using OpenNos.ServiceRef.Internal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace OpenNos.Handler
{
    public class BasicPacketHandler : IPacketHandler
    {
        #region Members

        private readonly ClientSession _session;

        #endregion

        #region Instantiation

        public BasicPacketHandler(ClientSession session)
        {
            _session = session;
        }

        #endregion

        #region Properties

        public ClientSession Session { get { return _session; } }

        #endregion

        #region Methods

        [Packet("compl")]
        public void Compliment(string packet)
        {
            string[] packetsplit = packet.Split(' ');
            long complimentCharacterId = 0;
            if (long.TryParse(packetsplit[3], out complimentCharacterId))
            {
                if (Session.Character.Level >= 30)
                {
                    if (Session.Character.LastLogin.AddMinutes(60) <= DateTime.Now)
                    {
                        if (Session.Account.LastCompliment.Date.AddDays(1) <= DateTime.Now.Date)
                        {
                            short? compliment = ClientLinkManager.Instance.GetProperty<short?>(complimentCharacterId, "Compliment");
                            compliment++;
                            ClientLinkManager.Instance.SetProperty(complimentCharacterId, "Compliment", compliment);
                            Session.Client.SendPacket(Session.Character.GenerateSay(String.Format(Language.Instance.GetMessageFromKey("COMPLIMENT_GIVEN"), ClientLinkManager.Instance.GetProperty<string>(complimentCharacterId, "Name")), 12));
                            AccountDTO account = Session.Account;
                            account.LastCompliment = DateTime.Now;
                            DAOFactory.AccountDAO.InsertOrUpdate(ref account);
                            ClientLinkManager.Instance.Broadcast(Session, Session.Character.GenerateSay(String.Format(Language.Instance.GetMessageFromKey("COMPLIMENT_RECEIVED"), Session.Character.Name), 12), ReceiverType.OnlySomeone, packetsplit[1].Substring(1));
                        }
                        else
                        {
                            Session.Client.SendPacket(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("COMPLIMENT_COOLDOWN"), 11));
                        }
                    }
                    else
                    {
                        Session.Client.SendPacket(Session.Character.GenerateSay(String.Format(Language.Instance.GetMessageFromKey("COMPLIMENT_LOGIN_COOLDOWN"), (Session.Character.LastLogin.AddMinutes(60) - DateTime.Now).Minutes), 11));
                    }
                }
                else
                {
                    Session.Client.SendPacket(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("COMPLIMENT_NOT_MINLVL"), 11));
                }
            }
        }

        [Packet("Char_NEW")]
        public void CreateCharacter(string packet)
        {
            if (Session.CurrentMap != null)
                return;
            // TODO: Hold Account Information in Authorized object
            long accountId = Session.Account.AccountId;
            string[] packetsplit = packet.Split(' ');

            byte slot = Convert.ToByte(packetsplit[3]);
            string characterName = packetsplit[2];

            if (slot <= 2 && DAOFactory.CharacterDAO.LoadBySlot(accountId, slot) == null)
            {
                if (characterName.Length > 3 && characterName.Length < 15)
                {
                    bool isIllegalCharacter = false;
                    for (int i = 0; i < characterName.Length; i++)
                    {
                        if (characterName[i] < 0x23 || characterName[i] > 0x7E)
                        {
                            isIllegalCharacter = true;
                        }
                    }

                    if (!isIllegalCharacter)
                    {
                        if (DAOFactory.CharacterDAO.LoadByName(characterName) == null)
                        {
                            if (Convert.ToByte(packetsplit[3]) > 2)
                                return;
                            Random r = new Random();
                            CharacterDTO newCharacter = new CharacterDTO()
                            {
                                Class = (byte)ClassType.Adventurer,
                                Gender = (Convert.ToByte(packetsplit[4]) >= 0 && Convert.ToByte(packetsplit[4]) <= 1 ? Convert.ToByte(packetsplit[4]) : Convert.ToByte(0)),
                                Gold = 0,
                                HairColor = Enum.IsDefined(typeof(HairColorType), Convert.ToByte(packetsplit[6])) ? Convert.ToByte(packetsplit[6]) : Convert.ToByte(0),
                                HairStyle = Enum.IsDefined(typeof(HairStyleType), Convert.ToByte(packetsplit[5])) ? Convert.ToByte(packetsplit[5]) : Convert.ToByte(0),
                                Hp = 221,
                                JobLevel = 1,
                                JobLevelXp = 0,
                                Level = 1,
                                LevelXp = 0,
                                MapId = 1,
                                MapX = (short)(r.Next(78, 81)),
                                MapY = (short)(r.Next(114, 118)),
                                Mp = 221,
                                Name = characterName,
                                Slot = slot,
                                AccountId = accountId,
                                StateEnum = CharacterState.Active,
                                WhisperBlocked = false,
                                FamilyRequestBlocked = false,
                                ExchangeBlocked = false,
                                BuffBlocked = false,
                                EmoticonsBlocked = false,
                                FriendRequestBlocked = false,
                                GroupRequestBlocked = false,
                                MinilandInviteBlocked = false,
                                HeroChatBlocked = false,
                                QuickGetUp = false,
                                MouseAimLock = false,
                                HpBlocked = false,
                            };

                            SaveResult insertResult = DAOFactory.CharacterDAO.InsertOrUpdate(ref newCharacter);
                            CharacterSkillDTO sk1 = new CharacterSkillDTO { CharacterId = newCharacter.CharacterId, SkillVNum = 200 };
                            CharacterSkillDTO sk2 = new CharacterSkillDTO { CharacterId = newCharacter.CharacterId, SkillVNum = 201 };
                            DAOFactory.CharacterSkillDAO.InsertOrUpdate(ref sk1);
                            DAOFactory.CharacterSkillDAO.InsertOrUpdate(ref sk2);
                            LoadCharacters(packet);
                        }
                        else Session.Client.SendPacketFormat($"info {Language.Instance.GetMessageFromKey("ALREADY_TAKEN")}");
                    }
                    else Session.Client.SendPacketFormat($"info {Language.Instance.GetMessageFromKey("INVALID_CHARNAME")}");
                }
            }
        }

        [Packet("Char_DEL")]
        public void DeleteCharacter(string packet)
        {
            if (Session.CurrentMap != null)
                return;
            string[] packetsplit = packet.Split(' ');
            AccountDTO account = DAOFactory.AccountDAO.LoadBySessionId(Session.SessionId);
            if (packetsplit.Length <= 3)
                return;
            if (account != null && account.Password == OpenNos.Core.EncryptionBase.sha256(packetsplit[3]))
            {
                DAOFactory.GeneralLogDAO.SetCharIdNull((long?)Convert.ToInt64(DAOFactory.CharacterDAO.LoadBySlot(account.AccountId, Convert.ToByte(packetsplit[2])).CharacterId));
                DAOFactory.CharacterDAO.DeleteByPrimaryKey(account.AccountId, Convert.ToByte(packetsplit[2]));
                LoadCharacters(packet);
            }
            else
            {
                Session.Client.SendPacket($"info {Language.Instance.GetMessageFromKey("BAD_PASSWORD")}");
            }
        }

        [Packet("dir")]
        public void Dir(string packet)
        {
            string[] packetsplit = packet.Split(' ');

            if (Convert.ToInt32(packetsplit[4]) == Session.Character.CharacterId)
            {
                Session.Character.Direction = Convert.ToInt32(packetsplit[2]);
                ClientLinkManager.Instance.Broadcast(Session, Session.Character.GenerateDir(), ReceiverType.AllOnMap);
            }
        }

        [Packet("ncif")]
        public void GetNamedCharacterInformation(string packet)
        {
            string[] packetsplit = packet.Split(' ');

            if (packetsplit[2] == "1")
            {
                ClientLinkManager.Instance.RequireBroadcastFromUser(Session, Convert.ToInt64(packetsplit[3]), "GenerateStatInfo");
            }
            if (packetsplit[2] == "2")
            {
                foreach (MapNpc npc in ServerManager.GetMap(Session.Character.MapId).Npcs)
                    if (npc.MapNpcId == Convert.ToInt32(packetsplit[3]))
                    {
                        NpcMonster npcinfo = ServerManager.GetNpc(npc.NpcVNum);
                        if (npcinfo == null)
                            return;
                        ClientLinkManager.Instance.Broadcast(Session, $"st 2 {packetsplit[3]} {npcinfo.Level} {npcinfo.HeroLevel} 100 100 50000 50000", ReceiverType.OnlyMe);
                    }
            }
            if (packetsplit[2] == "3")
            {
                foreach (MapMonster monster in ServerManager.GetMap(Session.Character.MapId).Monsters)
                    if (monster.MapMonsterId == Convert.ToInt32(packetsplit[3]))
                    {
                        NpcMonster monsterinfo = ServerManager.GetNpc(monster.MonsterVNum);
                        if (monsterinfo == null)
                            return;
                        ClientLinkManager.Instance.Broadcast(Session, $"st 3 {packetsplit[3]} {monsterinfo.Level} {monsterinfo.HeroLevel} {(int)((float)monster.CurrentHp / (float)monsterinfo.MaxHP * 100)} {(int)((float)monster.CurrentMp / (float)monsterinfo.MaxMP * 100)} {monster.CurrentHp} {monster.CurrentMp}", ReceiverType.OnlyMe);
                    }
            }
        }

        [Packet("npinfo")]
        public void GetStats(string packet)
        {
            Session.Client.SendPacket(Session.Character.GenerateStatChar());
        }

        [Packet(";")]
        public void GroupTalk(string packet)
        {
            string[] packetsplit = packet.Split(' ');
            string message = String.Empty;
            for (int i = 1; i < packetsplit.Length; i++)
                message += packetsplit[i] + " ";
            message = message.Substring(1).Trim();

            ClientLinkManager.Instance.Broadcast(Session, Session.Character.GenerateSpk(message, 3), ReceiverType.Group);
        }

        [Packet("guri")]
        public void Guri(string packet)
        {
            string[] packetsplit = packet.Split(' ');
            if (packetsplit[2] == "10" && Convert.ToInt32(packetsplit[5]) >= 973 && Convert.ToInt32(packetsplit[5]) <= 999 && !Session.Character.EmoticonsBlocked)
            {
                Session.Client.SendPacket(Session.Character.GenerateEff(Convert.ToInt32(packetsplit[5]) + 4099));
                ClientLinkManager.Instance.Broadcast(Session, Session.Character.GenerateEff(Convert.ToInt32(packetsplit[5]) + 4099),
                    ReceiverType.AllOnMapNoEmoBlocked);
            }
            if (packetsplit[2] == "2")
                ClientLinkManager.Instance.Broadcast(Session, $"guri 2 1 {Session.Character.CharacterId}", ReceiverType.AllOnMap);
        }

        [Packet("hero")]
        public void Hero(string packet)
        {
            if (DAOFactory.CharacterDAO.IsReputHero(Session.Character.CharacterId) >= 3)
            {
                string[] packetsplit = packet.Split(' ');
                string message = String.Empty;
                for (int i = 2; i < packetsplit.Length; i++)
                    message += packetsplit[i] + " ";
                message.Trim();

                ClientLinkManager.Instance.Broadcast(Session, $"msg 5 [{Session.Character.Name}]:{message}", ReceiverType.AllNoHeroBlocked);
            }
            else
            {
                Session.Client.SendPacket(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("USER_NOT_HERO"), 11));
            }
        }

        /// <summary>
        /// Load Characters, this is the Entrypoint for the Client, Wait for 3 Packets.
        /// </summary>
        /// <param name="packet"></param>
        /// <returns></returns>
        [Packet("OpenNos.EntryPoint", 3)]
        public void LoadCharacters(string packet)
        {
            string[] loginPacketParts = packet.Split(' ');

            // Load account by given SessionId
            if (Session.Account == null)
            {
                bool hasRegisteredAccountLogin = true;
                try
                {
                    hasRegisteredAccountLogin = ServiceFactory.Instance.CommunicationService.HasRegisteredAccountLogin(loginPacketParts[4], Session.SessionId);
                }
                catch (Exception ex)
                {
                    Logger.Log.Error(ex.Message);
                }

                if (loginPacketParts.Length > 4 && hasRegisteredAccountLogin)
                {
                    AccountDTO accountDTO = DAOFactory.AccountDAO.LoadByName(loginPacketParts[4]);

                    if (accountDTO != null)
                    {
                        if (accountDTO.Password.Equals(EncryptionBase.sha256(loginPacketParts[6])))
                        {
                            var account = new GameObject.Account()
                            {
                                AccountId = accountDTO.AccountId,
                                Name = accountDTO.Name,
                                Password = accountDTO.Password,
                                Authority = accountDTO.Authority,
                                LastCompliment = accountDTO.LastCompliment
                            };

                            Session.InitializeAccount(account);
                        }
                        else
                        {
                            Logger.Log.ErrorFormat($"Client {Session.Client.ClientId} forced Disconnection, invalid Password or SessionId.");
                            Session.Client.Disconnect();
                        }
                    }
                    else
                    {
                        Logger.Log.ErrorFormat($"Client {Session.Client.ClientId} forced Disconnection, invalid AccountName.");
                        Session.Client.Disconnect();
                    }
                }
                else
                {
                    Logger.Log.ErrorFormat($"Client {Session.Client.ClientId} forced Disconnection, login has not been registered or Account is already logged in.");
                    Session.Client.Disconnect();
                    Session.Destroy();
                    return;
                }
            }

            IEnumerable<CharacterDTO> characters = DAOFactory.CharacterDAO.LoadByAccount(Session.Account.AccountId);
            Logger.Log.InfoFormat(Language.Instance.GetMessageFromKey("ACCOUNT_ARRIVED"), Session.SessionId);
            Session.Client.SendPacket("clist_start 0");
            foreach (CharacterDTO character in characters)
            {
                IEnumerable<InventoryDTO> inventory = DAOFactory.InventoryDAO.LoadByType(character.CharacterId, (byte)InventoryType.Equipment);

                WearableInstance[] equipment = new WearableInstance[15];
                foreach (InventoryDTO equipmentEntry in inventory)
                {
                    WearableInstance currentInstance = equipmentEntry.ItemInstance as WearableInstance;
                    equipment[currentInstance.Item.EquipmentSlot] = currentInstance;
                }

                Session.Client.SendPacket($"clist {character.Slot} {character.Name} 0 {character.Gender} {character.HairStyle} {character.HairColor} 0 {character.Class} {character.Level} {character.HeroLevel} {(equipment[(byte)EquipmentType.Hat] != null ? equipment[(byte)EquipmentType.Hat].ItemVNum : 0)}.{(equipment[(byte)EquipmentType.Armor] != null ? equipment[(byte)EquipmentType.Armor].ItemVNum : 0)}.{(equipment[(byte)EquipmentType.MainWeapon] != null ? equipment[(byte)EquipmentType.MainWeapon].ItemVNum : 0)}.{(equipment[(byte)EquipmentType.SecondaryWeapon] != null ? equipment[(byte)EquipmentType.SecondaryWeapon].ItemVNum : 0)}.{(equipment[(byte)EquipmentType.Mask] != null ? equipment[(byte)EquipmentType.Mask].ItemVNum : 0)}.{(equipment[(byte)EquipmentType.Fairy] != null ? equipment[(byte)EquipmentType.Fairy].ItemVNum : 0)}.{(equipment[(byte)EquipmentType.CostumeSuit] != null ? equipment[(byte)EquipmentType.CostumeSuit].ItemVNum : 0)}.{(equipment[(byte)EquipmentType.CostumeHat] != null ? equipment[(byte)EquipmentType.CostumeHat].ItemVNum : 0)} 1  1 0 -1.-1 {(equipment[(byte)EquipmentType.Hat] != null ? (equipment[(byte)EquipmentType.Hat].Item.IsColored ? ((WearableInstance)equipment[(byte)EquipmentType.Hat]).Design : character.HairColor) : character.HairColor)} 0 0");
            }
            Session.Client.SendPacket("clist_end");
        }

        [Packet("gop")]
        public void Option(string packet)
        {
            string[] packetsplit = packet.Split(' ');
            if (packetsplit.Length == 4)
            {
                switch (int.Parse(packetsplit[2]))
                {
                    case (int)ConfigType.BuffBlocked:
                        Session.Character.BuffBlocked = int.Parse(packetsplit[3]) == 1;
                        Session.Client.SendPacket(Session.Character.GenerateMsg(Language.Instance.GetMessageFromKey(
                            Session.Character.BuffBlocked
                                ? "BUFF_BLOCKED"
                                : "BUFF_UNLOCKED"
                            ), 0));
                        break;

                    case (int)ConfigType.EmoticonsBlocked:
                        Session.Character.EmoticonsBlocked = int.Parse(packetsplit[3]) == 1;
                        Session.Client.SendPacket(Session.Character.GenerateMsg(Language.Instance.GetMessageFromKey(
                            Session.Character.EmoticonsBlocked
                                ? "EMO_BLOCKED"
                                : "EMO_UNLOCKED"
                            ), 0));
                        break;

                    case (int)ConfigType.ExchangeBlocked:
                        Session.Character.ExchangeBlocked = int.Parse(packetsplit[3]) == 0;
                        Session.Client.SendPacket(Session.Character.GenerateMsg(Language.Instance.GetMessageFromKey(
                            Session.Character.ExchangeBlocked
                                ? "EXCHANGE_BLOCKED"
                                : "EXCHANGE_UNLOCKED"
                            ), 0));
                        break;

                    case (int)ConfigType.FriendRequestBlocked:
                        Session.Character.FriendRequestBlocked = int.Parse(packetsplit[3]) == 0;
                        Session.Client.SendPacket(Session.Character.GenerateMsg(Language.Instance.GetMessageFromKey(
                            Session.Character.FriendRequestBlocked
                                ? "FRIEND_REQ_BLOCKED"
                                : "FRIEND_REQ_UNLOCKED"
                            ), 0));
                        break;

                    case (int)ConfigType.GroupRequestBlocked:
                        Session.Character.GroupRequestBlocked = int.Parse(packetsplit[3]) == 0;
                        Session.Client.SendPacket(Session.Character.GenerateMsg(Language.Instance.GetMessageFromKey(
                            Session.Character.GroupRequestBlocked
                                ? "GROUP_REQ_BLOCKED"
                                : "GROUP_REQ_UNLOCKED"
                            ), 0));
                        break;

                    case (int)ConfigType.HeroChatBlocked:
                        Session.Character.HeroChatBlocked = int.Parse(packetsplit[3]) == 1;
                        Session.Client.SendPacket(Session.Character.GenerateMsg(Language.Instance.GetMessageFromKey(
                            Session.Character.HeroChatBlocked
                                ? "HERO_CHAT_BLOCKED"
                                : "HERO_CHAT_UNLOCKED"
                            ), 0));
                        break;

                    case (int)ConfigType.HpBlocked:
                        Session.Character.HpBlocked = int.Parse(packetsplit[3]) == 1;
                        Session.Client.SendPacket(Session.Character.GenerateMsg(Language.Instance.GetMessageFromKey(
                            Session.Character.HpBlocked
                                ? "HP_BLOCKED"
                                : "HP_UNLOCKED"
                            ), 0));
                        break;

                    case (int)ConfigType.MinilandInviteBlocked:
                        Session.Character.MinilandInviteBlocked = int.Parse(packetsplit[3]) == 1;
                        Session.Client.SendPacket(Session.Character.GenerateMsg(Language.Instance.GetMessageFromKey(
                            Session.Character.MinilandInviteBlocked
                                ? "MINI_INV_BLOCKED"
                                : "MINI_INV_UNLOCKED"
                            ), 0));
                        break;

                    case (int)ConfigType.MouseAimLock:
                        Session.Character.MouseAimLock = int.Parse(packetsplit[3]) == 1;
                        Session.Client.SendPacket(Session.Character.GenerateMsg(Language.Instance.GetMessageFromKey(
                            Session.Character.MouseAimLock
                                ? "MOUSE_LOCKED"
                                : "MOUSE_UNLOCKED"
                            ), 0));
                        break;

                    case (int)ConfigType.QuickGetUp:
                        Session.Character.QuickGetUp = int.Parse(packetsplit[3]) == 1;
                        Session.Client.SendPacket(Session.Character.GenerateMsg(Language.Instance.GetMessageFromKey(
                            Session.Character.QuickGetUp
                                ? "QUICK_GET_UP_ENABLED"
                                : "QUICK_GET_UP_DISABLED"
                            ), 0));
                        break;

                    case (int)ConfigType.WhisperBlocked:
                        Session.Character.WhisperBlocked = int.Parse(packetsplit[3]) == 0;
                        Session.Client.SendPacket(Session.Character.GenerateMsg(Language.Instance.GetMessageFromKey(
                            Session.Character.WhisperBlocked
                                ? "WHISPER_BLOCKED"
                                : "WHISPER_UNLOCKED"
                            ), 0));
                        break;

                    case (int)ConfigType.FamilyRequestBlocked:
                        Session.Character.FamilyRequestBlocked = int.Parse(packetsplit[3]) == 0;
                        Session.Client.SendPacket(Session.Character.GenerateMsg(Language.Instance.GetMessageFromKey(
                            Session.Character.FamilyRequestBlocked
                                ? "FAMILY_REQ_LOCKED"
                                : "FAMILY_REQ_UNLOCKED"
                            ), 0));
                        break;

                    case (int)ConfigType.GroupSharing:
                        Group grp = ClientLinkManager.Instance.Groups.FirstOrDefault(s => s.Characters.Contains(Session.Character.CharacterId));
                        if (grp == null)
                            return;
                        if (grp.Characters.ElementAt(0) != Session.Character.CharacterId)
                        {
                            Session.Client.SendPacket(Session.Character.GenerateMsg(Language.Instance.GetMessageFromKey("NOT_MASTER"), 0));
                            return;
                        }
                        if (int.Parse(packetsplit[3]) == 0)
                        {
                            ClientLinkManager.Instance.Groups.FirstOrDefault(s => s.Characters.Contains(Session.Character.CharacterId)).SharingMode = 1;
                            ClientLinkManager.Instance.Broadcast(Session, Session.Character.GenerateMsg(Language.Instance.GetMessageFromKey("SHARING"), 0), ReceiverType.Group);
                        }
                        else
                        {
                            ClientLinkManager.Instance.Groups.FirstOrDefault(s => s.Characters.Contains(Session.Character.CharacterId)).SharingMode = 0;
                            ClientLinkManager.Instance.Broadcast(Session, Session.Character.GenerateMsg(Language.Instance.GetMessageFromKey("SHARING_BY_ORDER"), 0), ReceiverType.Group);
                        }

                        break;
                }
            }
            Session.Client.SendPacket(Session.Character.GenerateStat());
        }

        [Packet("pjoin")]
        public void PJoin(string packet)
        {
            string[] packetsplit = packet.Split(' ');
            if (packetsplit.Length > 3)
            {
                Boolean Blocked = false;
                string charName;
                long charId = -1;
                if (!long.TryParse(packetsplit[3], out charId))
                    return;
                Boolean grouped1 = false;
                Boolean grouped2 = false;
                foreach (Group group in ClientLinkManager.Instance.Groups)
                {
                    if ((group.Characters.Contains(charId) || group.Characters.Contains(Session.Character.CharacterId)) && group.Characters.Count == 3)
                    {
                        Session.Client.SendPacket(Session.Character.GenerateInfo(Language.Instance.GetMessageFromKey("GROUP_FULL")));
                        return;
                    }
                    if (group.Characters.Contains(charId))
                    {
                        grouped1 = true;
                    }
                    if (group.Characters.Contains(Session.Character.CharacterId))
                    {
                        grouped2 = true;
                    }
                }

                if (grouped1 && grouped2)
                {
                    Session.Client.SendPacket(Session.Character.GenerateInfo(Language.Instance.GetMessageFromKey("ALREADY_IN_GROUP")));
                    return;
                }
                if (Convert.ToInt32(packetsplit[2]) == 0 || Convert.ToInt32(packetsplit[2]) == 1)
                {
                    if (Session.Character.CharacterId != charId)
                    {
                        if (!long.TryParse(packetsplit[3], out charId)) return;
                        Blocked = ClientLinkManager.Instance.GetProperty<bool>(charId, "GroupRequestBlocked");

                        if (Blocked)
                        {
                            ClientLinkManager.Instance.Broadcast(Session, Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("GROUP_BLOCKED"), 11), ReceiverType.OnlyMe);
                        }
                        else
                        {
                            Session.Character.ExchangeInfo = new ExchangeInfo { CharId = charId, Confirm = false };

                            charName = (string)ClientLinkManager.Instance.GetProperty<string>(charId, "Name");
                            Session.Client.SendPacket(Session.Character.GenerateInfo(String.Format(Language.Instance.GetMessageFromKey("GROUP_REQUEST"), charName)));
                            ClientLinkManager.Instance.Broadcast(Session, Session.Character.GenerateDialog($"#pjoin^3^{ Session.Character.CharacterId} #pjoin^4^{Session.Character.CharacterId} {String.Format(Language.Instance.GetMessageFromKey("INVITED_YOU"), Session.Character.Name)}"), ReceiverType.OnlySomeone, charName);
                        }
                    }
                }
            }
        }

        [Packet("pleave")]
        public void PlayerLeave(string packet)
        {
            ClientLinkManager.Instance.GroupLeave(Session);
        }

        [Packet("preq")]
        public void Preq(string packet)
        {
            double currentRunningSeconds = (DateTime.Now - Process.GetCurrentProcess().StartTime.AddSeconds(-50)).TotalSeconds;
            double timeSpanSinceLastPortal = currentRunningSeconds - Session.Character.LastPortal;
            if (!(timeSpanSinceLastPortal >= 4))
            {
                Session.Client.SendPacket(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("CANT_MOVE"), 10));
                return;
            }

            foreach (Portal portal in ServerManager.GetMap(Session.Character.MapId).Portals)
            {
                if (Session.Character.MapY >= portal.SourceY - 1 && Session.Character.MapY <= portal.SourceY + 1
                    && Session.Character.MapX >= portal.SourceX - 1 && Session.Character.MapX <= portal.SourceX + 1)
                {
                    switch (portal.Type)
                    {
                        case (sbyte)PortalType.MapPortal:
                        case (sbyte)PortalType.TSNormal:
                        case (sbyte)PortalType.Open:
                        case (sbyte)PortalType.Miniland:
                        case (sbyte)PortalType.TSEnd:
                        case (sbyte)PortalType.End:
                        case (sbyte)PortalType.Effect:
                        case (sbyte)PortalType.ShopTeleport:
                            break;

                        default:
                            Session.Client.SendPacket(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("PORTAL_BLOCKED"), 10));
                            return;
                    }

                    ClientLinkManager.Instance.MapOut(Session.Character.CharacterId);
                    Session.Character.MapId = portal.DestinationMapId;
                    Session.Character.MapX = portal.DestinationX;
                    Session.Character.MapY = portal.DestinationY;

                    Session.Character.LastPortal = currentRunningSeconds;
                    Session.Client.SendPacket(Session.Character.GenerateCMap());
                    ClientLinkManager.Instance.ChangeMap(Session.Character.CharacterId);
                    break;
                }
            }
        }

        [Packet("pulse")]
        public void Pulse(string packet)
        {
            string[] packetsplit = packet.Split(' ');
            Session.Character.LastPulse += 60;
            if (Convert.ToInt32(packetsplit[2]) != Session.Character.LastPulse)
            {
                Session.Client.Disconnect();
            }
            Session.Character.DeleteTimeout();
        }

        [Packet("qset")]
        public void QuicklistSet(string packet)
        {
            string[] packetsplit = packet.Split(' ');
            if (packetsplit.Length > 4)
            {
                short type, q1, q2, data1 = 0, data2 = 0;
                if (!short.TryParse(packetsplit[2], out type) ||
                    !short.TryParse(packetsplit[3], out q1) || !short.TryParse(packetsplit[4], out q2))
                    return;
                if (packetsplit.Length > 6)
                {
                    short.TryParse(packetsplit[5], out data1);
                    short.TryParse(packetsplit[6], out data2);
                }
                // qset type q1 q2 data1 data2

                switch (type)
                {
                    case 0:
                    case 1:
                        // client says  qset 0 1 3 2 6
                        // answer    -> qset 1 3 0.2.6.0

                        Session.Character.QuicklistEntries.Add(new QuicklistEntry
                        {
                            CharacterId = Session.Character.CharacterId,
                            Type = type,
                            Q1 = q1,
                            Q2 = q2,
                            Slot = data1,
                            Pos = data2
                        });

                        Session.Client.SendPacket(string.Format("qset {0} {1} {2}.{3}.{4}.0", q1, q2, type, data1, data2));

                        break;

                    case 2:
                        // DragDrop / Reorder

                        // qset type to1 to2 from1 from2
                        // vars ->   q1  q2  data1 data2

                        QuicklistEntry qlFrom = Session.Character.QuicklistEntries.Single(n => n.Q1 == data1 && n.Q2 == data2);
                        QuicklistEntry qlTo = Session.Character.QuicklistEntries.SingleOrDefault(n => n.Q1 == q1 && n.Q2 == q2);

                        qlFrom.Q1 = q1;
                        qlFrom.Q2 = q2;

                        if (qlTo == null)
                        {
                            // Put 'from' to new position (datax)
                            Session.Client.SendPacket(string.Format("qset {0} {1} {2}.{3}.{4}.0", qlFrom.Q1, qlFrom.Q2, qlFrom.Type, qlFrom.Slot, qlFrom.Pos));
                            // old 'from' is now empty.
                            Session.Client.SendPacket(string.Format("qset {0} {1} 7.7.-1.0", data1, data2));
                        }
                        else
                        {
                            // Put 'from' to new position (datax)
                            Session.Client.SendPacket(string.Format("qset {0} {1} {2}.{3}.{4}.0", qlFrom.Q1, qlFrom.Q2, qlFrom.Type, qlFrom.Slot, qlFrom.Pos));
                            // 'from' is now 'to' because they exchanged
                            qlTo.Q1 = data1;
                            qlTo.Q2 = data2;
                            Session.Client.SendPacket(string.Format("qset {0} {1} {2}.{3}.{4}.0", qlTo.Q1, qlTo.Q2, qlTo.Type, qlTo.Slot, qlTo.Pos));
                        }

                        break;

                    case 3:
                        // Remove from Quicklist

                        Session.Character.QuicklistEntries.RemoveAll(n => n.Q1 == q1 && n.Q2 == q2);

                        Session.Client.SendPacket(string.Format("qset {0} {1} 7.7.-1.0", q1, q2));

                        break;

                    default:
                        return;
                }
            }
        }

        [Packet("req_info")]
        public void ReqInfo(string packet)
        {
            string[] packetsplit = packet.Split(' ');
            if (packetsplit[2] == "5")
            {
                NpcMonster npc = ServerManager.GetNpc(short.Parse(packetsplit[3]));
                if (npc != null)
                {
                    Session.Client.SendPacket(npc.GenerateEInfo());
                }
            }
            else
                ClientLinkManager.Instance.RequireBroadcastFromUser(Session, Convert.ToInt64(packetsplit[3]), "GenerateReqInfo");
        }

        [Packet("rest")]
        public void Rest(string packet)
        {
            Session.Character.IsSitting = !Session.Character.IsSitting;
            if (Session.Character.IsVehicled)
                Session.Character.IsSitting = false;
            if (Session.Character.ThreadCharChange != null && Session.Character.ThreadCharChange.IsAlive)
                Session.Character.ThreadCharChange.Abort();

            ClientLinkManager.Instance.Broadcast(Session, Session.Character.GenerateRest(), ReceiverType.AllOnMap);
        }

        [Packet("#revival")]
        public void Revive(string packet)
        {
            string[] packetsplit = packet.Split(' ', '^');
            byte type;
            if (packetsplit.Length > 2)
            {
                if (!byte.TryParse(packetsplit[2], out type))
                    return;
                if (Session.Character.Hp > 0)
                    return;
                switch (type)
                {
                    case 0:
                        int seed = 1012;
                        if (Session.Character.InventoryList.CountItem(seed) < 10 && Session.Character.Level > 20)
                        {
                            Session.Client.SendPacket(Session.Character.GenerateMsg(Language.Instance.GetMessageFromKey("NOT_ENOUGH_POWER_SEED"), 0));
                            ClientLinkManager.Instance.ReviveFirstPosition(Session.Character.CharacterId);
                            Session.Client.SendPacket(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("NOT_ENOUGH_SEED_SAY"), 0));
                        }
                        else
                        {
                            if (Session.Character.Level > 20)
                                Session.Client.SendPacket(Session.Character.GenerateSay(string.Format(Language.Instance.GetMessageFromKey("SEED_USED"), 10), 10));
                            Session.Character.Hp = (int)(Session.Character.HPLoad() / 2);
                            Session.Character.Mp = (int)(Session.Character.MPLoad() / 2);
                            Session.Client.SendPacket(Session.Character.GenerateTp());
                            ClientLinkManager.Instance.Broadcast(Session, Session.Character.GenerateTp(), ReceiverType.AllOnMap);
                            ClientLinkManager.Instance.Broadcast(Session, Session.Character.GenerateRevive(), ReceiverType.AllOnMap);
                            Session.Character.InventoryList.RemoveItemAmount(seed, 10);
                            Session.Character.GenerateStartupInventory();
                        }
                        break;

                    case 1:
                        ClientLinkManager.Instance.ReviveFirstPosition(Session.Character.CharacterId);
                        break;
                }
            }
        }

        [Packet("say")]
        public void Say(string packet)
        {
            string[] packetsplit = packet.Split(' ');
            string message = "";
            for (int i = 2; i < packetsplit.Length; i++)
                message += packetsplit[i] + " ";

            ClientLinkManager.Instance.Broadcast(Session,
                Session.Character.GenerateSay(message.Trim(), 0),
                ReceiverType.AllOnMapExceptMe);
        }

        [Packet("select")]
        public void SelectCharacter(string packet)
        {
            try
            {
                string[] packetsplit = packet.Split(' ');
                CharacterDTO characterDTO = DAOFactory.CharacterDAO.LoadBySlot(Session.Account.AccountId, Convert.ToByte(packetsplit[2]));
                if (characterDTO != null)
                    Session.Character = new Character(Session)
                    {
                        AccountId = characterDTO.AccountId,
                        CharacterId = characterDTO.CharacterId,
                        Class = characterDTO.Class,
                        Dignite = characterDTO.Dignite,
                        Gender = characterDTO.Gender,
                        Gold = characterDTO.Gold,
                        HairColor = characterDTO.HairColor,
                        HairStyle = characterDTO.HairStyle,
                        Hp = characterDTO.Hp,
                        JobLevel = characterDTO.JobLevel,
                        JobLevelXp = characterDTO.JobLevelXp,
                        Level = characterDTO.Level,
                        LevelXp = characterDTO.LevelXp,
                        MapId = characterDTO.MapId,
                        MapX = characterDTO.MapX,
                        MapY = characterDTO.MapY,
                        Mp = characterDTO.Mp,
                        State = characterDTO.State,
                        Faction = characterDTO.Faction,
                        Name = characterDTO.Name,
                        Reput = characterDTO.Reput,
                        Slot = characterDTO.Slot,
                        Authority = Session.Account.Authority,
                        SpAdditionPoint = characterDTO.SpAdditionPoint,
                        SpPoint = characterDTO.SpPoint,
                        LastPulse = 0,
                        LastPortal = 0,
                        LastSp = 0,
                        Invisible = false,
                        InvisibleGm = false,
                        ArenaWinner = characterDTO.ArenaWinner,
                        Morph = 0,
                        MorphUpgrade = 0,
                        MorphUpgrade2 = 0,
                        Direction = 0,
                        IsSitting = false,
                        BackPack = characterDTO.Backpack,
                        Speed = ServersData.SpeedData[characterDTO.Class],
                        Compliment = characterDTO.Compliment,
                        Backpack = characterDTO.Backpack,
                        BuffBlocked = characterDTO.BuffBlocked,
                        EmoticonsBlocked = characterDTO.EmoticonsBlocked,
                        WhisperBlocked = characterDTO.WhisperBlocked,
                        FamilyRequestBlocked = characterDTO.FamilyRequestBlocked,
                        ExchangeBlocked = characterDTO.ExchangeBlocked,
                        FriendRequestBlocked = characterDTO.FriendRequestBlocked,
                        GroupRequestBlocked = characterDTO.GroupRequestBlocked,
                        HeroChatBlocked = characterDTO.HeroChatBlocked,
                        HpBlocked = characterDTO.HpBlocked,
                        MinilandInviteBlocked = characterDTO.MinilandInviteBlocked,
                        QuickGetUp = characterDTO.QuickGetUp,
                        MouseAimLock = characterDTO.MouseAimLock,
                        LastLogin = DateTime.Now,
                        SnackHp = 0,
                        SnackMp = 0,
                        SnackAmount = 0,
                        MaxSnack = 0,
                        HeroLevel = characterDTO.HeroLevel,
                        HeroXp = characterDTO.HeroXp
                    };

                Session.Character.Update();
                Session.Character.LoadInventory();
                Session.Character.LoadQuicklists();
                DAOFactory.AccountDAO.WriteGeneralLog(Session.Character.AccountId, Session.Client.RemoteEndPoint.ToString(), Session.Character.CharacterId, "Connection", "World");
                Session.Client.SendPacket("OK");
                Session.HealthTask = new Task(() => HealthTask());

                Session.HealthTask.Start();

                // Inform everyone about connected character
                ServiceFactory.Instance.CommunicationService.ConnectCharacter(Session.Character.Name, Session.Account.Name);
            }
            catch (Exception ex)
            {
                Logger.Log.Error(ex);
            }
        }

        [Packet("#pjoin")]
        public void ValidPJoin(string packet)
        {
            string[] packetsplit = packet.Split(' ', '^');
            int type = -1;
            long CharId = -1;
            int newgroup = 1;
            Boolean blocked1 = false;
            Boolean blocked2 = false;
            if (packetsplit.Length > 3)
            {
                if (!int.TryParse(packetsplit[2], out type))
                    return;
                long.TryParse(packetsplit[3], out CharId);

                if (type == 3 && ClientLinkManager.Instance.GetProperty<string>(CharId, "Name") != null)
                {
                    foreach (Group group in ClientLinkManager.Instance.Groups)
                    {
                        if (group.Characters.Contains(Session.Character.CharacterId))
                        {
                            blocked1 = true;
                        }
                        if (group.Characters.Contains(CharId))
                        {
                            blocked2 = true;
                        }
                    }
                    foreach (Group group in ClientLinkManager.Instance.Groups)
                    {
                        if (group.Characters.Count == 3)
                        {
                            Session.Client.SendPacket(Session.Character.GenerateInfo(Language.Instance.GetMessageFromKey("GROUP_FULL")));

                            return;
                        }
                        else if (blocked2 == true && blocked1 == true)
                            return;
                        else if (group.Characters.Contains(CharId))
                        {
                            group.Characters.Add(Session.Character.CharacterId);
                            newgroup = 0;
                        }
                        else if (group.Characters.Contains(Session.Character.CharacterId))
                        {
                            group.Characters.Add(CharId);
                            newgroup = 0;
                        }
                    }
                    if (newgroup == 1)
                    {
                        Group group = new Group()
                        {
                            Characters = new List<long>()
                        };
                        group.Characters.Add(CharId);
                        group.Characters.Add(Session.Character.CharacterId);
                        ClientLinkManager.Instance.Groups.Add(group);
                    }

                    //player join group
                    ClientLinkManager.Instance.UpdateGroup(CharId);

                    string p = GeneratePidx(Session.Character.CharacterId);
                    if (p != "")
                        ClientLinkManager.Instance.Broadcast(Session, p, ReceiverType.AllOnMap);
                }
                else if (type == 4)
                {
                    ClientLinkManager.Instance.Broadcast(Session, Session.Character.GenerateInfo(String.Format(Language.Instance.GetMessageFromKey("REFUSED_REQUEST"), Session.Character.Name)), ReceiverType.OnlySomeone, "", CharId);
                }
            }
        }

        [Packet("walk")]
        public void Walk(string packet)
        {
            if (Session.Character.ThreadCharChange != null && Session.Character.ThreadCharChange.IsAlive)
                Session.Character.ThreadCharChange.Abort();

            string[] packetsplit = packet.Split(' ');
            if (packetsplit.Length <= 5)
                return;
            Session.Character.MapX = Convert.ToInt16(packetsplit[2]);
            Session.Character.MapY = Convert.ToInt16(packetsplit[3]);

            if (Session.Character.Speed.Equals(Convert.ToByte(packetsplit[5])) || Convert.ToByte(packetsplit[5]) == 10)
            {
                if (Map.GetDistance(new MapCell() { X = Session.Character.MapX, Y = Session.Character.MapY }, new MapCell() { X = Convert.ToInt16(packetsplit[2]), Y = Convert.ToInt16(packetsplit[3]) }) > 20)
                    Session.Client.Disconnect();
                ClientLinkManager.Instance.Broadcast(Session, Session.Character.GenerateMv(), ReceiverType.AllOnMapExceptMe);
                Session.Client.SendPacket(Session.Character.GenerateCond());
            }
            else
            {
                Session.Client.Disconnect();
            }
        }

        [Packet("/")]
        public void Whisper(string packet)
        {
            string[] packetsplit = packet.Split(' ');
            string message = String.Empty;
            for (int i = 2; i < packetsplit.Length; i++)
                message += packetsplit[i] + " ";
            message.Trim();

            ClientLinkManager.Instance.Broadcast(Session, Session.Character.GenerateSpk(message, 5), ReceiverType.OnlyMe);

            bool? Blocked = ClientLinkManager.Instance.GetProperty<bool?>(packetsplit[1].Substring(1), "WhisperBlocked");
            if (!Blocked.Equals(null))
            {
                if (!Convert.ToBoolean(Blocked))
                    ClientLinkManager.Instance.Broadcast(Session, Session.Character.GenerateSpk(message, 5), ReceiverType.OnlySomeone, packetsplit[1].Substring(1));
                else
                    Session.Client.SendPacket(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("WHISPERED_BLOCKED"), 11));
            }
            else ClientLinkManager.Instance.Broadcast(Session, Session.Character.GenerateInfo(Language.Instance.GetMessageFromKey("USER_NOT_CONNECTED")), ReceiverType.OnlyMe);
        }

        private string GeneratePidx(long CharId)
        {
            string stri = "pidx 1";
            foreach (long Id in ClientLinkManager.Instance.Groups.FirstOrDefault(s => s.Characters.Contains(CharId)).Characters)
            {
                stri += $" 1.{Id} ";
            }
            if (stri == "pidx 1")
                stri = "";
            return stri;
        }

        private async void HealthTask()
        {
            int x = 1;
            while (true)
            {
                bool change = false;
                if (Session.Character.Hp == 0)
                {
                    Session.Character.Mp = 0;
                    Session.Client.SendPacket(Session.Character.GenerateStat());
                    await Task.Delay(2000);
                    continue;
                }
                if (Session.Character.IsSitting)
                    await Task.Delay(1500);
                else
                    await Task.Delay(2000);
                if (Session.healthStop == true)
                {
                    Session.healthStop = false;
                    return;
                }
                if (x == 0)
                    x = 1;

                if (Session.Character.Hp + Session.Character.HealthHPLoad() < Session.Character.HPLoad())
                {
                    change = true;
                    Session.Character.Hp += Session.Character.HealthHPLoad();
                }
                else
                {
                    if (Session.Character.Hp != (int)Session.Character.HPLoad())
                        change = true;
                    Session.Character.Hp = (int)Session.Character.HPLoad();
                }
                if (x == 1)
                {
                    if (Session.Character.Mp + Session.Character.HealthMPLoad() < Session.Character.MPLoad())
                    {
                        Session.Character.Mp += Session.Character.HealthMPLoad();
                        change = true;
                    }
                    else
                    {
                        if (Session.Character.Mp != (int)Session.Character.MPLoad())
                            change = true;
                        Session.Character.Mp = (int)Session.Character.MPLoad();
                    }
                    x = 0;
                }
                if (change)
                {
                    Session.Client.SendPacket(Session.Character.GenerateStat());
                }
            }
        }

        #endregion
    }
}