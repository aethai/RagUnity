# RagUnity

RagUnity is a Unity client for Ragnarok Online on Windows. It reads maps, models, sprites, and effects from the game archives you already use, then connects to your Hercules server for login, character selection, and map play.

## Run in the Unity Editor

You need Windows, Unity **6000.3.14f1**, your own `data.grf`, and an account on a compatible Hercules server. The supplied server profile targets **PACKETVER 20240619**.

1. Clone the repository and open it in PowerShell.

   ```powershell
   git clone https://github.com/aethai/RagUnity.git
   cd RagUnity
   ```

2. Edit [server-profile.cfg](Unity/Assets/StreamingAssets/Ragnarok/server-profile.cfg). Set `host` to your server's hostname. Check all three ports against your server configuration: `loginPort` handles sign-in, `charPort` handles character selection, and `mapPort` handles entry into the world.

3. Point the client at your game data and launch Unity from the same PowerShell window. Adjust the editor path if you installed Unity outside the default Hub location.

   ```powershell
   $env:RO_CONTENT_ROOT = Read-Host 'Folder containing data.grf'
   $env:RO_GRF_FILES = 'data.grf'
   & "$env:ProgramFiles/Unity/Hub/Editor/6000.3.14f1/Editor/Unity.exe" -projectPath (Resolve-Path Unity).Path
   ```

4. Wait for Unity to import the project. In the Project window, open `Assets/Scenes/RORuntimeMap.unity`, press **Play**, and sign in with your game account.

### Game data

Keep GRF archives outside the source tree. If your server supplies a patch archive, put it in the same folder as `data.grf` and list its actual filename first. For an archive called `patch.grf`, set this before launching Unity:

```powershell
$env:RO_GRF_FILES = 'patch.grf;data.grf'
```

For a standalone Windows build, the default archive location is `ROData/data.grf` beside the executable. The same environment variables can override that location.

## Finding your way around the source

| If you want to work on… | Start here |
| --- | --- |
| Reading Ragnarok file formats | [ROIO loaders](Unity/Assets/Ragnarok/ROIO/Loaders) |
| Terrain, map models, and walkability | [Maps](Unity/Assets/Ragnarok/Runtime/Maps) |
| Login, character selection, and map packets | [Network](Unity/Assets/Ragnarok/Runtime/Network) |
| In-game windows and input | [UI](Unity/Assets/Ragnarok/Runtime/UI) |
| Attack timing and skill visuals | [Combat](Unity/Assets/Ragnarok/Runtime/Combat) and [Effects](Unity/Assets/Ragnarok/Runtime/Effects) |

## Troubleshooting

- **Unity cannot find the game data.** Check that `RO_CONTENT_ROOT` names the folder containing the archives and that every entry in `RO_GRF_FILES` exists there. If Unity was already open when you set the variables, close it and start it from that PowerShell window.
- **Login works, but character selection or map entry fails.** Check the port for the failing stage and confirm that the addresses advertised by your server are reachable from the client machine.
