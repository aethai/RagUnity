# Ragnarok Online client for Unity

A Windows client for Ragnarok Online. To run it, you need Unity **6000.3.14f1**, your own `data.grf` archive, and an account on a compatible Hercules server (PACKETVER 20240619).

1. Clone this repository.
2. Open `Unity/Assets/StreamingAssets/Ragnarok/server-profile.cfg` and enter your server's hostname and ports.
3. Open PowerShell in the repository root and run the commands below. When prompted, enter the folder containing `data.grf`.

```powershell
$env:RO_CONTENT_ROOT = Read-Host 'Folder containing data.grf'
$env:RO_GRF_FILES = 'data.grf'
& "$env:ProgramFiles/Unity/Hub/Editor/6000.3.14f1/Editor/Unity.exe" -projectPath (Resolve-Path Unity).Path
```

If your server requires an additional GRF archive, list it before `data.grf` in `RO_GRF_FILES`, separating the filenames with a semicolon.

Once Unity finishes importing the project, open `Assets/Scenes/RORuntimeMap.unity`, click **Play**, and sign in.
