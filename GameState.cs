using System;
using MelonLoader;
using Manager;
using MAI2.Util;
using MAI2System;

namespace maimai_rpc
{
    public enum RpcStateGroup
    {
        None = 0,
        Attract = 1,
        Entry = 2,
        MusicSelect = 3,
        Game = 4,
        Result = 5,
        Exiting = 6
    }

    public struct PlayerSlotInfo
    {
        public bool IsEntry;
        public string UserName;
        public uint Rating;
    }

    public struct SongTrackInfo
    {
        public uint TrackNumber;
        public int SongId;
        public string Title;
        public string Artist;
        public uint DurationMsec;
    }

    public static class GameStateHandler
    {
        public const string AssetBaseUrl = "https://asset.bway.lol";

        private static RpcStateGroup _lastGroup = RpcStateGroup.None;
        private static string _lastProcessName = null;

        public static RpcStateGroup LastGroup
        {
            get { return _lastGroup; }
        }

        public static string LastProcessName
        {
            get { return _lastProcessName; }
        }

        public static void ResetState()
        {
            _lastGroup = RpcStateGroup.None;
            _lastProcessName = null;
        }

        /// <summary>
        /// Normalizes music IDs to their canonical base/jacket ID.
        /// In Sinmai, Deluxe charts have ID + 10000 (10000..19999), and extended IDs have ID >= 100000.
        /// The game itself normalizes these using ID % 10000 in DataManager::LoadMusic.
        /// </summary>
        public static int NormalizeSongId(int musicId)
        {
            if (musicId >= 10000 && musicId < 20000)
            {
                return musicId % 10000;
            }
            if (musicId >= 100000)
            {
                return musicId % 10000;
            }
            return musicId;
        }

        private static void LogMsg(string message)
        {
            try
            {
                MelonLogger.Msg(message);
            }
            catch
            {
                System.Console.WriteLine(message);
            }
        }

        private static void LogWarn(string message)
        {
            try
            {
                MelonLogger.Warning(message);
            }
            catch
            {
                System.Console.WriteLine("[WARN] " + message);
            }
        }

        public static string GetGameId()
        {
            try
            {
                return !string.IsNullOrEmpty(ConstParameter.GameIDStr)
                    ? ConstParameter.GameIDStr.ToLowerInvariant()
                    : "sdga";
            }
            catch
            {
                return "sdga";
            }
        }

        public static string GetGameVersion()
        {
            try
            {
                var am = SingletonStateMachine<AmManager, AmManager.EState>.Instance;
                if (am != null)
                {
                    var vn = am.VersionNo;
                    if (vn.releaseNo > 0 && !string.IsNullOrEmpty(vn.versionString))
                    {
                        return vn.versionString;
                    }
                    if (!string.IsNullOrEmpty(vn.versionShotString))
                    {
                        return vn.versionShotString;
                    }
                }
            }
            catch
            {
            }

            try
            {
                var sysCfg = Singleton<SystemConfig>.Instance;
                if (sysCfg != null && sysCfg.config != null && sysCfg.config.romVersionInfo != null)
                {
                    var vn = sysCfg.config.romVersionInfo.versionNo;
                    if (vn.releaseNo > 0 && !string.IsNullOrEmpty(vn.versionString))
                    {
                        return vn.versionString;
                    }
                    if (!string.IsNullOrEmpty(vn.versionShotString))
                    {
                        return vn.versionShotString;
                    }
                }
            }
            catch
            {
            }

            try
            {
                uint nowVer = ConstParameter.NowGameVersion;
                uint minor = (nowVer % 10000) / 100;
                uint release = nowVer % 100;
                if (release > 0)
                {
                    return string.Format("1.{0:00}.{1:00}", minor, release);
                }
                return string.Format("1.{0:00}", minor);
            }
            catch
            {
                return "1.65";
            }
        }

        public static RpcStateGroup MapProcessToGroup(string processName)
        {
            if (string.IsNullOrEmpty(processName))
            {
                return RpcStateGroup.None;
            }

            if (processName == "AdvertiseProcess" || processName == "AdvDemoProcess")
            {
                return RpcStateGroup.Attract;
            }

            if (processName == "EntryProcess" || processName == "ModeSelectProcess")
            {
                return RpcStateGroup.Entry;
            }

            if (processName == "MusicSelectProcess" || processName == "TrackStartProcess")
            {
                return RpcStateGroup.MusicSelect;
            }

            if (processName == "GameProcess")
            {
                return RpcStateGroup.Game;
            }

            if (processName == "ResultProcess")
            {
                return RpcStateGroup.Result;
            }

            if (processName == "MapResultProcess" || processName == "GameOverProcess")
            {
                return RpcStateGroup.Exiting;
            }

            return RpcStateGroup.None;
        }

        public static DiscordActivity BuildActivity(
            string processName,
            PlayerSlotInfo p1,
            PlayerSlotInfo p2,
            SongTrackInfo song,
            string defaultLargeImage,
            long nowTimestamp,
            ref RpcStateGroup currentGroup)
        {
            return BuildActivity(processName, p1, p2, song, defaultLargeImage, nowTimestamp, ref currentGroup, GetGameId(), GetGameVersion());
        }

        public static DiscordActivity BuildActivity(
            string processName,
            PlayerSlotInfo p1,
            PlayerSlotInfo p2,
            SongTrackInfo song,
            string defaultLargeImage,
            long nowTimestamp,
            ref RpcStateGroup currentGroup,
            string gameId,
            string gameVersion)
        {
            RpcStateGroup newGroup = MapProcessToGroup(processName);
            if (newGroup == RpcStateGroup.None)
            {
                return null;
            }

            // Rule from rpc-specs.md: "For any GameState reusing another GameState's settings, don't set new RPC settings."
            if (newGroup == currentGroup)
            {
                return null;
            }

            if (string.IsNullOrEmpty(gameId))
            {
                gameId = GetGameId();
            }
            else
            {
                gameId = gameId.ToLowerInvariant();
            }
            if (string.IsNullOrEmpty(gameVersion))
            {
                gameVersion = GetGameVersion();
            }

            currentGroup = newGroup;

            var activity = new DiscordActivity
            {
                Type = 0
            };

            string ratingState;
            if (p1.IsEntry && p2.IsEntry)
            {
                ratingState = string.Format("Current Rating: {0} & {1}", p1.Rating, p2.Rating);
            }
            else if (p1.IsEntry)
            {
                ratingState = string.Format("Current Rating: {0}", p1.Rating);
            }
            else if (p2.IsEntry)
            {
                ratingState = string.Format("Current Rating: {0}", p2.Rating);
            }
            else
            {
                ratingState = "Current Rating: 0";
            }

            switch (newGroup)
            {
                case RpcStateGroup.Attract:
                    // Title / Attract mode (AdvertiseProcess / AdvDemoProcess)
                    activity.State = "Idle";
                    activity.LargeImageText = string.Format("{0} Version {1}", gameId, gameVersion);
                    activity.LargeImageKey = defaultLargeImage;
                    activity.SmallImageKey = "ellipsis";
                    activity.StartTimestamp = nowTimestamp;
                    break;

                case RpcStateGroup.Entry:
                    // Entering / Card Scanning (EntryProcess) & Mode Select (ModeSelectProcess)
                    activity.State = "Entering game";
                    activity.LargeImageText = string.Format("{0} Version {1}", gameId, gameVersion);
                    activity.LargeImageKey = defaultLargeImage;
                    activity.StartTimestamp = nowTimestamp;
                    break;

                case RpcStateGroup.MusicSelect:
                    // Choosing Song (MusicSelectProcess) & Loading Song (TrackStartProcess)
                    uint msTrackNo = song.TrackNumber > 0 ? song.TrackNumber : 1;
                    activity.Details = string.Format("Selecting track #{0}", msTrackNo);
                    activity.State = ratingState;
                    activity.LargeImageKey = defaultLargeImage;
                    activity.StartTimestamp = nowTimestamp;

                    if (p1.IsEntry && p2.IsEntry)
                    {
                        activity.LargeImageText = string.Format("{0} & {1}", p1.UserName, p2.UserName);
                    }
                    else if (p1.IsEntry)
                    {
                        activity.LargeImageText = p1.UserName;
                    }
                    else if (p2.IsEntry)
                    {
                        activity.LargeImageText = p2.UserName;
                    }
                    else
                    {
                        activity.LargeImageText = "maimai DX";
                    }
                    break;

                case RpcStateGroup.Game:
                    // Playing Song (GameProcess)
                    uint gameTrackNo = song.TrackNumber > 0 ? song.TrackNumber : 1;
                    activity.Details = string.Format("Playing Track #{0}", gameTrackNo);
                    activity.State = ratingState;

                    if (song.SongId > 0)
                    {
                        int normId = NormalizeSongId(song.SongId);
                        activity.LargeImageKey = string.Format("{0}/{1}/jacket/UI_Jacket_{2:D6}.png", AssetBaseUrl, gameId, normId);
                    }
                    else
                    {
                        activity.LargeImageKey = defaultLargeImage;
                    }

                    if (!string.IsNullOrEmpty(song.Title) && !string.IsNullOrEmpty(song.Artist))
                    {
                        activity.LargeImageText = string.Format("{0} by {1}", song.Title, song.Artist);
                    }
                    else if (!string.IsNullOrEmpty(song.Title))
                    {
                        activity.LargeImageText = song.Title;
                    }
                    else
                    {
                        activity.LargeImageText = "maimai DX";
                    }

                    activity.SmallImageKey = "play";
                    activity.SmallImageText = "Playing";

                    activity.StartTimestamp = null;
                    long songLenSec = song.DurationMsec > 0
                        ? (long)Math.Ceiling(song.DurationMsec / 1000.0)
                        : 135;
                    activity.EndTimestamp = nowTimestamp + songLenSec;
                    break;

                case RpcStateGroup.Result:
                    // Song Results (ResultProcess)
                    uint resTrackNo = song.TrackNumber > 0 ? song.TrackNumber : 1;
                    activity.Details = string.Format("Finished Track #{0}", resTrackNo);
                    activity.State = ratingState;

                    if (song.SongId > 0)
                    {
                        int normId = NormalizeSongId(song.SongId);
                        activity.LargeImageKey = string.Format("{0}/{1}/jacket/UI_Jacket_{2:D6}.png", AssetBaseUrl, gameId, normId);
                    }
                    else
                    {
                        activity.LargeImageKey = defaultLargeImage;
                    }

                    if (!string.IsNullOrEmpty(song.Title) && !string.IsNullOrEmpty(song.Artist))
                    {
                        activity.LargeImageText = string.Format("{0} by {1}", song.Title, song.Artist);
                    }
                    else if (!string.IsNullOrEmpty(song.Title))
                    {
                        activity.LargeImageText = song.Title;
                    }
                    else
                    {
                        activity.LargeImageText = "maimai DX";
                    }

                    activity.SmallImageKey = "pause";
                    activity.SmallImageText = "Finished";

                    activity.StartTimestamp = nowTimestamp;
                    activity.EndTimestamp = null;
                    break;

                case RpcStateGroup.Exiting:
                    // Area / Chiho Map (MapResultProcess) & Game Over (GameOverProcess)
                    activity.Details = "Exiting game";
                    activity.StartTimestamp = nowTimestamp;
                    // State and LargeImageText are intentionally left empty per rpc-specs.md
                    break;
            }

            return activity;
        }

        public static void HandleProcessTransition(Process.ProcessBase process)
        {
            if (process == null) return;
            string procName = process.GetType().Name;
            UpdateByProcessName(procName);
        }

        public static void ResendCurrentState()
        {
            if (_lastProcessName != null)
            {
                LogMsg(string.Format("[DiscordRPC] Resending presence for last known screen '{0}'...", _lastProcessName));
                _lastGroup = RpcStateGroup.None; // Reset group so update is forced
                UpdateByProcessName(_lastProcessName);
            }
            else
            {
                LogMsg("[DiscordRPC] No prior screen tracked yet. Defaulting to 'AdvertiseProcess'...");
                UpdateByProcessName("AdvertiseProcess");
            }
        }

        public static void UpdateByProcessName(string procName)
        {
            RpcStateGroup newGroup = MapProcessToGroup(procName);
            if (newGroup == RpcStateGroup.None)
            {
                // Non-target process (e.g. background fade/dialog)
                return;
            }

            // Check reuse rule before checking RPC readiness so suppressed transitions are clearly logged
            if (newGroup == _lastGroup)
            {
                LogMsg(string.Format("[DiscordRPC] Screen transition: {0} -> Group: {1} (reusing {2}; RPC update suppressed)",
                    procName, newGroup, _lastProcessName != null ? _lastProcessName : "previous"));
                return;
            }

            if (maimai_rpc.Instance == null || maimai_rpc.Instance.RPCHandler == null || !maimai_rpc.Instance.RPCHandler.IsReady)
            {
                LogWarn(string.Format("[DiscordRPC] Screen transition: {0} -> Group: {1}, but Discord RPC is not ready yet.", procName, newGroup));
                _lastProcessName = procName;
                return;
            }

            PlayerSlotInfo p1 = GetLivePlayerSlot(0);
            PlayerSlotInfo p2 = GetLivePlayerSlot(1);
            SongTrackInfo song = GetLiveSongTrackInfo(p1, p2);
            string defaultImage = maimai_rpc.DefaultLargeImage;
            long now = RPC.GetCurrentUnixTimestamp();

            DiscordActivity activity = BuildActivity(procName, p1, p2, song, defaultImage, now, ref _lastGroup);
            if (activity != null)
            {
                _lastProcessName = procName;

                string detailsStr = !string.IsNullOrEmpty(activity.Details) ? activity.Details : "(none)";
                string stateStr = !string.IsNullOrEmpty(activity.State) ? activity.State : "(none)";
                string largeImgStr = !string.IsNullOrEmpty(activity.LargeImageKey) ? activity.LargeImageKey : "(none)";
                string largeTextStr = !string.IsNullOrEmpty(activity.LargeImageText) ? activity.LargeImageText : "(none)";
                string smallImgStr = !string.IsNullOrEmpty(activity.SmallImageKey) ? activity.SmallImageKey : "(none)";
                string smallTextStr = !string.IsNullOrEmpty(activity.SmallImageText) ? activity.SmallImageText : "(none)";
                string countdownStr = activity.EndTimestamp.HasValue ? string.Format(", Countdown={0}s left", activity.EndTimestamp.Value - now) : "";

                LogMsg(string.Format("[DiscordRPC] Setting RPC [{0}]: Details=\"{1}\", State=\"{2}\", LargeImage=\"{3}\", LargeText=\"{4}\", SmallImage=\"{5}\", SmallText=\"{6}\"{7}",
                    procName, detailsStr, stateStr, largeImgStr, largeTextStr, smallImgStr, smallTextStr, countdownStr));

                maimai_rpc.Instance.RPCHandler.SetActivity(activity);
            }
        }

        private static PlayerSlotInfo GetLivePlayerSlot(int slot)
        {
            var info = new PlayerSlotInfo();
            try
            {
                var udm = Singleton<UserDataManager>.Instance;
                if (udm != null)
                {
                    var ud = udm.GetUserData(slot);
                    if (ud != null && ud.IsEntry)
                    {
                        info.IsEntry = true;
                        if (ud.Detail != null)
                        {
                            info.UserName = ud.Detail.UserName;
                            info.Rating = ud.Detail.Rating;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogWarn(string.Format("[DiscordRPC] Error reading PlayerSlot {0}: {1}", slot, ex.Message));
            }
            return info;
        }

        private static SongTrackInfo GetLiveSongTrackInfo(PlayerSlotInfo p1, PlayerSlotInfo p2)
        {
            var info = new SongTrackInfo();
            try
            {
                info.TrackNumber = GameManager.MusicTrackNumber;
                int musicId = 0;
                if (GameManager.SelectMusicID != null && GameManager.SelectMusicID.Length > 0)
                {
                    int slot = (p2.IsEntry && !p1.IsEntry && GameManager.SelectMusicID.Length > 1) ? 1 : 0;
                    musicId = GameManager.SelectMusicID[slot];
                }

                info.SongId = NormalizeSongId(musicId);
                if (musicId != info.SongId)
                {
                    LogMsg(string.Format("[DiscordRPC] Normalized Deluxe/Extended Music ID {0} -> {1}", musicId, info.SongId));
                }

                var dm = Singleton<DataManager>.Instance;
                if (dm != null && musicId > 0)
                {
                    var music = dm.GetMusic(musicId);
                    if (music == null && info.SongId != musicId)
                    {
                        music = dm.GetMusic(info.SongId);
                    }

                    if (music != null)
                    {
                        if (music.name != null && music.name.str != null)
                        {
                            info.Title = music.name.str;
                        }
                        if (music.artistName != null && music.artistName.str != null)
                        {
                            info.Artist = music.artistName.str;
                        }
                    }
                }

                var nm = NotesManager.Instance(0);
                if (nm != null)
                {
                    info.DurationMsec = nm.getPlayFinalMsec();
                    if (info.DurationMsec <= 0)
                    {
                        float endFrame = nm.getEndPlayFrame();
                        if (endFrame > 0)
                        {
                            info.DurationMsec = (uint)(endFrame / 60.0f * 1000.0f);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogWarn(string.Format("[DiscordRPC] Error reading SongTrackInfo: {0}", ex.Message));
            }
            return info;
        }
    }
}
