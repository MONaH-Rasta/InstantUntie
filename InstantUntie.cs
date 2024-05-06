using System;
using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Instant Untie", "MJSU", "1.0.10")]
    [Description("Instantly untie underwater boxes")]
    internal class InstantUntie : RustPlugin
    {
        #region Class Fields
        private PluginConfig _pluginConfig; //Plugin Config

        private const string UsePermission = "instantuntie.use";

        private static InstantUntie _ins;
        
        private const string AccentColor = "#de8732";
        
        #endregion

        #region Setup & Loading
        private void Init()
        {
            _ins = this;
            permission.RegisterPermission(UsePermission, this);
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [LangKeys.Chat] = $"<color=#bebebe>[<color={AccentColor}>{Title}</color>] {{0}}</color>",
                [LangKeys.Untie] = "The box will untie in {0} seconds. Please hold the use key down until this is completed.",
                [LangKeys.Canceled] = "You have canceled untying the box. Please hold the use key down to untie."
            }, this);
        }
        
        protected override void LoadDefaultConfig()
        {
            PrintWarning("Loading Default Config");
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            Config.Settings.DefaultValueHandling = DefaultValueHandling.Populate;
            _pluginConfig = AdditionalConfig(Config.ReadObject<PluginConfig>());
            Config.WriteObject(_pluginConfig);
        }

        private PluginConfig AdditionalConfig(PluginConfig config)
        {
            return config;
        }

        private void OnServerInitialized()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                AddBehavior(player);
            }
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            AddBehavior(player);
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            DestroyBehavior(player);
        }

        private void Unload()
        {
            foreach (UnderwaterBehavior water in GameObject.FindObjectsOfType<UnderwaterBehavior>())
            {
                water.DoDestroy();
            }

            _ins = null;
        }
        #endregion

        #region uMod Hooks
        private void OnUserPermissionGranted(string playerId, string permName)
        {
            if (permName != UsePermission)
            {
                return;
            }
            
            HandleUserChanges(playerId);
        }
        
        private void OnUserPermissionRevoked(string playerId, string permName)
        {
            if (permName != UsePermission)
            {
                return;
            }
            
            HandleUserChanges(playerId);
        }
        
        private void OnUserGroupAdded(string playerId, string groupName)
        {
            HandleUserChanges(playerId);
        }
        
        private void OnUserGroupRemoved(string playerId, string groupName)
        {
            HandleUserChanges(playerId);
        }

        private void OnGroupPermissionGranted(string groupName, string permName)
        {
            if (permName != UsePermission)
            {
                return;
            }

            NextTick(() =>
            {
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                {
                    HandleUserChanges(player);
                }
            });
        }
        
        private void OnGroupPermissionRevoked(string groupName, string permName)
        {
            if (permName != UsePermission)
            {
                return;
            }
            
            NextTick(() =>
            {
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                {
                    HandleUserChanges(player);
                }
            });
        }

        private void HandleUserChanges(string id)
        {
            NextTick(() =>
            {
                BasePlayer player = BasePlayer.Find(id);
                if (player == null)
                {
                    return;
                }

                HandleUserChanges(player);
            });
        }

        private void HandleUserChanges(BasePlayer player)
        {
            bool hasPerm = HasPermission(player, UsePermission);
            bool hasBehavior = player.GetComponent<UnderwaterBehavior>() != null;
            if (hasPerm == hasBehavior)
            {
                return;
            }

            if (hasBehavior)
            {
                DestroyBehavior(player);
            }
            else
            {
                AddBehavior(player);
            }
        }
        #endregion

        #region Helper Methods
        private T Raycast<T>(Ray ray, float distance) where T : BaseEntity
        {
            RaycastHit hit;
            if (!Physics.Raycast(ray, out hit, distance))
            {
                return null;
            }

            return hit.GetEntity() as T;
        }

        private void AddBehavior(BasePlayer player)
        {
            if (!HasPermission(player, UsePermission))
            {
                return;
            }

            if (player.GetComponent<UnderwaterBehavior>() == null)
            {
                player.gameObject.AddComponent<UnderwaterBehavior>();
            }
        }

        private void DestroyBehavior(BasePlayer player)
        {
            player.GetComponent<UnderwaterBehavior>()?.DoDestroy();
        }

        private void Chat(BasePlayer player, string format) => PrintToChat(player, Lang(LangKeys.Chat, player, format));
        
        private bool HasPermission(BasePlayer player, string perm) => permission.UserHasPermission(player.UserIDString, perm);

        private string Lang(string key, BasePlayer player = null, params object[] args)
        {
            try
            {
                return string.Format(lang.GetMessage(key, this, player?.UserIDString), args);
            }
            catch(Exception ex)
            {
                PrintError($"Lang Key '{key}' threw exception\n:{ex}");
                throw;
            }
        }
        #endregion

        #region Behavior
        private class UnderwaterBehavior : FacepunchBehaviour
        {
            private BasePlayer Player { get; set; }
            private FreeableLootContainer Box { get; set; }
            private float NextRaycastTime { get; set; }

            private void Awake()
            {
                enabled = false;
                Player = GetComponent<BasePlayer>();
                InvokeRandomized(UpdateUnderwater, 0f, _ins._pluginConfig.UnderWaterUpdateRate, 0.1f);
            }

            private void UpdateUnderwater()
            {
                bool isUnderwater = Player.WaterFactor() == 1f;
                if (isUnderwater && !enabled)
                {
                    enabled = true;
                }
                else if (!isUnderwater && enabled)
                {
                    enabled = false;
                }
            }

            private void FixedUpdate()
            {
                if (Box == null)
                {
                    if (NextRaycastTime < Time.realtimeSinceStartup && Player.serverInput.IsDown(BUTTON.USE))
                    {
                        Box = _ins.Raycast<FreeableLootContainer>(Player.eyes.HeadRay(), 3f);
                        if (Box == null || !Box.IsTiedDown())
                        {
                            return;
                        }

                        NextRaycastTime = Time.realtimeSinceStartup + _ins._pluginConfig.HeldKeyUpdateRate;
                        CancelInvoke(Untie);
                        Invoke(Untie, _ins._pluginConfig.UntieDuration);
                        if (_ins._pluginConfig.ShowUntieMessage)
                        {
                            _ins.Chat(Player, _ins.Lang(LangKeys.Untie, Player, _ins._pluginConfig.UntieDuration));
                        }
                    }
                }
                else
                {
                    if (!Player.serverInput.IsDown(BUTTON.USE))
                    {
                        Box = null;
                        CancelInvoke(Untie);

                        if (_ins._pluginConfig.ShowCanceledMessage)
                        {
                            _ins.Chat(Player, _ins.Lang(LangKeys.Canceled));
                        }
                    }
                }
            }

            private void Untie()
            {
                if (Box == null)
                {
                    return;
                }
                
                if (!Box.IsTiedDown())
                {
                    return;
                }
                
                Box.buoyancy.buoyancyScale = _ins._pluginConfig.BuoyancyScale;
                Box.GetRB().isKinematic = false;
                Box.buoyancy.enabled = true;
                Box.SetFlag(BaseEntity.Flags.Reserved8, false);
                Box.SendNetworkUpdate();
                if (Box.freedEffect.isValid)
                {
                    Effect.server.Run(Box.freedEffect.resourcePath, Box.transform.position, Vector3.up);
                }

                Box = null;
            }

            public void DoDestroy()
            {
                Destroy(this);
            }
        }
        #endregion

        #region Classes

        private class LangKeys
        {
            public const string Chat = "Chat";
            public const string Untie = "Untie";
            public const string Canceled = "UntieCanceled";
        }

        private class PluginConfig
        {
            [DefaultValue(0f)]
            [JsonProperty(PropertyName = "Untie Duration (Seconds)")]
            public float UntieDuration { get; set; }

            [DefaultValue(5f)]
            [JsonProperty(PropertyName = "How often to check if player is underwater (Seconds)")]
            public float UnderWaterUpdateRate { get; set; }
            
            [DefaultValue(1f)]
            [JsonProperty(PropertyName = "How often to check if a player is holding the use button (Seconds)")]
            public float HeldKeyUpdateRate { get; set; }
            
            [DefaultValue(true)]
            [JsonProperty(PropertyName = "Show Untie Message")]
            public bool ShowUntieMessage { get; set; }
            
            [DefaultValue(true)]
            [JsonProperty(PropertyName = "Show canceled message")]
            public bool ShowCanceledMessage { get; set; }
            
            [DefaultValue(1)]
            [JsonProperty(PropertyName = "Buoyancy Scale")]
            public float BuoyancyScale { get; set; }
        }
        #endregion
    }
}
