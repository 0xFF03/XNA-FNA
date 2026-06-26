$outputFile = "ProjectCodebase.txt"
if (Test-Path $outputFile) { Remove-Item $outputFile }

# Simply add or remove a '#' at the start of a line to toggle folders
$targetFolders = @(
    "Engine/Audio"
    "Engine/Core"
    "Engine/Debug"
    "Engine/Platform"
    "Engine/Platform/Networking"
    "Engine/Platform/UI"
    "Engine/StandardModules/Physics2D"
    "Engine/StandardModules/Multiplayer"
    "Engine/StandardModules/Combat"
    "Engine/StandardModules/Rendering2D"
    "Engine/StandardModules/WorldGen"
    "Engine/States"
    "Game/Core"
    "Game/Logic"
    "Game/Prefabs"
    "Game/Registries"
    "Game/UIStates"
)

foreach ($folder in $targetFolders) {
    if (Test-Path $folder) {
        Get-ChildItem -Path $folder -Recurse -Filter *.cs | Where-Object {
            $_.FullName -notmatch "\\bin\\" -and $_.FullName -notmatch "\\obj\\"
        } | ForEach-Object {
            $relativePath = Resolve-Path -Path $_.FullName -Relative

            # Clean paths to match standard Windows file layout format
            $cleanPath = $relativePath.Replace(".\", "").Replace("/", "\")

            "--- FILE: $cleanPath ---" | Out-File -FilePath $outputFile -Append -Encoding utf8

            $content = [System.IO.File]::ReadAllText($_.FullName)
            $content | Out-File -FilePath $outputFile -Append -Encoding utf8
            "`n" | Out-File -FilePath $outputFile -Append
        }
    }
}