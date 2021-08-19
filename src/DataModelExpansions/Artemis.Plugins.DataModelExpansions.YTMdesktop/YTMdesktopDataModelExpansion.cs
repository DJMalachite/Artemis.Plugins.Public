using Artemis.Core.Services;
using Artemis.Plugins.DataModelExpansions.YTMdesktop.DataModels;
using Serilog;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Diagnostics;
using Artemis.Core.Modules;
using Artemis.Core;
using System.Collections.Generic;

namespace Artemis.Plugins.DataModelExpansions.YTMdesktop
{

    [PluginFeature(Name = "Youtube Music Desktop Player", Icon = "Play", AlwaysEnabled = true)]
    public class YTMdesktopDataModelExpansion : Module<YTMdesktopDataModel>
    {
        #region Variables declarations

        private readonly ILogger _logger;
        private readonly IColorQuantizerService _colorQuantizer;
        private readonly IProcessMonitorService _processMonitorService;
        private readonly HttpClient _httpClient;
        private readonly ConcurrentDictionary<string, ColorSwatch> albumArtColorCache;
        private const string YTMD_PROCESS_NAME = "YouTube Music Desktop App";
        private YTMDesktopClient _YTMDesktopClient;
        private RootInfo _rootInfo;
        private string _trackId;
        private string _albumArtUrl;

        #endregion

        #region Constructor

        public YTMdesktopDataModelExpansion(ILogger logger, IColorQuantizerService colorQuantizer, IProcessMonitorService processMonitorService)
        {
            _processMonitorService = processMonitorService;
            _logger = logger;
            _colorQuantizer = colorQuantizer;

            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(1)
            };
            albumArtColorCache = new ConcurrentDictionary<string, ColorSwatch>();
            UpdateDuringActivationOverride = false;
        }

        public override List<IModuleActivationRequirement> ActivationRequirements { get; } = new()
        {
            new ProcessActivationRequirement("YouTube Music Desktop App")
        };

        #endregion

        #region Plugin Methods
        public override void Enable()
        {
            AddTimedUpdate(TimeSpan.FromSeconds(1), UpdateData);

            _YTMDesktopClient = new YTMDesktopClient();

        }
     
        private bool YoutubeIsRunning()
        {
            return _processMonitorService.GetRunningProcesses().Any(p => p.ProcessName == YTMD_PROCESS_NAME);
        }

        public override void Disable()
        {
            _YTMDesktopClient = null;
            _trackId = null;
            _albumArtUrl = null;
        }

        public override void Update(double deltaTime)
        {
            if (DataModel.Player.isPaused)
                return;

            if (DataModel.Track.duration == 0)
                return;

            DataModel.Player.seekbarCurrentPositionHuman = DataModel.Player.seekbarCurrentPositionHuman.Add(TimeSpan.FromMilliseconds(deltaTime * 1000));
            DataModel.Player.seekbarCurrentPosition = DataModel.Player.seekbarCurrentPositionHuman.TotalSeconds;
            DataModel.Player.statePercent = DataModel.Player.seekbarCurrentPosition / DataModel.Track.duration;
        }
        #endregion

        #region DataModel update methods

        private async Task UpdateData(double deltaTime)
        {
            UpdateYTMDekstopInfo();
        }

        private void UpdateYTMDekstopInfo()
        {
            if (!YoutubeIsRunning())
            {
                // Don't query server if YTMD proccess is down.
                DataModel.Empty();
                return;
            }

            try
            {
                // Update DataModel using /query API
                _YTMDesktopClient?.Update();
                _rootInfo = _YTMDesktopClient?.Data;

                if (_rootInfo != null)
                {
                    UpdateInfo(_rootInfo);
                }
                else
                {
                    DataModel.Empty();
                }
            }
            catch (Exception e)
            {
                _logger.Error(e.ToString());
            }
        }

        private void UpdateInfo(RootInfo data)
        {
            UpdatePlayerInfo(data.player);

            if (data.player.hasSong && data.track != null)
                UpdateTrackInfo(data.track);
            else
            {
                DataModel.Track.Empty();
            }
        }

        // Thanks again to diogotr7 for the original code
        // https://github.com/diogotr7/Artemis.Plugins/blob/a1846bb3b2e0cb426ecd2b9ae787bade8212f446/src/Artemis.Plugins.Modules.Spotify/SpotifyModule.cs#L209
        private async Task UpdateTrackInfo(TrackInfo track)
        {
            if (track.id != _trackId)
            {
                UpdateBasicTrackInfo(track);

                if (track.cover != _albumArtUrl)
                {
                    if (string.IsNullOrEmpty(track.cover))
                        return;
                    await UpdateAlbumArtColors(track.cover);
                    _albumArtUrl = track.cover;
                }

                _trackId = track.id;
            }
        }

        private void UpdatePlayerInfo(PlayerInfo player)
        {
            DataModel.Player.IsRunning = true;
            DataModel.Player.hasSong = player.hasSong;
            DataModel.Player.isPaused = player.isPaused;
            DataModel.Player.volumePercent = player.volumePercent;
            DataModel.Player.seekbarCurrentPosition = player.seekbarCurrentPosition;
            DataModel.Player.seekbarCurrentPositionHuman = TimeSpan.FromSeconds(player.seekbarCurrentPosition);
            DataModel.Player.statePercent = player.statePercent;
            DataModel.Player.likeStatus = player.likeStatus;
            DataModel.Player.repeatType = Enum.Parse<RepeatState>(player.repeatType, true);
        }

        private async Task UpdateAlbumArtColors(string albumArtUrl)
        {
            if (!albumArtColorCache.ContainsKey(albumArtUrl))
            {
                try
                {
                    using Stream stream = await _httpClient.GetStreamAsync(albumArtUrl);
                    using SKBitmap skbm = SKBitmap.Decode(stream);
                    SKColor[] skClrs = _colorQuantizer.Quantize(skbm.Pixels, 256);
                    albumArtColorCache[albumArtUrl] = _colorQuantizer.FindAllColorVariations(skClrs, true);
                }
                catch (Exception e)
                {
                    _logger.Error("Failed to get album art colors", e);
                }
            }

            if (albumArtColorCache.TryGetValue(albumArtUrl, out var colorDataModel))
                DataModel.Track.Colors = colorDataModel;
        }

        private void UpdateBasicTrackInfo(TrackInfo track)
        {
            DataModel.Track.author = track.author;
            DataModel.Track.title = track.title;
            DataModel.Track.album = track.album;
            DataModel.Track.cover = track.cover;
            DataModel.Track.duration = track.duration;
            DataModel.Track.durationHuman = TimeSpan.FromSeconds(track.duration);
            DataModel.Track.url = track.url;
            DataModel.Track.id = track.id;
            DataModel.Track.isVideo = track.isVideo;
            DataModel.Track.isAdvertisement = track.isAdvertisement;
            DataModel.Track.inLibrary = track.inLibrary;
        }

        #endregion
    }
}