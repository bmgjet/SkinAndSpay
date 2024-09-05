/*▄▄▄▄    ███▄ ▄███▓  ▄████  ▄▄▄██▀▀▀▓█████▄▄▄█████▓
 ▓█████▄ ▓██▒▀█▀ ██▒ ██▒ ▀█▒   ▒██   ▓█   ▀▓  ██▒ ▓▒
 ▒██▒ ▄██▓██    ▓██░▒██░▄▄▄░   ░██   ▒███  ▒ ▓██░ ▒░
 ▒██░█▀  ▒██    ▒██ ░▓█  ██▓▓██▄██▓  ▒▓█  ▄░ ▓██▓ ░ 
 ░▓█  ▀█▓▒██▒   ░██▒░▒▓███▀▒ ▓███▒   ░▒████▒ ▒██▒ ░ 
 ░▒▓███▀▒░ ▒░   ░  ░ ░▒   ▒  ▒▓▒▒░   ░░ ▒░ ░ ▒ ░░   
 ▒░▒   ░ ░  ░      ░  ░   ░  ▒ ░▒░    ░ ░  ░   ░    
  ░    ░ ░      ░   ░ ░   ░  ░ ░ ░      ░    ░      
  ░             ░         ░  ░   ░      ░  ░ 
        Chat Commands
        wallpaper skinid              =   Sets wallpaper to use custom skin id or returns to default if already set.
        spray skinid                  =   Sets spray can to use custom skin id or returns to default if already set.
        sprayresize size              =   Resizes spray decal being looked at to give size offset.
        spraysize size                =   Resizes spays being pained to this size offset.
        skinitem skinid               =   Just reskin with provided skin id.
        skinitem skinid "new name"    =   Reskin and change name of item.

        Console Command
        spray.give userid skinid
        wallpaper.give userid skinid
*/
using Oxide.Core.Plugins;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("SkinAndSpay", "bmgjet", "1.0.3")]
    [Description("Skin and name held entities")]
    public class SkinAndSpay : RustPlugin
    {
        //Permission to use command
        private const string permUse = "SkinAndSpay.use";
        private const string permSkin = "SkinAndSpay.skin";
        private const string permSize = "SkinAndSpay.size";
        private Dictionary<ulong, float> SpraySize = new Dictionary<ulong, float>();

        [PluginReference]
        Plugin EntityScaleManager;

        #region Oxide Hooks
        private void Init()
        {
            //register permission with server
            permission.RegisterPermission(permUse, this);
            permission.RegisterPermission(permSkin, this);
            permission.RegisterPermission(permSize, this);
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
        #endregion

        #region Methods
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

        private Item FindItemOnPlayer(BasePlayer player)
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

        private Item CreateItem(ulong skinID, bool wallpaper = false)
        {
            //Give Wallpaper or Spraycan
            var item = ItemManager.CreateByItemID(wallpaper ? -1501434104 : -596876839, 1, skinID);
            return item;
        }

        private void AdjustItem(BasePlayer player, Item item, ulong skin, string newname)
        {
            //Get entity reference
            Puts(item.info.itemid.ToString());
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

        #endregion

        #region Chat Commands

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

        [ChatCommand("wallpaper")]
        void wallpaper(BasePlayer player, string command, string[] args)
        {
            if (player != null)
            {
                //Check permission and reject users without it.
                if (!player.IPlayer.HasPermission(permUse))
                {
                    player.ChatMessage("Permission required");
                    return;
                }
                //Checks if player has wallpaper
                if (player.GetHeldEntity() is WallpaperPlanner)
                {
                    //Removes Modded Wallpaper
                    if (player.GetHeldEntity().skinID != 0)
                    {
                        AdjustItem(player, player.GetHeldEntity().GetItem(), 0, "WALLPAPER");
                        player.ChatMessage("Remove Custom Wallpaper Skin");
                    }
                    else
                    {
                        //Sets Modded Wallpaper
                        if (args != null && args.Length != 0)
                        {
                            ulong skin = 0;
                            if (!ulong.TryParse(args[0], out skin))
                            {
                                player.ChatMessage("Error processing skinid example /wallpaper 3315768442");
                                return;
                            }
                            AdjustItem(player, player.GetHeldEntity().GetItem(), skin, "CUSTOM WALLPAPER");
                            player.ChatMessage("Set Custom Wallpaper Skin");
                        }
                    }
                }
                else
                {
                    //Warn about not having spray Can
                    player.ChatMessage("Must Be Holding Wallpaper!");
                    return;
                }
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

        [ChatCommand("skinitem")]
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
                player.ChatMessage(@"/skinitem skinid or /SkinAndSpay skinid ""item name""");
                return;
            }
            else if (args.Length == 1)
            {
                //try convert first arg to a skinid ulong
                if (!ulong.TryParse(args[0], out skin))
                {
                    player.ChatMessage("Error processing skinid example /skinitem 633445454");
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
        #endregion

        #region Console Commands

        [ConsoleCommand("spray.give")]
        private void GiveCmd(ConsoleSystem.Arg arg)
        {
            if (arg.IsAdmin && arg.Args?.Length > 0)
            {
                var player = BasePlayer.Find(arg.Args[0]) ?? BasePlayer.FindSleeping(arg.Args[0]);
                if (player == null)
                {
                    PrintWarning($"Can't find player with that name/ID! {arg.Args[0]}");
                    return;
                }
                ulong SkinID = 0;
                if (ulong.TryParse(arg.Args[1], out SkinID))
                {
                    player.GiveItem(CreateItem(SkinID), BaseEntity.GiveItemReason.Crafted);
                }
            }
        }

        [ConsoleCommand("wallpaper.give")]
        private void GiveWPCmd(ConsoleSystem.Arg arg)
        {
            if (arg.IsAdmin && arg.Args?.Length > 0)
            {
                var player = BasePlayer.Find(arg.Args[0]) ?? BasePlayer.FindSleeping(arg.Args[0]);
                if (player == null)
                {
                    PrintWarning($"Can't find player with that name/ID! {arg.Args[0]}");
                    return;
                }
                ulong SkinID = 0;
                if (ulong.TryParse(arg.Args[1], out SkinID))
                {
                    player.GiveItem(CreateItem(SkinID, true), BaseEntity.GiveItemReason.Crafted);
                }
            }
        }
        #endregion
    }
}
