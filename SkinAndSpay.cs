using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("SkinAndSpay", "bmgjet", "1.0.0")]
    [Description("Skin and name held entitys")]
    public class SkinAndSpay : RustPlugin
    {
        //Chat commands
        //Spray skinid                  =   Sets spray can to use custom skin id or returns to default if already set.
        //SkinAndSpay skinid            =   Just reskin with provided skin id
        //SkinAndSpay skinid "new name" =   Reskin and change name of item.
        //Permission to use command
        public const string permUse = "SkinAndSpay.use";
        public const string permSkin = "SkinAndSpay.skin";
        public bool HookFallBack = false;
        List<ulong> Delay = new List<ulong>();
        private void Init()
        {
            //register permission with server
            permission.RegisterPermission(permUse, this);
            permission.RegisterPermission(permSkin, this);
            string Version = typeof(Oxide.Game.Rust.RustExtension).Assembly.GetName().Version.ToString();
            if (Version == "2.0.5532.0")
            {
                Puts("Setting Hook Fall Back Mode");
                HookFallBack = true;
            }
            else
            {
                Puts("Detected Version " + Version);
            }
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
                BaseEntity baseEntity2 = GameManager.server.CreateEntity(sc.SprayDecalEntityRef.resourcePath, vector, quaternion, true);
                baseEntity2.skinID = sc.skinID;
                baseEntity2.OnDeployed(null, sc.GetOwnerPlayer(), sc.GetItem());
                baseEntity2.Spawn();
                sc.GetItem().LoseCondition(sc.ConditionLossPerSpray);
                //Blocks normal spray
                return false;
            }
            return null;
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
                if (newname != "")
                {
                    item.name = newname;
                }
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
                            try
                            {
                                skin = ulong.Parse(args[0]);
                            }
                            catch
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
                try
                {
                    skin = ulong.Parse(args[0]);
                }
                catch
                {
                    player.ChatMessage("Error processing skinid example /SkinAndSpay 633445454");
                    return;
                }
                //Get item from player thats being skinned
                var item = FindItemOnPlayer(player);
                //check if a item was found
                if (item == null)
                {
                    return;
                }
                //Adjust the item
                AdjustItem(player, item, skin, "");
                player.ChatMessage("Skin changed, may take a few seconds to update");
            }
            else if (args.Length == 2)
            {
                //Reskin and Rename
                var item = FindItemOnPlayer(player);
                if (item == null)
                {
                    return;
                }
                //try convert first arg to a skinid ulong
                try
                {
                    skin = ulong.Parse(args[0]);
                }
                catch
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