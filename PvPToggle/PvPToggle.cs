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
        public static List<Player> PvPplayer = new List<Player>();
        public static string PvPType { get; set; }
        public static TSPlayer playerv3 { get; set; }
        public static TSPlayer player { get; set; }
        public static string playerv4 { get; set; }
        public static Player playerv2 { get; set; }
        public static int PvPFOn { get; set; }
        public static int PvPFOff { get; set; }
        public static int PvPBloodmoon { get; set; }
        public static bool EnableBloodMoon = false;

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
            GameHooks.Update += OnUpdate;
            NetHooks.GreetPlayer += OnGreetPlayer;
            GameHooks.Initialize += OnInitialize;
            ServerHooks.Leave += OnLeave;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                GameHooks.Update -= OnUpdate;
                NetHooks.GreetPlayer -= OnGreetPlayer;
                GameHooks.Initialize -= OnInitialize;
                ServerHooks.Leave -= OnLeave;
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
            Commands.ChatCommands.Add(new Command("pvpforce", ForceToggle, "forcepvp", "fpvp"));
            Commands.ChatCommands.Add(new Command("pvpbmoon", BloodToggle, "bloodmoonpvp", "bmpvp"));
        }

        public void OnGreetPlayer(int who, HandledEventArgs e)
        {
            lock (PvPplayer)
                PvPplayer.Add(new Player(who));
        }

        #region OnUpdate
        public void OnUpdate()
        {
            lock (PvPToggle.PvPplayer)
            {
                int On = 0;
                int Off = 0;
                int bloodmoon = 0;

                foreach (Player player in PvPToggle.PvPplayer)
                {
                    if (player.PvPType == "")
                    {
                        Off++;
                        PvPFOff = Off;
                    }
                    else if (player.PvPType == "forceon")
                    {
                        On++;
                        PvPFOn = On;
                        if (Main.player[player.Index].hostile == false)
                        {
                            Main.player[player.Index].hostile = true;
                            NetMessage.SendData((int)PacketTypes.TogglePvp, -1, -1, "", player.Index, 0f, 0f,
                                            0f);
                            player.TSPlayer.SendWarningMessage("Your PvP has been forced on, don't try and turn it off!");
                        }
                    }
                    else if (player.PvPType == "bloodmoon")
                    {
                        if (Main.bloodMoon && !Main.dayTime)
                        {

                            bloodmoon++;
                            PvPBloodmoon = bloodmoon;
                            if (Main.player[player.Index].hostile == false)
                            {
                                Main.player[player.Index].hostile = true;
                                NetMessage.SendData((int)PacketTypes.TogglePvp, -1, -1, "", player.Index, 0f, 0f,
                                                0f);
                                player.TSPlayer.SendWarningMessage("The blood moon's evil influence stops your PvP from turning off.");
                            }
                        }
                        else
                        {
                            player.PvPType = "";
                            player.TSPlayer.SendInfoMessage("The blood moon fades, and you have control over your PvP again!");
                        }
                    }
                }
            }

            if (Main.bloodMoon && EnableBloodMoon)
            {
                foreach (Player ply in PvPToggle.PvPplayer)
                {
                    ply.PvPType = "bloodmoon";
                    if (Main.player[ply.Index].hostile == false)
                    {
                        Main.player[ply.Index].hostile = true;
                        NetMessage.SendData((int)PacketTypes.TogglePvp, -1, -1, "", ply.Index, 0f, 0f, 0f);
                    }
                    ply.TSPlayer.SendInfoMessage("Your PvP has been forced on for the blood moon!");
                }
            }
        }
        #endregion

        public void OnLeave(int ply)
        {
            {
                lock (PvPplayer)
                {
                    PvPplayer.RemoveAll(plr => plr.Index == ply);
                }
            }
        }


        #region TogglePvP
        public void TogglePvP(CommandArgs args)
        {
            var playerv2 = Tools.GetPlayerByIndex(args.Player.Index);

            if (args.Parameters.Count > 1)
            {
                args.Player.SendErrorMessage("You used too many parameters! Try /pvp \"player's name\"!");
            }
            else if (args.Parameters.Count < 1)
            {
                args.Player.SendErrorMessage("You don't have enough parameters! Try /pvp \"player's name\"!");
                return;
            }

            string plStr = String.Join(" ", args.Parameters);

            var ply = TShock.Utils.FindPlayer(plStr);
            if (ply.Count < 1)
            {
                args.Player.SendErrorMessage("No players matched that name!");
            }
            else if (ply.Count > 1)
            {
                args.Player.SendErrorMessage("More than one player has that name!");
            }

            else
            {
                var player = ply[0];

                if (args.Parameters.Count == 1 && ply.Count == 1)
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

        #endregion

        #region BloodToggle
        public void BloodToggle(CommandArgs args)
        {
            if (args.Parameters.Count != 0)
            {
                args.Player.SendErrorMessage("Usage: /bloodmoonpvp");
                return;
            }
            if (EnableBloodMoon == false)
            {
                EnableBloodMoon = true;
                args.Player.SendInfoMessage("Forced PvP during bloodmoons is now activated!");
            }
            else if (EnableBloodMoon)
            {
                EnableBloodMoon = false;
                args.Player.SendInfoMessage("Forced PvP during bloodmoons is now deactivated!");
            }
        }
        #endregion

        #region ForceToggle
        public void ForceToggle(CommandArgs args)
        {
            if (args.Parameters.Count > 1)
            {
                args.Player.SendErrorMessage("Incorrect syntax. Use /fpvp \"player's name\" or *");
                return;
            }
            else if (args.Parameters.Count < 1)
            {
                args.Player.SendErrorMessage("Incorrect syntax. Use /fpvp \"player's name\" or *");
                return;
            }

            string plStr = String.Join(" ", args.Parameters);

            var players = TShock.Utils.FindPlayer(plStr);
            if (players.Count == 0 && ((plStr != "*") && (plStr != "all") && (plStr != "*off") && (plStr != "alloff")))
            {
                args.Player.SendErrorMessage("No players matched that name");
                return;
            }
            else if (players.Count > 1
                && ((plStr != "*") && (plStr != "all") && (plStr != "*off") && (plStr != "alloff")))
            {
                args.Player.SendErrorMessage("More than one player matched that name");
            }

            if (plStr == "*" || plStr == "all")
            {
                foreach (Player pl in PvPToggle.PvPplayer)
                {
                    pl.PvPType = "forceon";
                }
                TSPlayer.All.SendInfoMessage(string.Format("{0} has forced on everyone's PvP", args.Player.Name));
                return;
            }
            else if (plStr == "*off" || plStr == "alloff")
            {
                foreach (Player pl in PvPToggle.PvPplayer)
                {
                    pl.PvPType = "";
                }
                TSPlayer.All.SendInfoMessage(string.Format("{0} has stopped forcing everyone's PvP on. It can now be turned off", args.Player.Name));
            }

            else
            {
                if (args.Parameters.Count == 1 && players.Count == 1)
                {
                    var plr = players[0];

                    playerv2 = Tools.GetPlayerByIndex(players[0].Index);
                    playerv3 = players[0];
                    playerv4 = players[0].Name;

                    if (playerv2.PvPType == "")
                    {
                        playerv2.PvPType = "forceon";
                        Main.player[plr.Index].hostile = true;
                        NetMessage.SendData((int)PacketTypes.TogglePvp, -1, -1, "", plr.Index, 0f, 0f, 0f);
                        plr.SendInfoMessage(string.Format("{0} has forced your PvP on!", args.Player.Name));
                        args.Player.SendInfoMessage(string.Format("You have forced {0}'s PvP on!", playerv4));
                    }


                    else if (playerv2.PvPType == "forceon")
                    {
                        playerv2.PvPType = "";
                        Main.player[plr.Index].hostile = false;
                        NetMessage.SendData((int)PacketTypes.TogglePvp, -1, -1, "", plr.Index, 0f, 0f, 0f);
                        plr.SendInfoMessage(string.Format("{0} has turned your PvP off!", args.Player.Name));
                        args.Player.SendInfoMessage(string.Format("You have turned {0}'s PvP off!", playerv4));

                    }
                }
            }
        }
    }
#endregion


    public class Tools
    {
        public static Player GetPlayerByIndex(int index)
        {
            foreach (Player player in PvPToggle.PvPplayer)
            {
                if (player.Index == index)
                    return player;
            }
            return new Player(-1);
        }
    }
}