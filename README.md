# RagnarokUnity

Klient Ragnarok Online w Unity dla Windows.

1. Zainstaluj Unity **6000.3.14f1** i sklonuj repozytorium.
2. Przygotuj własne archiwum `data.grf` i konto na zgodnym serwerze Hercules (PACKETVER 20240619).
3. W `Unity/Assets/StreamingAssets/Ragnarok/server-profile.cfg` wpisz nazwę hosta i porty swojego serwera.
4. W katalogu repozytorium uruchom PowerShell. Podaj katalog swoich danych gry i uruchom edytor.

```powershell
$env:RO_CONTENT_ROOT = Read-Host 'Katalog zawierający data.grf'
$env:RO_GRF_FILES = 'data.grf'
& "$env:ProgramFiles/Unity/Hub/Editor/6000.3.14f1/Editor/Unity.exe" -projectPath (Resolve-Path Unity).Path
```

Po zaimportowaniu projektu otwórz scenę `Assets/Scenes/RORuntimeMap.unity`, kliknij **Play** i zaloguj się. Jeśli Twój serwer wymaga dodatkowego archiwum GRF, wpisz je przed `data.grf` w `RO_GRF_FILES`, oddzielając nazwy średnikiem.
