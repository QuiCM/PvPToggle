using System;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using Hooks;
using TShockAPI;
using TShockAPI.DB;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Threading;
using System.Text;
using System.Linq;

namespace PvPToggle
{
    [APIVersion(1, 12)]
    public class PvPToggle : TerrariaPlugin
    {

        public override Version Version
        {
            get { return Assembly.GetExecutingAssembly().GetName().Version; }
        }
        public override string Author
        {
            get { return "White"; }
        }
        public override string Name
        {
            get { return "PvPToggle"; }
        }

        public override string Description
        {
            get { return "Allows you to set players PvP"; }
        }

        public override void Initialize()
        {
            GameHooks.Initialize += OnInitialize;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                GameHooks.Initialize -= OnInitialize;
            }
            base.Dispose(disposing);
        }

        public PvPToggle(Main game)
            : base(game)
        {
            Order = 1;
        }

        public void OnInitialize()
        {
            Commands.ChatCommands.Add(new Command("pvpswitch", TogglePvP, "pvp", "togglepvp"));
        }

        public void TogglePvP(CommandArgs args)
        {
            var ply = TShock.Utils.FindPlayer(args.Parameters[0]);
            var player = ply[0];


            if (args.Parameters.Count > 1)
            {
                args.Player.SendErrorMessage("You used too many parameters! Try /pvp \"player's name\"!");
            }
            if (args.Parameters.Count == 0)
            {
                args.Player.SendErrorMessage("You need to include a player's name!");
            }
            if (args.Parameters.Count < 1)
            {
                args.Player.SendErrorMessage("You didn't use enough parameters! Try /pvp \"player's name\"!");
            }

            else if (args.Parameters.Count == 1)
            {
                if (ply.Count == 0)
                {
                    args.Player.SendErrorMessage("No players matched that name!");
                }
                else if (ply.Count > 1)
                {
                    args.Player.SendErrorMessage("More than one player has that name!");
                }

                else if (args.Parameters.Count == 1 && ply.Count == 1)
                {
                    if (!Main.player[player.Index].hostile)
                    {
                        Main.player[player.Index].hostile = true;
                        NetMessage.SendData((int)PacketTypes.TogglePvp, -1, -1, "", player.Index, 0f, 0f,
                                            0f);
                        args.Player.SendInfoMessage(string.Format("You have turned {0}'s PvP on!", player.Name));
                        player.SendInfoMessage(string.Format("{0} has turned your PvP on!", args.Player.Name));
                    }
                    else if (Main.player[player.Index].hostile)
                    {
                        Main.player[player.Index].hostile = false;
                        NetMessage.SendData((int)PacketTypes.TogglePvp, -1, -1, "", player.Index, 0f, 0f, 0f);

                        args.Player.SendInfoMessage(string.Format("You have turned {0}'s PvP off!", player.Name));
                        player.SendInfoMessage(string.Format("{0} has turned your PvP off!", args.Player.Name));
                    }
                }
            }
        }
    }
}
