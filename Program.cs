using System.Configuration;
using System.Text;
using System.Web;
using Newtonsoft.Json;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using Swan.Logging;
using static SpotifyAPI.Web.Scopes;

namespace SpotifyPlayerControl
{
    public class Program
    {
        private static readonly string? clientId = ConfigurationManager.AppSettings["ClientID"];

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        private static EmbedIOAuthServer _server;

        private static string CredentialsPath { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        private static void Exiting() => Console.CursorVisible = true;

        public static async Task<int> Main(string[] args)
        {
#if DEBUG
            //
            // Testing things out...
            //
            //args = new[] { "!start" };
            //args = new[] { "!pause" };
            //args = new[] { "!skip" };
            //args = new[] { "!prev" };
            //args = new[] { "!play", "https://open.spotify.com/track/14BIjb7JQJzUPBE7PxLV25?si=69f799ac9b154e82" };
            //args = new[] { "!play", "https%3A%2F%2Fopen.spotify.com%2Ftrack%2F1huvTbEYtgltjQRXzrNKGi" };
            //args = new[] { "!play", "Peace Orchestra - Double Drums" };
            //args = new[] { "!play", "Peace+Orchestra+-+Double+Drums" };
            //args = new[] { "!play", "Where is my mind?" };
            //args = new[] { "!song" };
            //args = new[] { "!next" };
            //args = new[] { "!recent" };
            //args = new[] { "!shuffle" }; //restriction violated... no idea what that means, doh!
            //args = new[] { "!repeat" }; //restriction violated... no idea what that means, doh!
            //args = new[] { "!mute" };
            //args = new[] { "!vol", "-50" };
            args = new[] { "!playlist" };
#endif

            // initialize path
            CredentialsPath = Path.Combine(System.AppContext.BaseDirectory, "credentials.json");

            // Disable logging output from web server
            // https://github.com/unosquare/embedio/wiki/Cookbook#logging-turn-off-or-customize
            //Logger.UnregisterLogger<ConsoleLogger>();

            var command = "";
            var payload = "";

            if (args.Length > 0)
            {
                command = args[0].ToLower();
            }
            if (args.Length > 1)
            {
                payload = args[1];
            }

            // This is a bug in the SWAN Logging library, need this hack to bring back the cursor
            AppDomain.CurrentDomain.ProcessExit += (sender, e) => Exiting();

            if (File.Exists(CredentialsPath))
            {
                await Start(command, payload);
            }
            else
            {
                _server = new EmbedIOAuthServer(new Uri("http://localhost:5000/spotifyCallback"), 5000);
                await StartAuthentication(command, payload);
            }

            await Task.Delay(-1);
            return 0;
        }

        private static async Task Start(string command, string payload)
        {
            var json = await File.ReadAllTextAsync(CredentialsPath);
            var token = JsonConvert.DeserializeObject<PKCETokenResponse>(json);

            var authenticator = new PKCEAuthenticator(clientId!, token!);
            authenticator.TokenRefreshed += (sender, token) => File.WriteAllText(CredentialsPath, JsonConvert.SerializeObject(token));

            var config = SpotifyClientConfig.CreateDefault().WithAuthenticator(authenticator);
            var spotifyClient = new SpotifyClient(config);

            // Get the current user profile
            var userProfile = await spotifyClient.UserProfile.Current();

            // Check if we have an active player or not
            var currentPlayback = await spotifyClient.Player.GetCurrentPlayback();

            if (currentPlayback != null && currentPlayback.Device.IsActive)
                switch (command)
                {
                    case "!start":
                    case "!resume":
                        await spotifyClient.Player.ResumePlayback();
                        break;
                    case "!pause":
                        await spotifyClient.Player.PausePlayback();
                        break;
                    case "!skip":
                        await spotifyClient.Player.SkipNext();
                        break;
                    case "!prev":
                    case "!back":
                        await spotifyClient.Player.SkipPrevious();
                        break;
                    case "!play":
                    case "!queue":
                        await AddTrackToQueue(spotifyClient, payload);
                        break;
                    case "!song":
                    case "!music":
                        await GetCurrentTrackInfo(spotifyClient);
                        break;
                    case "!next":
                        await GetNextTrackInfo(spotifyClient);
                        break;
                    case "!recent":
                        await GetRecentlyPlayed(spotifyClient);
                        break;
                    case "!shuffle":
                        await ToggleShuffle(spotifyClient, currentPlayback);
                        break;
                    case "!repeat":
                        await SwitchRepeat(spotifyClient, currentPlayback);
                        break;
                    case "!mute":
                        await ToggleMute(spotifyClient, currentPlayback);
                        break;
                    case "!vol":
                    case "!volume":
                        await ChangeVolume(spotifyClient, currentPlayback, payload);
                        break;
                    case "!playlist":
                        await GetCurrentPlaylist(spotifyClient, currentPlayback);
                        break;
                    default:
                        GreetUser(userProfile);
                        break;
                }
            else if (command != "") NoActivePlayer(userProfile);
            else GreetUser(userProfile);

            Environment.Exit(0);
        }

        private static async Task StartAuthentication(string command, string payload)
        {
            var (verifier, challenge) = PKCEUtil.GenerateCodes();

            await _server.Start();
            _server.AuthorizationCodeReceived += async (sender, response) =>
            {
                await _server.Stop();
                PKCETokenResponse token = await new OAuthClient().RequestToken(
                  new PKCETokenRequest(clientId!, response.Code, _server.BaseUri, verifier)
                );
                _server.Dispose();

                await File.WriteAllTextAsync(CredentialsPath, JsonConvert.SerializeObject(token));
                await Start(command, payload);
            };

            var request = new LoginRequest(_server.BaseUri, clientId!, LoginRequest.ResponseType.Code)
            {
                CodeChallenge = challenge,
                CodeChallengeMethod = "S256",
                Scope = new List<string> { UserReadPlaybackState, UserModifyPlaybackState, UserReadCurrentlyPlaying, UserReadPlaybackPosition, UserReadRecentlyPlayed }
            };

            Uri uri = request.ToUri();
            try
            {
                BrowserUtil.Open(uri);
            }
            catch (Exception)
            {
               $"Unable to open URL, manually open: {uri}".Warn();
            }
        }

        private static void GreetUser(PrivateUser userProfile)
        {
            ConsoleColor currentForeground = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\nMoin {userProfile.DisplayName},\nyour Spotify account with ID {userProfile.Id} is authenticated!");
            Console.ForegroundColor = currentForeground;
        }

        private static void NoActivePlayer(PrivateUser userProfile)
        {
            ConsoleColor currentForeground = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"\n{userProfile.DisplayName}, no active player found!");
            Console.ForegroundColor = currentForeground;
            Console.WriteLine("Please play a Spotify song on your client before triggering an action from the CLI and then try again.");
        }

        private static async Task AddTrackToQueue(SpotifyClient spotifyClient, string songRequest)
        {
            var sb = new StringBuilder();
            songRequest = HttpUtility.UrlDecode(songRequest);

            // Spotify URL given with track ID
            if (songRequest.StartsWith("https://open.spotify.com/track/"))
            {
                var uri = new Uri(songRequest);
                var trackId = uri.Segments[2];
                sb.Append("spotify:track:");
                sb.Append(trackId);

                var requestedTrack = await spotifyClient.Tracks.Get(trackId);
                if (requestedTrack != null && requestedTrack.Artists.Count > 0)
                {
                    var request = new PlayerAddToQueueRequest(sb.ToString());
                    await spotifyClient.Player.AddToQueue(request);
                    Console.Write($"'{requestedTrack.Artists[0].Name} - {requestedTrack.Name}'");
                }
                else
                {
                    Console.Write("404 - Track not found!");
                }
            }
            // Need to search for the song and hope the best
            else
            {
                // Input should be given in the format: "artist name - song title"
                var requestParts = songRequest.Split('-');

                if (requestParts.Length == 1)
                {
                    sb.Append(songRequest);
                }
                else if (requestParts.Length == 2)
                {
                    sb.Append("artist:");
                    sb.Append(requestParts[0].Trim());
                    sb.Append(" track:");
                    sb.Append(requestParts[1].Trim());
                }

                var searchRequest = new SearchRequest(SearchRequest.Types.Track, sb.ToString());
                var searchResponse = await spotifyClient.Search.Item(searchRequest);

                if (searchResponse.Tracks.Items != null && searchResponse.Tracks.Items.Count > 0)
                {
                    var request = new PlayerAddToQueueRequest(searchResponse.Tracks.Items[0].Uri);
                    await spotifyClient.Player.AddToQueue(request);
                    Console.Write($"'{searchResponse.Tracks.Items[0].Artists[0].Name} - {searchResponse.Tracks.Items[0].Name}'");
                }
                else
                {
                    Console.Write("404 - Track not found!");
                }
            }

            // Unable to remove or reorder things in queue!
            // https://community.spotify.com/t5/Spotify-for-Developers/API-Delete-Remove-songs-from-queue/td-p/4956378
            // await spotify.Player.SkipNext(); <- not working when more than one item were added to queue...
        }

        private static async Task GetCurrentTrackInfo(SpotifyClient spotifyClient)
        {
            var currentlyPlayingRequest = new PlayerCurrentlyPlayingRequest(PlayerCurrentlyPlayingRequest.AdditionalTypes.Track);
            var currentlyPlaying = await spotifyClient.Player.GetCurrentlyPlaying(currentlyPlayingRequest);
            if (currentlyPlaying != null && currentlyPlaying.IsPlaying)
            {
                var fullTrack = ((FullTrack)currentlyPlaying.Item);
                Console.Write($"'{fullTrack.Artists[0].Name} - {fullTrack.Name}' -> {fullTrack.ExternalUrls["spotify"]}");
            }
        }

        private static async Task GetNextTrackInfo(SpotifyClient spotifyClient)
        {
            var queueResponse = await spotifyClient.Player.GetQueue();
            if (queueResponse != null && queueResponse.Queue.Count > 0)
            {
                var fullTrack = ((FullTrack)queueResponse.Queue[0]);
                Console.Write($"'{fullTrack.Artists[0].Name} - {fullTrack.Name}' -> {fullTrack.ExternalUrls["spotify"]}");
            }
        }

        private static async Task GetRecentlyPlayed(SpotifyClient spotifyClient)
        {
            var recentlyPlayedItems = await spotifyClient.Player.GetRecentlyPlayed();
            if (recentlyPlayedItems == null || recentlyPlayedItems.Items.Count == 0) return;

            var fullTrack = recentlyPlayedItems.Items[0].Track;
            Console.Write($"'{fullTrack.Artists[0].Name} - {fullTrack.Name}' -> {fullTrack.ExternalUrls["spotify"]}");
        }

        private static async Task ToggleShuffle(SpotifyClient spotifyClient, CurrentlyPlayingContext currentPlayback)
        {
            var playerShuffleRequest = new PlayerShuffleRequest(!currentPlayback.ShuffleState);
            await spotifyClient.Player.SetShuffle(playerShuffleRequest);
        }

        private static async Task SwitchRepeat(SpotifyClient spotifyClient, CurrentlyPlayingContext currentPlayback)
        {
            var currentRepeatState = currentPlayback.RepeatState;
            var targetRepeatState = PlayerSetRepeatRequest.State.Off;
            switch (currentRepeatState)
            {
                case "off":
                    targetRepeatState = PlayerSetRepeatRequest.State.Track;
                    break;
                case "track":
                    targetRepeatState = PlayerSetRepeatRequest.State.Context;
                    break;
            }
            var playerRepeatRequest = new PlayerSetRepeatRequest(targetRepeatState);
            await spotifyClient.Player.SetRepeat(playerRepeatRequest);
        }

        private static async Task ToggleMute(SpotifyClient spotifyClient, CurrentlyPlayingContext currentPlayback)
        {
            var currentVolume = currentPlayback.Device.VolumePercent;

            if (currentVolume.HasValue && currentVolume.Value > 0)
            {
                await File.WriteAllTextAsync(Path.Combine(System.AppContext.BaseDirectory, "currentVolume.txt"), currentVolume.Value.ToString());
                await spotifyClient.Player.SetVolume(new PlayerVolumeRequest(0));
                Console.Write("0");
            }
            if (!currentVolume.HasValue || currentVolume.Value == 0)
            {
                var volume = await File.ReadAllTextAsync(Path.Combine(System.AppContext.BaseDirectory, "currentVolume.txt"));
                await spotifyClient.Player.SetVolume(new PlayerVolumeRequest(int.Parse(volume)));
                Console.Write(volume);
            }
        }

        private static async Task ChangeVolume(SpotifyClient spotifyClient, CurrentlyPlayingContext currentPlayback, string payload)
        {
            var currentVolume = currentPlayback.Device.VolumePercent;

            // null guard
            if (currentVolume == null) return;

            // no int guard
            int volumeRequest;
            if (!int.TryParse(payload, out volumeRequest)) return;

            // already muted guard
            if ((!currentVolume.HasValue || currentVolume.Value == 0) && volumeRequest < 1)
            {
                Console.Write("0");
                return;
            }

            // already max volume guard
            if ((currentVolume.HasValue && currentVolume.Value == 100) && volumeRequest > 0)
            {
                Console.Write("100");
                return;
            }

            var newVolume = currentVolume.Value + volumeRequest;

            // set max
            if (newVolume > 99)
            {
                await spotifyClient.Player.SetVolume(new PlayerVolumeRequest(100));
                Console.Write("100");
                return;
            }

            // set mute
            if (newVolume < 1)
            {
                await spotifyClient.Player.SetVolume(new PlayerVolumeRequest(0));
                Console.Write("0");
                return;
            }

            // change volume
            await spotifyClient.Player.SetVolume(new PlayerVolumeRequest(newVolume));
            Console.Write(newVolume);
        }

        private static async Task GetCurrentPlaylist(SpotifyClient spotifyClient, CurrentlyPlayingContext currentPlayback)
        {
            if (currentPlayback.Context == null || currentPlayback.Context.Type != "playlist")
            {
                Console.Write("404 - No playlist in use!");
                return;
            }

            var playListId = currentPlayback.Context.Uri.Replace("spotify:playlist:", "");
            var currentPlaylist = await spotifyClient.Playlists.Get(playListId);

            if (currentPlaylist == null)
            {
                Console.Write("404 - Playlist not found!");
                return;
            }

            // Bug in SpotifyAPi.Web library? Property "Public" is always false...
            //string urlString = !currentPlaylist.Public.Value ? "(private)" : $" -> {currentPlaylist.ExternalUrls["spotify"]}";
            string urlString = $" -> {currentPlaylist.ExternalUrls["spotify"]}";
            Console.Write($"'{currentPlaylist.Name.Trim()}' by {currentPlaylist.Owner.DisplayName.Trim()} {urlString}");
        }
    }
}