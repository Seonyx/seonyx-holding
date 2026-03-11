#!/usr/bin/env python3
"""
convert-to-bookml.py

Converts old pipe/bracket-format book files to a BookML XML zip package.

Usage:
  python3 convert-to-bookml.py <input-dir> --title "Book Title" --surname "Pemberton"
                               [--firstname "Jane"] [--book-id my-book] [--output out.zip]
                               [--draft 1] [--based-on 0] [--draft-label "First draft"]

Supported input formats:
  Chapter files:
    [[uid]] text ...          (double-bracket, Mayfly / Carrier format)
    [uid] text ...            (single-bracket per-line, Chilli-Racers format)
    uid|text                  (pipe-delimited)

  Meta files:
    [[uid]] summary           (double-bracket inline summary)
    [uid]                     (single-bracket block — text follows on subsequent lines)
    uid|ordinal|summary       (pipe-delimited)

  Filename patterns:
    Ch01_Title.md / Ch01_notes.md / Ch01_meta.md
    chapter_01.md / chapter_01_meta.md
    ch01_title.md (subdirectory layout)

  Directory layouts:
    Flat:        input-dir/ch01_title.md, input-dir/ch01_title_meta.md
    Subdir:      input-dir/chapters/ch01_title.md
                 input-dir/chapters_meta/ch01_title_meta.md

Note on PIDs:
  Source UIDs (e.g. C01-4E4JEXDWPR) are used directly as BookML para PIDs.
  This preserves paragraph identity across drafts without requiring a PID registry.
"""

import re
import os
import sys
import zipfile
import argparse
from datetime import datetime, timezone
from xml.etree import ElementTree as ET
from xml.dom import minidom

BOOKML_NS = "https://bookml.org/ns/1.0"
BOOKML_VERSION = "1.0"

ET.register_namespace('', BOOKML_NS)

# ---------- Filename patterns (tried in order) ----------
CH_NUM_PATTERNS = [
    re.compile(r'[Cc]h(\d{1,2})[_\-]', re.IGNORECASE),                       # ch01_title.md
    re.compile(r'chapter[_\-](\d{1,2})(?:[_\-\.])', re.IGNORECASE),          # chapter_01.md
]

# ---------- Content patterns ----------
# Double-bracket: [[uid]] text (may span to next [[uid]])
BRACKET_DOUBLE_PATTERN = re.compile(r'\[\[([A-Za-z0-9\-]+)\]\]\s*(.*?)(?=\[\[|$)', re.DOTALL)

# Single-bracket on same line: [uid] text (space optional)
BRACKET_SINGLE_PATTERN = re.compile(r'^\[([A-Za-z0-9]+)\]\s*(.+)$', re.MULTILINE)

# Pipe format: uid|text
PIPE_PATTERN = re.compile(r'^([A-Za-z0-9]{6,})\|(.+)$', re.MULTILINE)

# Single-bracket block meta: [uid] alone on a line, then content until next [uid] or end
META_BLOCK_PATTERN = re.compile(
    r'^\[([A-Za-z0-9\-]+)\]\s*\n(.*?)(?=^\[[A-Za-z0-9\-]+\]\s*\n|\Z)',
    re.MULTILINE | re.DOTALL
)

# Chapter heading — take everything after the leading '# '
CHAPTER_HEADING = re.compile(r'^#\s+(.+)$', re.MULTILINE)

# Strip "Chapter NN - " or "Chapter NN: " prefix from title
CHAPTER_TITLE_PREFIX = re.compile(r'^Chapter\s+\d+\s*[-:]\s*', re.IGNORECASE)

# Section marker for "Draft Paragraphs (keyed)"
DRAFT_PARA_SECTION = re.compile(r'##\s*Draft Paragraphs\s*\(keyed\)', re.IGNORECASE)


def Q(tag):
    return f'{{{BOOKML_NS}}}{tag}'


def read_file(path):
    with open(path, 'r', encoding='utf-8-sig') as f:
        return f.read()


def find_chapter_num(filename):
    for pat in CH_NUM_PATTERNS:
        m = pat.search(filename)
        if m:
            return int(m.group(1))
    return None


def parse_chapter(content, filename):
    """Returns (ch_num, title, paragraphs=[(uid, text)])"""
    ch_num = find_chapter_num(filename) or 1

    title_match = CHAPTER_HEADING.search(content)
    raw_title = title_match.group(1).strip() if title_match else f"Chapter {ch_num}"
    title = CHAPTER_TITLE_PREFIX.sub('', raw_title).strip() or raw_title

    # Restrict parsing to content after "## Draft Paragraphs (keyed)" marker, if present
    section_match = DRAFT_PARA_SECTION.search(content)
    body = content[section_match.end():] if section_match else content

    paragraphs = []
    double_matches = BRACKET_DOUBLE_PATTERN.findall(body)
    if double_matches:
        for uid, text in double_matches:
            text = text.strip()
            if text:
                paragraphs.append((uid.strip(), text))
    else:
        single_matches = BRACKET_SINGLE_PATTERN.findall(body)
        if single_matches:
            for uid, text in single_matches:
                paragraphs.append((uid.strip(), text.strip()))
        else:
            for uid, text in PIPE_PATTERN.findall(body):
                paragraphs.append((uid.strip(), text.strip()))

    return ch_num, title, paragraphs


def parse_meta_summaries(content):
    """Returns {uid: summary_text}"""
    result = {}

    # Try double-bracket inline format first
    double_matches = BRACKET_DOUBLE_PATTERN.findall(content)
    if double_matches:
        for uid, text in double_matches:
            uid = uid.strip()
            if uid not in result:
                result[uid] = text.strip()
        return result

    # Try single-bracket block format: [uid] on own line, content follows
    block_matches = META_BLOCK_PATTERN.findall(content)
    if block_matches:
        for uid, block_text in block_matches:
            uid = uid.strip()
            summary = block_text.strip()
            if uid not in result and summary:
                result[uid] = summary
        return result

    # Fall back to pipe format: uid|ordinal|summary or uid|summary
    for line in content.splitlines():
        line = line.strip()
        if not line:
            continue
        parts = line.split('|', 2)
        if len(parts) < 2:
            continue
        uid = parts[0].strip()
        if not re.match(r'^[A-Za-z0-9\-]+$', uid):
            continue
        meta_text = parts[2].strip() if len(parts) == 3 else parts[1].strip()
        if uid not in result:
            result[uid] = meta_text
    return result


def parse_notes(content):
    """Returns {uid: note_text}, skipping n/a entries"""
    result = {}
    for line in content.splitlines():
        line = line.strip()
        if not line:
            continue
        pipe_idx = line.find('|')
        if pipe_idx <= 0:
            continue
        uid = line[:pipe_idx].strip()
        if not re.match(r'^[A-Za-z0-9\-]+$', uid):
            continue
        note = line[pipe_idx + 1:].strip()
        if not note or note.lower() == 'n/a':
            continue
        if uid not in result:
            result[uid] = note
    return result


def assign_seqs(paragraphs):
    """Returns {uid: seq} where seq = 1000, 2000, 3000, ...
    Source UIDs are used directly as BookML PIDs, so no PID generation is needed.
    """
    return {uid: (i + 1) * 1000 for i, (uid, _) in enumerate(paragraphs)}


def prettify(elem):
    raw = ET.tostring(elem, encoding='unicode')
    reparsed = minidom.parseString(raw.encode('utf-8'))
    return reparsed.toprettyxml(indent='  ', encoding='UTF-8').decode('utf-8')


def build_chapter_xml(chapter_id, ch_num, chapter_title, book_id, draft_number, paragraphs, uid_to_seq):
    root = ET.Element(Q('chapter'))
    root.set('bookml-version', BOOKML_VERSION)
    root.set('id', chapter_id)
    root.set('book-id', book_id)
    root.set('draft', str(draft_number))

    chinfo = ET.SubElement(root, Q('chapterinfo'))
    chapnum_el = ET.SubElement(chinfo, Q('chapternumber'))
    chapnum_el.text = str(ch_num)
    title_el = ET.SubElement(chinfo, Q('title'))
    title_el.text = chapter_title

    section = ET.SubElement(root, Q('section'))
    section.set('seq', '1000')

    for uid, text in paragraphs:
        seq = uid_to_seq.get(uid)
        if seq is None:
            continue
        para = ET.SubElement(section, Q('para'))
        para.set('pid', uid)          # Source UID is the PID — stable across drafts
        para.set('seq', str(seq))
        para.set('type', 'normal')
        para.set('draft-created', str(draft_number))
        para.set('draft-modified', str(draft_number))
        para.set('modified-by', 'human')
        para.text = text

    return prettify(root)


def build_meta_xml(chapter_id, book_id, draft_number, meta_summaries):
    root = ET.Element(Q('meta'))
    root.set('bookml-version', BOOKML_VERSION)
    root.set('chapter-id', chapter_id)
    root.set('book-id', book_id)
    root.set('draft', str(draft_number))

    for uid, summary in meta_summaries.items():
        if not summary:
            continue
        pm = ET.SubElement(root, Q('para-meta'))
        pm.set('pid', uid)
        tags_el = ET.SubElement(pm, Q('tags'))
        tag_el = ET.SubElement(tags_el, Q('tag'))
        tag_el.text = summary

    return prettify(root)


def build_notes_xml(chapter_id, book_id, draft_number, notes_data, author_name, now_str):
    root = ET.Element(Q('notes'))
    root.set('bookml-version', BOOKML_VERSION)
    root.set('chapter-id', chapter_id)
    root.set('book-id', book_id)
    root.set('draft', str(draft_number))

    note_counter = {}
    for uid, note_text in notes_data.items():
        n = note_counter.get(uid, 0) + 1
        note_counter[uid] = n

        pn = ET.SubElement(root, Q('para-notes'))
        pn.set('pid', uid)
        note_el = ET.SubElement(pn, Q('note'))
        note_el.set('id', f'{uid}-N{n:03d}')
        note_el.set('type', 'craft')
        note_el.set('author-type', 'human')
        note_el.set('author', author_name)
        note_el.set('draft', str(draft_number))
        note_el.set('created', now_str)
        body = ET.SubElement(note_el, Q('body'))
        body.text = note_text

    return prettify(root)


def build_book_xml(book_id, title, firstname, surname, draft_number, based_on, draft_label,
                   today_str, now_str, components):
    author_name = f'{firstname} {surname}'.strip() if firstname else surname

    root = ET.Element(Q('book'))
    root.set('bookml-version', BOOKML_VERSION)
    root.set('id', book_id)

    bookinfo = ET.SubElement(root, Q('bookinfo'))

    title_el = ET.SubElement(bookinfo, Q('title'))
    title_el.text = title

    author_el = ET.SubElement(bookinfo, Q('author'))
    author_el.set('role', 'author')
    if firstname:
        fn = ET.SubElement(author_el, Q('firstname'))
        fn.text = firstname
    sn = ET.SubElement(author_el, Q('surname'))
    sn.text = surname

    genre_el = ET.SubElement(bookinfo, Q('genre'))
    genre_el.text = 'fiction'

    lang_el = ET.SubElement(bookinfo, Q('language'))
    lang_el.text = 'en'

    versioning = ET.SubElement(bookinfo, Q('versioning'))
    draft = ET.SubElement(versioning, Q('draft'))
    draft.set('number', str(draft_number))
    draft.set('status', 'in-progress')
    draft.set('created', now_str)
    draft.set('based-on', str(based_on))
    draft.set('author-type', 'human')
    draft.set('author', author_name)
    draft.set('label', draft_label or f'Draft {draft_number}')

    created_el = ET.SubElement(bookinfo, Q('created'))
    created_el.text = today_str
    modified_el = ET.SubElement(bookinfo, Q('modified'))
    modified_el.text = today_str

    contents = ET.SubElement(root, Q('contents'))
    bodymatter = ET.SubElement(contents, Q('bodymatter'))
    for seq_num, (chapter_id, ch_file, meta_file, notes_file, chapter_title) in enumerate(components, 1):
        comp = ET.SubElement(bodymatter, Q('component'))
        comp.set('id', chapter_id)
        comp.set('type', 'chapter')
        comp.set('seq', str(seq_num * 1000))
        comp.set('chapter-file', ch_file)
        comp.set('meta-file', meta_file)
        comp.set('notes-file', notes_file)
        comp.set('title', chapter_title)
        comp.set('draft', str(draft_number))

    return prettify(root)


def find_chapters(input_dir):
    """Detect flat vs subdirectory layout and scan accordingly."""
    chapters_dir = os.path.join(input_dir, 'chapters')
    meta_dir = os.path.join(input_dir, 'chapters_meta')

    if os.path.isdir(chapters_dir):
        return find_chapters_subdirs(chapters_dir, meta_dir if os.path.isdir(meta_dir) else None)
    else:
        return find_chapters_flat(input_dir)


def find_chapters_subdirs(chapters_dir, meta_dir):
    """Subdirectory layout: chapters/ contains chapter files, chapters_meta/ has meta."""
    groups = {}

    # All files in chapters/ are chapter content (not meta)
    for fname in os.listdir(chapters_dir):
        fpath = os.path.join(chapters_dir, fname)
        if not os.path.isfile(fpath):
            continue
        if not fname.lower().endswith(('.md', '.txt')):
            continue
        ch_num = find_chapter_num(fname)
        if ch_num is None:
            continue
        if ch_num not in groups:
            groups[ch_num] = {'chapter': None, 'meta': None, 'notes': None, 'fname': ''}
        groups[ch_num]['chapter'] = fpath
        groups[ch_num]['fname'] = fname

    # All files in chapters_meta/ are meta files
    if meta_dir:
        for fname in os.listdir(meta_dir):
            fpath = os.path.join(meta_dir, fname)
            if not os.path.isfile(fpath):
                continue
            if not fname.lower().endswith(('.md', '.txt')):
                continue
            ch_num = find_chapter_num(fname)
            if ch_num is None or ch_num not in groups:
                continue
            lower = fname.lower()
            if '_notes' in lower:
                groups[ch_num]['notes'] = fpath
            else:
                groups[ch_num]['meta'] = fpath

    return sorted(groups.items())


def find_chapters_flat(input_dir):
    """Flat directory layout (original behavior)."""
    groups = {}
    for fname in os.listdir(input_dir):
        fpath = os.path.join(input_dir, fname)
        if not os.path.isfile(fpath):
            continue
        if not fname.lower().endswith(('.md', '.txt')):
            continue
        ch_num = find_chapter_num(fname)
        if ch_num is None:
            continue
        if ch_num not in groups:
            groups[ch_num] = {'chapter': None, 'meta': None, 'notes': None, 'fname': ''}
        lower = fname.lower()
        if '_meta' in lower:
            groups[ch_num]['meta'] = fpath
        elif '_notes' in lower:
            groups[ch_num]['notes'] = fpath
        else:
            groups[ch_num]['chapter'] = fpath
            groups[ch_num]['fname'] = fname
    return sorted(groups.items())


def main():
    parser = argparse.ArgumentParser(description='Convert old book files to BookML ZIP package')
    parser.add_argument('input_dir', help='Directory containing old-format chapter files')
    parser.add_argument('--title', required=True, help='Book title')
    parser.add_argument('--surname', required=True, help='Author surname')
    parser.add_argument('--firstname', default='', help='Author first name')
    parser.add_argument('--book-id', default=None, help='Book ID slug (e.g. carrier-to-yesterday)')
    parser.add_argument('--output', default=None, help='Output ZIP path')
    parser.add_argument('--draft', type=int, default=1, help='Draft number (default: 1)')
    parser.add_argument('--based-on', type=int, default=0, dest='based_on',
                        help='Draft number this is based on (default: 0)')
    parser.add_argument('--draft-label', default=None, dest='draft_label',
                        help='Human-readable label for this draft')
    args = parser.parse_args()

    input_dir = os.path.abspath(args.input_dir)
    if not os.path.isdir(input_dir):
        print(f"ERROR: Not a directory: {input_dir}", file=sys.stderr)
        sys.exit(1)

    book_id = args.book_id or re.sub(r'[^a-z0-9]+', '-', args.title.lower()).strip('-')[:30]
    output_path = args.output or os.path.join(
        os.path.dirname(input_dir),
        os.path.basename(input_dir) + '.bookml.zip'
    )

    now = datetime.now(timezone.utc)
    now_str = now.strftime('%Y-%m-%dT%H:%M:%SZ')
    today_str = now.strftime('%Y-%m-%d')
    author_name = f'{args.firstname} {args.surname}'.strip() if args.firstname else args.surname

    chapters = find_chapters(input_dir)
    if not chapters:
        print("ERROR: No chapter files found", file=sys.stderr)
        print("  Expected filenames like: ch01_title.md, chapter_01.md, Ch01_Title.md", file=sys.stderr)
        sys.exit(1)

    zip_entries = {}
    components = []

    for ch_num, group in chapters:
        chapter_id = f"CH{ch_num:02d}"
        if not group['chapter']:
            print(f"WARNING: No chapter content file for chapter {ch_num}, skipping")
            continue

        content = read_file(group['chapter'])
        _, chapter_title, paragraphs = parse_chapter(content, group['fname'])
        print(f"  {chapter_id}: '{chapter_title}' — {len(paragraphs)} paragraphs")

        meta_summaries = parse_meta_summaries(read_file(group['meta'])) if group['meta'] else {}
        notes_data = parse_notes(read_file(group['notes'])) if group['notes'] else {}
        print(f"          {len(meta_summaries)} meta entries, {len(notes_data)} notes")

        uid_to_seq = assign_seqs(paragraphs)

        ch_file = f"{chapter_id.lower()}-chapter.xml"
        meta_file = f"{chapter_id.lower()}-meta.xml"
        notes_file = f"{chapter_id.lower()}-notes.xml"

        zip_entries[ch_file] = build_chapter_xml(
            chapter_id, ch_num, chapter_title, book_id, args.draft,
            paragraphs, uid_to_seq)
        zip_entries[meta_file] = build_meta_xml(
            chapter_id, book_id, args.draft, meta_summaries)
        zip_entries[notes_file] = build_notes_xml(
            chapter_id, book_id, args.draft, notes_data, author_name, now_str)
        components.append((chapter_id, ch_file, meta_file, notes_file, chapter_title))

    zip_entries['book.xml'] = build_book_xml(
        book_id, args.title, args.firstname, args.surname,
        args.draft, args.based_on, args.draft_label,
        today_str, now_str, components)

    with zipfile.ZipFile(output_path, 'w', zipfile.ZIP_DEFLATED) as zf:
        for filename, xml_content in zip_entries.items():
            zf.writestr(filename, xml_content.encode('utf-8'))

    print(f"\nWrote: {output_path}")
    print(f"  {len(zip_entries)} files ({len(components)} chapter(s) + book.xml)")
    print(f"  Draft {args.draft}" + (f" (based on draft {args.based_on})" if args.based_on else ""))


if __name__ == '__main__':
    main()
