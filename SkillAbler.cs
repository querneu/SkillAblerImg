using System;
using System.Collections.Generic;
using UnityEngine;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using System.Linq;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("SkillAbler", "Lucas", "0.5.0")]
    [Description("A custom skill menu system with full game progression and prestige system.")]
    public class SkillAbler : RustPlugin
    {
        // Guarda o ID do jogador que mais recentemente acertou o helicóptero
        private readonly Dictionary<ulong, ulong> heliLastHitPlayer = new Dictionary<ulong, ulong>();
        private readonly HashSet<ulong> deadHelis = new HashSet<ulong>();
        private readonly HashSet<ulong> processedHelis = new HashSet<ulong>();

        private readonly HashSet<ulong> rewardedCrates = new HashSet<ulong>();
        private const string MainPanelName = "MainSkillablerMenu";
        private const string SubmenuPanelName = "SubmenuSkillablerMenu";
        private const string XpHudName = "SkillAblerXpHud";

        private const string ImageBaseUrl =
            "https://raw.githubusercontent.com/querneu/SkillAblerImg/refs/heads/master/";

        private const int MaxLevel = 100;
        private const int BaseXpReq = 1000;
        // Nome único para o container da UI do botão no inventário

        private const string XP_UI_NAME = "SkillAbler_XPNotification";


        private class Skill
        {
            public string Name;
            public string Icon;

            public Skill(string name, string icon)
            {
                Name = name;
                Icon = icon;
            }
        }

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

        // =========================================================
        // PLAYER DATA & SAVING
        // =========================================================

        private class PlayerData
        {
            public int Level = 1;
            public int XP = 0;
            public int SkillPoints = 0;
            public int PrestigePoints = 0;

            public int MiningLevel = 0;
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

        private Dictionary<ulong, PlayerData> playerData = new Dictionary<ulong, PlayerData>();

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject("SkillAbler_Data", playerData);
        private void LoadData() => playerData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerData>>("SkillAbler_Data") ?? new Dictionary<ulong, PlayerData>();

        // =========================================================
        // HOOKS & INIT
        // =========================================================

        private void Init()
        {
            LoadData();
            Puts("=================================");
            Puts("  SKILLABLER 0.5.0 LOADED!");
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
            processedHelis.Clear();
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

        //Coleta de XP
        private void OnCollectIngredient(CollectibleEntity entity, ItemAmount itemAmount, BasePlayer player)
        {
            if (player == null || player.IsNpc || itemAmount == null) return;

            // Concede XP para QUALQUER item coletado do chão
            AddXP(player, 10); // Ajuste a quantidade de XP se desejar
        }

        //Recycler XP
        private void OnRecycleItem(Recycler recycler, Item item)
        {
            if (recycler == null || item == null) return;

            // 1. Tenta obter o dono do inventário
            BasePlayer player = recycler.inventory?.playerOwner;

            // 2. Se for nulo (reciclador de monumento público), pega o jogador real mais próximo (raio de 3.5m)
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

            // Concede XP por item reciclado
            AddXP(player, 15);
        }

        // =========================================================
        // LOGICA DE XP E PROGRESSÃO
        // =========================================================

        private void ShowFloatingXP(BasePlayer player, int amount)
        {
            if (player == null || !player.IsConnected) return;

            // Destrói a UI anterior para não empilhar textos
            CuiHelper.DestroyUi(player, XP_UI_NAME);

            var container = new CuiElementContainer();

            container.Add(new CuiElement
            {
                Name = XP_UI_NAME,
                Parent = "Hud",
                Components =
        {
            new CuiTextComponent
            {
                Text = $"+{amount} XP",
                FontSize = 20,
                Align = UnityEngine.TextAnchor.MiddleCenter,
                Color = "0.33 0.87 0.44 1.0", // Verde vivo
                Font = "RobotoCondensed-Bold.ttf"
            },
            new CuiRectTransformComponent
            {
                AnchorMin = "0.4 0.53",
                AnchorMax = "0.6 0.58"
            }
        }
            });

            CuiHelper.AddUi(player, container);

            // Apaga a mensagem após 0.2 segundos
            timer.Once(0.2f, () =>
            {
                if (player != null && player.IsConnected)
                {
                    CuiHelper.DestroyUi(player, XP_UI_NAME);
                }
            });
        }
        private int GetRequiredXpForLevel(int level)
        {
            if (level >= MaxLevel) return BaseXpReq * 10; // Cap visual

            // Incremento do custo escalando dinamicamente até o nível 100
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

        // 1. Batidas em Recursos (Árvores e Nódulos de Pedra/Metal)
        private void OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (player == null || player.IsNpc || item == null) return;

            // Concede XP a cada extração via ferramenta
            AddXP(player, 10); // Ajuste o valor conforme necessário
        }


        //HELI
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

            // Se já entregou XP para este helicóptero, ignora
            if (heliNetId != 0 && deadHelis.Contains(heliNetId)) return;

            BasePlayer killer = info?.InitiatorPlayer ?? info?.Initiator as BasePlayer ?? entity.lastAttacker as BasePlayer;

            // Se o jogo não enviou a referência do jogador na morte, busca no nosso dicionário de dano
            if (killer == null && heliNetId != 0 && heliLastHitPlayer.ContainsKey(heliNetId))
            {
                ulong killerId = heliLastHitPlayer[heliNetId];
                killer = BasePlayer.FindByID(killerId);
            }

            if (killer != null && !killer.IsNpc)
            {
                if (heliNetId != 0) deadHelis.Add(heliNetId);
                AddXP(killer, 3000);
            }

            if (heliNetId != 0) heliLastHitPlayer.Remove(heliNetId);
        }

        // 2. Recursos Finalizados (Árvore derrubada ou Nódulo completamente minerado)
        private void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (player == null) return;
            AddXP(player, 100);
        }
        // 3. Crafting de Itens (XP por unidade)
        // 3. Crafting de Itens (XP por unidade)
        private void OnItemCraftFinished(ItemCraftTask task, Item item)
        {
            if (item == null) return;

            // Resgata o jogador através do dono do item finalizado ou da raiz do container
            BasePlayer player = item.GetOwnerPlayer() ?? item.GetRootContainer()?.playerOwner;

            // Se o item foi criado no chão/inventory no segundo do fim, busca pelos jogadores ativos no servidor
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
        // 4. Danos / Destruição de Barris e Caixas
        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return;

            // Checa se é o Patrol Helicopter
            if (entity is BaseHelicopter || entity.ShortPrefabName.Contains("patrol") || entity.ShortPrefabName.Contains("helicopter"))
            {
                BasePlayer attacker = info.InitiatorPlayer ?? info.Initiator as BasePlayer;
                ulong heliNetId = entity.net?.ID.Value ?? 0;

                if (heliNetId != 0 && attacker != null && !attacker.IsNpc)
                {
                    heliLastHitPlayer[heliNetId] = attacker.userID;
                }

                // Se o golpe atual zerou a vida do helicóptero (golpe fatal)
                if (entity.health - info.damageTypes.Total() <= 0)
                {
                    RewardHeliXP(entity, info);
                }
            }
        }

        // 5. Construções Colocadas
        private void OnEntityBuilt(Planner plan, GameObject go)
        {
            BasePlayer player = plan?.GetOwnerPlayer();
            if (player == null) return;
            AddXP(player, 15);
        }

        // 6. Upgrades de Estrutura
        private void OnStructureUpgrade(BuildingBlock block, BasePlayer player, BuildingGrade.Enum grade)
        {
            if (player == null) return;
            AddXP(player, 30 * (int)grade);
        }


        private void OnHelicopterKilled(PatrolHelicopterAI heli, HitInfo info)
        {
            if (heli == null) return;

            BaseHelicopter baseHeli = heli.GetComponent<BaseHelicopter>();
            ulong heliNetId = baseHeli?.net?.ID.Value ?? 0;

            if (heliNetId != 0 && deadHelis.Contains(heliNetId)) return;

            // Obtém o atacante
            BasePlayer killer = info?.InitiatorPlayer ?? info?.Initiator as BasePlayer;

            // Se o info veio nulo (queda por destruição de rotor), busca o último jogador a causar dano
            if (killer == null && heliNetId != 0 && heliLastHitPlayer.ContainsKey(heliNetId))
            {
                ulong killerId = heliLastHitPlayer[heliNetId];
                killer = BasePlayer.FindByID(killerId);
            }

            if (killer != null && !killer.IsNpc)
            {
                if (heliNetId != 0) deadHelis.Add(heliNetId);
                AddXP(killer, 3000);
            }

            if (heliNetId != 0) heliLastHitPlayer.Remove(heliNetId);
        }


        // 7. Abates (PVP, Cientistas e Animais)
        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null) return;

            // Se for o Bradley APC
            if (entity is BradleyAPC)
            {
                RewardHeliXP(entity, info);
                return;
            }

            // Obtém o jogador que destruiu o barril/entidade
            BasePlayer killer = info?.InitiatorPlayer ?? info?.Initiator as BasePlayer ?? entity.lastAttacker as BasePlayer;

            // 1. DESTRUIÇÃO DE BARRIS DE LOOT / ROAD CRAWL (30 XP)
            if (entity is LootContainer container)
            {
                // Verifica se é qualquer tipo de barril (barrel, oil_barrel, loot_barrel, etc.)
                if (container.ShortPrefabName.Contains("barrel"))
                {
                    if (killer != null && !killer.IsNpc)
                    {
                        AddXP(killer, 30);
                    }
                    return;
                }
            }



            // Se for o helicóptero caindo via OnEntityDeath tradicional
            if (entity is BaseHelicopter || entity.ShortPrefabName.Contains("patrol") || entity.ShortPrefabName.Contains("helicopter"))
            {
                RewardHeliXP(entity, info);
                return;
            }

            // DEMAIS ENTIDADES (PVP, NPCS, ANIMAIS)
            if (info == null) return;

            BasePlayer generalKiller = info.InitiatorPlayer ?? info.Initiator as BasePlayer ?? entity.lastAttacker as BasePlayer;
            if (generalKiller == null || generalKiller == entity || generalKiller.IsNpc) return;

            if (entity is BasePlayer victim)
            {
                if (victim.IsNpc)
                    AddXP(generalKiller, 120); // Cientista / NPC
                else
                    AddXP(generalKiller, 300); // Player Real (PvP)

                return;
            }

            if (entity is BaseNpc || entity is BaseAnimalNPC || entity.IsNpc)
            {
                AddXP(generalKiller, 80); // Animais e NPCs
                return;
            }
        }
        // Lista para registrar quais caixas hackeáveis já deram XP
        private readonly HashSet<ulong> rewardedHackableCrates = new HashSet<ulong>();

        // 8. Loot em Containers/Caixas de Monumento
        private void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (player == null || player.IsNpc || entity == null) return;

            // 1. Caixa Hackeável (Timed Crate / Laptop) -> 1000 XP
            if (entity is HackableLockedCrate hackableCrate)
            {
                // Se a caixa já foi totalmente hackeada e ainda não deu o XP nesta sessão
                if (hackableCrate.IsFullyHacked() && !rewardedHackableCrates.Contains(hackableCrate.net.ID.Value))
                {
                    rewardedHackableCrates.Add(hackableCrate.net.ID.Value);
                    AddXP(player, 1000);
                }
                return;
            }

            // 2. Qualquer outra caixa comum / militar / elite -> 30 XP
            if (entity is LootContainer container)
            {
                string prefabName = container.ShortPrefabName.ToLower();

                // Inclui "crate", "barrel", "supply_drop" ou a classe SupplyDrop
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

            // Concede XP ao pegar qualquer coletável do chão
            AddXP(player, 15);
        }

        private void OnCropGather(GrowableEntity plant, Item item, BasePlayer player)
        {
            if (plant == null || player == null || player.IsNpc) return;

            // Concede XP ao colher plantas crescidas
            AddXP(player, 15);
        }
        //Crate Hacking
        private void OnCrateHacked(HackableLockedCrate crate, BasePlayer player)
        {
            if (crate == null || player == null || player.IsNpc) return;

            // Concede 1000 XP ao jogador que hackeou/desbloqueou a caixa
            AddXP(player, 1000);
        }

        // =========================================================
        // COMMANDS & PRESTIGIO
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

            // Nível / Prestígio
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

            // Texto XP
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

            // Barra de Progresso
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

            // Header
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

            // Exibição dos Skill Points e Prestige Points
            string pointsText = $"SKILL POINTS: {data.SkillPoints}  |  PRESTIGE: {data.PrestigePoints}";
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.45 0.15", AnchorMax = "0.78 0.85" },
                Text = { Text = pointsText, FontSize = 15, Align = TextAnchor.MiddleRight, Font = "RobotoCondensed-Bold.ttf", Color = "0.90 0.75 0.30 1.00" }
            }, "SkillAblerHeader");

            // Botão de Prestígio (Visível apenas se nível >= 100)
            if (data.Level >= MaxLevel)
            {
                container.Add(new CuiButton
                {
                    Button =
                    {
                        Color = "0.55 0.20 0.80 0.95",
                        Command = "skillabler.doPrestige"
                    },
                    RectTransform = { AnchorMin = "0.80 0.18", AnchorMax = "0.92 0.82" },
                    Text = { Text = "PRESTIGIO", FontSize = 13, Align = TextAnchor.MiddleCenter, Font = "RobotoCondensed-Bold.ttf", Color = "1.00 1.00 1.00 1.00" }
                }, "SkillAblerHeader");
            }

            // Fechar UI
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
        // SUBMENU DE SKILL
        // =========================================================

        private void ShowSubmenu(BasePlayer player, string skillName)
        {
            if (player == null) return;

            CuiHelper.DestroyUi(player, MainPanelName);
            CuiHelper.DestroyUi(player, SubmenuPanelName);

            Skill skill = skills.Find(s => s.Name.Equals(skillName, StringComparison.OrdinalIgnoreCase));
            if (skill == null) return;

            PlayerData data = GetPlayerData(player);
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                Image = { Color = "0.02 0.02 0.025 0.96", FadeIn = 0.1f },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-640 -360", OffsetMax = "640 360" },
                CursorEnabled = true
            }, "Overlay", SubmenuPanelName);

            // Header
            container.Add(new CuiPanel
            {
                Image = { Color = "0.10 0.11 0.12 0.95" },
                RectTransform = { AnchorMin = "0 0.89", AnchorMax = "1 1" }
            }, SubmenuPanelName, "SubmenuHeader");

            // Botão Voltar
            container.Add(new CuiButton
            {
                Button = { Color = "0.20 0.22 0.25 0.90", Command = "skillabler.openmain" },
                RectTransform = { AnchorMin = "0.02 0.18", AnchorMax = "0.12 0.82" },
                Text = { Text = "< VOLTAR", FontSize = 14, Align = TextAnchor.MiddleCenter, Font = "RobotoCondensed-Bold.ttf", Color = "1.00 1.00 1.00 1.00" }
            }, "SubmenuHeader");

            // Titulo
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.20 0.15", AnchorMax = "0.80 0.85" },
                Text = { Text = skill.Name.ToUpperInvariant(), FontSize = 30, Align = TextAnchor.MiddleCenter, Font = "RobotoCondensed-Bold.ttf", Color = "1.00 1.00 1.00 1.00" }
            }, "SubmenuHeader");

            // Botão Fechar UI
            container.Add(new CuiButton
            {
                Button = { Color = "0.65 0.08 0.08 0.90", Close = SubmenuPanelName },
                RectTransform = { AnchorMin = "0.94 0.15", AnchorMax = "0.985 0.85" },
                Text = { Text = "X", FontSize = 24, Align = TextAnchor.MiddleCenter, Font = "RobotoCondensed-Bold.ttf", Color = "1.00 1.00 1.00 1.00" }
            }, "SubmenuHeader");

            // Corpo do Submenu
            string bodyPanel = "SubmenuBody";
            container.Add(new CuiPanel
            {
                Image = { Color = "0.08 0.09 0.10 0.98" },
                RectTransform = { AnchorMin = "0.05 0.05", AnchorMax = "0.95 0.84" }
            }, SubmenuPanelName, bodyPanel);

            container.Add(new CuiElement
            {
                Parent = bodyPanel,
                Components =
                {
                    new CuiRawImageComponent { Url = ImageBaseUrl + skill.Icon },
                    new CuiRectTransformComponent { AnchorMin = "0.05 0.60", AnchorMax = "0.20 0.90" }
                }
            });

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.22 0.60", AnchorMax = "0.95 0.90" },
                Text =
                {
                    Text = $"Árvore de Habilidades de {skill.Name}\nPontos de Prestígio Disponíveis: {data.PrestigePoints}",
                    FontSize = 18,
                    Align = TextAnchor.MiddleLeft,
                    Font = "RobotoCondensed-Bold.ttf",
                    Color = "0.80 0.80 0.80 1.00"
                }
            }, bodyPanel);

            CuiHelper.AddUi(player, container);
        }
    }
}