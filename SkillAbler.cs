using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Oxide.Core;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("SkillAbler", "Lucas", "0.6.0")]
    [Description("A custom skill menu system with full game progression and prestige system.")]
    public class SkillAbler : RustPlugin
    {
        //ADMIN COMMANDS
        [ConsoleCommand("skillabler.givexp")]
        private void ConsoleGiveXp(ConsoleSystem.Arg arg)
        {
            // Verifica se quem usou o comando é Admin do servidor
            if (arg.Player() != null && !arg.Player().IsAdmin)
            {
                SendReply(arg, "<color=#FF5555>[SkillAbler]</color> Você não tem permissão para usar este comando!");
                return;
            }

            if (!arg.HasArgs(2))
            {
                SendReply(arg, "Sintaxe correta: skillabler.givexp <steamid/nome> <quantidade>");
                return;
            }

            BasePlayer target = arg.GetPlayerOrSleeper(0);
            if (target == null)
            {
                SendReply(arg, "Jogador não encontrado!");
                return;
            }

            int amount = arg.GetInt(1, 0);
            if (amount <= 0) return;

            AddXP(target, amount);
            SendReply(arg, $"<color=#55FF55>[SkillAbler]</color> Adicionado {amount} XP para {target.displayName}.");
        }


        [ConsoleCommand("skillabler.resetcategory")]
        private void ConsoleResetCategory(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null || !arg.HasArgs(1)) return;

            string targetSub = arg.GetString(0).ToLower();
            PlayerData data = GetPlayerData(player);

            if (targetSub == "yield")
            {
                int spent = data.MiningNovice + data.MiningAmateur + data.MiningVeteran + data.MiningExpert;
                if (spent <= 0) return;

                int scrapCost = spent * 100;
                if (player.inventory.GetAmount(365538570) < scrapCost) // Item ID do Scrap
                {
                    SendReply(player, $"<color=#FF5555>[SkillAbler]</color> Você precisa de {scrapCost} Scrap para resetar Mining Yield!");
                    return;
                }

                player.inventory.Take(null, 365538570, scrapCost);
                data.SkillPoints += spent;
                data.MiningNovice = 0;
                data.MiningAmateur = 0;
                data.MiningVeteran = 0;
                data.MiningExpert = 0;
            }
            else if (targetSub == "nodes")
            {
                int spent = data.MiningSweetSpot + data.MiningLikeCandy + data.MiningOnSpot + data.MiningJustLikeThat;
                if (spent <= 0) return;

                int scrapCost = spent * 100;
                if (player.inventory.GetAmount(365538570) < scrapCost)
                {
                    SendReply(player, $"<color=#FF5555>[SkillAbler]</color> Você precisa de {scrapCost} Scrap para resetar Mining Nodes!");
                    return;
                }

                player.inventory.Take(null, 365538570, scrapCost);
                data.SkillPoints += spent;
                data.MiningSweetSpot = 0;
                data.MiningLikeCandy = 0;
                data.MiningOnSpot = 0;
                data.MiningJustLikeThat = 0;
            }

            Interface.Oxide.DataFileSystem.WriteObject($"SkillAbler/{player.userID}", data);
            ShowSubmenu(player, "Mining", true);
        }


        [ConsoleCommand("skillabler.doresetcategory")]
        private void ConsoleDoResetCategory(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null || !arg.HasArgs(1)) return;

            CuiHelper.DestroyUi(player, "SkillAbler_ResetConfirmationModal");

            string subCategory = arg.GetString(0).ToLower();
            PlayerData data = GetPlayerData(player);

            ItemDefinition scrapDef = ItemManager.FindItemDefinition("scrap");
            if (scrapDef == null) return;

            if (subCategory == "yield")
            {
                int spent = data.MiningNovice + data.MiningAmateur + data.MiningVeteran + data.MiningExpert + data.MiningSpecialist;
                if (spent <= 0) return;

                int scrapCost = spent * 100;
                if (player.inventory.GetAmount(scrapDef.itemid) < scrapCost)
                {
                    SendReply(player, $"<color=#FF5555>[SkillAbler]</color> Você precisa de {scrapCost} Scrap para resetar Mining Yield!");
                    return;
                }

                player.inventory.Take(null, scrapDef.itemid, scrapCost);
                player.Command("note.inv", scrapDef.itemid, -scrapCost);

                data.SkillPoints += spent;
                data.MiningNovice = 0;
                data.MiningAmateur = 0;
                data.MiningVeteran = 0;
                data.MiningExpert = 0;
                data.MiningSpecialist = 0;

                SendReply(player, $"<color=#55FF55>[SkillAbler]</color> Subcategoria Mining Yield resetada com sucesso!");
            }
            else if (subCategory == "nodes")
            {
                int spent = data.MiningSweetSpot + data.MiningLikeCandy + data.MiningOnSpot + data.MiningJustLikeThat;
                if (spent <= 0) return;

                int scrapCost = spent * 100;
                if (player.inventory.GetAmount(scrapDef.itemid) < scrapCost)
                {
                    SendReply(player, $"<color=#FF5555>[SkillAbler]</color> Você precisa de {scrapCost} Scrap para resetar Mining Nodes!");
                    return;
                }

                player.inventory.Take(null, scrapDef.itemid, scrapCost);
                player.Command("note.inv", scrapDef.itemid, -scrapCost);

                data.SkillPoints += spent;
                data.MiningSweetSpot = 0;
                data.MiningLikeCandy = 0;
                data.MiningOnSpot = 0;
                data.MiningJustLikeThat = 0;

                SendReply(player, $"<color=#55FF55>[SkillAbler]</color> Subcategoria Mining Nodes resetada com sucesso!");
            }

            SaveData();
            ShowSubmenu(player, "Mining", true);
        }


        [ConsoleCommand("skillabler.doresetskill")]
        private void ConsoleDoResetSkill(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null || !arg.HasArgs(1)) return;

            CuiHelper.DestroyUi(player, "SkillAbler_ResetConfirmationModal");

            string skillName = arg.GetString(0);
            PlayerData data = GetPlayerData(player);

            if (skillName.Equals("MiningNodes", StringComparison.OrdinalIgnoreCase) || skillName.Equals("Mining", StringComparison.OrdinalIgnoreCase))
            {
                int spentPoints = data.MiningSweetSpot + data.MiningLikeCandy + data.MiningOnSpot + data.MiningJustLikeThat;

                if (spentPoints <= 0)
                {
                    SendReply(player, "<color=#FF5555>[SkillAbler]</color> Você não possui pontos investidos nesta subárvore.");
                    return;
                }

                int scrapCost = spentPoints * 100;
                ItemDefinition scrapDef = ItemManager.FindItemDefinition("scrap");
                if (scrapDef == null) return;

                int playerScrap = player.inventory.GetAmount(scrapDef.itemid);
                if (playerScrap < scrapCost)
                {
                    SendReply(player, $"<color=#FF5555>[SkillAbler]</color> Você precisa de <color=#FFFF55>{scrapCost} Scrap</color> (Você tem {playerScrap}).");
                    return;
                }

                player.inventory.Take(null, scrapDef.itemid, scrapCost);
                player.Command("note.inv", scrapDef.itemid, -scrapCost);

                data.SkillPoints += spentPoints;
                data.MiningSweetSpot = 0;
                data.MiningLikeCandy = 0;
                data.MiningOnSpot = 0;
                data.MiningJustLikeThat = 0;

                EffectNetwork.Send(new Effect("assets/bundled/prefabs/fx/gestures/shrug.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
                SendReply(player, $"<color=#55FF55>[SkillAbler]</color> Habilidades de Mining Nodes resetadas! Devolvidos <color=#FFFF55>{spentPoints} Pontos</color>.");

                ShowSubmenu(player, "Mining", true);
            }
        }

        private struct SubSkillData
        {
            public string Name;
            public string LevelText;
            public string Desc;
            public string IconUrl;
            public bool CanUpgrade;
            public string Command;
            public bool Locked;

            public SubSkillData(string name, string levelText, string desc, string iconUrl, bool canUpgrade, string command, bool locked)
            {
                Name = name;
                LevelText = levelText;
                Desc = desc;
                IconUrl = iconUrl;
                CanUpgrade = canUpgrade;
                Command = command;
                Locked = locked;
            }
        }

        [ConsoleCommand("skillabler.confirmreset")]
        private void ConsoleConfirmReset(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null || !arg.HasArgs(1)) return;

            string subCategory = arg.GetString(0).ToLower();
            PlayerData data = GetPlayerData(player);

            int spentPoints = 0;
            string displayName = "";

            if (subCategory == "yield")
            {
                spentPoints = data.MiningNovice + data.MiningAmateur + data.MiningVeteran + data.MiningExpert + data.MiningSpecialist;
                displayName = "MINING YIELD";
            }
            else if (subCategory == "nodes")
            {
                spentPoints = data.MiningSweetSpot + data.MiningLikeCandy + data.MiningOnSpot + data.MiningJustLikeThat;
                displayName = "MINING NODES";
            }

            if (spentPoints <= 0)
            {
                SendReply(player, "<color=#FF5555>[SkillAbler]</color> Você não possui pontos investidos nesta subcategoria.");
                return;
            }

            int scrapCost = spentPoints * 100;
            string modalName = "SkillAbler_ResetConfirmationModal";
            CuiHelper.DestroyUi(player, modalName);

            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                Image = { Color = "0.0 0.0 0.0 0.85" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                CursorEnabled = true
            }, "Overlay", modalName);

            string boxName = $"{modalName}_Box";
            container.Add(new CuiPanel
            {
                Image = { Color = "0.12 0.13 0.15 0.98" },
                RectTransform = { AnchorMin = "0.30 0.35", AnchorMax = "0.70 0.65" }
            }, modalName, boxName);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.05 0.60", AnchorMax = "0.95 0.90" },
                Text = { Text = $"RESETAR {displayName}?", FontSize = 18, Align = TextAnchor.MiddleCenter, Font = "RobotoCondensed-Bold.ttf", Color = "1.00 0.30 0.30 1.00" }
            }, boxName);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.05 0.35", AnchorMax = "0.95 0.60" },
                Text = { Text = $"Esta ação resetará apenas a subcategoria {displayName}.\nCusto: {scrapCost} Scrap. Retorno: {spentPoints} Pontos.", FontSize = 13, Align = TextAnchor.MiddleCenter, Font = "RobotoCondensed-Bold.ttf", Color = "0.85 0.85 0.85 1.00" }
            }, boxName);

            container.Add(new CuiButton
            {
                Button = { Color = "0.20 0.60 0.20 0.95", Command = $"skillabler.doresetcategory {subCategory}" },
                RectTransform = { AnchorMin = "0.10 0.10", AnchorMax = "0.45 0.32" },
                Text = { Text = "CONFIRMAR", FontSize = 13, Align = TextAnchor.MiddleCenter, Font = "RobotoCondensed-Bold.ttf", Color = "1.00 1.00 1.00 1.00" }
            }, boxName);

            container.Add(new CuiButton
            {
                Button = { Color = "0.60 0.20 0.20 0.95", Close = modalName },
                RectTransform = { AnchorMin = "0.55 0.10", AnchorMax = "0.90 0.32" },
                Text = { Text = "CANCELAR", FontSize = 13, Align = TextAnchor.MiddleCenter, Font = "RobotoCondensed-Bold.ttf", Color = "1.00 1.00 1.00 1.00" }
            }, boxName);

            CuiHelper.AddUi(player, container);
        }
        private const string MainPanelName = "MainSkillablerMenu";
        private const string SubmenuPanelName = "SubmenuSkillablerMenu";
        private const string XpHudName = "SkillAblerXpHud";
        private const string XpNotificationName = "SkillAbler_XPNotification";
        private const string ImageBaseUrl = "https://raw.githubusercontent.com/querneu/SkillAblerImg/refs/heads/master/";
        private const int MaxLevel = 100;
        private const int BaseXpReq = 1000;

        private readonly Dictionary<ulong, ulong> heliLastHitPlayer = new Dictionary<ulong, ulong>();
        private readonly HashSet<ulong> deadHelis = new HashSet<ulong>();
        private readonly HashSet<ulong> rewardedCrates = new HashSet<ulong>();
        private readonly HashSet<ulong> rewardedHackableCrates = new HashSet<ulong>();

        private readonly List<Skill> skills = new List<Skill>
        {
            new Skill("Mining", "MiningIcon.png"),
            new Skill("Woodcutting", "WoodCuttingIcon.png"),
            new Skill("Harvesting", "HarvestingIcon.png"),
            new Skill("Farming", "FarmingIcon.png"),
            new Skill("Exploration", "ExplorationIcon.png"),
            new Skill("Combat", "CombatIcon.png"),
            new Skill("Medical", "MedicalIcon.png"),
            new Skill("Raiding", "RaidingIcon.png"),
            new Skill("Team", "TeamIcon.png"),
            new Skill("Vehicles", "VehiclesIcon.png"),
            new Skill("Crafting", "CraftingIcon.png"),
            new Skill("Building", "BuildingIcon.png")
        };

        private Dictionary<ulong, PlayerData> playerData = new Dictionary<ulong, PlayerData>();

        private class Skill
        {
            public string Name { get; }
            public string Icon { get; }

            public Skill(string name, string icon)
            {
                Name = name;
                Icon = icon;
            }
        }

        private class PlayerData
        {
            public int Level = 1;
            public int XP = 0;
            public int SkillPoints = 0;
            public int PrestigePoints = 0;

            // Habilidades da Categoria: Mining Yield
            public int MiningNovice = 0;      // (0/10) +5% por nível
            public int MiningAmateur = 0;     // (0/5)  +10% por nível
            public int MiningVeteran = 0;     // (0/3)  +25% por nível
            public int MiningExpert = 0;      // (0/1)  +100%
            public int MiningSpecialist = 0;  // (0/1)  Ultimate (Jackhammer no yield penalty)
            // Habilidades da Categoria: Mining Nodes
            public int MiningSweetSpot = 0; //Max 3     
            public int MiningLikeCandy = 0; // Max 1 (Requer Sweet Spot 3/3)

            public int MiningOnSpot = 0; // Max 1 (Requer Like Candy 1/1)
            public int MiningJustLikeThat = 0; //Max 1(Requer On Spot 1/1)

            // Demais categorias
            public int WoodcuttingLevel = 0;
            public int HarvestingLevel = 0;
            public int FarmingLevel = 0;
            public int ExplorationLevel = 0;
            public int CombatLevel = 0;
            public int MedicalLevel = 0;
            public int RaidingLevel = 0;
            public int TeamLevel = 0;
            public int VehiclesLevel = 0;
            public int CraftingLevel = 0;
            public int BuildingLevel = 0;
        }

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject("SkillAbler_Data", playerData);

        private void LoadData() =>
            playerData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerData>>("SkillAbler_Data") ?? new Dictionary<ulong, PlayerData>();

        private void Init()
        {
            LoadData();
            Puts("=================================");
            Puts("  SKILLABLER 0.6.0 LOADED!");
            Puts("=================================");
        }

        private void OnServerSave() => SaveData();

        private void OnServerInitialized()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                CreateXpHud(player);
            }
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player != null)
            {
                CreateXpHud(player);
            }
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player == null) return;

            CuiHelper.DestroyUi(player, MainPanelName);
            CuiHelper.DestroyUi(player, SubmenuPanelName);
            CuiHelper.DestroyUi(player, XpHudName);
        }

        private void Unload()
        {
            SaveData();
            heliLastHitPlayer.Clear();
            rewardedCrates.Clear();
            rewardedHackableCrates.Clear();
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, MainPanelName);
                CuiHelper.DestroyUi(player, SubmenuPanelName);
                CuiHelper.DestroyUi(player, XpHudName);
            }
        }

        private PlayerData GetPlayerData(BasePlayer player)
        {
            if (!playerData.ContainsKey(player.userID))
            {
                playerData[player.userID] = new PlayerData();
            }

            return playerData[player.userID];
        }

        // =========================================================
        // LÓGICA DE BÔNUS E HABILIDADES DE MINERAÇÃO
        // =========================================================

        private float GetMiningYieldMultiplier(PlayerData data)
        {
            float bonus = 0f;
            bonus += data.MiningNovice * 0.05f;   // +5% por nível
            bonus += data.MiningAmateur * 0.10f;  // +10% por nível
            bonus += data.MiningVeteran * 0.25f;  // +25% por nível
            bonus += data.MiningExpert * 1.00f;   // +100%

            return 1.0f + bonus;
        }

        // 1. Batidas em Recursos (Nódulos de Pedra/Metal e Árvores)
        private void OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (player == null || player.IsNpc || item == null || dispenser == null) return;

            AddXP(player, 10);

            if (dispenser.gatherType == ResourceDispenser.GatherType.Ore)
            {
                PlayerData data = GetPlayerData(player);

                // Specialist Miner: Durabilidade do Jackhammer
                Item activeItem = player.GetActiveItem();
                if (activeItem != null && activeItem.info.shortname == "jackhammer")
                {
                    if (data.MiningSpecialist > 0)
                    {
                        dispenser.fractionRemaining = Mathf.Clamp01(dispenser.fractionRemaining);
                        activeItem.condition = activeItem.maxCondition;
                        activeItem.MarkDirty();
                    }
                }

                // Detecta se esta batida acertou o hotspot (sweet spot) do nódulo
                bool hitSweetSpot = DidHitSweetSpot(dispenser);

                if (hitSweetSpot)
                {
                    // Like Candy: dobra a quantidade recebida no sweet spot
                    if (data.MiningLikeCandy > 0)
                        item.amount *= 2;

                    // Just Like That: coleta o nódulo inteiro ao acertar o sweet spot
                    if (data.MiningJustLikeThat > 0)
                        QueueCollectWholeNode(dispenser, player, data);
                }

                // Sweet Spot: Progresso acelerado de destruição do nódulo
                if (data.MiningSweetSpot > 0 && dispenser.fractionRemaining > 0f)
                {
                    float bonusProgress = 0.20f * data.MiningSweetSpot;
                    dispenser.fractionRemaining = Mathf.Clamp01(dispenser.fractionRemaining - bonusProgress);
                }

                // Multiplicador de Yield
                float multiplier = GetMiningYieldMultiplier(data);
                if (multiplier > 1.0f)
                {
                    item.amount = Mathf.Max(1, Mathf.RoundToInt(item.amount * multiplier));
                }
            }
        }
        private void OnCollectIngredient(CollectibleEntity entity, ItemAmount itemAmount, BasePlayer player)
        {
            if (player == null || player.IsNpc || itemAmount == null) return;
            AddXP(player, 10);
        }

        private void OnRecycleItem(Recycler recycler, Item item)
        {
            if (recycler == null || item == null) return;

            BasePlayer player = recycler.inventory?.playerOwner;

            if (player == null)
            {
                player = BasePlayer.activePlayerList.FirstOrDefault(p =>
                    p != null &&
                    !p.IsNpc &&
                    p.IsConnected &&
                    UnityEngine.Vector3.Distance(p.transform.position, recycler.transform.position) <= 3.5f
                );
            }

            if (player == null || player.IsNpc) return;

            AddXP(player, 15);
        }

        // =========================================================
        // LOGICA DE XP E PROGRESSÃO
        // =========================================================

        private int GetRequiredXpForLevel(int level)
        {
            if (level >= MaxLevel) return BaseXpReq * 10;

            float scale = 1.0f + ((level - 1) * 0.09f);
            return Mathf.RoundToInt(BaseXpReq * scale);
        }

        private void AddXP(BasePlayer player, int amount)
        {
            if (player == null || amount <= 0) return;

            PlayerData data = GetPlayerData(player);

            if (data.Level >= MaxLevel)
            {
                data.XP = GetRequiredXpForLevel(MaxLevel);
                CreateXpHud(player);
                return;
            }

            data.XP += amount;
            int reqXp = GetRequiredXpForLevel(data.Level);

            while (data.XP >= reqXp && data.Level < MaxLevel)
            {
                data.XP -= reqXp;
                data.Level++;
                data.SkillPoints++;

                EffectNetwork.Send(new Effect("assets/bundled/prefabs/fx/player/howl_level_up.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
                SendReply(player, $"<color=#55FF55>[SkillAbler]</color> Parabéns! Você alcançou o <color=#FFFF55>Nível {data.Level}</color>! (+1 Ponto de Habilidade)");

                reqXp = GetRequiredXpForLevel(data.Level);
            }

            CreateXpHud(player);
        }

        // =========================================================
        // INTERAÇÕES E GANHOS DE XP (GAMEPLAY HOOKS)
        // =========================================================

        private void OnPatrolHelicopterKilled(PatrolHelicopterAI heli, HitInfo info)
        {
            if (heli == null) return;

            BaseHelicopter baseHeli = heli.GetComponent<BaseHelicopter>();
            if (baseHeli != null)
            {
                RewardHeliXP(baseHeli, info);
            }
        }

        private void RewardHeliXP(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null) return;

            ulong heliNetId = entity.net?.ID.Value ?? 0;
            if (heliNetId != 0 && deadHelis.Contains(heliNetId)) return;

            BasePlayer killer = info?.InitiatorPlayer ?? info?.Initiator as BasePlayer ?? entity.lastAttacker as BasePlayer;
            if (killer == null && heliNetId != 0 && heliLastHitPlayer.TryGetValue(heliNetId, out ulong lastHitPlayerId))
            {
                killer = BasePlayer.FindByID(lastHitPlayerId);
            }

            if (killer != null && !killer.IsNpc)
            {
                if (heliNetId != 0)
                {
                    deadHelis.Add(heliNetId);
                    heliLastHitPlayer.Remove(heliNetId);
                }

                AddXP(killer, 3000);
            }
        }

        private void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (player == null) return;

            if (dispenser.gatherType == ResourceDispenser.GatherType.Ore && item != null)
            {
                PlayerData data = GetPlayerData(player);
                float multiplier = GetMiningYieldMultiplier(data);
                if (multiplier > 1.0f)
                {
                    item.amount = Mathf.Max(1, Mathf.RoundToInt(item.amount * multiplier));
                }
            }

            AddXP(player, 100);
        }

        private void OnItemCraftFinished(ItemCraftTask task, Item item)
        {
            if (item == null) return;

            BasePlayer player = item.GetOwnerPlayer() ?? item.GetRootContainer()?.playerOwner;

            if (player == null)
            {
                foreach (BasePlayer activePlayer in BasePlayer.activePlayerList)
                {
                    if (activePlayer.inventory?.crafting?.queue != null && activePlayer.inventory.crafting.queue.Contains(task))
                    {
                        player = activePlayer;
                        break;
                    }
                }
            }

            if (player == null) return;

            int xpGained = 20 * item.amount;
            AddXP(player, xpGained);
        }

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return;

            if (entity is BaseHelicopter || entity.ShortPrefabName.Contains("patrol") || entity.ShortPrefabName.Contains("helicopter"))
            {
                BasePlayer attacker = info.InitiatorPlayer ?? info.Initiator as BasePlayer;
                ulong heliNetId = entity.net?.ID.Value ?? 0;

                if (heliNetId != 0 && attacker != null && !attacker.IsNpc)
                {
                    heliLastHitPlayer[heliNetId] = attacker.userID;
                }

                if (entity.health - info.damageTypes.Total() <= 0)
                {
                    RewardHeliXP(entity, info);
                }
            }
        }

        private void OnEntityBuilt(Planner plan, GameObject go)
        {
            BasePlayer player = plan?.GetOwnerPlayer();
            if (player == null) return;
            AddXP(player, 15);
        }

        private void OnStructureUpgrade(BuildingBlock block, BasePlayer player, BuildingGrade.Enum grade)
        {
            if (player == null) return;
            AddXP(player, 30 * (int)grade);
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null) return;

            if (entity is BradleyAPC)
            {
                RewardHeliXP(entity, info);
                return;
            }

            BasePlayer killer = info?.InitiatorPlayer ?? info?.Initiator as BasePlayer ?? entity.lastAttacker as BasePlayer;

            if (entity is LootContainer container)
            {
                if (container.ShortPrefabName.Contains("barrel"))
                {
                    if (killer != null && !killer.IsNpc)
                    {
                        AddXP(killer, 30);
                    }
                    return;
                }
            }

            if (entity is BaseHelicopter || entity.ShortPrefabName.Contains("patrol") || entity.ShortPrefabName.Contains("helicopter"))
            {
                RewardHeliXP(entity, info);
                return;
            }

            if (info == null) return;

            BasePlayer generalKiller = info.InitiatorPlayer ?? info.Initiator as BasePlayer ?? entity.lastAttacker as BasePlayer;
            if (generalKiller == null || generalKiller == entity || generalKiller.IsNpc) return;

            if (entity is BasePlayer victim)
            {
                if (victim.IsNpc)
                    AddXP(generalKiller, 120);
                else
                    AddXP(generalKiller, 300);

                return;
            }

            if (entity is BaseNpc || entity is BaseAnimalNPC || entity.IsNpc)
            {
                AddXP(generalKiller, 80);
                return;
            }
        }

        private void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (player == null || player.IsNpc || entity == null) return;

            if (entity is HackableLockedCrate hackableCrate)
            {
                if (hackableCrate.IsFullyHacked() && !rewardedHackableCrates.Contains(hackableCrate.net.ID.Value))
                {
                    rewardedHackableCrates.Add(hackableCrate.net.ID.Value);
                    AddXP(player, 1000);
                }
                return;
            }

            if (entity is LootContainer container)
            {
                string prefabName = container.ShortPrefabName.ToLower();

                if (container is SupplyDrop || prefabName.Contains("crate") || prefabName.Contains("drop"))
                {
                    ulong containerId = container.net?.ID.Value ?? 0;

                    if (containerId != 0 && !rewardedCrates.Contains(containerId))
                    {
                        rewardedCrates.Add(containerId);
                        AddXP(player, 30);
                    }
                }
            }
        }

        private void OnCollectiblePickup(CollectibleEntity collectible, BasePlayer player)
        {
            if (collectible == null || player == null || player.IsNpc) return;
            AddXP(player, 15);
        }

        private void OnCropGather(GrowableEntity plant, Item item, BasePlayer player)
        {
            if (plant == null || player == null || player.IsNpc) return;
            AddXP(player, 15);
        }

        private void OnCrateHacked(HackableLockedCrate crate, BasePlayer player)
        {
            if (crate == null || player == null || player.IsNpc) return;
            AddXP(player, 1000);
        }

        // =========================================================
        // COMANDOS & EVOLUÇÃO DE SKILLS
        // =========================================================

        [ChatCommand("skills")]
        private void SkillsCommand(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            ShowSkillMenu(player);
        }

        [ConsoleCommand("skillabler.opensubmenu")]
        private void ConsoleOpenSubmenu(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null || !arg.HasArgs(1)) return;

            string skillName = arg.GetString(0);
            ShowSubmenu(player, skillName);
        }

        [ConsoleCommand("skillabler.openmain")]
        private void ConsoleOpenMain(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;

            ShowSkillMenu(player);
        }

        [ConsoleCommand("skillabler.upgradeskill")]
        private void ConsoleUpgradeSkill(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null || !arg.HasArgs(1)) return;

            string skillKey = arg.GetString(0);
            PlayerData data = GetPlayerData(player);

            if (data.SkillPoints <= 0)
            {
                SendReply(player, "<color=#FF5555>[SkillAbler]</color> Você não tem pontos suficientes!");
                return;
            }

            bool upgraded = false;

            switch (skillKey.ToLower())
            {
                // --- SUBCATEGORIA: MINING YIELD ---
                case "novice":
                    if (data.MiningNovice >= 10) return;
                    data.MiningNovice++;
                    upgraded = true;
                    break;

                case "amateur":
                    if (data.MiningNovice < 10 || data.MiningAmateur >= 5) return;
                    data.MiningAmateur++;
                    upgraded = true;
                    break;

                case "veteran":
                    if (data.MiningAmateur < 5 || data.MiningVeteran >= 3) return;
                    data.MiningVeteran++;
                    upgraded = true;
                    break;

                case "expert":
                    if (data.MiningVeteran < 3 || data.MiningExpert >= 1) return; // Máximo 1
                    data.MiningExpert++;
                    upgraded = true;
                    break;

                case "specialist":
                    if (data.MiningExpert < 1 || data.MiningSpecialist >= 1) return; // Requer Expert 1/1
                    data.MiningSpecialist++;
                    upgraded = true;
                    break;

                // --- SUBCATEGORIA: MINING NODES ---
                case "sweetspot":
                    if (data.MiningSweetSpot >= 3) return;
                    data.MiningSweetSpot++;
                    upgraded = true;
                    break;

                case "likecandy":
                    if (data.MiningSweetSpot < 3)
                    {
                        SendReply(player, "<color=#FF5555>[SkillAbler]</color> Requer Sweet Spot (3/3)!");
                        return;
                    }
                    if (data.MiningLikeCandy >= 1) return;
                    data.MiningLikeCandy++;
                    upgraded = true;
                    break;

                case "onspot":
                    if (data.MiningLikeCandy < 1)
                    {
                        SendReply(player, "<color=#FF5555>[SkillAbler]</color> Requer Like Candy (1/1)!");
                        return;
                    }
                    if (data.MiningOnSpot >= 1) return;
                    data.MiningOnSpot++;
                    upgraded = true;
                    break;

                case "justlikethat":
                    if (data.MiningOnSpot < 1)
                    {
                        SendReply(player, "<color=#FF5555>[SkillAbler]</color> Requer On Spot (1/1)!");
                        return;
                    }
                    if (data.MiningJustLikeThat >= 1) return;
                    data.MiningJustLikeThat++;
                    upgraded = true;
                    break;
            }

            if (upgraded)
            {
                data.SkillPoints--;
                Interface.Oxide.DataFileSystem.WriteObject($"SkillAbler/{player.userID}", data);
                ShowSubmenu(player, "Mining", true);
            }
        }

        [ConsoleCommand("skillabler.doPrestige")]
        private void ConsoleDoPrestige(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;

            PlayerData data = GetPlayerData(player);
            if (data.Level < MaxLevel)
            {
                SendReply(player, "<color=#FF5555>[SkillAbler]</color> Você precisa atingir o nível 100 para prestigiar!");
                return;
            }

            data.Level = 1;
            data.XP = 0;
            data.SkillPoints = 0;
            data.PrestigePoints += 1;

            SendReply(player, $"<color=#AA55FF>[SkillAbler]</color> Você resetou e recebeu +1 Ponto de Prestígio! Total: {data.PrestigePoints}");

            ShowSkillMenu(player);
            CreateXpHud(player);
        }

        // =========================================================
        // HUD PERMANENTE (XP BAR)
        // =========================================================

        private void CreateXpHud(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, XpHudName);

            PlayerData data = GetPlayerData(player);
            int reqXp = GetRequiredXpForLevel(data.Level);
            float progress = Mathf.Clamp01((float)data.XP / reqXp);

            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                Image = { Color = "0.08 0.09 0.10 0.85" },
                RectTransform =
                {
                    AnchorMin = "1 0",
                    AnchorMax = "1 0",
                    OffsetMin = "-390 18",
                    OffsetMax = "-215 90"
                }
            }, "Hud", XpHudName);

            string lvlText = data.PrestigePoints > 0 ? $"LVL {data.Level} [P{data.PrestigePoints}]" : $"LVL {data.Level}";
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.05 0.60", AnchorMax = "0.95 0.92" },
                Text =
                {
                    Text = lvlText,
                    FontSize = 13,
                    Align = TextAnchor.MiddleLeft,
                    Font = "RobotoCondensed-Bold.ttf",
                    Color = "1.00 0.85 0.30 1.00"
                }
            }, XpHudName);

            string xpText = data.Level >= MaxLevel ? "MAX LEVEL" : $"{data.XP}/{reqXp} XP";
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.05 0.60", AnchorMax = "0.95 0.92" },
                Text =
                {
                    Text = xpText,
                    FontSize = 11,
                    Align = TextAnchor.MiddleRight,
                    Font = "RobotoCondensed-Bold.ttf",
                    Color = "0.80 0.80 0.80 1.00"
                }
            }, XpHudName);

            string barBg = $"{XpHudName}_BarBg";
            container.Add(new CuiPanel
            {
                Image = { Color = "0.15 0.15 0.15 0.90" },
                RectTransform = { AnchorMin = "0.05 0.20", AnchorMax = "0.95 0.50" }
            }, XpHudName, barBg);

            container.Add(new CuiPanel
            {
                Image = { Color = data.Level >= MaxLevel ? "0.67 0.33 1.00 0.90" : "0.20 0.70 1.00 0.90" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = $"{progress} 1" }
            }, barBg);

            CuiHelper.AddUi(player, container);
        }

        // =========================================================
        // MAIN MENU
        // =========================================================

        private void ShowSkillMenu(BasePlayer player)
        {
            if (player == null) return;

            CuiHelper.DestroyUi(player, MainPanelName);
            CuiHelper.DestroyUi(player, SubmenuPanelName);

            PlayerData data = GetPlayerData(player);
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                Image = { Color = "0.02 0.02 0.025 0.96", FadeIn = 0.1f },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-640 -360", OffsetMax = "640 360" },
                CursorEnabled = true
            }, "Overlay", MainPanelName);

            container.Add(new CuiPanel
            {
                Image = { Color = "0.10 0.11 0.12 0.95" },
                RectTransform = { AnchorMin = "0 0.89", AnchorMax = "1 1" }
            }, MainPanelName, "SkillAblerHeader");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.20 0.15", AnchorMax = "0.80 0.85" },
                Text = { Text = "SKILL ABLER", FontSize = 32, Align = TextAnchor.MiddleCenter, Font = "RobotoCondensed-Bold.ttf", Color = "1.00 1.00 1.00 1.00" }
            }, "SkillAblerHeader");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.03 0.15", AnchorMax = "0.20 0.85" },
                Text = { Text = $"LEVEL {data.Level}", FontSize = 16, Align = TextAnchor.MiddleLeft, Font = "RobotoCondensed-Bold.ttf", Color = "0.80 0.80 0.80 1.00" }
            }, "SkillAblerHeader");

            string pointsText = $"SKILL POINTS: {data.SkillPoints}  |  PRESTIGE: {data.PrestigePoints}";
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.45 0.15", AnchorMax = "0.78 0.85" },
                Text = { Text = pointsText, FontSize = 15, Align = TextAnchor.MiddleRight, Font = "RobotoCondensed-Bold.ttf", Color = "0.90 0.75 0.30 1.00" }
            }, "SkillAblerHeader");

            if (data.Level >= MaxLevel)
            {
                container.Add(new CuiButton
                {
                    Button = { Color = "0.55 0.20 0.80 0.95", Command = "skillabler.doPrestige" },
                    RectTransform = { AnchorMin = "0.80 0.18", AnchorMax = "0.92 0.82" },
                    Text = { Text = "PRESTIGIO", FontSize = 13, Align = TextAnchor.MiddleCenter, Font = "RobotoCondensed-Bold.ttf", Color = "1.00 1.00 1.00 1.00" }
                }, "SkillAblerHeader");
            }

            container.Add(new CuiButton
            {
                Button = { Color = "0.65 0.08 0.08 0.90", Close = MainPanelName },
                RectTransform = { AnchorMin = "0.94 0.15", AnchorMax = "0.985 0.85" },
                Text = { Text = "X", FontSize = 24, Align = TextAnchor.MiddleCenter, Font = "RobotoCondensed-Bold.ttf", Color = "1.00 1.00 1.00 1.00" }
            }, "SkillAblerHeader");

            for (int i = 0; i < skills.Count; i++)
            {
                CreateSkillCard(container, MainPanelName, skills[i], i);
            }

            CuiHelper.AddUi(player, container);
        }

        // 1. Método Original (usado pelo Menu Principal de Categorias)
        private void CreateSkillCard(CuiElementContainer container, string parent, Skill skill, int index)
        {
            int column = index % 4;
            int row = index / 4;

            float columnWidth = 0.225f;
            float minX = 0.025f + (column * 0.25f);
            float maxX = minX + columnWidth;

            float rowHeight = 0.235f;
            float maxY = 0.855f - (row * 0.245f);
            float minY = maxY - rowHeight;

            string cardName = $"SkillCard_{index}";

            container.Add(new CuiPanel
            {
                Image = { Color = "0.08 0.09 0.10 0.98" },
                RectTransform = { AnchorMin = $"{minX} {minY}", AnchorMax = $"{maxX} {maxY}" }
            }, parent, cardName);

            container.Add(new CuiElement
            {
                Parent = cardName,
                Components =
        {
            new CuiRawImageComponent { Url = ImageBaseUrl + skill.Icon, Color = "1.00 1.00 1.00 1.00" },
            new CuiRectTransformComponent { AnchorMin = "0.27 0.25", AnchorMax = "0.73 0.88", OffsetMin = "0 0", OffsetMax = "0 0" }
        }
            });

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.05 0.03", AnchorMax = "0.95 0.22" },
                Text = { Text = skill.Name.ToUpperInvariant(), FontSize = 16, Align = TextAnchor.MiddleCenter, Font = "RobotoCondensed-Bold.ttf", Color = "1.00 1.00 1.00 1.00" }
            }, cardName);

            container.Add(new CuiButton
            {
                Button = { Color = "0 0 0 0", Command = $"skillabler.opensubmenu {skill.Name}" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Text = { Text = "" }
            }, cardName);
        }


        // =========================================================
        // SUBMENU DE SKILL / ÁRVORE DE MINERAÇÃO
        // =========================================================
        // Navegação:
        // - Scroll principal: SOMENTE vertical e responde à roda do mouse.
        // - Cada lista de skills: SOMENTE horizontal.
        // - A barra horizontal funciona normalmente por arraste.
        // - Sem paginação.

        // refresh = true: NÃO recria o painel nem os scroll views (preserva a posição do scroll);
        // apenas substitui os elementos dinâmicos (pontos, títulos, botões de reset e cards).
        private void ShowSubmenu(BasePlayer player, string categoryName, bool refresh = false)
        {
            if (player == null) return;

            if (!refresh)
            {
                CuiHelper.DestroyUi(player, SubmenuPanelName);
                CuiHelper.DestroyUi(player, MainPanelName);
            }
            else
            {
                CuiHelper.DestroyUi(player, "SubmenuPointsLabel");
            }

            PlayerData data = GetPlayerData(player);
            CuiElementContainer container = new CuiElementContainer();
            string headerPanel = "SubmenuHeader";
            string mainViewport = "SubmenuMainViewport";
            string mainScrollContent = "SubmenuMainScrollContent";

            if (!refresh)
            {
            // Background Principal
            container.Add(new CuiPanel
            {
                Image = { Color = "0.02 0.02 0.025 0.96", FadeIn = 0.05f },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-640 -360", OffsetMax = "640 360" },
                CursorEnabled = true
            }, "Overlay", SubmenuPanelName);

            // Header Fixo
            container.Add(new CuiPanel
            {
                Image = { Color = "0.10 0.11 0.12 0.95" },
                RectTransform = { AnchorMin = "0 0.89", AnchorMax = "1 1" }
            }, SubmenuPanelName, headerPanel);

            container.Add(new CuiButton
            {
                Button = { Color = "0.20 0.22 0.25 0.90", Command = "skillabler.openmain" },
                RectTransform = { AnchorMin = "0.02 0.18", AnchorMax = "0.07 0.82" },
                Text = { Text = "←", FontSize = 20, Align = TextAnchor.MiddleCenter, Font = "RobotoCondensed-Bold.ttf", Color = "1 1 1 1" }
            }, headerPanel);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.09 0.15", AnchorMax = "0.50 0.85" },
                Text = { Text = categoryName.ToUpperInvariant(), FontSize = 22, Align = TextAnchor.MiddleLeft, Font = "RobotoCondensed-Bold.ttf", Color = "1.00 1.00 1.00 1.00" }
            }, headerPanel);

            container.Add(new CuiButton
            {
                Button = { Color = "0.65 0.08 0.08 0.90", Close = SubmenuPanelName },
                RectTransform = { AnchorMin = "0.93 0.18", AnchorMax = "0.98 0.82" },
                Text = { Text = "X", FontSize = 20, Align = TextAnchor.MiddleCenter, Font = "RobotoCondensed-Bold.ttf", Color = "1 1 1 1" }
            }, headerPanel);

            // =========================================================
            // VIEWPORT PRINCIPAL - SOMENTE VERTICAL
            // =========================================================
            container.Add(new CuiPanel
            {
                Image = { Color = "0.05 0.05 0.06 0.98" },
                RectTransform = { AnchorMin = "0.02 0.03", AnchorMax = "0.98 0.87" }
            }, SubmenuPanelName, mainViewport);

            container.Add(new CuiElement
            {
                Parent = mainViewport,
                Name = mainScrollContent,
                Components =
        {
            new CuiImageComponent { Color = "0 0 0 0" },
            new CuiRectTransformComponent
            {
                AnchorMin = "0 0",
                AnchorMax = "1 1"
            },
            new CuiScrollViewComponent
            {
                ContentTransform = new CuiRectTransform
                {
                    AnchorMin = "0 1",
                    AnchorMax = "1 1",
                    OffsetMin = "0 -800",
                    OffsetMax = "0 0",
                    Pivot = "0.5 1"
                },
                Vertical = true,
                Horizontal = false,
                MovementType = UnityEngine.UI.ScrollRect.MovementType.Clamped,
                Elasticity = 0.15f,
                Inertia = true,
                DecelerationRate = 0.30f,
                ScrollSensitivity = 35f,
                VerticalScrollbar = new CuiScrollbar
                {
                    AutoHide = false,
                    Size = 14f
                }
            },
            new CuiNeedsCursorComponent()
        }
            });
            } // fim !refresh

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.52 0.15", AnchorMax = "0.90 0.85" },
                Text = { Text = $"PONTOS DISPONÍVEIS: {data.SkillPoints}", FontSize = 16, Align = TextAnchor.MiddleRight, Font = "RobotoCondensed-Bold.ttf", Color = "0.90 0.75 0.30 1.00" }
            }, headerPanel, "SubmenuPointsLabel");

            if (categoryName.Equals("Mining", StringComparison.OrdinalIgnoreCase))
            {
                // 1. MINING YIELD
                int yieldPoints = data.MiningNovice + data.MiningAmateur + data.MiningVeteran + data.MiningExpert + data.MiningSpecialist;
                List<SubSkillData> yieldSkills = new List<SubSkillData>
        {
            new SubSkillData("Novice Miner", $"{data.MiningNovice}/10", "Aumenta mining yield em +5% por nível.", "novice.png", data.MiningNovice < 10, "skillabler.upgradeskill novice", false),
            new SubSkillData("Amateur Miner", $"{data.MiningAmateur}/5", "Aumenta mining yield em +10% por nível.", "amateur.png", data.MiningNovice >= 10 && data.MiningAmateur < 5, "skillabler.upgradeskill amateur", data.MiningNovice < 10),
            new SubSkillData("Veteran Miner", $"{data.MiningVeteran}/3", "Aumenta mining yield em +25% por nível.", "veteran.png", data.MiningAmateur >= 5 && data.MiningVeteran < 3, "skillabler.upgradeskill veteran", data.MiningAmateur < 5),
            new SubSkillData("Expert Miner", $"{data.MiningExpert}/1", "Aumenta mining yield em +100%.", "expert.png", data.MiningVeteran >= 3 && data.MiningExpert < 1, "skillabler.upgradeskill expert", data.MiningVeteran < 3),
            new SubSkillData("Specialist Miner", $"{data.MiningSpecialist}/1", "Jackhammer sem penalidade de yield.", "specialist.png", data.MiningExpert >= 1 && data.MiningSpecialist < 1, "skillabler.upgradeskill specialist", data.MiningExpert < 1)
        };

                RenderSubCategorySection(player, container, mainScrollContent, "yield", "► MINING YIELD", yieldPoints, yieldSkills, 0.52f, 0.98f, refresh);

                // 2. MINING NODES
                int nodesPoints = data.MiningSweetSpot + data.MiningLikeCandy + data.MiningOnSpot + data.MiningJustLikeThat;
                List<SubSkillData> nodesSkills = new List<SubSkillData>
        {
            new SubSkillData("Sweet Spot", $"{data.MiningSweetSpot}/3", "Acertar o sweet spot conta como 2 acertos.", "sweetspot.png", data.MiningSweetSpot < 3, "skillabler.upgradeskill sweetspot", false),
            new SubSkillData("Like Candy", $"{data.MiningLikeCandy}/1", "Dobro de yield ao acertar o sweet spot.", "likecandy.png", data.MiningSweetSpot >= 3 && data.MiningLikeCandy < 1, "skillabler.upgradeskill likecandy", data.MiningSweetSpot < 3),
            new SubSkillData("On Spot", $"{data.MiningOnSpot}/1", "Qualquer acerto vai direto no sweet spot.", "onspot.png", data.MiningLikeCandy >= 1 && data.MiningOnSpot < 1, "skillabler.upgradeskill onspot", data.MiningLikeCandy < 1),
            new SubSkillData("Just Like That", $"{data.MiningJustLikeThat}/1", "Acertar o sweet spot minera o nó inteiro.", "justlikethat.png", data.MiningOnSpot >= 1 && data.MiningJustLikeThat < 1, "skillabler.upgradeskill justlikethat", data.MiningOnSpot < 1)
        };

                RenderSubCategorySection(player, container, mainScrollContent, "nodes", "► MINING NODES", nodesPoints, nodesSkills, 0.02f, 0.48f, refresh);
            }

            CuiHelper.AddUi(player, container);
        }

        private void RenderSubCategorySection(BasePlayer player, CuiElementContainer container, string parentPanel, string subCategory, string title, int spentPoints, List<SubSkillData> subSkills, float anchorMinY, float anchorMaxY, bool refresh)
        {
            string sectionPanel = $"SubSection_{subCategory}";
            string cardsViewport = $"CardsViewport_{subCategory}";

            if (refresh)
            {
                // Remove só o que muda; o painel e o scroll view permanecem (mantém a posição)
                CuiHelper.DestroyUi(player, $"SubTitle_{subCategory}");
                CuiHelper.DestroyUi(player, $"SubReset_{subCategory}");
                foreach (var sk in subSkills)
                    CuiHelper.DestroyUi(player, $"Card_{sk.Name.Replace(" ", "")}");
            }
            else
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = "0.08 0.09 0.11 0.60" },
                    RectTransform = { AnchorMin = $"0.01 {anchorMinY.ToString(System.Globalization.CultureInfo.InvariantCulture)}", AnchorMax = $"0.99 {anchorMaxY.ToString(System.Globalization.CultureInfo.InvariantCulture)}" }
                }, parentPanel, sectionPanel);
            }

            // Título
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.01 0.86", AnchorMax = "0.50 0.98" },
                Text = { Text = title, FontSize = 15, Align = TextAnchor.MiddleLeft, Font = "RobotoCondensed-Bold.ttf", Color = "0.40 0.80 1.00 1.00" }
            }, sectionPanel, $"SubTitle_{subCategory}");

            // Reset
            if (spentPoints > 0)
            {
                container.Add(new CuiButton
                {
                    Button = { Color = "0.75 0.35 0.10 0.95", Command = $"skillabler.confirmreset {subCategory}" },
                    RectTransform = { AnchorMin = "0.75 0.86", AnchorMax = "0.99 0.98" },
                    Text = { Text = $"RESET ({spentPoints * 100} SCRAP)", FontSize = 10, Align = TextAnchor.MiddleCenter, Font = "RobotoCondensed-Bold.ttf", Color = "1 1 1 1" }
                }, sectionPanel, $"SubReset_{subCategory}");
            }

            // =========================================================
            // ÁREA HORIZONTAL DAS SKILLS
            // =========================================================
            int totalItems = subSkills.Count;

            const float gap = 12f;
            const float cardWidth = 280f;
            float contentWidth = Mathf.Max(900f, gap + totalItems * (cardWidth + gap));

            if (!refresh)
            {
            container.Add(new CuiElement
            {
                Parent = sectionPanel,
                Name = cardsViewport,
                Components =
        {
            new CuiImageComponent { Color = "0.0 0.0 0.0 0.10" },
            new CuiRectTransformComponent
            {
                AnchorMin = "0.01 0.03",
                AnchorMax = "0.99 0.83"
            },
            new CuiScrollViewComponent
            {
                ContentTransform = new CuiRectTransform
                {
                    AnchorMin = "0 0",
                    AnchorMax = "0 1",
                    OffsetMin = "0 0",
                    OffsetMax = $"{contentWidth.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)} 0",
                    Pivot = "0 0.5"
                },
                Vertical = false,
                Horizontal = true,
                MovementType = UnityEngine.UI.ScrollRect.MovementType.Clamped,
                Elasticity = 0.15f,
                Inertia = true,
                DecelerationRate = 0.30f,
                ScrollSensitivity = 25f,
                HorizontalScrollbar = new CuiScrollbar
                {
                    AutoHide = false,
                    Size = 18f,
                    Invert = true // corrige a direção da barra horizontal (arrastar p/ direita = rolar p/ direita)
                }
            },
            new CuiNeedsCursorComponent()
        }
            });
            }

            for (int i = 0; i < totalItems; i++)
            {
                CreateHorizontalSubSkillCard(container, cardsViewport, subSkills[i], i, totalItems);
            }
        }
        private void CreateHorizontalSubSkillCard(CuiElementContainer container, string parentContent, SubSkillData skill, int index, int totalItems)
        {
            string cardPanel = $"Card_{skill.Name.Replace(" ", "")}";

            const float gap = 12f;
            const float cardWidth = 280f;

            float minX = gap + index * (cardWidth + gap);
            float maxX = minX + cardWidth;

            string strMinX = minX.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            string strMaxX = maxX.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

            container.Add(new CuiPanel
            {
                Image = { Color = skill.Locked ? "0.12 0.13 0.15 0.50" : "0.15 0.18 0.20 0.85" },
                RectTransform =
                {
                    AnchorMin = "0 0.03",
                    AnchorMax = "0 0.97",
                    OffsetMin = $"{strMinX} 0",
                    OffsetMax = $"{strMaxX} 0"
                }
            }, parentContent, cardPanel);

            // Ícone
            container.Add(new CuiElement
            {
                Parent = cardPanel,
                Components =
                {
                    new CuiRawImageComponent { Url = skill.IconUrl, Color = skill.Locked ? "0.3 0.3 0.3 0.4" : "1 1 1 0.9" },
                    new CuiRectTransformComponent { AnchorMin = "0.20 0.50", AnchorMax = "0.80 0.92" }
                }
            });

            // Nome e Nível
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.03 0.35", AnchorMax = "0.97 0.48" },
                Text = { Text = $"{skill.Name.ToUpper()} ({skill.LevelText})", FontSize = 10, Align = TextAnchor.MiddleCenter, Font = "RobotoCondensed-Bold.ttf", Color = skill.Locked ? "0.5 0.5 0.5 1" : "0.9 0.7 0.2 1" }
            }, cardPanel);

            // Descrição
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.03 0.20", AnchorMax = "0.97 0.34" },
                Text = { Text = skill.Desc, FontSize = 8, Align = TextAnchor.MiddleCenter, Font = "RobotoCondensed-Normal.ttf", Color = "0.8 0.8 0.8 0.8" }
            }, cardPanel);

            // Botão Upgrade ou Status
            if (skill.Locked)
            {
                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.05 0.04", AnchorMax = "0.95 0.19" },
                    Text = { Text = "BLOQUEADO", FontSize = 9, Align = TextAnchor.MiddleCenter, Font = "RobotoCondensed-Bold.ttf", Color = "0.7 0.2 0.2 1" }
                }, cardPanel);
            }
            else if (skill.CanUpgrade)
            {
                container.Add(new CuiButton
                {
                    Button = { Color = "0.18 0.55 0.22 0.95", Command = skill.Command },
                    RectTransform = { AnchorMin = "0.05 0.04", AnchorMax = "0.95 0.19" },
                    Text = { Text = "UPGRADE", FontSize = 9, Align = TextAnchor.MiddleCenter, Font = "RobotoCondensed-Bold.ttf", Color = "1 1 1 1" }
                }, cardPanel);
            }
            else
            {
                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.05 0.04", AnchorMax = "0.95 0.19" },
                    Text = { Text = "MÁXIMO", FontSize = 9, Align = TextAnchor.MiddleCenter, Font = "RobotoCondensed-Bold.ttf", Color = "0.2 0.7 0.3 1" }
                }, cardPanel);
            }
        }

        private void RenderMiningTree(CuiElementContainer container, string parent, PlayerData data)
        {
            // Posições dos cards na árvore
            RenderSkillNode(container, parent, "novice_miner", "Novice Miner", $"Aumenta o rendimento de mineração em 5% por nível.", data.MiningNovice, 10, true, "0.05 0.70", "0.95 0.86");
            RenderSkillNode(container, parent, "amateur_miner", "Amateur Miner", $"Aumenta o rendimento de mineração em 10% por nível.\n(Requer: Novice Miner Max)", data.MiningAmateur, 5, data.MiningNovice >= 10, "0.05 0.52", "0.95 0.68");
            RenderSkillNode(container, parent, "veteran_miner", "Veteran Miner", $"Aumenta o rendimento de mineração em 25% por nível.\n(Requer: Amateur Miner Max)", data.MiningVeteran, 3, data.MiningAmateur >= 5, "0.05 0.34", "0.95 0.50");
            RenderSkillNode(container, parent, "expert_miner", "Expert Miner", $"Aumenta o rendimento de mineração em 100%.\n(Requer: Veteran Miner Max)", data.MiningExpert, 1, data.MiningVeteran >= 3, "0.05 0.16", "0.95 0.32");
            RenderSkillNode(container, parent, "specialist_miner", "Specialist Miner (Ultimate)", $"O Jackhammer não sofre penalidade de rendimento em nódulos de mineração.\n(Requer: Expert Miner)", data.MiningSpecialist, 1, data.MiningExpert >= 1, "0.05 0.02", "0.95 0.14");
        }


        private void RenderSkillNode(CuiElementContainer container, string parent, string skillId, string title, string desc, int currentLvl, int maxLvl, bool reqMet, string anchorMin, string anchorMax)
        {
            string panelName = $"Node_{skillId}";
            string bgColor = reqMet ? (currentLvl >= maxLvl ? "0.15 0.25 0.15 0.90" : "0.12 0.14 0.16 0.90") : "0.10 0.10 0.10 0.50";

            container.Add(new CuiPanel
            {
                Image = { Color = bgColor },
                RectTransform = { AnchorMin = anchorMin, AnchorMax = anchorMax }
            }, parent, panelName);

            // Nome e Nível
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.02 0.50", AnchorMax = "0.70 0.95" },
                Text = { Text = $"{title} ({currentLvl}/{maxLvl})", FontSize = 15, Align = TextAnchor.MiddleLeft, Font = "RobotoCondensed-Bold.ttf", Color = reqMet ? "1.00 1.00 1.00 1.00" : "0.50 0.50 0.50 1.00" }
            }, panelName);

            // Descrição
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.02 0.05", AnchorMax = "0.70 0.50" },
                Text = { Text = desc, FontSize = 11, Align = TextAnchor.MiddleLeft, Font = "RobotoCondensed-Bold.ttf", Color = "0.70 0.70 0.70 1.00" }
            }, panelName);

            // Botão Evoluir
            if (reqMet && currentLvl < maxLvl)
            {
                container.Add(new CuiButton
                {
                    Button = { Color = "0.20 0.60 0.20 0.90", Command = $"skillabler.upgradeskill {skillId}" },
                    RectTransform = { AnchorMin = "0.80 0.20", AnchorMax = "0.98 0.80" },
                    Text = { Text = "+ EVOLUIR", FontSize = 12, Align = TextAnchor.MiddleCenter, Font = "RobotoCondensed-Bold.ttf", Color = "1.00 1.00 1.00 1.00" }
                }, panelName);
            }
            else if (currentLvl >= maxLvl)
            {
                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.80 0.20", AnchorMax = "0.98 0.80" },
                    Text = { Text = "MÁXIMO", FontSize = 12, Align = TextAnchor.MiddleCenter, Font = "RobotoCondensed-Bold.ttf", Color = "0.30 0.90 0.30 1.00" }
                }, panelName);
            }
        }

        // Renderizador com Suporte a Linhas (row = 0 Superior, row = 1 Inferior)
        private void CreateSubSkillCard(CuiElementContainer container, string parent, string name, string levelText, string desc, string iconUrl, int col, int row, bool canUpgrade, string command, bool locked = false)
        {
            float cardWidth = 0.23f;
            float spacingX = 0.016f;
            float minX = 0.015f + col * (cardWidth + spacingX);
            float maxX = minX + cardWidth;

            // Ajustado para o container rolável estendido
            float maxY = row == 0 ? 0.88f : 0.43f;
            float minY = maxY - 0.38f;

            string cardName = $"SubSkillCard_{row}_{col}";
            string bgColor = locked ? "0.12 0.12 0.14 0.60" : "0.14 0.16 0.18 0.95";

            container.Add(new CuiPanel
            {
                Image = { Color = bgColor },
                RectTransform = { AnchorMin = $"{minX} {minY}", AnchorMax = $"{maxX} {maxY}" }
            }, parent, cardName);

            if (!string.IsNullOrEmpty(iconUrl))
            {
                string iconColor = locked ? "0.30 0.30 0.30 0.30" : "1.00 1.00 1.00 1.00";
                container.Add(new CuiElement
                {
                    Parent = cardName,
                    Components =
            {
                new CuiRawImageComponent { Url = ImageBaseUrl + iconUrl, Color = iconColor },
                new CuiRectTransformComponent { AnchorMin = "0.32 0.50", AnchorMax = "0.68 0.92" }
            }
                });
            }

            string titleColor = locked ? "0.50 0.50 0.50 1.00" : "1.00 0.80 0.20 1.00";
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.02 0.32", AnchorMax = "0.98 0.47" },
                Text = { Text = $"{name.ToUpperInvariant()} ({levelText})", FontSize = 11, Align = TextAnchor.MiddleCenter, Font = "RobotoCondensed-Bold.ttf", Color = titleColor }
            }, cardName);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.02 0.17", AnchorMax = "0.98 0.31" },
                Text = { Text = desc, FontSize = 9, Align = TextAnchor.MiddleCenter, Font = "RobotoCondensed-Bold.ttf", Color = "0.75 0.75 0.75 1.00" }
            }, cardName);

            if (canUpgrade)
            {
                container.Add(new CuiButton
                {
                    Button = { Color = "0.20 0.60 0.20 0.90", Command = command },
                    RectTransform = { AnchorMin = "0.08 0.03", AnchorMax = "0.92 0.16" },
                    Text = { Text = "UPGRADE", FontSize = 10, Align = TextAnchor.MiddleCenter, Font = "RobotoCondensed-Bold.ttf", Color = "1.00 1.00 1.00 1.00" }
                }, cardName);
            }
            else if (locked)
            {
                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.08 0.03", AnchorMax = "0.92 0.16" },
                    Text = { Text = "BLOQUEADO", FontSize = 10, Align = TextAnchor.MiddleCenter, Font = "RobotoCondensed-Bold.ttf", Color = "0.80 0.40 0.40 1.00" }
                }, cardName);
            }
        }
        //MINING - Yield - ULTIMATE
        // Intercepta a perda de condição do item no inventário
        private object OnLoseCondition(Item item, ref float amount)
        {
            if (item == null || item.info.shortname != "jackhammer") return null;

            BasePlayer player = item.GetOwnerPlayer();
            if (player == null || player.IsNpc) return null;

            PlayerData data = GetPlayerData(player);
            if (data.MiningSpecialist > 0)
            {
                // Zera o dano que o item iria sofrer
                amount = 0f;
                item.condition = item.maxCondition;
                return true;
            }

            return null;
        }

        // ---------------------------------------------------------
        // Detecção de Sweet Spot (hotspot) + Just Like That
        // ---------------------------------------------------------
        // O Rust incrementa OreResourceEntity.bonusesKilled quando o jogador acerta o hotspot
        // e zera quando erra. Comparamos com o último valor visto para detectar o acerto.
        private readonly Dictionary<ulong, int> lastOreBonuses = new Dictionary<ulong, int>();
        private readonly HashSet<ulong> pendingWholeNode = new HashSet<ulong>();
        private ulong lastHotspotOreId;
        private int lastHotspotFrame = -1;
        private bool lastHotspotResult;

        private bool DidHitSweetSpot(ResourceDispenser dispenser)
        {
            OreResourceEntity ore = dispenser.GetComponent<OreResourceEntity>();
            if (ore == null || ore.net == null) return false;

            ulong id = ore.net.ID.Value;

            // OnDispenserGather é chamado 1x por item do nódulo (ex.: minério + HQM) na mesma batida;
            // reaproveita o resultado dentro do mesmo frame.
            if (lastHotspotFrame == Time.frameCount && lastHotspotOreId == id)
                return lastHotspotResult;

            int current = ore.bonusesKilled;
            lastOreBonuses.TryGetValue(id, out int last);
            bool hit = current > last;

            if (current <= 0) lastOreBonuses.Remove(id);
            else
            {
                if (lastOreBonuses.Count > 500) lastOreBonuses.Clear();
                lastOreBonuses[id] = current;
            }

            lastHotspotFrame = Time.frameCount;
            lastHotspotOreId = id;
            lastHotspotResult = hit;
            return hit;
        }

        private void QueueCollectWholeNode(ResourceDispenser dispenser, BasePlayer player, PlayerData data)
        {
            BaseEntity nodeEntity = dispenser.GetComponent<BaseEntity>();
            if (nodeEntity == null || nodeEntity.net == null) return;

            ulong nodeId = nodeEntity.net.ID.Value;
            if (!pendingWholeNode.Add(nodeId)) return; // já agendado nesta batida

            // Executa no próximo tick para não mexer no nódulo enquanto o Rust ainda processa a batida
            NextTick(() =>
            {
                pendingWholeNode.Remove(nodeId);
                lastOreBonuses.Remove(nodeId);

                if (dispenser == null || nodeEntity == null || nodeEntity.IsDestroyed) return;
                if (player == null || !player.IsConnected) return;

                float multiplier = GetMiningYieldMultiplier(data);

                foreach (ItemAmount res in dispenser.containedItems)
                {
                    int remaining = Mathf.FloorToInt(res.amount);
                    if (remaining <= 0) continue;

                    int give = Mathf.Max(1, Mathf.RoundToInt(remaining * multiplier));
                    Item bonusItem = ItemManager.CreateByItemID(res.itemid, give);
                    if (bonusItem == null) continue;

                    player.GiveItem(bonusItem, BaseEntity.GiveItemReason.ResourceHarvested);
                    res.amount = 0f;
                }

                dispenser.fractionRemaining = 0f;
                nodeEntity.Kill(BaseNetworkable.DestroyMode.Gib);
            });
        }

        // *On spot*: Redireciona automaticamente qualquer batida para o Sweet Spot
        private object OnSweetSpotAim(ResourceDispenser dispenser, BasePlayer player, Vector3 point)
        {
            if (player == null) return null;

            PlayerData data = GetPlayerData(player);
            if (data.MiningOnSpot > 0 && dispenser.gatherType == ResourceDispenser.GatherType.Ore)
            {
                // Força a engine a tratar a batida como se tivesse atingido o ponto reluzente
                return true;
            }

            return null;
        }

    }
}