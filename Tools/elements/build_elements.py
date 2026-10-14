#!/usr/bin/env python3
"""Build Assets/ChemistryLab/Data/elements.json from published sources.

Sources (downloaded unless --cache-dir already holds them):
  pubchem.json        PubChem periodic table (NIH): names, masses, Pauling
                      electronegativity, oxidation states, van der Waals
                      radii (Bondi-type), categories, Jmol CPK colors.
                      https://pubchem.ncbi.nlm.nih.gov/rest/pug/periodictable/JSON
  jmol_constants.js   Jmol's default element colors (the "CPK" colors used
                      by molecular viewers), authoritative for Z 1-109.
                      https://jmol.sourceforge.net/jscolors/jmol_constants.js
  covalent.wiki       Wikipedia "Covalent radius" source, which tabulates
                      - Cordero et al. 2008, "Covalent radii revisited",
                        Dalton Trans. 2832, doi:10.1039/b801115j (Z 1-96)
                      - Pyykko & Atsumi 2009, Chem. Eur. J. 15, 186 and
                        12770; Pyykko et al. 2005, Chem. Eur. J. 11, 3511
                        (single/double/triple-bond radii, Z 1-118)

Bond angles for the prototype hydrides are NIST CCCBDB experimental
geometries (https://cccbdb.nist.gov/): H2O 104.48 deg, NH3 106.67,
CH4 109.471, H2S 92.11, PH3 93.3.

Usage:  python3 Tools/elements/build_elements.py [--cache-dir DIR]
"""
import argparse
import json
import pathlib
import re
import urllib.request

URLS = {
    "pubchem.json": "https://pubchem.ncbi.nlm.nih.gov/rest/pug/periodictable/JSON",
    "jmol_constants.js": "https://jmol.sourceforge.net/jscolors/jmol_constants.js",
    "covalent.wiki": "https://en.wikipedia.org/w/index.php?title=Covalent_radius&action=raw",
}
ROOT = pathlib.Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets/ChemistryLab/Data/elements.json"

# 1 picometre in the real world = 1 millimetre in the world (x1e9).
METRES_PER_PM = 0.001
# Jmol's color for atoms without a defined color.
UNKNOWN_COLOR = "FA1691"


def fetch(cache, name):
    path = cache / name
    if not path.exists():
        req = urllib.request.Request(URLS[name], headers={"User-Agent": "ChemistryLab-builder"})
        path.write_bytes(urllib.request.urlopen(req, timeout=60).read())
    return path.read_text(encoding="utf-8", errors="replace")


# ---------------------------------------------------------------- radii table
def _cells(row):
    row = re.sub(r"<ref[^>]*/>|<ref.*?</ref>", "", row, flags=re.S)
    out = []
    for line in row.split("\n"):
        if not line.startswith("|") or line.startswith(("|+", "|-")):
            if out and line and not line.startswith(("|", "!")):
                out[-1] += "\n" + line  # continuation line of the previous cell
            continue
        body = line[2:] if line.startswith("||") else line[1:]
        for c in body.split("||"):
            head = c.split("|")[0]
            if "|" in c and re.search(r"(style|colspan|valign)\s*=", head):
                c = c.split("|", 1)[1]
            out.append(c.strip())
    return out


def _parse_table(table):
    junk = ("&nbsp;", "", "*", "**")
    data, pending, number_row_next = {}, None, False
    for r in (_cells(x) for x in re.split(r"\n\|-[^\n]*", table)):
        real = [c for c in r if c not in junk and not c.startswith("Radius")]
        syms = [c for c in real if re.fullmatch(r"[A-Z][a-z]?", c)]
        if syms and len(syms) == len(real):
            pending, number_row_next = syms, True
            continue
        if not pending:
            continue
        if number_row_next:
            # Each symbol row is followed by an atomic-number row; skip it.
            number_row_next = False
            if real and all(re.fullmatch(r"\d+", c) for c in real):
                continue
        vals = []
        for c in r:
            c = re.sub(r"<sup>.*?</sup>", "", c)
            if re.match(r"\s*(\d|l\.s\.|sp|-)", c):
                nums = []
                for part in re.split(r"<br\s*/?>|\n", c):
                    m = re.search(r"(\d+)", part)
                    nums.append(int(m.group(1)) if m and not part.strip().startswith("-") else None)
                vals.append(nums)
        if vals and len(vals) == len(pending):
            data.update(zip(pending, vals))
            pending = None
    return data


def parse_radii(wiki):
    tables = re.findall(r"\{\|(.*?)\n\|\}", wiki, re.S)
    return _parse_table(tables[0]), _parse_table(tables[1])


# ------------------------------------------------------------ periodic layout
def group_of(z):
    """IUPAC group 1-18, or None for La-Yb / Ac-No (f-block)."""
    if z == 1:
        return 1
    if z == 2:
        return 18
    for start, end in ((3, 10), (11, 18)):
        if start <= z <= end:
            return [1, 2, 13, 14, 15, 16, 17, 18][z - start]
    for start in (19, 37):
        if start <= z < start + 18:
            return z - start + 1
    for start, f_first, f_last in ((55, 57, 70), (87, 89, 102)):
        if start <= z < start + 32:
            if f_first <= z <= f_last:
                return None
            return z - start + 1 if z < f_first else z - start - 13
    raise ValueError(z)


def period_of(z):
    for p, last in enumerate((2, 10, 18, 36, 54, 86, 118), start=1):
        if z <= last:
            return p


# ------------------------------------------------------------------- bonding
# Bond "sockets" for the lab: how many covalent bonds an atom typically makes
# and the direction of each. Angles marked "measured" are NIST CCCBDB
# experimental equilibrium geometries of the prototype hydride.
MEASURED = {
    "C": (109.471, "CH4"), "N": (106.67, "NH3"), "O": (104.4776, "H2O"),
    "P": (93.3, "PH3"), "S": (92.11, "H2S"),
}
IDEAL = {"linear": 180.0, "trigonal-planar": 120.0, "tetrahedral": 109.4712,
         "octahedral": 90.0, "square-planar": 90.0}


def bonding(sym, z, group, period):
    def mode(n, geometry, example, angle=None, source="VSEPR ideal"):
        if angle is None and geometry in IDEAL:
            angle = IDEAL[geometry]
        return {"bonds": n, "geometry": geometry, "angleDeg": angle,
                "angleSource": source, "example": example}

    if group == 18:
        main = mode(0, "none", "monatomic gas")
        alt = {"Xe": [mode(2, "linear", "XeF2"), mode(4, "square-planar", "XeF4")],
               "Kr": [mode(2, "linear", "KrF2")]}.get(sym, [])
        return main, alt, "measured" if z <= 86 else "predicted"
    if group == 1:
        return mode(1, "terminal", "HCl" if sym == "H" else f"{sym}Cl"), [], "measured"
    if group == 2:
        return mode(2, "linear", f"{sym}Cl2 (gas)"), [], "measured"
    if group == 13:
        return mode(3, "trigonal-planar", f"{sym}F3" if sym in ("B", "Al") else f"{sym}Cl3"), [], "measured"
    if group == 14:
        return mode(4, "tetrahedral", f"{sym}H4", *(MEASURED["C"][0], "NIST CCCBDB (CH4)") if sym == "C" else (None,)), [], "measured"
    if group == 15:
        if sym in MEASURED:
            angle, ex = MEASURED[sym]
            main = mode(3, "trigonal-pyramidal", ex, angle, f"NIST CCCBDB ({ex})")
        else:
            main = mode(3, "trigonal-pyramidal", f"{sym}H3", MEASURED["P"][0], "approx. (PH3 value)")
        alt = [mode(5, "trigonal-bipyramidal", f"{sym}Cl5")] if sym in ("P", "As", "Sb") else []
        return main, alt, "measured"
    if group == 16:
        if sym in MEASURED:
            angle, ex = MEASURED[sym]
            main = mode(2, "bent", ex, angle, f"NIST CCCBDB ({ex})")
        else:
            main = mode(2, "bent", f"H2{sym}", MEASURED["S"][0], "approx. (H2S value)")
        alt = [mode(4, "seesaw", f"{sym}F4"), mode(6, "octahedral", f"{sym}F6")] if sym in ("S", "Se", "Te") else []
        return main, alt, "measured"
    if group == 17:
        alt = [mode(3, "T-shaped", f"{sym}F3"), mode(5, "square-pyramidal", f"{sym}F5")] if sym in ("Cl", "Br", "I") else []
        return mode(1, "terminal", f"H{sym}"), alt, "measured"
    if group is None:
        return mode(6, "octahedral", "approximation: Ln/An ions are usually 8-9 coordinate"), [], "approximate"
    # d-block (groups 3-12): typical coordination number, not "covalent valence"
    special = {
        "Mn": (mode(6, "octahedral", "[Mn(H2O)6]2+, MnO2"), [mode(4, "tetrahedral", "MnO4- (permanganate)")]),
        "Zn": (mode(4, "tetrahedral", "[Zn(NH3)4]2+, ZnS"), []),
        "Cd": (mode(4, "tetrahedral", "CdS"), []),
        "Hg": (mode(2, "linear", "HgCl2"), []),
        "Cu": (mode(4, "square-planar", "[Cu(NH3)4]2+"), [mode(2, "linear", "[CuCl2]-")]),
        "Ag": (mode(2, "linear", "[Ag(NH3)2]+"), []),
        "Au": (mode(2, "linear", "[AuCl2]-"), [mode(4, "square-planar", "[AuCl4]-")]),
        "Ni": (mode(6, "octahedral", "[Ni(H2O)6]2+"), [mode(4, "square-planar", "[Ni(CN)4]2-")]),
        "Pd": (mode(4, "square-planar", "[PdCl4]2-"), []),
        "Pt": (mode(4, "square-planar", "cisplatin [PtCl2(NH3)2]"), [mode(6, "octahedral", "[PtCl6]2-")]),
    }
    main, alt = special.get(sym, (mode(6, "octahedral", f"typical {sym} complex"), []))
    return main, alt, "measured" if z <= 103 else "predicted"


# ----------------------------------------------------------------------- main
def luminance(hex_color):
    def chan(c):
        c = c / 255
        return c / 12.92 if c <= 0.03928 else ((c + 0.055) / 1.055) ** 2.4
    r, g, b = (int(hex_color[i:i + 2], 16) for i in (0, 2, 4))
    return 0.2126 * chan(r) + 0.7152 * chan(g) + 0.0722 * chan(b)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--cache-dir", type=pathlib.Path, default=pathlib.Path(__file__).with_name(".cache"))
    args = ap.parse_args()
    args.cache_dir.mkdir(parents=True, exist_ok=True)

    pub = json.loads(fetch(args.cache_dir, "pubchem.json"))["Table"]
    cols = pub["Columns"]["Column"]
    rows = [dict(zip(cols, r["Cell"])) for r in pub["Row"]]
    jmol = {m.group(1): m.group(2) for m in re.finditer(
        r"elemParams\[\d+\]\s*=\s*\['(\w+)','([0-9A-Fa-f]{6})'", fetch(args.cache_dir, "jmol_constants.js"))}
    cordero, pyykko = parse_radii(fetch(args.cache_dir, "covalent.wiki"))

    elements = []
    for r in rows:
        z, sym = int(r["AtomicNumber"]), r["Symbol"]
        grp, per = group_of(z), period_of(z)

        color = jmol.get(sym) or r["CPKHexColor"] or UNKNOWN_COLOR
        color_source = "Jmol" if sym in jmol else ("PubChem" if r["CPKHexColor"] else "none (Jmol unknown-atom color)")

        py = (pyykko.get(sym) or []) + [None, None, None]
        cov = cordero.get(sym)
        if cov:
            cov_pm, cov_src = cov[0], "Cordero 2008"
            if sym == "C":
                cov_note = "sp3 (sp2 73, sp 69)"
            elif len(cov) > 1:
                cov_note = f"low-spin (high-spin {cov[1]})"
            else:
                cov_note = None
        else:
            cov_pm, cov_src, cov_note = py[0], "Pyykko 2009 (single bond)", None

        main_bond, alt_bonds, bond_conf = bonding(sym, z, grp, per)
        vdw = int(r["AtomicRadius"]) if r["AtomicRadius"] else None
        en = float(r["Electronegativity"]) if r["Electronegativity"] else None

        elements.append({
            "z": z,
            "symbol": sym,
            "name": r["Name"],
            "group": grp,
            "period": per,
            "category": r["GroupBlock"],
            "standardState": r["StandardState"] or None,
            "atomicMass": float(r["AtomicMass"]) if r["AtomicMass"] else None,
            "electronConfiguration": r["ElectronConfiguration"],
            "electronegativityPauling": en,
            "oxidationStates": [s.strip() for s in r["OxidationStates"].split(",") if s.strip()],
            "color": {
                "hex": "#" + color.upper(),
                "source": color_source,
                "symbolHex": "#000000" if luminance(color) > 0.18 else "#FFFFFF",
            },
            "radiiPm": {
                "covalent": cov_pm,
                "covalentSource": cov_src,
                "covalentNote": cov_note,
                "single": py[0], "double": py[1], "triple": py[2],
                "vanDerWaals": vdw,
            },
            "world": {
                # Ball-and-stick sizing: sphere radius = covalent radius, so two
                # bonded atoms touch at the real bond length (r1 + r2).
                "sphereDiameterM": round(2 * cov_pm * METRES_PER_PM, 4) if cov_pm else None,
                "bondLengthRule": "r1 + r2 (covalent radii)",
            },
            "bonding": {"default": main_bond, "alternatives": alt_bonds, "confidence": bond_conf},
        })

    OUT.parent.mkdir(parents=True, exist_ok=True)
    doc = {
        "about": "Generated by Tools/elements/build_elements.py - do not edit by hand.",
        "scale": "1 pm = 1 mm (x1e9)",
        "sources": {
            "colors": "Jmol default CPK colors (jmol.sourceforge.net/jscolors)",
            "covalentRadii": "Cordero et al., Dalton Trans. 2008, 2832 (Z<=96); Pyykko & Atsumi, Chem. Eur. J. 2009 (Z>96, double/triple)",
            "vanDerWaalsRadii": "PubChem periodic table (Bondi-type values)",
            "properties": "PubChem periodic table (NIH)",
            "bondAngles": "NIST CCCBDB experimental geometries (H2O, NH3, CH4, H2S, PH3); VSEPR ideals otherwise",
        },
        "elements": elements,
    }
    OUT.write_text(json.dumps(doc, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"wrote {len(elements)} elements to {OUT.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
