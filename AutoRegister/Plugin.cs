using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;

namespace AutoRegister
{
    [ApiVersion(2, 1)]
    public class Plugin : TerrariaPlugin
    {
        public override string Name => "AutoRegister";
        public override Version Version => new Version(1, 0, 1, 0);
        public override string Author => "brian91292 & moisterrific, atualizado por brasilzinhoz";
        public override string Description => "A TShock plugin to automatically register a user account for new players.";

        public Plugin(Main game) : base(game) { }

        private readonly Dictionary<string, string> tmpPasswords = new();

        public override void Initialize()
        {
            ServerApi.Hooks.ServerJoin.Register(this, OnServerJoin);
            ServerApi.Hooks.NetGreetPlayer.Register(this, OnGreetPlayer);
        }

        private async void OnGreetPlayer(GreetPlayerEventArgs args)
        {
            var tsConfig = TShock.Config.Settings;
            var player = TShock.Players[args.Who];
            string cmd = tsConfig.CommandSpecifier;

            if (tsConfig.DisableUUIDLogin && !tsConfig.DisableLoginBeforeJoin)
                return;

            await Task.Delay(1000);
            if (tmpPasswords.TryGetValue(player.Name, out string password))
            {
                try
                {
                    player.SendMessage($"Your account \"{player.Name}\" has been auto-registered.", Color.White);
                    player.SendMessage($"Your randomly generated password is {password}", Color.Green);
                    if (tsConfig.DisableUUIDLogin)
                        player.SendMessage($"Please sign in using {cmd}login {password}", Color.White);
                    player.SendMessage($"You can change this at any time by using {cmd}password {password} \"new password\"", Color.Red);
                }
                catch
                {
                    player.SendErrorMessage("Failed to retrieve your randomly generated password, please contact your server administrator.");
                    TShock.Log.ConsoleError("AutoRegister returned an error.");
                }
                tmpPasswords.Remove(player.Name);
            }
            else if (!player.IsLoggedIn)
            {
                player.SendErrorMessage($"Your account \"{player.Name}\" could not be auto-registered!");
                player.SendErrorMessage("This name has already been registered by another player.");
                player.SendErrorMessage("Please try again using a different name.");
            }
        }

        private void OnServerJoin(JoinEventArgs args)
        {
            var tsConfig = TShock.Config.Settings;
            var player = TShock.Players[args.Who];

            if (tsConfig.DisableUUIDLogin && !tsConfig.DisableLoginBeforeJoin)
            {
                TShock.Log.ConsoleError("AutoRegister will not work when DisableUUIDLogin is true AND DisableLoginBeforeJoin is false!");
                return;
            }

            if (!tsConfig.RequireLogin)
            {
                TShock.Log.ConsoleError("AutoRegister will not work when RequireLogin is set to false via config!");
                return;
            }

            if (TShock.UserAccounts.GetUserAccountByName(player.Name) == null && player.Name != TSServerPlayer.AccountName)
            {
                string password = GenerateSecureRandomString();
                tmpPasswords[player.Name] = password;

                TShock.UserAccounts.AddUserAccount(new UserAccount(
                    player.Name,
                    BCrypt.Net.BCrypt.HashPassword(password.Trim(), tsConfig.BCryptWorkFactor),
                    player.UUID,
                    tsConfig.DefaultRegistrationGroupName,
                    DateTime.UtcNow.ToString("s"),
                    DateTime.UtcNow.ToString("s"),
                    ""));

                TShock.Log.ConsoleInfo($"Auto-registered an account for \"{player.Name}\" ({player.IP})");
            }
            else
            {
                TShock.Log.ConsoleInfo($"Unable to auto-register \"{player.Name}\" ({player.IP}) because an account with this name already exists.");
            }
        }

        private static string GenerateSecureRandomString(int length = 10)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            using var rng = RandomNumberGenerator.Create();
            var data = new byte[length];
            rng.GetBytes(data);
            return new string(data.Select(b => chars[b % chars.Length]).ToArray());
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.ServerJoin.Deregister(this, OnServerJoin);
                ServerApi.Hooks.NetGreetPlayer.Deregister(this, OnGreetPlayer);
            }
            base.Dispose(disposing);
        }
    }
}
