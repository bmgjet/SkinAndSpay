using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("SkinAndSpay", "bmgjet", "1.0.1")]
    [Description("Skin and name held entitys")]
    public class SkinAndSpay : RustPlugin
    {
        //Chat commands
        //Spray skinid                  =   Sets spray can to use custom skin id or returns to default if already set.
        //Sprayresize size              =   Resizes spray decal being looked at to give size offset.
        //Spraysize size                =   Resizes spays being pained to this size offset.
        //SkinAndSpay skinid            =   Just reskin with provided skin id.
        //SkinAndSpay skinid "new name" =   Reskin and change name of item.

        //Permission to use command
        public const string permUse = "SkinAndSpay.use";
        public const string permSkin = "SkinAndSpay.skin";
        public const string permSize = "SkinAndSpay.size";
        public bool HookFallBack = false; //Auto enables based on version unless you want to switch to a worse mode.
        public List<ulong> Delay = new List<ulong>();
        public Dictionary<ulong, float> SpraySize = new Dictionary<ulong, float>();

        [PluginReference]
        Plugin EntityScaleManager;

        public FieldInfo _AssemblyVersion = typeof(RustExtension).GetField("AssemblyVersion",BindingFlags.NonPublic | BindingFlags.Static);

        private void Init()
        {
            //register permission with server
            permission.RegisterPermission(permUse, this);
            permission.RegisterPermission(permSkin, this);
            permission.RegisterPermission(permSize, this);
            //Fall Back Mode for oxide version missing the hook. Or if byh choice you want to use alternative method.
            string Version = _AssemblyVersion.GetValue(null).ToString();
            if (Version == "2.0.5532")
            {
                Puts("Setting Hook Fall Back Mode");
                HookFallBack = true;
            }
            else { Puts("Detected Version " + Version); }
            if (!HookFallBack) { Unsubscribe(nameof(OnPlayerInput)); }
            else { Unsubscribe(nameof(OnSprayCreate)); }
        }

        void OnPlayerInput(BasePlayer player, InputState input)
        {
            //Falls back for oxide version with out hook.
            if (HookFallBack)
            {
                //Checks Player has permission to reduce server load on heavy hook.
                if (player.IPlayer.HasPermission(permUse))
                {
                    //Checks they have spray can and adds a delay cool down from function firing
                    if (player.GetHeldEntity() is SprayCan && input.WasJustReleased(BUTTON.FIRE_PRIMARY) && !Delay.Contains(player.userID))
                    {
                        Delay.Add(player.userID);
                        timer.Once(3, () => { Delay.Remove(player.userID); });
                        timer.Once(1f, () =>
                        {
                            //Scans where player is looking to find decal
                            RaycastHit hit;
                            if (!Physics.Raycast(player.eyes.HeadRay(), out hit)) { return; }
                            var entity = hit.GetEntity();
                            if (entity != null && entity.prefabID == 3884356627)
                            {
                                //Found decal so apply skin from held spray can
                                entity.skinID = player.GetHeldEntity().skinID;
                                entity.SendNetworkUpdateImmediate();
                                if (SpraySize.Count != 0) { ShouldRescale(player, entity); }
                                return;
                            }
                        });
                    }
                }
            }
        }

        private object OnSprayCreate(SprayCan sc, Vector3 vector, Quaternion quaternion)
        {
            //Checks if modded spray can
            if (sc.skinID != 0)
            {
                //Uses modded skin ID
                BaseEntity baseEntity = GameManager.server.CreateEntity(sc.SprayDecalEntityRef.resourcePath, vector, quaternion, true);
                baseEntity.skinID = sc.skinID;
                baseEntity.OnDeployed(null, sc.GetOwnerPlayer(), sc.GetItem());
                baseEntity.Spawn();
                sc.GetItem().LoseCondition(sc.ConditionLossPerSpray);
                if (SpraySize.Count != 0) { ShouldRescale(sc.GetOwnerPlayer(), baseEntity); }
                //Blocks normal spray
                return false;
            }
            return null;
        }

        private void ShouldRescale(BasePlayer player, BaseEntity entity)
        {
            //Checks if resize should be applied
            if (player == null || entity == null) { return; }
            if (SpraySize.ContainsKey(player.userID))
            {
                //Applys Resize;
                NextFrame(() =>
                {
                    if (EntityScaleManager != null)
                    {
                        EntityScaleManager.Call("API_ScaleEntity", entity, SpraySize[player.userID]);
                        player.ChatMessage("Applied Resize!");
                        return;
                    }
                });
            }
        }

        Item FindItemOnPlayer(BasePlayer player)
        {
            //Check player has active item
            var item = player.GetActiveItem();
            if (item == null)
            {
                //If no active item check if belt is empty
                if (player.inventory.containerBelt.IsEmpty())
                {
                    player.ChatMessage("No items found in belt");
                    return null;
                }
                //if belt isnt empty grab the first slot
                item = player.inventory.containerBelt.GetSlot(0);
                //Check if there was a item in that first slot
                if (item == null)
                {
                    player.ChatMessage("Please hold or have item in slot 1 that can be skinned");
                    return null;
                }
            }
            //Return the item thats been found
            return item;
        }

        void AdjustItem(BasePlayer player, Item item, ulong skin, string newname)
        {
            //Get entity reference
            var entity = item.GetHeldEntity();
            //Remove item from player
            player.inventory.containerBelt.Remove(item);
            //wait 1 frame before doing the changes
            NextFrame(() =>
            {
                //Change items skin
                item.skin = skin;
                //change item name if name is passed
                if (newname != "") { item.name = newname; }
                //set entity skin if item has a entity
                if (entity != null)
                {
                    entity.skinID = skin;
                    entity.SendNetworkUpdateImmediate();
                }
                //Update server
                item.MarkDirty();
                //Give item back after 5 secs to fix placement indercator issue.
                timer.Once(3f, () => { player.inventory.GiveItem(item, player.inventory.containerBelt); });
            });
        }

        [ChatCommand("sprayresize")]
        void sprayresize(BasePlayer player, string command, string[] args)
        {
            if (player == null) { return; }
            //Checks required plugin.
            if (args != null && args.Length == 1)
            {
                //Check permission and reject users without it.
                if (!player.IPlayer.HasPermission(permSize))
                {
                    player.ChatMessage("Permission required");
                    return;
                }
                if (player.IsBuildingBlocked())
                {
                    player.ChatMessage("Building Blocked!");
                    return;
                }
                //Checks if player has spray can
                if (player.GetHeldEntity() is SprayCan)
                {
                    //Scans where player is looking to find decal
                    RaycastHit hit;
                    if (!Physics.Raycast(player.eyes.HeadRay(), out hit)) { return; }
                    var entity = hit.GetEntity();
                    if (entity != null && entity.prefabID == 3884356627)
                    {
                        float sprays = 1;
                        if (!float.TryParse(args[0], out sprays))
                        {
                            player.ChatMessage("Resize Failed!");
                            return;
                        }
                        sprays = UnityEngine.Mathf.Clamp(sprays, 0.3f, 30);
                        if (sprays > 6f) { player.ChatMessage("Over sized sprays wont be visable when up close!"); }
                        //Send scale command to EntityScaleManager
                        if (EntityScaleManager != null)
                        {
                            EntityScaleManager.Call("API_ScaleEntity", entity, sprays);
                            player.ChatMessage("Applied Resize!");
                            return;
                        }
                    }
                }
                else
                {
                    player.ChatMessage("You must be holding a spray can!");
                    return;
                }
            }
            player.ChatMessage("Invalid Args");
        }

        [ChatCommand("spraysize")]
        void spraysize(BasePlayer player, string command, string[] args)
        {
            if (player != null)
            {
                //Check permission and reject users without it.
                if (!player.IPlayer.HasPermission(permSize))
                {
                    player.ChatMessage("Permission required");
                    return;
                }
                //Get new scale ammount
                float sprays = 1f;
                if (!float.TryParse(args[0], out sprays))
                {
                    player.ChatMessage("Provide floating point as arg for new spray size 1.0 = default");
                    return;
                }
                //Size Limits
                sprays = UnityEngine.Mathf.Clamp(sprays, 0.3f, 30);
                if (sprays > 6f) { player.ChatMessage("Over sized sprays wont be visable when up close!"); }
                //Adds or edits players setting
                if (SpraySize.ContainsKey(player.userID)) { SpraySize[player.userID] = sprays; }
                else { SpraySize.Add(player.userID, sprays); }
                player.ChatMessage("Set new spray size to " + sprays.ToString());
            }
        }

        [ChatCommand("spray")]
        void spray(BasePlayer player, string command, string[] args)
        {
            if (player != null)
            {
                //Check permission and reject users without it.
                if (!player.IPlayer.HasPermission(permUse))
                {
                    player.ChatMessage("Permission required");
                    return;
                }
                //Checks if player has spray can
                if (player.GetHeldEntity() is SprayCan)
                {
                    //Removes Modded Spray Can
                    if (player.GetHeldEntity().skinID != 0)
                    {
                        AdjustItem(player, player.GetHeldEntity().GetItem(), 0, "SPRAY CAN");
                        player.ChatMessage("Remove Custom Spray Skin");
                    }
                    else
                    {
                        //Sets Modded Spray Can
                        if (args != null && args.Length != 0)
                        {
                            ulong skin = 0;
                            if (!ulong.TryParse(args[0], out skin))
                            {
                                player.ChatMessage("Error processing skinid example /spray 2816580876");
                                return;
                            }
                            AdjustItem(player, player.GetHeldEntity().GetItem(), skin, "CUSTOM SPRAY CAN");
                            player.ChatMessage("Set Custom Spray Skin");
                        }
                    }
                }
                else
                {
                    //Warn about not having spray Can
                    player.ChatMessage("Must Be Holding A Spray Can!");
                    return;
                }
            }
        }

        [ChatCommand("SkinAndSpay")]
        void ItemRenameCommand(BasePlayer player, string command, string[] args)
        {
            //Load default skin 0
            ulong skin = 0;
            //Check permission and reject users without it.
            if (!player.IPlayer.HasPermission(permSkin))
            {
                player.ChatMessage("Permission required");
                return;
            }
            //Check enough args are provided
            if (args.Length == 0 || args.Length >= 3)
            {
                player.ChatMessage(@"/SkinAndSpay skinid or /SkinAndSpay skinid ""item name""");
                return;
            }
            else if (args.Length == 1)
            {
                //try convert first arg to a skinid ulong
                if (!ulong.TryParse(args[0], out skin))
                {
                    player.ChatMessage("Error processing skinid example /SkinAndSpay 633445454");
                    return;
                }
                //Get item from player thats being skinned
                var item = FindItemOnPlayer(player);
                //check if a item was found
                if (item == null) { return; }
                //Adjust the item
                AdjustItem(player, item, skin, "");
                player.ChatMessage("Skin changed, may take a few seconds to update");
            }
            else if (args.Length == 2)
            {
                //Reskin and Rename
                var item = FindItemOnPlayer(player);
                if (item == null) { return; }
                //try convert first arg to a skinid ulong
                if (!ulong.TryParse(args[0], out skin))
                {
                    player.ChatMessage("Error converting skinid string to ulong please only use numbers");
                    return;
                }
                //Check names not too long
                if (args[1].Length > 64)
                {
                    player.ChatMessage("Item Name Too Long must be less then 64 charaters");
                    return;
                }
                //adjust item
                AdjustItem(player, item, skin, args[1]);
                player.ChatMessage("Skin and name changed, may take a few seconds to update");
            }
        }
    }
}