$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$names = @(
    'TownDialogueController', 'TownMissionResidentDialogueController', 'TownConversationController',
    'DialogueActor2D', 'DialoguePresenter2D', 'PawnShopUIController', 'MerchantPurchaseController',
    'SellCartInventory', 'ShopSellTransactionController', 'TownUnifiedTradeScreen2D',
    'ExpeditionUnifiedTradeController2D', 'DungeonNpcShop2D', 'CaveExitElevator2D',
    'ExpeditionResultCollector2D'
)
$guidToName = @{}
$usage = @{}
foreach ($name in $names) { $usage[$name] = [Collections.Generic.List[string]]::new() }
Get-ChildItem (Join-Path $projectRoot 'Assets') -Recurse -Filter '*.cs.meta' | ForEach-Object {
    $name = $_.BaseName.Replace('.cs', '')
    if ($name -in $names) {
        $match = [regex]::Match([IO.File]::ReadAllText($_.FullName), '(?m)^guid: ([a-f0-9]+)')
        if ($match.Success) { $guidToName[$match.Groups[1].Value] = $name }
    }
}
Get-ChildItem (Join-Path $projectRoot 'Assets') -Recurse -File | Where-Object {
    $_.Extension -in '.unity', '.prefab' -and $_.FullName -notmatch '\\(Backups|_Recovery|Samples)\\'
} | ForEach-Object {
    $asset = $_
    $scriptMatches = [regex]::Matches([IO.File]::ReadAllText($asset.FullName),
        'm_Script: \{fileID: 11500000, guid: ([a-f0-9]+)')
    $groups = $scriptMatches | ForEach-Object { $guidToName[$_.Groups[1].Value] } |
        Where-Object { $_ } | Group-Object
    foreach ($group in $groups) {
        $relative = $asset.FullName.Substring($projectRoot.Length + 1).Replace('\', '/')
        $usage[$group.Name].Add($relative + ' (' + $group.Count + ')')
    }
}
$report = [Collections.Generic.List[string]]::new()
$report.Add('# シーン・Prefabの直接コンポーネント参照')
$report.Add('')
$report.Add('Tools/UpdateComponentUsage.ps1による静的走査。括弧内は直接記録されたコンポーネント数。非Activeも含む。Prefab継承・実行時生成・UnityEvent・コード内検索は展開しないため、参照なしは未使用の証明ではない。Backups/_Recovery/Samplesは除外。')
foreach ($name in $names) {
    $report.Add('')
    $report.Add('## ' + $name)
    $report.Add('')
    if ($usage[$name].Count -eq 0) { $report.Add('直接参照なし。') }
    else { foreach ($entry in $usage[$name]) { $report.Add('- ' + $entry) } }
}
$destination = Join-Path $projectRoot 'Docs/SceneComponentUsage.md'
[IO.File]::WriteAllLines($destination, $report, [Text.UTF8Encoding]::new($false))
Write-Output "Updated: $destination"
