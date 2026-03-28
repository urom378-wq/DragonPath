# ============================================================
#  DragonPath プロジェクト セットアップスクリプト
#  使い方:
#    1. このファイルを DragonPath フォルダ（Assets と同じ場所）に置く
#    2. PowerShell を管理者で開く
#    3. cd C:\Users\あなたの名前\Desktop\DragonPath
#    4. .\setup_project.ps1
# ============================================================

Write-Host ""
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "  DragonPath プロジェクト セットアップ開始" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

# 現在地を確認
$projectRoot = Get-Location
Write-Host "プロジェクトフォルダ: $projectRoot" -ForegroundColor Yellow

# Assets フォルダが存在するか確認
if (-not (Test-Path "Assets")) {
    Write-Host "ERROR: Assets フォルダが見つかりません。" -ForegroundColor Red
    Write-Host "DragonPath フォルダ内で実行してください。" -ForegroundColor Red
    Write-Host "例: cd C:\Users\Taro\Desktop\DragonPath" -ForegroundColor Yellow
    exit 1
}

Write-Host "Assets フォルダを確認... OK" -ForegroundColor Green
Write-Host ""

# ============================================================
#  フォルダ構成を作成
# ============================================================

$folders = @(
    "Assets\Scripts\Core",
    "Assets\Scripts\Combat",
    "Assets\Scripts\Items",
    "Assets\Scripts\Skills",
    "Assets\Scripts\UI",
    "Assets\Scripts\Dialogue",
    "Assets\Scripts\Shop",
    "Assets\Scripts\Title",
    "Assets\Scripts\Quest",
    "Assets\Scripts\Tutorial",
    "Assets\Scripts\Map",
    "Assets\Scripts\Audio",
    "Assets\Scripts\Effects",
    "Assets\Scripts\Mobile",
    "Assets\Scripts\Steam",
    "Assets\Scripts\Data",
    "Assets\Scripts\Debug",
    "Assets\Editor",
    "Assets\Scenes",
    "Assets\Prefabs\Player",
    "Assets\Prefabs\Enemies",
    "Assets\Prefabs\UI",
    "Assets\Prefabs\Effects",
    "Assets\ScriptableObjects\Items",
    "Assets\ScriptableObjects\Skills",
    "Assets\ScriptableObjects\Quests",
    "Assets\ScriptableObjects\Dialogues",
    "Assets\ScriptableObjects\Shops",
    "Assets\ScriptableObjects\Audio",
    "Assets\ScriptableObjects\Effects",
    "Assets\Audio\BGM",
    "Assets\Audio\SE",
    "Assets\Materials",
    "Assets\Textures\UI",
    "Assets\Textures\Characters",
    "Assets\Fonts",
    ".github\workflows"
)

Write-Host "フォルダを作成しています..." -ForegroundColor Cyan

foreach ($folder in $folders) {
    if (-not (Test-Path $folder)) {
        New-Item -ItemType Directory -Path $folder -Force | Out-Null
        Write-Host "  作成: $folder" -ForegroundColor Green
    } else {
        Write-Host "  スキップ（既存）: $folder" -ForegroundColor Gray
    }
}

Write-Host ""

# ============================================================
#  .gitkeep ファイルを作成（空フォルダを Git で追跡するため）
# ============================================================

$emptyFolders = @(
    "Assets\Prefabs\Player",
    "Assets\Prefabs\Enemies",
    "Assets\Prefabs\UI",
    "Assets\Prefabs\Effects",
    "Assets\Audio\BGM",
    "Assets\Audio\SE",
    "Assets\Materials",
    "Assets\Textures\UI",
    "Assets\Textures\Characters",
    "Assets\Fonts"
)

foreach ($folder in $emptyFolders) {
    $gitkeep = "$folder\.gitkeep"
    if (-not (Test-Path $gitkeep)) {
        New-Item -ItemType File -Path $gitkeep -Force | Out-Null
    }
}

# ============================================================
#  スクリプトファイルの配置先マッピング
# ============================================================

Write-Host "スクリプトの配置先マッピングを生成..." -ForegroundColor Cyan
Write-Host ""

$scriptMap = @{
    # Core
    "GameManager.cs"          = "Assets\Scripts\Core"
    "PlayerStats.cs"          = "Assets\Scripts\Core"
    "PlayerController.cs"     = "Assets\Scripts\Core"
    # Combat
    "PlayerAttack.cs"         = "Assets\Scripts\Combat"
    "EnemyAI.cs"              = "Assets\Scripts\Combat"
    "BossEnemy.cs"            = "Assets\Scripts\Combat"
    # Items
    "ItemData.cs"             = "Assets\Scripts\Items"
    "Inventory.cs"            = "Assets\Scripts\Items"
    # Skills
    "SkillManager.cs"         = "Assets\Scripts\Skills"
    # UI
    "UIManager.cs"            = "Assets\Scripts\UI"
    "InventoryUI.cs"          = "Assets\Scripts\UI"
    "SkillUI.cs"              = "Assets\Scripts\UI"
    # Dialogue
    "DialogueManager.cs"      = "Assets\Scripts\Dialogue"
    # Shop
    "ShopManager.cs"          = "Assets\Scripts\Shop"
    # Title
    "TitleScreen.cs"          = "Assets\Scripts\Title"
    # Quest
    "QuestManager.cs"         = "Assets\Scripts\Quest"
    # Tutorial
    "TutorialManager.cs"      = "Assets\Scripts\Tutorial"
    # Map
    "MapTransitionManager.cs" = "Assets\Scripts\Map"
    # Audio
    "AudioManager.cs"         = "Assets\Scripts\Audio"
    # Effects
    "EffectManager.cs"        = "Assets\Scripts\Effects"
    # Mobile
    "MobileUIManager.cs"      = "Assets\Scripts\Mobile"
    # Steam
    "SteamManager.cs"         = "Assets\Scripts\Steam"
    "SteamAchievements.cs"    = "Assets\Scripts\Steam"
    "SteamCloudSave.cs"       = "Assets\Scripts\Steam"
    "SteamLeaderboard.cs"     = "Assets\Scripts\Steam"
    "SteamRichPresence.cs"    = "Assets\Scripts\Steam"
    # Data
    "SaveLoadManager.cs"      = "Assets\Scripts\Data"
    # Debug
    "GameTestRunner.cs"       = "Assets\Scripts\Debug"
    "DebugOverlay.cs"         = "Assets\Scripts\Debug"
    # Editor（Assets\Editor に置くこと！）
    "BuildChecker.cs"         = "Assets\Editor"
}

# ダウンロードフォルダからのコピーを試みる
$downloadFolder = "$env:USERPROFILE\Downloads"
$copied = 0
$missing = 0

foreach ($script in $scriptMap.Keys) {
    $src  = "$downloadFolder\$script"
    $dest = "$($scriptMap[$script])\$script"

    if (Test-Path $src) {
        Copy-Item -Path $src -Destination $dest -Force
        Write-Host "  コピー完了: $script → $($scriptMap[$script])" -ForegroundColor Green
        $copied++
    } else {
        Write-Host "  未検出: $script（手動配置が必要）" -ForegroundColor Yellow
        $missing++
    }
}

Write-Host ""

# ============================================================
#  steam_appid.txt を作成
# ============================================================

if (-not (Test-Path "steam_appid.txt")) {
    "480" | Out-File -FilePath "steam_appid.txt" -Encoding ASCII
    Write-Host "steam_appid.txt を作成しました（テスト用 ID: 480）" -ForegroundColor Green
}

# ============================================================
#  結果サマリー
# ============================================================

Write-Host ""
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "  セットアップ完了！" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "フォルダ作成: $($folders.Count) 件" -ForegroundColor Green
Write-Host "スクリプト自動コピー: $copied 件" -ForegroundColor Green

if ($missing -gt 0) {
    Write-Host "手動配置が必要: $missing 件" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "手動配置が必要なスクリプトの配置先:" -ForegroundColor Yellow

    foreach ($script in $scriptMap.Keys) {
        $dest = "$($scriptMap[$script])\$script"
        if (-not (Test-Path $dest)) {
            Write-Host "  $script  →  $($scriptMap[$script])" -ForegroundColor Yellow
        }
    }
}

Write-Host ""
Write-Host "次のステップ:" -ForegroundColor Cyan
Write-Host "  1. Unity エディタに戻り Assets フォルダを確認" -ForegroundColor White
Write-Host "  2. このターミナルで git init を実行" -ForegroundColor White
Write-Host "  3. git add . → git commit → git push" -ForegroundColor White
Write-Host ""
