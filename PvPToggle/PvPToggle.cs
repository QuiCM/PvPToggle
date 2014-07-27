using System;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace PvPToggle
{
    [ApiVersion(1, 16)]
    public class PvpToggle : TerrariaPlugin
    {
        public static readonly List<Player> PvPplayer = new List<Player>();
        private static readonly List<string> TeamColors = new List<string> { "white", "red", "green", "blue", "yellow" };
        private static PvPConfig Config { get; set; }

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
            ServerApi.Hooks.GameUpdate.Register(this, OnUpdate);
            ServerApi.Hooks.NetGreetPlayer.Register(this, OnGreetPlayer);
            ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
            ServerApi.Hooks.ServerLeave.Register(this, OnLeave);

            Config = new PvPConfig();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.GameUpdate.Deregister(this, OnUpdate);
                ServerApi.Hooks.NetGreetPlayer.Deregister(this, OnGreetPlayer);
                ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
                ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
            }
            base.Dispose(disposing);
        }

        public PvpToggle(Main game)
            : base(game)
        {
            Order = 1;
        }

        private static void OnInitialize(EventArgs e)
        {
            Commands.ChatCommands.Add(new Command(PvPSwitch, "pvp"));
            Commands.ChatCommands.Add(new Command("pvp.switch", TogglePvP, "tpvp"));
            Commands.ChatCommands.Add(new Command(TeamSwitch, "team"));
            Commands.ChatCommands.Add(new Command("pvp.team", ToggleTeam, "tteam"));
            Commands.ChatCommands.Add(new Command("pvp.force", ForceToggle, "forcepvp", "fpvp"));
            Commands.ChatCommands.Add(new Command("pvp.moon", BloodToggle, "bloodmoonpvp", "bmpvp"));

            SetUpConfig();
        }

        private static void OnGreetPlayer(GreetPlayerEventArgs args)
        {
            lock (PvPplayer)
                PvPplayer.Add(new Player(args.Who));
        }

        #region OnUpdate

        private static void OnUpdate(EventArgs e)
        {
            lock (PvPplayer)
            {
                foreach (var player in PvPplayer)
                {
                    switch (player.PvPType)
                    {
                        case "forceon":
                            if (Main.player[player.Index].hostile) continue;
                            Main.player[player.Index].hostile = true;
                            NetMessage.SendData((int) PacketTypes.TogglePvp, -1, -1, "", player.Index);
                            player.TSPlayer.SendWarningMessage("Your PvP has been forced on, don't try and turn it off!");
                            break;
                        case "bloodmoon":
                            if (Main.bloodMoon && !Main.dayTime)
                            {
                                if (Main.player[player.Index].hostile == false)
                                {
                                    Main.player[player.Index].hostile = true;
                                    NetMessage.SendData((int) PacketTypes.TogglePvp, -1, -1, "", player.Index);
                                    player.TSPlayer.SendWarningMessage(
                                        "The blood moon's evil influence stops your PvP from turning off.");
                                }
                            }
                            else
                            {
                                player.PvPType = "";
                                player.TSPlayer.SendInfoMessage(
                                    "The blood moon fades, and you have control over your PvP again!");
                            }
                            break;
                    }
                }
            }

            if (!Main.bloodMoon || !Config.ForcePvPOnBloodMoon) return;

            foreach (var ply in PvPplayer.Where(ply => ply.PvPType != "bloodmoon"))
            {
                ply.PvPType = "bloodmoon";
                if (Main.player[ply.Index].hostile == false)
                {
                    Main.player[ply.Index].hostile = true;
                    NetMessage.SendData((int)PacketTypes.TogglePvp, -1, -1, "", ply.Index);
                }
                ply.TSPlayer.SendInfoMessage("Your PvP has been forced on for the blood moon!");
            }
        }
        #endregion

        #region Config

        private static void SetUpConfig()
        {
            var configPath = Path.Combine(TShock.SavePath, "PvPtoggle.json");
            (Config = PvPConfig.Read(configPath)).Write(configPath);
        }

        #endregion

        private static void OnLeave(LeaveEventArgs args)
        {
            lock (PvPplayer)
                PvPplayer.RemoveAll(plr => plr.Index == args.Who);
        }

        #region PvPSwitch

        private static void PvPSwitch(CommandArgs args)
        {
            if (TShock.Config.PvPMode == "always" || TShock.Config.PvPMode == "disabled")
            {
                args.Player.SendErrorMessage("Command blocked by server configuration");
                return;
            }

            if (args.Parameters.Count != 0)
            {
                args.Player.SendErrorMessage("Invalid Syntax. Try /pvp");
                return;
            }

            if (!Main.player[args.Player.Index].hostile)
            {
                Main.player[args.Player.Index].hostile = true;
                NetMessage.SendData((int) PacketTypes.TogglePvp, -1, -1, "", args.Player.Index);
                args.Player.SendInfoMessage("Your PvP is now enabled.");
            }
            else if (Main.player[args.Player.Index].hostile)
            {
                Main.player[args.Player.Index].hostile = false;
                NetMessage.SendData((int) PacketTypes.TogglePvp, -1, -1, "", args.Player.Index);
                args.Player.SendInfoMessage("Your PvP is now disabled.");
            }
        }

        #endregion

        #region TogglePvP

        private static void TogglePvP(CommandArgs args)
        {

            if (args.Parameters.Count != 1)
            {
                args.Player.SendErrorMessage("You used too many parameters! Try /tpvp \"player's name\"!");
            }

            var plStr = String.Join(" ", args.Parameters);

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
                        NetMessage.SendData((int)PacketTypes.TogglePvp, -1, -1, "", player.Index);
                        args.Player.SendInfoMessage(string.Format("You have turned {0}'s PvP on!", player.Name));
                        player.SendInfoMessage(string.Format("{0} has turned your PvP on!", args.Player.Name));

                    }
                    else if (Main.player[player.Index].hostile)
                    {
                        Main.player[player.Index].hostile = false;
                        NetMessage.SendData((int)PacketTypes.TogglePvp, -1, -1, "", player.Index);

                        args.Player.SendInfoMessage(string.Format("You have turned {0}'s PvP off!", player.Name));
                        player.SendInfoMessage(string.Format("{0} has turned your PvP off!", args.Player.Name));
                    }
                }
            }

        }

        #endregion

        #region TeamSwitch

        private static void TeamSwitch(CommandArgs args)
        {
            if (args.Parameters.Count != 1)
            {
                args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /team [team color]");
                return;
            }

            var team = args.Parameters[0];

            if (TeamColors.Contains(team.ToLower()))
            {
				args.Player.TPlayer.team = TeamColors.IndexOf(team);
				NetMessage.SendData((int)PacketTypes.PlayerTeam, -1, -1, "", args.Player.Index);
				args.Player.SendData(PacketTypes.PlayerTeam, "", args.Player.Index);
                NetMessage.SendData((int) PacketTypes.PlayerTeam, -1, -1, "", args.Player.Index);
                args.Player.SendSuccessMessage("Joined the {0} team!", team);
            }
            else
                args.Player.SendErrorMessage("Invalid team color!");
        }
        #endregion

        #region ToggleTeam

        private static void ToggleTeam(CommandArgs args)
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
            if (foundplr.Count > 1)
            {
                TShock.Utils.SendMultipleMatchError(args.Player, foundplr.Select(p => p.Name));
                return;
            }

            var team = args.Parameters[1];

            if (TeamColors.Contains(team.ToLower()))
			{
				foundplr[0].TPlayer.team = TeamColors.IndexOf(team);
				NetMessage.SendData((int)PacketTypes.PlayerTeam, -1, -1, "", foundplr[0].Index);
				foundplr[0].SendData(PacketTypes.PlayerTeam, "", foundplr[0].Index);
                foundplr[0].SendInfoMessage("{0} changed you to the {1} team!", args.Player.Name, team);
                args.Player.SendSuccessMessage("Changed {0} to the {1} team!", foundplr[0].Name, team);
            }
            else
                args.Player.SendErrorMessage("Invalid team color!");
        }

        #endregion

        #region BloodToggle
        private static void BloodToggle(CommandArgs args)
        {
            if (args.Parameters.Count != 0)
            {
                args.Player.SendErrorMessage("Usage: /bloodmoonpvp");
                return;
            }
            Config.ForcePvPOnBloodMoon = !Config.ForcePvPOnBloodMoon;

            args.Player.SendInfoMessage(Config.ForcePvPOnBloodMoon
                ? "Players will now have PvP forced on during bloodmoons"
                : "Players will no longer have PvP forced on during bloodmoons");
        }
        #endregion

        #region ForceToggle

        private static void ForceToggle(CommandArgs args)
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
            if (players.Count > 1
                && ((plStr != "*") && (plStr != "all") && (plStr != "*off") && (plStr != "alloff")))
            {
                args.Player.SendErrorMessage("More than one player matched that name");
                return;
            }

            if (plStr == "*" || plStr == "all")
            {
                foreach (var pl in PvPplayer)
                    pl.PvPType = "forceon";
                TSPlayer.All.SendInfoMessage(string.Format("{0} has forced on everyone's PvP", args.Player.Name));
                return;
            }
            if (plStr == "*off" || plStr == "alloff")
            {
                foreach (var pl in PvPplayer)
                    pl.PvPType = "";
                TSPlayer.All.SendInfoMessage(string.Format("{0} has stopped forcing everyone's PvP on. It can now be turned off", args.Player.Name));
            }

            else
            {
                if (args.Parameters.Count == 1 && players.Count == 1)
                {
                    var plr = players[0];

                    var player = Tools.GetPlayerByIndex(players[0].Index);

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
            return PvpToggle.PvPplayer.FirstOrDefault(player => player.Index == index);
        }
    }
    #endregion

    #region Config
    public class PvPConfig
    {
        public bool ForcePvPOnBloodMoon;

        public void Write(string path)
        {
            File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
        }

        public static PvPConfig Read(string path)
        {
            if (!File.Exists(path))
                return new PvPConfig();
            return JsonConvert.DeserializeObject<PvPConfig>(File.ReadAllText(path));
        }
    }
    #endregion
}
