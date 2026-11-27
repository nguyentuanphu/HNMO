#!/usr/bin/env python3
"""Compares decoded desktop client CSVs (client-s6 and client-s9) against HNMO server CSVs and web data.

Outputs:
  docs/audit/data/diff-client-s6.txt
  docs/audit/data/diff-client-s9.txt
"""

from __future__ import annotations

import csv
import json
import os
import re
import sys
from pathlib import Path

from bmd_common import cstr, xor3


def load_csv(path: Path) -> list[dict[str, str]]:
    if not path.is_file():
        return []
    with path.open("r", encoding="utf-8-sig", newline="") as f:
        return list(csv.DictReader(f))


def read_json(path: Path) -> object:
    raw = path.read_bytes()
    if raw.startswith(b"\xff\xfe"):
        return json.loads(raw.decode("utf-16-le").lstrip("\ufeff"))
    return json.loads(raw.decode("utf-8-sig"))


def compare_tables(repo: Path, s9_skills_json: Path | None = None, s9_master_json: Path | None = None,
                   s9_web_mon_json: Path | None = None, s9_web_ovr_json: Path | None = None,
                   s9_cat_cs: Path | None = None) -> tuple[str, str]:
    s6_dir = repo / "docs" / "audit" / "data" / "client-s6"
    s9_dir = repo / "docs" / "audit" / "data" / "client-s9"
    h6_dir = repo / "docs" / "audit" / "data" / "hnmo"
    h9_dir = repo / "docs" / "audit" / "data" / "hnmo-s9"

    def load_web_items() -> dict[tuple[int, int], dict]:
        out: dict[tuple[int, int], dict] = {}
        items_dir = repo / "client" / "src" / "bin" / "Data" / "Items"
        for p in sorted(items_dir.glob("Group*.json")):
            data = json.loads(p.read_text(encoding="utf-8"))
            grp = int(data.get("group", p.name[5:7]))
            for it in data.get("items", []):
                out[(grp, int(it["number"]))] = it
        return out

    def load_buffs_json() -> dict[int, dict]:
        p = repo / "client" / "src" / "bin" / "Data" / "Buffs" / "Buffs.json"
        if not p.is_file():
            return {}
        data = json.loads(p.read_text(encoding="utf-8"))
        return {int(b["number"]): b for b in data.get("buffs", [])}

    def load_web_master(p: Path) -> dict[tuple[int, int], dict]:
        if not p or not p.is_file():
            return {}
        data = json.loads(p.read_text(encoding="utf-8"))
        out = {}
        nodes = data.get("nodes", data.get("trees", []))
        if isinstance(nodes, list):
            for n in nodes:
                if "skill" in n:
                    out[(int(n.get("classCode", 0)), int(n["skill"]))] = n
        if not out and isinstance(data, dict):
            for cls_k, entries in data.items():
                if isinstance(entries, list):
                    for n in entries:
                        if isinstance(n, dict) and "skill" in n:
                            out[(int(n.get("classCode", 0)), int(n["skill"]))] = n
        return out

    web_items = load_web_items()
    buffs_json = load_buffs_json()

    def gen_diff(season: str, c_dir: Path, h_dir: Path, is_s9: bool) -> list[str]:
        out = [
            f"# Desktop Client vs HNMO Value Diff ({season})",
            f"# Client CSVs: docs/audit/data/{c_dir.name}/ | Server CSVs: docs/audit/data/{h_dir.name}/",
            "",
        ]

        # 1. Skills
        c_sk = {int(r["id"]): r for r in load_csv(c_dir / "skills.csv")}
        h_sk = {int(r["Number"]): r for r in load_csv(h_dir / "skills.csv")}
        s9_ov_sk = {}
        if is_s9 and s9_skills_json and s9_skills_json.is_file():
            for e in read_json(s9_skills_json):
                s9_ov_sk[int(e["number"])] = e

        sk_match = sk_diff = sk_miss = 0
        sk_lines = []
        for sid in sorted(c_sk):
            c = c_sk[sid]
            h = h_sk.get(sid)
            if not h:
                ov = s9_ov_sk.get(sid)
                if ov:
                    d_parts = []
                    if int(c["mana"]) != int(ov["mana"]):
                        d_parts.append(f"mana: client={c['mana']} vs hnmo_s9json={ov['mana']}")
                    if int(c["distance"]) != int(ov["distance"]):
                        d_parts.append(f"dist: client={c['distance']} vs hnmo_s9json={ov['distance']}")
                    if c["name"].lower() != str(ov["name"]).lower():
                        d_parts.append(f"name: client={c['name']!r} vs hnmo_s9json={ov['name']!r}")
                    if d_parts:
                        sk_diff += 1
                        sk_lines.append(f"  DIFFERS {c['source']}[{c['record']}] skill={sid} ({c['name']}): " + "; ".join(d_parts))
                    else:
                        sk_match += 1
                else:
                    sk_miss += 1
                    sk_lines.append(f"  MISSING {c['source']}[{c['record']}] skill={sid} ({c['name']}): missing in HNMO skills.csv")
                continue
            diffs = []
            if int(c["mana"]) != int(h["ManaCost"] or 0):
                diffs.append(f"mana: client={c['mana']} vs hnmo={h['ManaCost'] or 0}")
            if int(c["ag"]) != int(h["AbilityCost"] or 0):
                diffs.append(f"ag: client={c['ag']} vs hnmo={h['AbilityCost'] or 0}")
            if int(c["distance"]) != int(h["Range"] or 0):
                diffs.append(f"range: client={c['distance']} vs hnmo={h['Range'] or 0}")
            if diffs:
                sk_diff += 1
                sk_lines.append(f"  DIFFERS {c['source']}[{c['record']}] skill={sid} ({c['name']}): " + "; ".join(diffs))
            else:
                sk_match += 1
        out.append(f"## Skills ({c_dir.name}/skills.csv: {len(c_sk)} client vs {len(h_sk)} server): match={sk_match}, differs={sk_diff}, missing={sk_miss}")
        out.extend(sk_lines)

        # 2. Master Tree
        c_mt = load_csv(c_dir / "master_tree.csv")
        h_mt = load_csv(repo / "docs" / "audit" / "data" / "client-s6" / "master_tree.csv")
        mt_diffs = []
        mt_match = 0
        # Compare against HNMO master skills (skills >= 300 in server) and web master-tree
        for r in c_mt:
            sid = int(r["skill"])
            h = h_sk.get(sid)
            if not h:
                mt_diffs.append(f"  MISSING MasterSkillTreeData.bmd[{r['record']}] idx={r['index']} cls={r['classCode']} skill={sid}: missing in HNMO server skills.csv")
                continue
            h_max = int(h["MasterMaxLevel"] or 0)
            c_max = int(r["maxLevel"])
            if h_max != c_max:
                mt_diffs.append(f"  DIFFERS MasterSkillTreeData.bmd[{r['record']}] idx={r['index']} cls={r['classCode']} skill={sid} ({c_sk.get(sid, {}).get('name', '')}): maxLevel client={c_max} vs hnmo={h_max}")
            else:
                mt_match += 1
        out.append(f"\n## Master Tree ({len(c_mt)} rows): match_maxLevel={mt_match}, differs_or_missing={len(mt_diffs)}")
        out.extend(mt_diffs[:80])
        if len(mt_diffs) > 80:
            out.append(f"  ... ({len(mt_diffs) - 80} more)")

        # 3. Buffs
        c_bf = {int(r["id"]): r for r in load_csv(c_dir / "buffs.csv")}
        bf_miss = [f"  MISSING BuffEffect_eng.bmd[{r['record']}] id={bid} ({r['name']})" for bid, r in sorted(c_bf.items()) if bid not in buffs_json]
        out.append(f"\n## Buffs ({len(c_bf)} client vs {len(buffs_json)} web Buffs.json): match={len(c_bf)-len(bf_miss)}, missing={len(bf_miss)}")
        out.extend(bf_miss[:40])
        if len(bf_miss) > 40:
            out.append(f"  ... ({len(bf_miss) - 40} more)")

        # 4. Items (size, slot, level, reqLvl/Str/Agi/Ene/Cmd) vs Server CSV & Web Group*.json
        c_it = {(int(r["group"]), int(r["number"])): r for r in load_csv(c_dir / "items.csv")}
        h_it = {(int(r["Group"]), int(r["Number"])): r for r in load_csv(h_dir / "items.csv")}
        s9_ovr_map = {}
        if is_s9 and s9_web_ovr_json and s9_web_ovr_json.is_file():
            ovr_data = read_json(s9_web_ovr_json)
            for o in ovr_data if isinstance(ovr_data, list) else ovr_data.get("items", []):
                if isinstance(o, dict) and "group" in o:
                    s9_ovr_map[(int(o["group"]), int(o["number"]))] = o

        it_match = it_diff = it_miss_srv = it_miss_web = 0
        it_lines = []
        for k in sorted(c_it):
            c = c_it[k]
            h = h_it.get(k)
            if not h:
                it_miss_srv += 1
                if len(it_lines) < 120:
                    it_lines.append(f"  MISSING_SERVER {c['source']}[{c['record']}] ({k[0]}:{k[1]}) {c['name']!r}")
                continue
            w = web_items.get(k)
            if not w and k not in s9_ovr_map:
                it_miss_web += 1
            diffs = []
            if (int(c["width"]), int(c["height"])) != (int(h["Width"]), int(h["Height"])):
                diffs.append(f"size: client={c['width']}x{c['height']} vs hnmo={h['Width']}x{h['Height']}")
            if int(c["reqLvl"]) != int(h["ReqLvl"] or 0):
                diffs.append(f"reqLvl: client={c['reqLvl']} vs hnmo={h['ReqLvl'] or 0}")
            if int(c["reqStr"]) != int(h["ReqStr"] or 0):
                diffs.append(f"reqStr: client={c['reqStr']} vs hnmo={h['ReqStr'] or 0}")
            if int(c["reqAgi"]) != int(h["ReqAgi"] or 0):
                diffs.append(f"reqAgi: client={c['reqAgi']} vs hnmo={h['ReqAgi'] or 0}")
            if int(c["reqEne"]) != int(h["ReqEne"] or 0):
                diffs.append(f"reqEne: client={c['reqEne']} vs hnmo={h['ReqEne'] or 0}")
            if int(c["reqCmd"]) != int(h["ReqCmd"] or 0):
                diffs.append(f"reqCmd: client={c['reqCmd']} vs hnmo={h['ReqCmd'] or 0}")
            if diffs:
                it_diff += 1
                if len(it_lines) < 200:
                    it_lines.append(f"  DIFFERS {c['source']}[{c['record']}] ({k[0]}:{k[1]}) {c['name']!r}: " + "; ".join(diffs))
            else:
                it_match += 1
        out.append(f"\n## Items ({len(c_it)} client vs {len(h_it)} server): match={it_match}, differs={it_diff}, missing_server={it_miss_srv}, missing_web={it_miss_web}")
        out.extend(it_lines[:150])
        if len(it_lines) > 150:
            out.append(f"  ... ({len(it_lines) - 150} more)")

        # 5. Monsters / NPCs
        c_npc = {int(r["id"]): r for r in load_csv(c_dir / "npcs.csv")}
        h_mon = {int(r["Number"]): r for r in load_csv(h_dir / "monsters.csv")}
        npc_miss = [f"  MISSING_SERVER {r['source']}[{r['record']}] id={nid} {r['name']!r}" for nid, r in sorted(c_npc.items()) if nid not in h_mon]
        web_mon_extra = ""
        if is_s9 and s9_web_mon_json and s9_web_mon_json.is_file():
            wm = read_json(s9_web_mon_json)
            wm_list = wm.get("monsters", wm) if isinstance(wm, dict) else wm
            web_mon_extra = f", web_s9_json={len(wm_list)}"
        out.append(f"\n## Monsters / NPCs ({len(c_npc)} client vs {len(h_mon)} server{web_mon_extra}): matched_ids={len(c_npc)-len(npc_miss)}, missing_server={len(npc_miss)}")
        out.extend(npc_miss[:50])

        # 6. Warps, Mixes, S9 Muun & Pentagram summaries
        c_wp = {int(r["index"]): r for r in load_csv(c_dir / "warps.csv")}
        h_wp = {int(r["Index"]): r for r in load_csv(h_dir / "warps.csv")}
        wp_lines = []
        wp_match = 0
        for wid, c in sorted(c_wp.items()):
            h = h_wp.get(wid)
            if not h:
                wp_lines.append(f"  MISSING_SERVER movereq_eng.bmd[{c['record']}] warp={wid} ({c['mainName']})")
                continue
            if int(c["reqLevel"]) != int(h["LevelRequirement"]) or int(c["zen"]) != int(h["Costs"]):
                wp_lines.append(f"  DIFFERS movereq_eng.bmd[{c['record']}] warp={wid} ({c['mainName']}): client(lv={c['reqLevel']},zen={c['zen']}) vs hnmo(lv={h['LevelRequirement']},zen={h['Costs']})")
            else:
                wp_match += 1
        out.append(f"\n## Warps ({len(c_wp)} client vs {len(h_wp)} server): match={wp_match}, differs_or_missing={len(wp_lines)}")
        out.extend(wp_lines)

        c_mx = load_csv(c_dir / "mixes.csv")
        h_mx = load_csv(h_dir / "mixes.csv")
        out.append(f"\n## Mixes: client={len(c_mx)} recipes (Mix.bmd) vs server={len(h_mx)} rules (mixes.csv)")

        if is_s9:
            c_muun = load_csv(c_dir / "muun_info.csv")
            c_mopt = load_csv(c_dir / "muun_options.csv")
            c_mex = load_csv(c_dir / "muun_exchange.csv")
            c_pent = load_csv(c_dir / "pentagrams.csv")
            c_ert = load_csv(c_dir / "errtel_options.csv")
            c_pmix = load_csv(c_dir / "pentagram_mixes.csv")
            out.append(f"\n## Season 9 Muun & Pentagram/Errtel Client Tables:")
            out.append(f"  MuunInfo.bmd: {len(c_muun)} rows | MuunOption_eng.bmd: {len(c_mopt)} rows | MuunExchange.bmd: {len(c_mex)} rows")
            out.append(f"  PentagramOption_eng.bmd: {len(c_pent)} pentagrams | PentagramJewelOptionValue_eng.bmd: {len(c_ert)} errtel options (23 non-linear lv0..10 curves + lv1..10 mix rates) | PentagramMixNeedSource.bmd: {len(c_pmix)} mix recipes")

        return "\n".join(out) + "\n", ""

    d6, _ = gen_diff("Season 6.3", s6_dir, h6_dir, is_s9=False)
    d9, _ = gen_diff("Season 9", s9_dir, h9_dir, is_s9=True)
    return d6, d9


def main() -> int:
    repo = Path(__file__).resolve().parents[3]
    s9_sk = Path(os.environ.get("S9_SKILLS_JSON", "")) if os.environ.get("S9_SKILLS_JSON") else None
    s9_wm = Path(os.environ.get("S9_WEB_MON_JSON", "")) if os.environ.get("S9_WEB_MON_JSON") else None
    s9_wo = Path(os.environ.get("S9_WEB_OVR_JSON", "")) if os.environ.get("S9_WEB_OVR_JSON") else None
    d6, d9 = compare_tables(repo, s9_skills_json=s9_sk, s9_web_mon_json=s9_wm, s9_web_ovr_json=s9_wo)
    (repo / "docs" / "audit" / "data" / "diff-client-s6.txt").write_text(d6, encoding="utf-8")
    (repo / "docs" / "audit" / "data" / "diff-client-s9.txt").write_text(d9, encoding="utf-8")
    print("Wrote diff-client-s6.txt and diff-client-s9.txt")
    return 0


if __name__ == "__main__":
    sys.exit(main())
