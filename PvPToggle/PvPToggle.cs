using System;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using TerrariaApi;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Threading;
using System.Text;
using System.Linq;
using Newtonsoft.Json;

namespace PvPToggle
{
    [ApiVersion(1, 14)]
    public class PvPToggle : TerrariaPlugin
    {
        public static List<Player> PvPplayer = new List<Player>();
        public static List<string> teamColors = new List<string>() { "white", "red", "green", "blue", "yellow" };
        public static string savepath { get { return Path.Combine(TShock.SavePath, "PvpTog.json"); } }
        public static PvPConfig Config { get; set; }

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
            var Hook = ServerApi.Hooks;

            Hook.GameUpdate.Register(this, (args) => { OnUpdate(); });
            Hook.NetGreetPlayer.Register(this, OnGreetPlayer);
            Hook.GameInitialize.Register(this, (args) => { OnInitialize(); });
            Hook.ServerLeave.Register(this, OnLeave);

            Config = new PvPConfig();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                var Hook = ServerApi.Hooks;

                Hook.GameUpdate.Deregister(this, (args) => { OnUpdate(); });
                Hook.NetGreetPlayer.Deregister(this, OnGreetPlayer);
                Hook.GameInitialize.Deregister(this, (args) => { OnInitialize(); });
                Hook.ServerLeave.Deregister(this, OnLeave);
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
            Commands.ChatCommands.Add(new Command(PvPSwitch, "pvp"));
            Commands.ChatCommands.Add(new Command("pvpswitch", TogglePvP, "tpvp"));
            Commands.ChatCommands.Add(new Command(TeamSwitch, "team"));
            Commands.ChatCommands.Add(new Command("teamtoggle", ToggleTeam, "tteam"));
            Commands.ChatCommands.Add(new Command("pvpforce", ForceToggle, "forcepvp", "fpvp"));
            Commands.ChatCommands.Add(new Command("pvpbmoon", BloodToggle, "bloodmoonpvp", "bmpvp"));

            SetUpConfig();
        }

        public void OnGreetPlayer(GreetPlayerEventArgs args)
        {
            lock (PvPplayer)
                PvPplayer.Add(new Player(args.Who));
        }

        #region OnUpdate
        public void OnUpdate()
        {
            lock (PvPToggle.PvPplayer)
            {
                foreach (Player player in PvPToggle.PvPplayer)
                {
                    if (player.PvPType == "forceon")
                    {
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

            if (Main.bloodMoon && Config.ForcePvPOnBloodMoon)
            {
                foreach (Player ply in PvPToggle.PvPplayer)
                {
                    if (ply.PvPType != "bloodmoon")
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
        }
        #endregion

        #region Config
        public void SetUpConfig()
        {
            try
            {
                if (!File.Exists(savepath))
                {
                    Config.Write(savepath);
                }
                else
                    PvPConfig.Read(savepath);
            }
            catch
            {
                Log.ConsoleError("Error in PvpTog.json; Check logs for more details");
            }
        }
        #endregion

        public void OnLeave(LeaveEventArgs args)
        {
            lock (PvPplayer)
            {
                PvPplayer.RemoveAll(plr => plr.Index == args.Who);
            }
        }

        #region PvPSwitch
        public void PvPSwitch(CommandArgs args)
        {
            if (args.Parameters.Count != 0)
            {
                args.Player.SendErrorMessage("Invalid Syntax. Try /pvp");
                return;
            }
            else
            {
                if (!Main.player[args.Player.Index].hostile)
                {
                    Main.player[args.Player.Index].hostile = true;
                    NetMessage.SendData((int)PacketTypes.TogglePvp, -1, -1, "", args.Player.Index, 0f, 0f,
                                        0f);
                    args.Player.SendInfoMessage("Your PvP is now enabled.");
                }
                else if (Main.player[args.Player.Index].hostile)
                {
                    Main.player[args.Player.Index].hostile = false;
                    NetMessage.SendData((int)PacketTypes.TogglePvp, -1, -1, "", args.Player.Index, 0f, 0f,
                                        0f);
                    args.Player.SendInfoMessage("Your PvP is now disabled.");
                }
            }
        }
        #endregion

        #region TogglePvP
        public void TogglePvP(CommandArgs args)
        {
            var playerv2 = Tools.GetPlayerByIndex(args.Player.Index);

            if (args.Parameters.Count != 1)
            {
                args.Player.SendErrorMessage("You used too many parameters! Try /tpvp \"player's name\"!");
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

        #region TeamSwitch
        public static void TeamSwitch(CommandArgs args)
        {
            string team = args.Parameters[0];

            if (args.Parameters.Count > 1)
            {
                args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /team [team color]");
                return;
            }
            if (teamColors.Contains(team.ToLower()))
            {
                args.Player.SetTeam(teamColors.IndexOf(team));
                args.Player.SendSuccessMessage("Joined the {0} team!", team);
            }
            else
                args.Player.SendErrorMessage("Invalid team color!");
        }
        #endregion

        #region ToggleTeam
        public static void ToggleTeam(CommandArgs args)
        {
            if (args.Parameters.Count < 2 || args.Parameters.Count > 3)
            {
                args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /tteam [player] [team color]");
                return;
            }
            var foundplr = TShock.Utils.FindPlayer(args.Parameters[0]);
            if (foundplr.Count == 0)
            {
                args.Player.SendErrorMessage("Invalid player!");
                return;
            }
            else if (foundplr.Count > 1)
            {
                TShock.Utils.SendMultipleMatchError(args.Player, foundplr.Select(p => p.Name));
                return;
            }
            else
            {
                string team = args.Parameters[1];

                if (teamColors.Contains(team.ToLower()))
                {
                    foundplr[0].SetTeam(teamColors.IndexOf(team));
                    foundplr[0].SendInfoMessage("{0} changed you to the {1} team!", args.Player.Name, team);
                    args.Player.SendSuccessMessage("Changed {0} to the {1} team!", foundplr[0].Name, team);
                }
                else
                    args.Player.SendErrorMessage("Invalid team color!");
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
            Config.ForcePvPOnBloodMoon = !Config.ForcePvPOnBloodMoon;

            if (Config.ForcePvPOnBloodMoon)
                args.Player.SendInfoMessage("Players will now have PvP forced on during bloodmoons");
            else
                args.Player.SendInfoMessage("Players will no longer have PvP forced on during bloodmoons");
        }
        #endregion

        #region ForceToggle
        public void ForceToggle(CommandArgs args)
        {
            if (args.Parameters.Count != 1)
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

                    Player player = Tools.GetPlayerByIndex(players[0].Index);

                    if (player.PvPType == "")
                    {
                        player.PvPType = "forceon";
                        Main.player[plr.Index].hostile = true;
                        NetMessage.SendData((int)PacketTypes.TogglePvp, -1, -1, "", plr.Index, 0f, 0f, 0f);
                        plr.SendInfoMessage(string.Format("{0} has forced your PvP on!", args.Player.Name));
                        args.Player.SendInfoMessage(string.Format("You have forced {0}'s PvP on!", player.PlayerName));
                    }


                    else if (player.PvPType == "forceon")
                    {
                        player.PvPType = "";
                        Main.player[plr.Index].hostile = false;
                        NetMessage.SendData((int)PacketTypes.TogglePvp, -1, -1, "", plr.Index, 0f, 0f, 0f);
                        plr.SendInfoMessage(string.Format("{0} has turned your PvP off!", args.Player.Name));
                        args.Player.SendInfoMessage(string.Format("You have turned {0}'s PvP off!", player.PlayerName));

                    }
                }
            }
        }
    }
        #endregion

    #region Tools
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
    #endregion

    #region Config
    public class PvPConfig
    {
        public bool ForcePvPOnBloodMoon = false;

        public static PvPConfig Read(string path)
        {
            if (!File.Exists(path))
                return new PvPConfig();
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return Read(fs);
            }
        }

        public static PvPConfig Read(Stream stream)
        {
            using (var sr = new StreamReader(stream))
            {
                var cf = JsonConvert.DeserializeObject<PvPConfig>(sr.ReadToEnd());
                if (ConfigRead != null)
                    ConfigRead(cf);
                return cf;
            }
        }

        public void Write(string path)
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Write))
            {
                Write(fs);
            }
        }

        public void Write(Stream stream)
        {
            var str = JsonConvert.SerializeObject(this, Formatting.Indented);
            using (var sw = new StreamWriter(stream))
            {
                sw.Write(str);
            }
        }
        public static Action<PvPConfig> ConfigRead;
    }
    #endregion
}
