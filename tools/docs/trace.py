#!/usr/bin/env python3
"""SDD 추적표(6장) 자동 생성과 문서 ID 일관성 검사.

원본은 두 곳뿐입니다.
  - docs/SDD/04-elements.md 설계 요소 표의 마지막 열 「관련 FR/NFR」
  - docs/SDD/06-trace-notes.md 의 비고 (| FR-001 | 비고 |)
docs/SDD/06-trace.md 는 이 스크립트가 만드는 파일이라 직접 고치지 않습니다.

사용법 (저장소 루트에서):
  python tools/docs/trace.py build   # 06-trace.md 와 00-index.md 파일 지도 다시 만들기
  python tools/docs/trace.py check   # 어긋난 곳만 출력, 문제가 있으면 종료 코드 1
"""
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
DOCS = ROOT / "docs"
SDD = DOCS / "SDD"
ELEMENTS = SDD / "04-elements.md"
NOTES = SDD / "06-trace-notes.md"
TRACE = SDD / "06-trace.md"
INDEX = SDD / "00-index.md"

# FR-001, DES-040~047, FR-010~FR-012 같은 표기를 찾는다
ID_RE = re.compile(r"\b(FR|NFR|DES|SCR)-(\d{3})(?:\s*~\s*(?:(?:FR|NFR|DES|SCR)-)?(\d{3}))?")

TRACE_HEADER = """> SDD(소프트웨어 설계 명세서)의 일부 · 목차: [00-index.md](00-index.md)
> **자동 생성 파일입니다. 직접 고치지 마세요.** 연결은 [04-elements.md](04-elements.md)의
> 「관련 FR/NFR」 열, 비고는 [06-trace-notes.md](06-trace-notes.md)에서 가져옵니다.
> 다시 만들기: `python tools/docs/trace.py build`

## 6. 요구사항 추적표
"""


def read(path):
    return path.read_text(encoding="utf-8")


def find_ids(text, kinds=None, expand=True):
    """문자열에서 ID 목록을 뽑는다. expand=True면 범위(~)를 펼친다."""
    out = []
    for m in ID_RE.finditer(text):
        kind, a, b = m.group(1), int(m.group(2)), m.group(3)
        if kinds and kind not in kinds:
            continue
        nums = [a]
        if b and expand and 0 < int(b) - a < 200:
            nums = range(a, int(b) + 1)
        elif b:
            nums = [a, int(b)]
        out += [f"{kind}-{n:03d}" for n in nums]
    return out


def cells(line):
    return [c.strip() for c in line.strip().strip("|").split("|")]


def read_elements():
    """4장 표에서 DES -> 관련 FR/NFR 목록과 중복 DES를 돌려준다."""
    rel, dup = {}, []
    for line in read(ELEMENTS).splitlines():
        if not line.startswith("| DES-"):
            continue
        c = cells(line)
        if not re.fullmatch(r"DES-\d{3}", c[0]):
            continue
        if c[0] in rel:
            dup.append(c[0])
        rel[c[0]] = find_ids(c[-1], {"FR", "NFR"})
    return rel, dup


def read_notes():
    notes = {}
    if NOTES.exists():
        for line in read(NOTES).splitlines():
            if re.match(r"\|\s*N?FR-\d{3}\s*\|", line):
                c = cells(line)
                notes[c[0]] = c[1] if len(c) > 1 else ""
    return notes


def srs_ids():
    """SRS에 정의된 FR/NFR (#### FR-001 제목) 순서대로."""
    return re.findall(r"^#{2,5} ((?:FR|NFR)-\d{3})\b", read(DOCS / "SRS.md"), re.M)


def render_trace():
    rel, _ = read_elements()
    notes = read_notes()
    back = {}
    for des, reqs in rel.items():
        for r in reqs:
            back.setdefault(r, set()).add(des)
    order = srs_ids()
    extra = sorted((set(back) | set(notes)) - set(order))
    out = [TRACE_HEADER]
    for kind, title in (("FR", "6.1 기능 요구사항"), ("NFR", "6.2 비기능 요구사항")):
        out += ["", f"### {title}", "", f"| {kind} | DES ID | 비고 |", "|---|---|---|"]
        for rid in order + extra:
            if rid.split("-")[0] != kind:
                continue
            des = ", ".join(sorted(back.get(rid, []))) or "—"
            out.append(f"| {rid} | {des} | {notes.get(rid, '')} |")
    return "\n".join(out) + "\n"


def render_filemap():
    """00-index.md에 넣을 파일 지도: 파일마다 ##·### 제목."""
    lines = []
    for f in sorted(SDD.glob("*.md")):
        if f.name == INDEX.name:
            continue
        heads = [h.strip("# ").strip() for h in read(f).splitlines() if re.match(r"#{2,3} ", h)]
        heads = [h if len(h) <= 40 else h[:38] + "…" for h in heads]
        lines.append(f"- [{f.name}]({f.name}): " + " / ".join(heads))
    return "\n".join(lines)


def build():
    TRACE.write_text(render_trace(), encoding="utf-8")
    text = read(INDEX)
    new = re.sub(r"(<!-- FILEMAP:BEGIN -->\n).*?(<!-- FILEMAP:END -->)",
                 lambda m: m.group(1) + render_filemap() + "\n" + m.group(2), text, flags=re.S)
    INDEX.write_text(new, encoding="utf-8")
    print("06-trace.md, 00-index.md 파일 지도를 다시 만들었습니다.")


def check():
    errors, infos = [], []
    rel, dup = read_elements()
    srs = set(srs_ids())
    notes = read_notes()
    linked = {r for reqs in rel.values() for r in reqs}

    if dup:
        errors.append(("4장에 중복된 DES", sorted(set(dup))))
    missing = sorted(srs - linked - set(notes))
    if missing:
        errors.append(("설계 연결이 없는 요구사항(4장에 추가하거나 비고로 이유 적기)", missing))
    noted = sorted((srs - linked) & set(notes))
    if noted:
        infos.append(("연결 없음, 비고로 설명됨", noted))
    unknown = sorted(linked - srs)
    if unknown:
        errors.append(("4장이 가리키는 ID가 SRS에 없음", unknown))

    des = set(rel)
    targets = [f for f in SDD.glob("*.md") if f.name not in (ELEMENTS.name, TRACE.name)]
    targets += [DOCS / "STORYBOARD.md", DOCS / "SRS.md"]
    for f in targets:
        if not f.exists():
            continue
        bad = sorted(set(find_ids(read(f), {"DES"}, expand=False)) - des)
        if bad:
            errors.append((f"4장에 없는 DES 참조 ({f.relative_to(DOCS).as_posix()})", bad))

    if not TRACE.exists() or read(TRACE) != render_trace():
        errors.append(("06-trace.md가 최신이 아님", ["python tools/docs/trace.py build 실행"]))

    for title, items in infos:
        print(f"[정보] {title} ({len(items)}): {', '.join(items[:30])}")
    for title, items in errors:
        more = " …" if len(items) > 30 else ""
        print(f"[오류] {title} ({len(items)}): {', '.join(items[:30])}{more}")
    if not errors:
        print("검사 통과: 추적표와 ID가 일치합니다.")
    return 1 if errors else 0


if __name__ == "__main__":
    cmd = sys.argv[1] if len(sys.argv) > 1 else "check"
    if cmd == "build":
        build()
    elif cmd == "check":
        sys.exit(check())
    else:
        print(__doc__)
        sys.exit(2)
