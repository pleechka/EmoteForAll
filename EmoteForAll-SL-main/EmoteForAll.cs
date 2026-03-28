using Exiled.API.Features;
using HarmonyLib;
using System;

namespace EmoteForAll
{
    public class EmoteForAll : Plugin<Config.Config>
    {
        public override string Name => "EmoteForAll";
        public override string Author => "pleechka, creepycats";
        public override Version Version => new Version(1, 0, 0);

        // ReSharper disable once UnusedMember.Global
        public static EmoteForAll Instance { get; private set; }
        private Harmony _harmony;
        private Handlers.PlayerHandler _playerHandler;

        public override void OnEnabled()
        {
            Instance = this;
            // ReSharper disable once StringLiteralTypo
            Log.Info($"{Name} v{Version} - updated by pleechka");

            if (_harmony is null)
            {
                _harmony = new Harmony("EmoteForAll");
                _harmony.PatchAll();
            }

            _playerHandler = new Handlers.PlayerHandler();
            Exiled.Events.Handlers.Player.Hurting += _playerHandler.Hurting;

            Log.Info("Plugin Enabled!");
        }

        public override void OnDisabled()
        {
            if (_harmony is not null)
            {
                _harmony.UnpatchAll("EmoteForAll");
                _harmony = null;
            }

            Exiled.Events.Handlers.Player.Hurting -= _playerHandler.Hurting;
            _playerHandler = null;

            Log.Info("Disabled Plugin Successfully");
        }
    }
}