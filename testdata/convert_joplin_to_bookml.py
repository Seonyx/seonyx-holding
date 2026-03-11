#!/usr/bin/env python3
"""
Convert MAYFLY Joplin export to BookML XML format.

Input:  testdata/MAYFLY_JOPLIN_EXPORT/
Output: testdata/mayfly-joplin-draft1.bookml.zip

Joplin file formats:
  Chapter N.md         ->  UNIQUEID|paragraph text
  Chapter_N_meta.md    ->  UNIQUEID|seq|description
  Chapter_N_notes.md   ->  UNIQUEID|note text  (n/a entries skipped)

Chapters 1-5 have notes files; chapters 6-12 do not.
"""

import os
import re
import zipfile

EXPORT_DIR  = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'MAYFLY_JOPLIN_EXPORT')
OUTPUT_ZIP  = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'mayfly-joplin-draft1.bookml.zip')

NAMESPACE       = 'https://bookml.org/ns/1.0'
BOOK_ID         = 'mayfly'
BOOK_TITLE      = 'Mayfly'
TODAY_DATE      = '2026-03-04'
TODAY_DATETIME  = '2026-03-04T00:00:00'
DRAFT           = 1
NUM_CHAPTERS    = 12
NOTES_CHAPTERS  = set(range(1, 6))   # chapters 1-5 have notes files

# --------------------------------------------------------------------------- #
# Parsing                                                                      #
# --------------------------------------------------------------------------- #

_UID_PAT = re.compile(r'([A-Z0-9]{12})\|')


def parse_chapter_file(path):
    """Return list of (uid, text) in file order, skipping blanks."""
    entries = []
    with open(path, 'r', encoding='utf-8') as fh:
        for line in fh:
            line = line.strip()
            if not line:
                continue
            m = _UID_PAT.search(line)
            if m:
                # Text starts after the first pipe
                pipe_pos = line.index('|', m.start())
                text = line[pipe_pos + 1:].strip()
                if text:
                    entries.append((m.group(1), text))
    return entries


def parse_meta_file(path):
    """Return dict uid -> description (ignoring seq number)."""
    meta = {}
    with open(path, 'r', encoding='utf-8') as fh:
        for line in fh:
            line = line.strip()
            if not line:
                continue
            m = _UID_PAT.search(line)
            if not m:
                continue
            uid = m.group(1)
            # Format: UID|seq|description  - skip seq, grab description
            parts = line[m.start():].split('|', 2)
            if len(parts) == 3:
                desc = parts[2].strip()
                if desc:
                    meta[uid] = desc
    return meta


def parse_notes_file(path):
    """Return dict uid -> note text (entries where note is n/a are skipped)."""
    notes = {}
    with open(path, 'r', encoding='utf-8') as fh:
        for line in fh:
            line = line.strip()
            if not line:
                continue
            m = _UID_PAT.search(line)
            if not m:
                continue
            uid = m.group(1)
            # Text is everything after the first pipe following uid
            tail_start = m.start() + len(uid) + 1   # skip "UID|"
            note = line[tail_start:].strip()
            # Skip n/a variants
            if note.lower().strip('/') == 'n/a':
                continue
            if note:
                notes[uid] = note
    return notes


# --------------------------------------------------------------------------- #
# XML helpers                                                                  #
# --------------------------------------------------------------------------- #

def xe(text):
    """Escape special XML characters in text content."""
    return (text
            .replace('&', '&amp;')
            .replace('<', '&lt;')
            .replace('>', '&gt;'))


def ch_id(n):
    return f'ch{n:02d}'


def pid_prefix(n):
    return f'CH{n:02d}'


def make_pid(chapter_num, para_index_1based):
    """Para index is 1-based. Returns e.g. CH01-P0010 for index 1."""
    return f'{pid_prefix(chapter_num)}-P{para_index_1based * 10:04d}'


# --------------------------------------------------------------------------- #
# XML generators                                                               #
# --------------------------------------------------------------------------- #

def chapter_xml(chapter_num, paragraphs):
    cid   = ch_id(chapter_num)
    lines = [
        '<?xml version="1.0" encoding="UTF-8"?>',
        f'<chapter xmlns="{NAMESPACE}"',
        f'         bookml-version="1.0"',
        f'         id="{cid}"',
        f'         book-id="{BOOK_ID}"',
        f'         draft="{DRAFT}">',
        '  <chapterinfo>',
        f'    <chapternumber>{chapter_num}</chapternumber>',
        f'    <title>Chapter {chapter_num}</title>',
        '  </chapterinfo>',
        '  <section seq="1000">',
    ]
    for i, (uid, text) in enumerate(paragraphs, start=1):
        pid = make_pid(chapter_num, i)
        seq = i * 1000
        lines.append(
            f'    <para pid="{pid}" seq="{seq}" type="normal"'
            f' draft-created="{DRAFT}" draft-modified="{DRAFT}"'
            f' modified-by="ai">{xe(text)}</para>'
        )
    lines += [
        '  </section>',
        '</chapter>',
    ]
    return '\n'.join(lines)


def meta_xml(chapter_num, paragraphs, meta_data):
    """
    Produces a meta file where each paragraph with a Joplin meta description
    gets a <para-meta> record with the description stored as <topic>.
    Paragraphs without a meta description are omitted (sparse file).
    """
    cid = ch_id(chapter_num)
    lines = [
        '<?xml version="1.0" encoding="UTF-8"?>',
        f'<meta xmlns="{NAMESPACE}"',
        f'      bookml-version="1.0"',
        f'      chapter-id="{cid}"',
        f'      book-id="{BOOK_ID}"',
        f'      draft="{DRAFT}">',
    ]

    for i, (uid, _text) in enumerate(paragraphs, start=1):
        if uid in meta_data:
            pid = make_pid(chapter_num, i)
            lines.append(f'  <para-meta pid="{pid}">')
            lines.append(f'    <topic>{xe(meta_data[uid])}</topic>')
            lines.append('  </para-meta>')

    lines.append('</meta>')
    return '\n'.join(lines)


def notes_xml(chapter_num, paragraphs, notes_data):
    """
    Produces a notes file for editorial annotations from the Joplin notes file.
    Each annotation becomes a <note type="query"> entry.
    """
    cid = ch_id(chapter_num)
    lines = [
        '<?xml version="1.0" encoding="UTF-8"?>',
        f'<notes xmlns="{NAMESPACE}"',
        f'       bookml-version="1.0"',
        f'       chapter-id="{cid}"',
        f'       book-id="{BOOK_ID}"',
        f'       draft="{DRAFT}">',
    ]

    for i, (uid, _text) in enumerate(paragraphs, start=1):
        if uid in notes_data:
            pid = make_pid(chapter_num, i)
            note_id = f'{pid}-N001'
            lines.append(f'  <para-notes pid="{pid}">')
            lines.append(
                f'    <note id="{note_id}" type="query"'
                f' author-type="human" draft="{DRAFT}">'
            )
            lines.append(f'      <body>{xe(notes_data[uid])}</body>')
            lines.append('    </note>')
            lines.append('  </para-notes>')

    lines.append('</notes>')
    return '\n'.join(lines)


def book_xml(chapter_count):
    lines = [
        '<?xml version="1.0" encoding="UTF-8"?>',
        f'<book xmlns="{NAMESPACE}"',
        f'      bookml-version="1.0"',
        f'      id="{BOOK_ID}"',
        f'      xml:lang="en">',
        '  <bookinfo>',
        f'    <title>{BOOK_TITLE}</title>',
        '    <author role="author">',
        '      <surname>Unknown</surname>',
        '    </author>',
        '    <genre>fiction</genre>',
        '    <language>en</language>',
        '    <versioning>',
        f'      <draft number="{DRAFT}" status="in-progress"',
        f'             created="{TODAY_DATETIME}"',
        f'             based-on="0"',
        f'             author-type="human"',
        f'             author="Joplin Import"',
        f'             label="Initial Joplin import"/>',
        '    </versioning>',
        f'    <created>{TODAY_DATE}</created>',
        f'    <modified>{TODAY_DATE}</modified>',
        '  </bookinfo>',
        '  <contents>',
        '    <bodymatter>',
    ]
    for n in range(1, chapter_count + 1):
        cid = ch_id(n)
        seq = n * 1000
        lines += [
            f'      <component id="{cid}" type="chapter" seq="{seq}"',
            f'                 chapter-file="{cid}/{cid}-chapter.xml"',
            f'                 meta-file="{cid}/{cid}-meta.xml"',
            f'                 notes-file="{cid}/{cid}-notes.xml"',
            f'                 title="Chapter {n}"',
            f'                 draft="{DRAFT}"/>',
        ]
    lines += [
        '    </bodymatter>',
        '  </contents>',
        '</book>',
    ]
    return '\n'.join(lines)


# --------------------------------------------------------------------------- #
# Main                                                                         #
# --------------------------------------------------------------------------- #

def main():
    print(f'Reading Joplin export from: {EXPORT_DIR}')
    print(f'Output zip:                 {OUTPUT_ZIP}')
    print()

    chapter_paragraphs = {}  # chapter_num -> [(uid, text)]
    chapter_meta       = {}  # chapter_num -> {uid: desc}
    chapter_notes      = {}  # chapter_num -> {uid: note}

    # ------------------------------------------------------------------ #
    # Parse input files                                                    #
    # ------------------------------------------------------------------ #
    for n in range(1, NUM_CHAPTERS + 1):
        chapter_path = os.path.join(EXPORT_DIR, f'Chapter {n}.md')
        meta_path    = os.path.join(EXPORT_DIR, f'Chapter_{n}_meta.md')
        notes_path   = os.path.join(EXPORT_DIR, f'Chapter_{n}_notes.md')

        if not os.path.isfile(chapter_path):
            print(f'  ERROR: chapter file not found: {chapter_path}')
            continue

        paras = parse_chapter_file(chapter_path)
        chapter_paragraphs[n] = paras
        print(f'  Chapter {n:2d}: {len(paras):3d} paragraphs', end='')

        if os.path.isfile(meta_path):
            chapter_meta[n] = parse_meta_file(meta_path)
            print(f'  {len(chapter_meta[n]):3d} meta entries', end='')
        else:
            chapter_meta[n] = {}
            print(f'    (no meta file)', end='')

        if n in NOTES_CHAPTERS and os.path.isfile(notes_path):
            chapter_notes[n] = parse_notes_file(notes_path)
            print(f'  {len(chapter_notes[n]):3d} notes', end='')
        else:
            chapter_notes[n] = {}

        print()

    print()

    # ------------------------------------------------------------------ #
    # Build zip                                                            #
    # ------------------------------------------------------------------ #
    with zipfile.ZipFile(OUTPUT_ZIP, 'w', zipfile.ZIP_DEFLATED) as zf:

        # book.xml
        book_content = book_xml(NUM_CHAPTERS)
        zf.writestr('book.xml', book_content.encode('utf-8'))
        print('  [+] book.xml')

        # Per-chapter files
        for n in range(1, NUM_CHAPTERS + 1):
            if n not in chapter_paragraphs:
                continue
            paras      = chapter_paragraphs[n]
            meta_data  = chapter_meta.get(n, {})
            notes_data = chapter_notes.get(n, {})
            cid        = ch_id(n)

            ch_content  = chapter_xml(n, paras)
            met_content = meta_xml(n, paras, meta_data)
            nt_content  = notes_xml(n, paras, notes_data)

            zf.writestr(f'{cid}/{cid}-chapter.xml', ch_content.encode('utf-8'))
            zf.writestr(f'{cid}/{cid}-meta.xml',    met_content.encode('utf-8'))
            zf.writestr(f'{cid}/{cid}-notes.xml',   nt_content.encode('utf-8'))
            print(f'  [+] {cid}/ ({len(paras)} paras)')

    print()
    print(f'Done. Created: {OUTPUT_ZIP}')

    # ------------------------------------------------------------------ #
    # Quick sanity check: list zip contents                               #
    # ------------------------------------------------------------------ #
    with zipfile.ZipFile(OUTPUT_ZIP, 'r') as zf:
        names = zf.namelist()
    print(f'Zip contains {len(names)} files:')
    for name in sorted(names):
        print(f'  {name}')


if __name__ == '__main__':
    main()
