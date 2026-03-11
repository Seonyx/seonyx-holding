#!/usr/bin/env python3
"""
Convert Mayfly draft-2 unpacked folder to BookML XML format.

Input:  testdata/Mayfly-unpacked/
Output: testdata/mayfly-draft2.bookml.zip

Input file formats
  ch0N_chapter-N.md       ->  [[CH0N-PXXXX]] prose text <!-- REV2 -->
  ch0N_chapter-N_meta.md  ->  [[CH0N-PXXXX]] Topic: description
  ch0N_chapter-N_notes.txt->  CH0N-PXXXX|[query] note text
  mayfly-revision-state.json -> per-pid {when, agent, model}

All files are UTF-8 with BOM; chapter/meta files may have CRLF endings.
"""

import json
import os
import re
import zipfile

INPUT_DIR  = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'Mayfly-unpacked')
OUTPUT_ZIP = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'mayfly-draft2.bookml.zip')

NAMESPACE    = 'https://bookml.org/ns/1.0'
BOOK_ID      = 'mayfly'
BOOK_TITLE   = 'Mayfly'
DRAFT        = 2
BASED_ON     = 1
NUM_CHAPTERS = 12

# Fallback date used when a paragraph isn't in the revision state
FALLBACK_DATETIME = '2026-03-10T07:39:39+01:00'
FALLBACK_DATE     = '2026-03-10'

# The earliest timestamp in the revision state (used as draft created date)
DRAFT_CREATED_DATETIME = '2026-03-10T07:39:39+01:00'
# The latest timestamp (used as book modified date)
DRAFT_MODIFIED_DATE    = '2026-03-11'


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def xe(text):
    """XML-escape a string."""
    return (text
            .replace('&', '&amp;')
            .replace('<', '&lt;')
            .replace('>', '&gt;')
            .replace('"', '&quot;'))


def ch_id(n):
    return f'ch{n:02d}'


def chapter_id_upper(n):
    """Return upper-case chapter ID used in PIDs, e.g. CH01."""
    return f'CH{n:02d}'


# ---------------------------------------------------------------------------
# Parsers
# ---------------------------------------------------------------------------

_PARA_RE  = re.compile(r'^\[\[([A-Z0-9\-]+)\]\]\s+(.+?)(?:\s*<!--\s*REV\d+\s*-->)?\s*$')
_META_RE  = re.compile(r'^\[\[([A-Z0-9\-]+)\]\]\s+Topic:\s+(.+?)\s*$', re.IGNORECASE)
_NOTE_RE  = re.compile(r'^([A-Z0-9\-]+)\|\[(\w+)\]\s+(.+?)\s*$')


def parse_chapter(path):
    """Return ordered list of (pid, text)."""
    paras = []
    with open(path, encoding='utf-8-sig') as f:
        for line in f:
            line = line.rstrip('\r\n')
            m = _PARA_RE.match(line)
            if m:
                paras.append((m.group(1), m.group(2)))
    return paras


def parse_meta(path):
    """Return {pid: topic_text} dict."""
    meta = {}
    with open(path, encoding='utf-8-sig') as f:
        for line in f:
            line = line.rstrip('\r\n')
            m = _META_RE.match(line)
            if m:
                meta[m.group(1)] = m.group(2)
    return meta


def parse_notes(path):
    """Return {pid: (note_type, note_text)} dict."""
    notes = {}
    if not os.path.isfile(path):
        return notes
    with open(path, encoding='utf-8-sig') as f:
        for line in f:
            line = line.rstrip('\r\n')
            m = _NOTE_RE.match(line)
            if m:
                notes[m.group(1)] = (m.group(2), m.group(3))
    return notes


def load_revision_state(path):
    """Return {ch_id_lower: {pid: {when, agent, model}}}."""
    with open(path, encoding='utf-8-sig') as f:
        state = json.load(f)
    return state.get('revised', {})


# ---------------------------------------------------------------------------
# XML generators
# ---------------------------------------------------------------------------

def chapter_xml(n, paras, rev_state):
    """Generate chapter XML for chapter n."""
    cid      = ch_id(n)
    ch_upper = chapter_id_upper(n)
    ch_revs  = rev_state.get(cid, {})

    lines = [
        '<?xml version="1.0" encoding="UTF-8"?>',
        f'<chapter xmlns="{NAMESPACE}"',
        f'         xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"',
        f'         xsi:schemaLocation="{NAMESPACE} ../schema/bookml-chapter.xsd"',
        f'         bookml-version="1.0"',
        f'         id="{cid}"',
        f'         book-id="{BOOK_ID}"',
        f'         draft="{DRAFT}">',
        '  <chapterinfo>',
        f'    <chapternumber>{n}</chapternumber>',
        f'    <title>Chapter {n}</title>',
        '  </chapterinfo>',
        '  <section seq="1000">',
    ]

    for i, (pid, text) in enumerate(paras, start=1):
        seq      = i * 1000
        rev_info = ch_revs.get(pid, {})
        mod_date = rev_info.get('when', FALLBACK_DATETIME)

        lines.append(
            f'    <para pid="{pid}" seq="{seq}" type="normal"'
            f' draft-created="{BASED_ON}" draft-modified="{DRAFT}"'
            f' modified-by="ai" modified-date="{xe(mod_date)}">'
        )
        lines.append(f'      {xe(text)}')
        lines.append('    </para>')

    lines += ['  </section>', '</chapter>']
    return '\n'.join(lines)


def meta_xml(n, paras, meta_data):
    """Generate meta XML for chapter n (sparse — only paragraphs with a topic)."""
    cid = ch_id(n)

    lines = [
        '<?xml version="1.0" encoding="UTF-8"?>',
        f'<meta xmlns="{NAMESPACE}"',
        f'      xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"',
        f'      xsi:schemaLocation="{NAMESPACE} ../schema/bookml-meta.xsd"',
        f'      bookml-version="1.0"',
        f'      chapter-id="{cid}"',
        f'      book-id="{BOOK_ID}"',
        f'      draft="{DRAFT}">',
    ]

    has_entries = False
    for pid, _text in paras:
        if pid in meta_data:
            lines.append(f'  <para-meta pid="{pid}">')
            lines.append(f'    <topic>{xe(meta_data[pid])}</topic>')
            lines.append('  </para-meta>')
            has_entries = True

    lines.append('</meta>')

    if not has_entries:
        return (
            '<?xml version="1.0" encoding="UTF-8"?>\n'
            f'<meta xmlns="{NAMESPACE}"'
            f' xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"'
            f' xsi:schemaLocation="{NAMESPACE} ../schema/bookml-meta.xsd"'
            f' bookml-version="1.0" chapter-id="{cid}"'
            f' book-id="{BOOK_ID}" draft="{DRAFT}"/>'
        )

    return '\n'.join(lines)


def notes_xml(n, paras, notes_data):
    """Generate notes XML for chapter n (only paragraphs with notes)."""
    cid = ch_id(n)

    lines = [
        '<?xml version="1.0" encoding="UTF-8"?>',
        f'<notes xmlns="{NAMESPACE}"',
        f'       xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"',
        f'       xsi:schemaLocation="{NAMESPACE} ../schema/bookml-notes.xsd"',
        f'       bookml-version="1.0"',
        f'       chapter-id="{cid}"',
        f'       book-id="{BOOK_ID}"',
        f'       draft="{DRAFT}">',
    ]

    has_entries = False
    for pid, _text in paras:
        if pid in notes_data:
            note_type, note_text = notes_data[pid]
            note_id = f'{pid}-N001'
            lines.append(f'  <para-notes pid="{pid}">')
            lines.append(
                f'    <note id="{note_id}" type="{xe(note_type)}"'
                f' author-type="human" draft="{DRAFT}">'
            )
            lines.append(f'      <body>{xe(note_text)}</body>')
            lines.append('    </note>')
            lines.append('  </para-notes>')
            has_entries = True

    lines.append('</notes>')

    if not has_entries:
        return (
            '<?xml version="1.0" encoding="UTF-8"?>\n'
            f'<notes xmlns="{NAMESPACE}"'
            f' xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"'
            f' xsi:schemaLocation="{NAMESPACE} ../schema/bookml-notes.xsd"'
            f' bookml-version="1.0" chapter-id="{cid}"'
            f' book-id="{BOOK_ID}" draft="{DRAFT}"/>'
        )

    return '\n'.join(lines)


def book_xml():
    components = []
    for n in range(1, NUM_CHAPTERS + 1):
        cid = ch_id(n)
        seq = n * 1000
        components.append(
            f'      <component id="{cid}" type="chapter" seq="{seq}"\n'
            f'                 chapter-file="{cid}/{cid}-chapter.xml"\n'
            f'                 meta-file="{cid}/{cid}-meta.xml"\n'
            f'                 notes-file="{cid}/{cid}-notes.xml"\n'
            f'                 title="Chapter {n}"\n'
            f'                 draft="{DRAFT}"/>'
        )

    return '\n'.join([
        '<?xml version="1.0" encoding="UTF-8"?>',
        f'<book xmlns="{NAMESPACE}"',
        f'      bookml-version="1.0"',
        f'      id="{BOOK_ID}"',
        f'      xml:lang="en">',
        '  <bookinfo>',
        f'    <title>{xe(BOOK_TITLE)}</title>',
        '    <author role="author">',
        '      <surname>Unknown</surname>',
        '    </author>',
        '    <genre>fiction</genre>',
        '    <language>en</language>',
        '    <versioning>',
        f'      <draft number="{DRAFT}" status="in-progress"',
        f'             created="{DRAFT_CREATED_DATETIME}"',
        f'             based-on="{BASED_ON}"',
        f'             author-type="ai"',
        f'             author="Mayfly / GPT-5.2"',
        f'             label="Mayfly AI revision"/>',
        '    </versioning>',
        f'    <created>{FALLBACK_DATE}</created>',
        f'    <modified>{DRAFT_MODIFIED_DATE}</modified>',
        '  </bookinfo>',
        '  <contents>',
        '    <bodymatter>',
        '\n'.join(components),
        '    </bodymatter>',
        '  </contents>',
        '</book>',
    ])


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def main():
    rev_state_path = os.path.join(INPUT_DIR, 'mayfly-revision-state.json')
    rev_state = load_revision_state(rev_state_path)
    print(f'Revision state loaded: {sum(len(v) for v in rev_state.values())} total revised pids')

    with zipfile.ZipFile(OUTPUT_ZIP, 'w', zipfile.ZIP_DEFLATED) as zf:

        # book.xml
        bxml = book_xml()
        zf.writestr('book.xml', bxml.encode('utf-8'))
        print('[+] book.xml')

        for n in range(1, NUM_CHAPTERS + 1):
            cid = ch_id(n)

            chapter_path = os.path.join(INPUT_DIR, f'{cid}_chapter-{n}.md')
            meta_path    = os.path.join(INPUT_DIR, f'{cid}_chapter-{n}_meta.md')
            notes_path   = os.path.join(INPUT_DIR, f'{cid}_chapter-{n}_notes.txt')

            paras      = parse_chapter(chapter_path)
            meta_data  = parse_meta(meta_path)
            notes_data = parse_notes(notes_path)

            ch_xml  = chapter_xml(n, paras, rev_state)
            mt_xml  = meta_xml(n, paras, meta_data)
            nt_xml  = notes_xml(n, paras, notes_data)

            zf.writestr(f'{cid}/{cid}-chapter.xml', ch_xml.encode('utf-8'))
            zf.writestr(f'{cid}/{cid}-meta.xml',    mt_xml.encode('utf-8'))
            zf.writestr(f'{cid}/{cid}-notes.xml',   nt_xml.encode('utf-8'))

            note_count = len(notes_data)
            meta_count = len(meta_data)
            print(f'[+] {cid}: {len(paras)} paras, {meta_count} meta, {note_count} notes')

    print(f'\nDone: {OUTPUT_ZIP}')

    # Quick sanity check
    with zipfile.ZipFile(OUTPUT_ZIP, 'r') as zf:
        names = sorted(zf.namelist())
    print(f'Zip contains {len(names)} files.')


if __name__ == '__main__':
    main()
