// Reads the host's own state once per frame so the ownership decision can be
// made in one place. Everything is read through RepoApi (reflection), so a
// renamed field only turns a feature off.

using System;
using UnityEngine;

namespace MinecraftInRepo.Host
{
    public sealed class HostProbe
    {
        private readonly RepoApi api;
        private readonly Action<string> log;

        public HostProbe(RepoApi repoApi, Action<string> log)
        {
            api = repoApi;
            this.log = log ?? delegate { };
        }

        /// <summary>The host has a local character we can own or follow with.</summary>
        public bool HasCharacter { get; private set; }

        /// <summary>The host is moving or showing the character itself.</summary>
        public bool Cutscene { get; private set; }

        /// <summary>A host window is open over the body.</summary>
        public bool HostMenuOpen { get; private set; }

        /// <summary>Where the host's character is (R.E.P.O. space).</summary>
        public Vector3 CharacterPosition { get; private set; }

        /// <summary>Where the host's camera is (R.E.P.O. space).</summary>
        public Vector3 EyePosition { get; private set; }

        /// <summary>Distance from the camera down to the character's origin, measured live.</summary>
        public float EyeToCharacter { get; private set; }

        /// <summary>The raw game state string, for the one-line-per-second log.</summary>
        public string GameState { get; private set; }

        public object Avatar { get; private set; }
        public object Controller { get; private set; }
        public object AvatarVisuals { get; private set; }
        public object Health { get; private set; }
        public object Tumble { get; private set; }
        public object ToolController { get; private set; }

        public void Refresh()
        {
            Avatar = api.StaticValue("PlayerAvatar", "instance");
            Controller = api.StaticValue("PlayerController", "instance");
            HasCharacter = Avatar != null && Controller != null;

            EyePosition = Camera.main != null ? Camera.main.transform.position : new Vector3();
            CharacterPosition = HasCharacter
                ? (Vector3)(api.InstanceValue(Avatar, "playerTransform") is Transform
                    ? ((Transform)api.InstanceValue(Avatar, "playerTransform")).position
                    : ((Component)Avatar).transform.position)
                : new Vector3();
            EyeToCharacter = EyePosition.y - CharacterPosition.y;

            if (HasCharacter)
            {
                AvatarVisuals = api.InstanceValue(Avatar, "playerAvatarVisuals");
                Health = api.InstanceValue(Avatar, "playerHealth");
                Tumble = api.InstanceValue(Avatar, "tumble");
                ToolController = api.StaticValue("ToolController", "instance");
            }
            else
            {
                AvatarVisuals = null;
                Health = null;
                Tumble = null;
                ToolController = null;
            }

            Cutscene = ReadCutscene();
            HostMenuOpen = ReadMenu();
        }

        private bool ReadCutscene()
        {
            object director = api.StaticValue("GameDirector", "instance");
            if (director != null)
            {
                if (api.InstanceValue<bool>(director, "DisableInput", false))
                {
                    GameState = "disable-input";
                    return true;
                }

                object state = api.InstanceValue(director, "currentState");
                string name = state == null ? "?" : state.ToString();
                GameState = name;
                if (!string.Equals(name, "Main", StringComparison.OrdinalIgnoreCase))
                {
                    return true;   // Start, End, Death, Result, Outro, Load ...
                }
            }

            object run = api.StaticValue("RunManager", "instance");
            if (run != null)
            {
                if (api.InstanceValue<bool>(run, "restarting", false)
                    || api.InstanceValue<bool>(run, "gameOver", false)
                    || api.InstanceValue<bool>(run, "allPlayersDead", false)
                    || api.InstanceValue<bool>(run, "waitToChangeScene", false)
                    || api.InstanceValue<bool>(run, "levelMainMenu", false)
                    || api.InstanceValue<bool>(run, "levelLobbyMenu", false)
                    || api.InstanceValue<bool>(run, "levelSplashScreen", false)
                    || !api.InstanceValue<bool>(run, "runStarted", true))
                {
                    GameState = "run-state";
                    return true;
                }
            }

            if (Avatar != null)
            {
                if (api.InstanceValue<bool>(Avatar, "deadSet", false)
                    || api.InstanceValue<bool>(Avatar, "spectating", false)
                    || api.InstanceValue<bool>(Avatar, "isDisabled", false)
                    || !api.InstanceValue<bool>(Avatar, "spawned", true))
                {
                    GameState = "avatar-state";
                    return true;
                }
            }

            if (Tumble != null && api.InstanceValue<bool>(Tumble, "isTumbling", false))
            {
                GameState = "tumbling";
                return true;
            }

            return false;
        }

        private bool menuProbed;

        private bool ReadMenu()
        {
            object menu = api.StaticValue("MenuManager", "instance");
            if (menu == null)
            {
                return false;
            }

            object page = api.InstanceValue(menu, "currentMenuPage");
            object state = api.InstanceValue(menu, "currentMenuState");
            string stateName = state == null ? null : state.ToString();

            // A closed page is an inactive object in R.E.P.O.; the state enum
            // (Closed / Open / ...) is the second opinion. When neither is
            // readable we say "closed": stealing the body on a false positive is
            // far worse than missing a menu.
            bool active = false;
            object activeSelf = PageActiveSelf(page);
            if (activeSelf is bool)
            {
                active = (bool)activeSelf;
            }

            bool open = active && (string.IsNullOrEmpty(stateName) ||
                                   stateName.IndexOf("Clos", StringComparison.OrdinalIgnoreCase) < 0);

            if (!menuProbed && page != null)
            {
                menuProbed = true;
                log("[MinecraftInRepo] menu probe: page=" + page.GetType().Name +
                    " activeSelf=" + activeSelf + " state=" + (stateName ?? "?") + " -> open=" + open);
            }
            return open;
        }

        private object PageActiveSelf(object page)
        {
            if (page == null)
            {
                return null;
            }
            object active = api.InstanceValue(page, "activeSelf");
            if (active is bool)
            {
                return active;
            }
            object owner = api.InstanceValue(page, "gameObject");
            if (owner != null)
            {
                active = api.InstanceValue(owner, "activeSelf");
                if (active is bool)
                {
                    return active;
                }
            }
            return null;
        }
    }
}
