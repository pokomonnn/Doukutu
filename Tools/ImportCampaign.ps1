$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$sources = Get-ChildItem (Join-Path $root '脚本') -Recurse -Filter *.txt | Where-Object {
    $_.DirectoryName -notmatch '一般会話' -and $_.Name -notmatch '^99|README|一覧|メモ|ルート|条件'
}
$nodes = [System.Collections.Generic.List[object]]::new()
foreach ($file in $sources) {
    $stage = if ($file.Directory.Name -match '１') {1} elseif ($file.Directory.Name -match '２') {2} elseif ($file.Directory.Name -match '３') {3} else {4}
    $quest = if ($file.Name -match 'サブ0([1-3])') {"S${stage}S0$($Matches[1])"} elseif ($file.Name -match 'S\d-S0([1-3])') {"S${stage}S0$($Matches[1])"} else {"S${stage}M01"}
    $node = $null
    foreach ($line in Get-Content -LiteralPath $file.FullName -Encoding utf8) {
        if ($line -cmatch '^[A-Z][A-Z0-9_]+$') {
            $node = @{id="$quest/$line"; source=$file.FullName.Substring($root.Length+1); lines=[System.Collections.Generic.List[string]]::new(); choices=[System.Collections.Generic.List[object]]::new()}
            $nodes.Add($node)
        } elseif ($line -match '^【|^＝') { $node = $null }
        elseif ($null -ne $node -and $line -match '^［選択肢］\s*(.+?)\s*→\s*([A-Z][A-Z0-9_]+)\s*$') {
            $node.choices.Add(@{label=$Matches[1].Trim('「','」'); target=if($Matches[2] -eq 'END') {'END'} else {"$quest/$($Matches[2])"}})
        }
        elseif ($null -ne $node -and ($line -match '^[^［【※・\s][^：]*：「' -or $line -match '^［(地の文|記録|確認メッセージ)］')) {
            $node.lines.Add(($line -replace '^［(地の文|記録|確認メッセージ)］',''))
        }
    }
}
# Each row is one explicitly authored, reviewable progression transaction.
# Dependencies: comma = AND, vertical bar = OR. Narrative items use persistent records.
$rows = @'
S1M01|消えた遠征隊の足跡|diary|1|30|古い図書室||DUNGEON_DIARY|S1M01_ClueFound|0
S1M01|消えた遠征隊の足跡|report1|0|0|町長に日誌を報告|S1M01_ClueFound|MAYOR_REPORT|S1M01_Completed|100
S1S01|戻らぬ仲間|finn|1|16|負傷したフィン||FINN_FOUND,FINN_AGREE||0
S1S01|戻らぬ仲間|rescue|1|3|フィンと帰還する|step_finn|FINN_ELEVATOR,TOWN_FINN_RESCUED|S1S01_Rescued|0
S1S01|戻らぬ仲間|reportFinn|0|0|ロアンに救出を報告|S1S01_Rescued|ROAN_SUCCESS|S1S01_Completed|100
S1S02|病に伏した仲間|medicine|1|42|旧診療所の薬||CLINIC_MEDICINE|S1S02_MedicineTaken|0
S1S02|病に伏した仲間|deliverMedicine|1|10|バルドに薬を渡す|S1S02_MedicineTaken|BALDO_DELIVER,BALDO_REWARD|S1S02_Completed,PlayerBanditTruce|100
S1S03|古城の設計図|blueprint|1|56|資料室の設計図||ARCHIVE_TAKE|S1S03_BlueprintTaken|0
S1S03|古城の設計図|reportBlueprint|0|0|グレンに設計図を渡す|S1S03_BlueprintTaken|GLEN_HANDOVER,GLEN_SUMMARY,GLEN_REWARD|S1S03_Completed|100
S2M01|封鎖された昇降路|machine|2|60|機械室の記録箱||S2_RECORD_READ,S2_RECORD_TAKE|S2M01_ClueFound|0
S2M01|封鎖された昇降路|report2|0|0|町長に起動手順を報告|S2M01_ClueFound|MAYOR_S2_REPORT|S2M01_Completed|100
S2S01|奪われた補給物資|original|2|23|奪われた補給箱||SUPPLY_RECLAIM|S2S01_Goods|0
S2S01|奪われた補給物資|alternative|2|45|旧配給所の代替物資||OLD_RATION_TAKE|S2S01_Goods|0
S2S01|奪われた補給物資|reportSupply|0|0|ロアンに補給物資を渡す|S2S01_Goods|ROAN_S2_DELIVER|S2S01_Completed|100
S2S02|新たな稼ぎ口|ledger|2|36|倉庫の搬入記録||S2_WAREHOUSE_TAKE|S2S02_LedgerFound|0
S2S02|新たな稼ぎ口|permit|0|0|町長と入町許可を交渉|S2S02_LedgerFound,PlayerBanditTruce|MAYOR_S2_BANDIT_FIRST,MAYOR_S2_BANDIT_EXPLAIN,MAYOR_S2_BANDIT_REVEAL,MAYOR_S2_BANDIT_ARGUE|S2S02_PermitIssued|0
S2S02|新たな稼ぎ口|reportPermit|2|10|バルドに許可証を渡す|S2S02_PermitIssued|BALDO_S2_PERMIT_PRESENT,BALDO_S2_SUCCESS|S2S02_Completed,PlayerBanditFriendly|100
S2S03|止まった水路|bearing|2|17|水車の軸受け||S2_PART_1_TAKE|S2S03_BearingFound|0
S2S03|止まった水路|valve|2|30|配管の弁||S2_PART_2_TAKE|S2S03_ValveFound|0
S2S03|止まった水路|gear|2|52|予備の歯車||S2_PART_3_TAKE|S2S03_GearFound|0
S2S03|止まった水路|install|2|67|グレンに部品を渡す|S2S03_BearingFound,S2S03_ValveFound,S2S03_GearFound|GLEN_S2_READY,GLEN_S2_INSTALL|S2S03_Installed|0
S2S03|止まった水路|water|2|73|修理済みの水門|S2S03_Installed|S2_WATER_GATE_ACTIVATE|S2S03_WaterRestored|0
S2S03|止まった水路|reportWater|0|0|グレンに水路復旧を報告|S2S03_WaterRestored|GLEN_S2_SUCCESS|S2S03_Completed|100
S3M01|失われた巡礼路|map|3|66|巡礼路の経路図||S3_MAP_READ,S3_MAP_TAKE|S3M01_ClueFound|0
S3M01|失われた巡礼路|report3|0|0|町長に経路図を報告|S3M01_ClueFound|MAYOR_S3_REPORT|S3M01_Completed|100
S3S01|争いの終わる場所|route|3|22|裏方用の通路||S3_S01_ROUTE_DISCOVERED|S3S01_RouteFound|0
S3S01|争いの終わる場所|proposal|0|0|ロアンに迂回路を提案|S3S01_RouteFound|ROAN_S3_ROUTE_FOUND,ROAN_S3_PROPOSAL|S3S01_Proposal|0
S3S01|争いの終わる場所|truce|3|12|バルドと通路の停戦交渉|S3S01_Proposal|BALDO_S3_TRUCE_TALK,BALDO_S3_TRUCE_SAFETY|S3S01_BaldoAgreed|0
S3S01|争いの終わる場所|reportTruce|0|0|ロアンに合意を報告|S3S01_BaldoAgreed|ROAN_S3_SUCCESS|S3S01_Completed,faction_adventurer_support|100
S3S02|失われた交易路|trade|3|42|地上への交易路||S3_S02_LEDGER_READ,S3_S02_TRACKS,S3_S02_SLOPE,S3_S02_CLEAR,S3_S02_EXIT_FOUND|S3S02_RouteFound|0
S3S02|失われた交易路|reportTrade|3|12|バルドに交易路を報告|S3S02_RouteFound,S2S02_Completed|BALDO_S3_SUCCESS|S3S02_Completed,faction_bandit_support|100
S3S03|眠れる古城|castleLedger|3|52|古城の修繕台帳||S3_S03_LEDGER_READ,S3_S03_LEDGER_TAKE|S3S03_LedgerFound|0
S3S03|眠れる古城|structure|3|33|回廊の構造調査||S3_S03_STRUCTURE_INSPECT|S3S03_StructureChecked|0
S3S03|眠れる古城|reportCastle|0|0|グレンに再建調査を報告|S3S03_LedgerFound,S3S03_StructureChecked,S2S03_WaterRestored|GLEN_S3_SUCCESS|S3S03_Completed,faction_dwarf_support|100
S4M01|消えた貴族|garden|4|40|庭の貴族と話す||S4_GARDEN_FIRST,S4_LORD_IDENTIFY,S4_LORD_REUNION,S4_WIFE_TRUTH,S4_GODDESS_FIRST,S4_GODDESS_PLACE,S4_REUNION_TALK,S4_TOWN_REPORT,S4_LORD_REGRET|S4M01_StorySeen,S4M01_TownTruthTold,S4M01_GoddessMet|0
'@
$steps = foreach ($row in $rows -split '\r?\n') {
    $p = $row.Split('|')
    @{ quest=$p[0]; questTitle=$p[1]; id=$p[2]; stage=[int]$p[3]; x=[float]$p[4]; title=$p[5]; requires=@($p[6].Split(',',[StringSplitOptions]::RemoveEmptyEntries)); nodes=@($p[7].Split(',') | ForEach-Object {"$($p[0])/$_"}); flags=@($p[8].Split(',',[StringSplitOptions]::RemoveEmptyEntries)); reward=[int]$p[9] }
}
$offers = @('S1M01/MAYOR_OFFER','S1S01/ROAN_FIRST','S1S02/BALDO_REQUEST','S1S03/GLEN_REQUEST','S2M01/MAYOR_S2_FIRST','S2S01/ROAN_S2_FIRST','S2S02/BALDO_S2_REQUEST','S2S03/GLEN_S2_REQUEST','S3M01/MAYOR_S3_FIRST','S3S01/ROAN_S3_FIRST','S3S02/BALDO_S3_FIRST_EARLY','S3S03/GLEN_S3_FIRST_DEFAULT')
$ids = @{}; foreach ($n in $nodes) { if ($ids.ContainsKey($n.id)) {throw "Duplicate $($n.id)"}; $ids[$n.id]=$n }
foreach ($s in $steps) { foreach ($id in $s.nodes) { if (!$ids.ContainsKey($id) -or $ids[$id].lines.Count -eq 0) {throw "Missing dialogue $id"} } }
$data = @{version=1; nodes=@($nodes); steps=@($steps); offers=$offers}
$dest = Join-Path $root 'Assets/Resources/Campaign/Story.json'
[IO.File]::WriteAllText($dest, ($data | ConvertTo-Json -Depth 12), [Text.UTF8Encoding]::new($false))
Write-Output "Imported $($nodes.Count) dialogue nodes; $($steps.Count) progression steps."
