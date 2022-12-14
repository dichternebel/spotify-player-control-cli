# Spotify Player Control CLI [![Licensed under the MIT License](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/dichternebel/spotify-player-control-cli/blob/main/LICENSE)
Control your active Spotify player with this .Net Core CLI.

This .Net Core console app allows you to control your currently active Spotify player. It gives back some information to be used for example as a chat reponse on your Twitch channel.
It works standalone or being called by another application like Streamer.bot, SAMMI, StreamDeck etc.

## Prerequisites

- A Spotify Premium account.
- A registered [Spotify app](https://developer.spotify.com/documentation/general/guides/authorization/app-settings/) with callback `http://localhost:5000/spotifyCallback` and it's ClientID.
- This thing downloaded from the [releases section](https://github.com/dichternebel/spotify-player-control-cli/releases).

## Setting things up
Once you downloaded the zip file extract the two files to wherever you want. Might be e.g. `C:\Tools\SpotifyPlayerControl\`.
Now open the `SpotifyPlayerControl.dll.config` e.g. with Notepad/Editor and paste your Spotify ClientID into the XML document like this:
```xml
<?xml version="1.0" encoding="utf-8" ?>
<configuration>
  <appSettings>
    <add key="ClientID" value="01234567890abcdefghijklmnopqrst"/>
  </appSettings>
</configuration>
```
Save the file and now open `cmd` or `terminal` at the same location and run the application once by typing `SpotifyPlayerControl.exe` and hit `enter`.

You should now be prompted to allow the app to control your player. Just agree. This is OAuth2: Only the app itself is able to do things! You can revoke that anytime if you want to.

If everything went smooth, you will see a new file called `credentials.json` where your current access token is stored.
This is done in order to reduce the authentication roundtrips and you should not be prompted anymore unless you delete the file.

## Using it 

- Start your Spotify player: the desktop app, the web player in a browser, or the app on your phone. Whatever!
- Play a song.
- Give Spotify a few seconds to register your player as an active device.

## Stand-alone usage
- Go to the `cmd` and type e.g. `SpotifyPlayerControl.exe !song` to get information about the currently played track.

## Usage with with Streamer.bot
- Go to the [releases](https://github.com/dichternebel/spotify-player-control-cli/releases) and download the Streamer.bot export
- Import it into Streamer.bot and you will get three entries to the *Commands* tab and one to the *Actions* tab
- **Attention:** The imported Commands are disabled by default! You have to enable them manually!
- Go to the *Actions* tab and just change the Sub-Action `Set argument %path%` in the command called `TwitchSpotifyPlayerControl` to point to the location of `SpotifyPlayerControl.exe`
- Go to your Twitch chat and send e.g. `!song` to get information about the currently played track.

## Usage with SAMMI
- Go to [releases](https://github.com/dichternebel/spotify-player-control-cli/releases) and download the SAMMI deck export
- Import the json as a new deck in SAMMI
- Open the darkened "SpotifyPlayerControl" button and change the "Set Local Variable" named `path` to point to the location of `SpotifyPlayerControl.exe`
- Hit `Save & Close`
- Open SAMMI Deck and press one of the lighter buttons to trigger an action

## Supported Commands
_Hint:_ To use the commands in `cmd` the second argument has to be quoted like `!play "Where is my mind?"`. This is not needed in the Twitch chat. There you can just send `!play Where is my mind?`

| Purpose          | Command Example                                                                   | Output example                                                        |
|:-----------------|:----------------------------------------------------------------------------------|:----------------------------------------------------------------------|
| Queue a song     | `!play peace orchestra-double drums` or `!queue...`                               | 'Peace Orchestra - Double Drums' |
| Queue a song     | `!play https://open.spotify.com/track/3whUFHX7grfr61GiaymK4p?si=76e406b024084334` | 'Peace Orchestra - Double Drums' |
| Queue a song     | `!play where is my mind`                                                          | 'Pixies - Where is my mind?' |
| Get current song | `!song` or `!music`                                                               | 'Artist - Title' -> https://open.spotify.com/track/12345... |
| Get next song    | `!next`                                                                           | 'Artist - Title' -> https://open.spotify.com/track/12345... |
| Get recent song  | `!recent`                                                                         | 'Artist - Title' -> https://open.spotify.com/track/12345... |
| Get playlist     | `!playlist`                                                                       | 'Playlistname' by Owner -> https://open.spotify.com/playlist/12345... |
| Resume           | `!start` or `!resume`||
| Pause            | `!pause`||
| Skip to next     | `!skip`||
| Skip to previous | `!back` or `!prev`||
| Shuffle playback | `!shuffle` (_warning_: weird errors may occur!)||
| Switch Repeat    | `!repeat` (_warning_: weird errors may occur!)||
| Toggle Mute      | `!mute`                                                                           | 0 or 100 |
| Change Volume    | `!vol -10` or `!volume -10` to decrease by 10                                     | `current Volume` |

 ## How is this working and why?

 This thing is just a wrapper using the [SpotifyAPI-NET](https://github.com/JohnnyCrazy/SpotifyAPI-NET) library by JohnnyCrazy.
 There's not much happening here except that it tries to work around some limitations of the Spotify Web API itself.

 It is implementing only a subset of the library features focussing on the player controls. Therefore the naming.
