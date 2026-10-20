from pathlib import Path
import re, subprocess
root = Path(__file__).resolve().parents[1]
removed = subprocess.check_output(["git", "-c", "core.quotepath=false", "-c", "core.safecrlf=false", "diff", "--name-only", "--diff-filter=D"], cwd=root).decode("utf-8").splitlines()
guids = {}
for p in removed:
    if not p.endswith(".meta") or any(x in p for x in ("MerchantShop", "CaveTutorialManager2DEditor")):
        continue
    original = subprocess.check_output(["git", "show", "HEAD:" + p], cwd=root).decode("utf-8-sig")
    guids[re.search(r"guid: (\w+)", original).group(1)] = p
hits = []
for p in (root/"Assets").rglob("*"):
    if p.suffix not in (".unity", ".prefab", ".asset"): continue
    text = p.read_text(encoding="utf-8-sig", errors="replace")
    for guid, origin in guids.items():
        if guid in text: hits.append(f"{p.relative_to(root)} -> {origin}")
report = [f"Checked {len(guids)} deleted script/item/mission GUIDs.", *hits]
(root/"Logs/RetiredReferenceAudit.txt").write_text("\n".join(report), encoding="utf-8")
print("\n".join(report))
if hits: raise SystemExit(1)
print("RETIRED REFERENCE AUDIT PASS")
