#!/usr/bin/env python3
from __future__ import annotations
from pathlib import Path
import json, re, sqlite3, sys, tempfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
errors: list[str] = []
notes: list[str] = []

# XML/XAML/project parsing.
xml_files = list(ROOT.rglob('*.xaml')) + list(ROOT.rglob('*.csproj')) + list(ROOT.rglob('*.slnx'))
for path in xml_files:
    if path.suffix == '.slnx':
        continue
    try:
        ET.parse(path)
    except Exception as exc:
        errors.append(f'XML parse: {path.relative_to(ROOT)}: {exc}')
notes.append(f'Parsed {len(xml_files)} XAML/project files.')

# Shipped JSON parsing.
json_files = list(ROOT.rglob('*.json'))
for path in json_files:
    try:
        json.loads(path.read_text(encoding='utf-8-sig'))
    except Exception as exc:
        errors.append(f'JSON parse: {path.relative_to(ROOT)}: {exc}')
notes.append(f'Parsed {len(json_files)} JSON files.')

# XAML code-behind class/event coverage.
handler_re = re.compile(r'\b(?:Click|Checked|Unchecked|SelectionChanged|KeyDown|MouseDoubleClick|MouseDown|ValueChanged|Loaded|TextChanged)="([A-Za-z_]\w*)"')
class_re = re.compile(r'x:Class="([^"]+)"')
for xaml in ROOT.joinpath('src', 'CudaSpirit.App').rglob('*.xaml'):
    text = xaml.read_text(encoding='utf-8')
    match = class_re.search(text)
    if not match:
        continue
    code = Path(str(xaml) + '.cs')
    if not code.exists():
        errors.append(f'Missing code-behind: {xaml.relative_to(ROOT)}')
        continue
    cs = code.read_text(encoding='utf-8')
    class_name = match.group(1).split('.')[-1]
    if not re.search(rf'\bpartial\s+class\s+{re.escape(class_name)}\b', cs):
        errors.append(f'x:Class mismatch: {xaml.relative_to(ROOT)} -> {class_name}')
    for handler in sorted(set(handler_re.findall(text))):
        if not re.search(rf'\b{re.escape(handler)}\s*\(', cs):
            errors.append(f'Missing event handler {handler}: {xaml.relative_to(ROOT)}')
notes.append('Checked XAML classes and event wiring.')

# Resource keys referenced by StaticResource/DynamicResource.
keys: set[str] = set()
refs: list[tuple[Path, str]] = []
for xaml in ROOT.joinpath('src', 'CudaSpirit.App').rglob('*.xaml'):
    text = xaml.read_text(encoding='utf-8')
    keys.update(re.findall(r'x:Key="([^"]+)"', text))
    refs.extend((xaml, key) for key in re.findall(r'\{(?:Static|Dynamic)Resource\s+([^}\s]+)\}', text))
builtins = {'x:Static', 'SystemColors.ControlBrushKey'}
for path, key in refs:
    if key not in keys and key not in builtins:
        errors.append(f'Unknown resource {key}: {path.relative_to(ROOT)}')

notes.append(f'Checked {len(refs)} resource references against {len(keys)} declared keys.')

# Release metadata and shell contracts.
def project_version(path: Path) -> str | None:
    match = re.search(r'<Version>([^<]+)</Version>', path.read_text(encoding='utf-8'))
    return match.group(1) if match else None

app_version = project_version(ROOT / 'src/CudaSpirit.App/CudaSpirit.App.csproj')
core_version = project_version(ROOT / 'src/CudaSpirit.Core/CudaSpirit.Core.csproj')
if app_version != '2.4.1' or core_version != '2.4.1':
    errors.append(f'Version mismatch: app={app_version}, core={core_version}, expected 2.4.1.')
http_fetch = (ROOT / 'src/CudaSpirit.Core/Services/LiveData/HttpFetch.cs').read_text(encoding='utf-8')
if 'CudaSpirit/2.4.1' not in http_fetch:
    errors.append('HTTP user-agent was not bumped to CudaSpirit/2.4.1.')

main_xaml = (ROOT / 'src/CudaSpirit.App/MainWindow.xaml').read_text(encoding='utf-8')
main_cs = (ROOT / 'src/CudaSpirit.App/MainWindow.xaml.cs').read_text(encoding='utf-8')
nav_tags = set(re.findall(r'<RadioButton[^>]+Tag="([^"]+)"', main_xaml))
view_tags = set(re.findall(r'"([a-z]+)"\s*=>\s*new\s+\w+View\(', main_cs))
if nav_tags != view_tags:
    errors.append(f'Navigation mismatch. XAML-only={sorted(nav_tags-view_tags)}, code-only={sorted(view_tags-nav_tags)}')
for required in ('CommandPlaceholder', 'PageCrumbText', 'NavBrandDetails', 'GuidedHeader', 'ToolsHeader', 'AppHeader'):
    if f'x:Name="{required}"' not in main_xaml:
        errors.append(f'Missing premium shell element: {required}.')
if 'OnCommandTextChanged' not in main_cs:
    errors.append('Command palette placeholder handler is missing.')
notes.append(f'Verified 2.4.1 release metadata and {len(nav_tags)} shell navigation destinations.')

# Regression guard for the compiler error found by the first real Windows build.
pearl_advisor = (ROOT / 'src/CudaSpirit.Core/Services/Guidance/PearlShopAdvisor.cs').read_text(encoding='utf-8')
if re.search(r'var\s+shieldActive\s*=.*?is\s*\{\s*\}\s+shieldUntil.*?;\s*if\s*\(shieldActive\)', pearl_advisor, re.S):
    errors.append('Pearl Shop no-spend shield uses a pattern variable outside its definite-assignment scope (CS0165 regression).')
if 'var shieldUntil = settings.PearlSpendingFreezeUntilUtc;' not in pearl_advisor or 'shieldUntil.GetValueOrDefault()' not in pearl_advisor:
    errors.append('Pearl Shop no-spend shield definite-assignment fix is missing.')
notes.append('Checked the Pearl Shop CS0165 definite-assignment regression.')


# Regression guards from the real V2.3.1 WPF compiler pass.
def top_level_arg_count(argument_text: str) -> int:
    argument_text = argument_text.strip()
    if not argument_text:
        return 0
    depth = 0
    count = 1
    for ch in argument_text:
        if ch in '([{<':
            depth += 1
        elif ch in ')]}>':
            depth = max(0, depth - 1)
        elif ch == ',' and depth == 0:
            count += 1
    return count

for cs_path in ROOT.joinpath('src', 'CudaSpirit.App').rglob('*.cs'):
    cs_text = cs_path.read_text(encoding='utf-8-sig')
    # Thickness has only one-value and four-edge public constructors in WPF.
    for match in re.finditer(r'new\s+Thickness\s*\(([^()]*)\)', cs_text):
        arity = top_level_arg_count(match.group(1))
        if arity not in (1, 4):
            line = cs_text.count('\n', 0, match.start()) + 1
            errors.append(f'Invalid WPF Thickness constructor arity {arity}: {cs_path.relative_to(ROOT)}:{line}')
    # WPF temporary projects did not resolve the unqualified System.IO type in the
    # real build, so files that use Directory/File must import or qualify it.
    uses_io_static = bool(re.search(r'(?<![\w.])(?:Directory|File)\s*\.', cs_text))
    has_io = 'using System.IO;' in cs_text or 'System.IO.Directory.' in cs_text or 'System.IO.File.' in cs_text
    if uses_io_static and not has_io:
        errors.append(f'Unqualified System.IO static type without explicit import: {cs_path.relative_to(ROOT)}')

appearance_text = (ROOT / 'src/CudaSpirit.App/Infra/AppearanceService.cs').read_text(encoding='utf-8')
for required in ('new Thickness(12, 7, 12, 7)', 'new Thickness(18, 10, 18, 10)', 'new Thickness(14, 8, 14, 8)'):
    if required not in appearance_text:
        errors.append(f'Appearance density padding regression: missing {required}.')
main_window_text = (ROOT / 'src/CudaSpirit.App/MainWindow.xaml.cs').read_text(encoding='utf-8')
if 'new Thickness(14, 9, 14, 9)' not in main_window_text:
    errors.append('Navigation padding regression: expected the four-edge expanded value.')
data_center_text = (ROOT / 'src/CudaSpirit.App/Views/DataCenterView.xaml.cs').read_text(encoding='utf-8')
if 'using System.IO;' not in data_center_text or 'System.IO.Directory.Exists' not in data_center_text:
    errors.append('Data Center System.IO compiler fix is missing.')
notes.append('Checked all prior Windows compiler regressions: Thickness arity and System.IO resolution.')


# Obsidian UI geometry and packaging contracts.
app_xaml_root = ROOT / 'src/CudaSpirit.App'
for xaml_path in app_xaml_root.rglob('*.xaml'):
    xaml_text = xaml_path.read_text(encoding='utf-8')
    if '<Ellipse' in xaml_text:
        errors.append(f'Obsidian UI regression: decorative Ellipse remains in {xaml_path.relative_to(ROOT)}.')
    for match in re.finditer(r'CornerRadius="([0-9.,]+)"', xaml_text):
        values = [float(part) for part in match.group(1).split(',')]
        if any(value > 4 for value in values):
            line = xaml_text.count('\n', 0, match.start()) + 1
            errors.append(f'Obsidian UI regression: CornerRadius above 4 at {xaml_path.relative_to(ROOT)}:{line}.')
if 'CornerRadius" Value="999"' in (ROOT / 'src/CudaSpirit.App/Themes/BlackSpirit.xaml').read_text(encoding='utf-8'):
    errors.append('Obsidian UI regression: pill radius 999 returned.')
main_shell = (ROOT / 'src/CudaSpirit.App/MainWindow.xaml').read_text(encoding='utf-8')
for contract in ('LOCAL / READ-ONLY', 'COMMAND COCKPIT', 'CornerRadius="3"'):
    if contract not in main_shell:
        errors.append(f'Obsidian shell contract missing: {contract}.')
notes.append('Checked Obsidian UI geometry: no decorative ellipses, no radii above 4, and crisp shell contracts present.')

# Transparent overlay icon reliability. Font glyphs and color emoji are fragile in
# layered WPF windows, especially across Windows/font/DPI configurations.
overlay_files = [
    ROOT / 'src/CudaSpirit.App/Overlay/OverlayWindow.xaml',
    ROOT / 'src/CudaSpirit.App/Overlay/TasksWindow.xaml',
]
for overlay_path in overlay_files:
    overlay_text = overlay_path.read_text(encoding='utf-8')
    if 'Segoe MDL2 Assets' in overlay_text:
        errors.append(f'Overlay icon regression: font glyph remains in {overlay_path.relative_to(ROOT)}.')
    if 'TextOptions.TextRenderingMode="Grayscale"' not in overlay_text:
        errors.append(f'Overlay rendering regression: grayscale layered-window rendering missing in {overlay_path.relative_to(ROOT)}.')
overlay_code = '\n'.join(path.read_text(encoding='utf-8') for path in ROOT.joinpath('src/CudaSpirit.App/Overlay').glob('*.cs'))
for fragile_symbol in ('🔒', '🔓', '🎮', '⚠', '⏳', '◈', '✓'):
    if fragile_symbol in overlay_code:
        errors.append(f'Overlay icon regression: fragile emoji symbol remains: {fragile_symbol}')
for contract in ('M13.4,5.8 C12.5,3.5', 'M2,2 L14,14 M14,2 L2,14', 'M1,1 L6,5 L1,9'):
    if contract not in '\n'.join(path.read_text(encoding='utf-8') for path in overlay_files):
        errors.append(f'Overlay vector icon contract missing: {contract}')
notes.append('Checked layered-overlay vector icons, DPI rounding, grayscale rendering, and emoji-free status text.')

global_json = json.loads((ROOT / 'global.json').read_text(encoding='utf-8'))
sdk = global_json.get('sdk', {})
if sdk.get('version') != '8.0.100' or sdk.get('rollForward') != 'latestMajor':
    errors.append('SDK compatibility regression: global.json must allow compatible installed .NET 8+ SDKs.')
release_builder = ROOT / 'tools/Build-NexusRelease.ps1'
if not release_builder.exists() or 'End users need no .NET runtime or SDK.' not in release_builder.read_text(encoding='utf-8'):
    errors.append('Self-contained Nexus release builder is missing or incomplete.')
notes.append('Checked compatible-SDK policy and self-contained Nexus release builder.')

def mask_csharp(text: str) -> str:
    """Mask comments and literals while preserving newlines for diagnostics."""
    chars = list(text)
    n = len(text)

    def mask(a: int, b: int) -> None:
        for k in range(a, min(b, n)):
            if chars[k] != '\n':
                chars[k] = ' '

    def consume_line_comment(i: int) -> int:
        end = text.find('\n', i + 2)
        return n if end < 0 else end

    def consume_block_comment(i: int) -> int:
        end = text.find('*/', i + 2)
        return n if end < 0 else end + 2

    def consume_char(i: int) -> int:
        j = i + 1
        while j < n:
            if text[j] == '\\':
                j += 2
            elif text[j] == "'":
                return j + 1
            else:
                j += 1
        return n

    def consume_regular(i: int, opening_len: int = 1) -> int:
        j = i + opening_len
        while j < n:
            if text[j] == '\\':
                j += 2
            elif text[j] == '"':
                return j + 1
            else:
                j += 1
        return n

    def consume_verbatim(i: int, opening_len: int = 2) -> int:
        j = i + opening_len
        while j < n:
            if text.startswith('""', j):
                j += 2
            elif text[j] == '"':
                return j + 1
            else:
                j += 1
        return n

    def consume_raw(i: int) -> int:
        q = i
        while q < n and text[q] == '$':
            q += 1
        quote_count = 0
        while q + quote_count < n and text[q + quote_count] == '"':
            quote_count += 1
        delimiter = '"' * quote_count
        end = text.find(delimiter, q + quote_count)
        return n if end < 0 else end + quote_count

    def consume_interpolated(i: int, verbatim: bool, opening_len: int) -> int:
        # Track interpolation expressions so quotes inside expressions do not end
        # the outer string, e.g. $"{x.Replace("\\\"", "...")}".
        j = i + opening_len
        depth = 0
        while j < n:
            if depth == 0:
                if verbatim and text.startswith('""', j):
                    j += 2
                    continue
                if not verbatim and text[j] == '\\':
                    j += 2
                    continue
                if text.startswith('{{', j) or text.startswith('}}', j):
                    j += 2
                    continue
                if text[j] == '{':
                    depth = 1
                    j += 1
                    continue
                if text[j] == '"':
                    return j + 1
                j += 1
                continue

            # Inside an interpolation expression, consume nested comments/literals.
            if text.startswith('//', j):
                j = consume_line_comment(j)
                continue
            if text.startswith('/*', j):
                j = consume_block_comment(j)
                continue
            raw_q = j
            while raw_q < n and text[raw_q] == '$':
                raw_q += 1
            qc = 0
            while raw_q + qc < n and text[raw_q + qc] == '"':
                qc += 1
            if qc >= 3:
                j = consume_raw(j)
                continue
            if text.startswith('$@"', j) or text.startswith('@$"', j):
                j = consume_interpolated(j, True, 3)
                continue
            if text.startswith('$"', j):
                j = consume_interpolated(j, False, 2)
                continue
            if text.startswith('@"', j):
                j = consume_verbatim(j, 2)
                continue
            if text[j] == '"':
                j = consume_regular(j, 1)
                continue
            if text[j] == "'":
                j = consume_char(j)
                continue
            if text[j] == '{':
                depth += 1
            elif text[j] == '}':
                depth -= 1
            j += 1
        return n

    i = 0
    while i < n:
        if text.startswith('//', i):
            end = consume_line_comment(i)
            mask(i, end)
            i = end
            continue
        if text.startswith('/*', i):
            end = consume_block_comment(i)
            mask(i, end)
            i = end
            continue

        raw_q = i
        while raw_q < n and text[raw_q] == '$':
            raw_q += 1
        qc = 0
        while raw_q + qc < n and text[raw_q + qc] == '"':
            qc += 1
        if qc >= 3:
            end = consume_raw(i)
            mask(i, end)
            i = end
            continue

        if text.startswith('$@"', i) or text.startswith('@$"', i):
            end = consume_interpolated(i, True, 3)
        elif text.startswith('$"', i):
            end = consume_interpolated(i, False, 2)
        elif text.startswith('@"', i):
            end = consume_verbatim(i, 2)
        elif text[i] == '"':
            end = consume_regular(i, 1)
        elif text[i] == "'":
            end = consume_char(i)
        else:
            i += 1
            continue
        mask(i, end)
        i = end

    return ''.join(chars)

# C# lexical balance and invalid regular-string escapes.
allowed_escapes = set("'\"\\0abfnrtvuxU")
string_re = re.compile(r'(?<!@)(?:\$)?"(?!"")((?:\\.|[^"\\])*)"')
cs_files = list(ROOT.joinpath('src').rglob('*.cs'))
for path in cs_files:
    text = path.read_text(encoding='utf-8')
    for m in string_re.finditer(text):
        for esc in re.finditer(r'\\(.)', m.group(1), re.S):
            if esc.group(1) not in allowed_escapes:
                line = text.count('\n', 0, m.start()) + 1
                errors.append(f'Invalid C# escape \\{esc.group(1)}: {path.relative_to(ROOT)}:{line}')
    # Mask comments/literals before a delimiter-balance smoke test.
    clean = mask_csharp(text)
    for opener, closer in [('(', ')'), ('[', ']'), ('{', '}')]:
        depth = 0
        for ch in clean:
            if ch == opener: depth += 1
            elif ch == closer: depth -= 1
            if depth < 0:
                errors.append(f'Unbalanced {opener}{closer}: {path.relative_to(ROOT)}')
                break
        if depth != 0:
            errors.append(f'Unbalanced {opener}{closer} depth {depth}: {path.relative_to(ROOT)}')
notes.append(f'Lexically checked {len(cs_files)} C# files.')

# Extract and execute schema SQL blocks before ExecNoLock method.
db_file = ROOT / 'src/CudaSpirit.Core/Services/Data/AppDatabase.cs'
db_text = db_file.read_text(encoding='utf-8')
schema_area = db_text[:db_text.index('    private void ExecNoLock')]
blocks = re.findall(r'ExecNoLock\("""([\s\S]*?)"""\)', schema_area)
if not blocks:
    errors.append('Could not locate schema SQL blocks.')
else:
    with tempfile.TemporaryDirectory() as td:
        db_path = Path(td) / 'validation.db'
        conn = sqlite3.connect(db_path)
        conn.execute('PRAGMA foreign_keys=ON')
        try:
            for block in blocks:
                conn.executescript(block)
            conn.execute("INSERT INTO knowledge_record(source_id,external_id,kind,title,summary,content,url,region,tags,metadata_json,content_hash,confidence,retrieved_at) VALUES('test','1','guide','Transfer an item','storage maid magnus','Find My Item Ctrl F','','global','transfer storage','{}','x',1.0,'2026-07-23T00:00:00Z')")
            hit = conn.execute("SELECT count(*) FROM knowledge_fts WHERE knowledge_fts MATCH 'transfer'").fetchone()[0]
            if hit != 1:
                errors.append(f'FTS trigger test returned {hit}, expected 1.')
            conn.execute("INSERT INTO route_node(node_key,name,updated_at) VALUES('a','A','2026-07-23T00:00:00Z')")
            conn.execute("INSERT INTO route_node(node_key,name,updated_at) VALUES('b','B','2026-07-23T00:00:00Z')")
            conn.execute("INSERT INTO route_edge(from_key,to_key,travel_minutes,updated_at) VALUES('a','b',2.5,'2026-07-23T00:00:00Z')")
            conn.execute("INSERT INTO companion_task(title,created_at) VALUES('Claim rewards','2026-07-23T00:00:00Z')")
            conn.execute("INSERT INTO item_decision_history(item_name,verdict,created_at) VALUES('Box','Stop','2026-07-23T00:00:00Z')")
            conn.execute("INSERT INTO pearl_evaluation_history(offer_name,evaluated_at) VALUES('Bundle','2026-07-23T00:00:00Z')")
            conn.commit()
            fk = conn.execute('PRAGMA foreign_key_check').fetchall()
            if fk: errors.append(f'Foreign-key check failed: {fk}')
            version = conn.execute("SELECT value FROM schema_info WHERE key='schema_version'").fetchone()
            if not version or version[0] != '4': errors.append(f'Unexpected schema version: {version}')
        except Exception as exc:
            errors.append(f'SQLite schema test: {exc}')
        finally:
            conn.close()
notes.append(f'Executed {len(blocks)} SQLite schema/FTS blocks and smoke-tested companion tables.')

# No stale build output in source release.
for forbidden in ('bin', 'obj', '.vs'):
    found = [p for p in ROOT.rglob(forbidden) if p.is_dir()]
    if found: errors.append(f'Forbidden build directory present: {found[0].relative_to(ROOT)}')

for note in notes:
    print('PASS:', note)
github_required = [
    ROOT / '.gitignore',
    ROOT / '.gitattributes',
    ROOT / '.github' / 'workflows' / 'windows-release.yml',
    ROOT / '.github' / 'dependabot.yml',
    ROOT / 'REPOSITORY_SETUP.md',
]
for required in github_required:
    if not required.exists():
        errors.append(f'Missing GitHub repository file: {required.relative_to(ROOT)}')

main_xaml = (ROOT / 'src' / 'CudaSpirit.App' / 'MainWindow.xaml').read_text(encoding='utf-8')
for token in ['CommandPlaceholder', 'PageCrumbText', 'NavBrandDetails', 'SidebarSafetyText']:
    if token not in main_xaml:
        errors.append(f'Premium shell element missing: {token}')
main_code = (ROOT / 'src' / 'CudaSpirit.App' / 'MainWindow.xaml.cs').read_text(encoding='utf-8')
for token in ['OnCommandTextChanged', 'AnimatePageChange', 'ApplyShellPreferences']:
    if token not in main_code:
        errors.append(f'Premium shell behavior missing: {token}')

if errors:
    for error in errors: print('FAIL:', error)
    sys.exit(1)
print('PASS: validation completed with no detected errors.')
