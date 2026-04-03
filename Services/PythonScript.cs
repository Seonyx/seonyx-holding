namespace Seonyx.Web.Services
{
    /// <summary>
    /// Embeds the generate_audiobook.py script that is bundled into every
    /// audiobook package ZIP. The script is identical in every package;
    /// only config.json varies between downloads.
    /// </summary>
    public static class PythonScript
    {
        public static readonly string Source = @"#!/usr/bin/env python3
""""""
Audiobook Generator -- Book Editor Export
Generates audio from the chapter text files in this package using Piper TTS.

Usage:
    python generate_audiobook.py

Requirements:
    Python 3.8+  (no additional packages needed)
    ffmpeg       (optional -- enables MP3 output in addition to WAV)

Piper TTS and the required voice model will be downloaded automatically
on first run (~30-100 MB depending on voice quality).
""""""

import json
import os
import platform
import shutil
import struct
import subprocess
import sys
import urllib.request
import wave
import zipfile
from pathlib import Path

# ---------------------------------------------------------------------------
# Paths
# ---------------------------------------------------------------------------
SCRIPT_DIR   = Path(__file__).parent.resolve()
CHAPTERS_DIR = SCRIPT_DIR / ""chapters""
OUTPUT_DIR   = SCRIPT_DIR / ""output""
CACHE_DIR    = SCRIPT_DIR / "".piper_cache""
VOICES_DIR   = CACHE_DIR / ""voices""
BINARY_DIR   = CACHE_DIR / ""bin""

CONFIG_FILE  = SCRIPT_DIR / ""config.json""

# ---------------------------------------------------------------------------
# Piper release info
# ---------------------------------------------------------------------------
PIPER_RELEASE_BASE = (
    ""https://github.com/OHF-Voice/piper1-gpl/releases/latest/download/""
)
PIPER_VOICE_BASE = (
    ""https://huggingface.co/rhasspy/piper-voices/resolve/v1.0.0/""
)

# Map (system, machine) -> (archive_name, binary_name_inside_archive)
PIPER_BINARIES = {
    (""windows"", ""amd64""):  (""piper_windows_amd64.zip"",       ""piper.exe""),
    (""linux"",   ""x86_64""): (""piper_linux_x86_64.tar.gz"",     ""piper""),
    (""linux"",   ""aarch64""):(""piper_linux_aarch64.tar.gz"",    ""piper""),
    (""darwin"",  ""arm64""):  (""piper_macos_aarch64.tar.gz"",    ""piper""),
    (""darwin"",  ""x86_64""): (""piper_macos_x86_64.tar.gz"",     ""piper""),
}

# ---------------------------------------------------------------------------
# Silence generation (pure Python -- no ffmpeg needed)
# ---------------------------------------------------------------------------
SCENE_BREAK_SILENCE_SECS  = 1.5
CHAPTER_END_SILENCE_SECS  = 2.0

def make_silence_wav(duration_secs, sample_rate):
    """"""Return raw WAV bytes for a period of silence.""""""
    n_frames = int(duration_secs * sample_rate)
    import io
    buf = io.BytesIO()
    with wave.open(buf, ""wb"") as wf:
        wf.setnchannels(1)
        wf.setsampwidth(2)          # 16-bit
        wf.setframerate(sample_rate)
        wf.writeframes(b""\x00\x00"" * n_frames)
    return buf.getvalue()

# ---------------------------------------------------------------------------
# Platform detection
# ---------------------------------------------------------------------------
def detect_platform():
    system  = platform.system().lower()
    machine = platform.machine().lower()
    # Normalise machine name
    if machine in (""x86_64"", ""amd64""):
        machine = ""amd64"" if system == ""windows"" else ""x86_64""
    key = (system, machine)
    if key not in PIPER_BINARIES:
        print(""ERROR: Unsupported platform: {} / {}"".format(system, machine))
        print(""Supported: Windows x64, Linux x86_64/aarch64, macOS arm64/x86_64"")
        sys.exit(1)
    return key

# ---------------------------------------------------------------------------
# Download helpers
# ---------------------------------------------------------------------------
def download_file(url, dest, label):
    dest.parent.mkdir(parents=True, exist_ok=True)
    print(""  Downloading {}..."".format(label))
    def progress(count, block, total):
        if total > 0:
            pct = min(count * block * 100 // total, 100)
            print(""    {}%"".format(pct), end=""\r"")
    urllib.request.urlretrieve(url, dest, reporthook=progress)
    print(""    Done ({:,} KB)         "".format(dest.stat().st_size // 1024))

def ensure_piper(platform_key):
    archive_name, binary_name = PIPER_BINARIES[platform_key]
    binary_path = BINARY_DIR / binary_name

    if binary_path.exists():
        return binary_path

    print(""Piper TTS binary not found. Downloading..."")
    archive_path = CACHE_DIR / archive_name
    download_file(PIPER_RELEASE_BASE + archive_name, archive_path, ""Piper binary"")

    BINARY_DIR.mkdir(parents=True, exist_ok=True)
    if archive_name.endswith("".zip""):
        with zipfile.ZipFile(archive_path) as zf:
            for member in zf.namelist():
                filename = Path(member).name
                if filename in (binary_name, ""espeak-ng-data"") or \
                   ""espeak-ng-data"" in member:
                    zf.extract(member, BINARY_DIR)
    else:
        import tarfile
        with tarfile.open(archive_path) as tf:
            tf.extractall(BINARY_DIR)

    # Make binary executable on Unix
    if platform_key[0] != ""windows"":
        binary_path.chmod(0o755)

    archive_path.unlink()  # clean up archive
    print(""Piper installed."")
    return binary_path

def ensure_voice(voice_id):
    """"""
    Returns (onnx_path, sample_rate).
    voice_id format: en_GB-alan-medium  or  en_US-ryan-high
    """"""
    onnx_path   = VOICES_DIR / ""{}.onnx"".format(voice_id)
    config_path = VOICES_DIR / ""{}.onnx.json"".format(voice_id)

    if not onnx_path.exists() or not config_path.exists():
        print(""Voice model '{}' not found. Downloading..."".format(voice_id))
        VOICES_DIR.mkdir(parents=True, exist_ok=True)

        # Build HuggingFace URL from voice_id
        # Format: en_GB-alan-medium -> en/en_GB/alan/medium/
        parts   = voice_id.split(""-"")          # ['en_GB', 'alan', 'medium']
        locale  = parts[0]                      # en_GB
        lang    = locale.split(""_"")[0]          # en
        name    = ""-"".join(parts[1:-1])         # alan  (handles multi-word names)
        quality = parts[-1]                     # medium
        base    = ""{}{}/{}/{}/{:s}/"".format(
            PIPER_VOICE_BASE, lang, locale, name, quality)

        download_file(base + ""{}.onnx"".format(voice_id),      onnx_path,   ""voice model"")
        download_file(base + ""{}.onnx.json"".format(voice_id), config_path, ""voice config"")
        print(""Voice '{}' installed."".format(voice_id))

    with open(config_path) as f:
        config = json.load(f)
    sample_rate = config[""audio""][""sample_rate""]
    return onnx_path, sample_rate

# ---------------------------------------------------------------------------
# Chapter text parsing
# ---------------------------------------------------------------------------
def parse_chapter(txt_path):
    """"""
    Returns a list of dicts:
      { ""type"": ""speech"",          ""text"": ""..."" }
      { ""type"": ""scene_break""                      }
      { ""type"": ""chapter_heading"", ""text"": ""..."" }
    """"""
    items = []
    chapter_number = None
    chapter_title  = None

    with open(txt_path, encoding=""utf-8"") as f:
        lines = f.readlines()

    prose_buffer = []

    def flush_prose():
        text = "" "".join(prose_buffer).strip()
        if text:
            items.append({""type"": ""speech"", ""text"": text})
        prose_buffer.clear()

    for line in lines:
        line = line.rstrip(""\n"")
        if line.startswith(""## CHAPTER_NUMBER:""):
            flush_prose()
            chapter_number = line[len(""## CHAPTER_NUMBER:""):].strip()
        elif line.startswith(""## CHAPTER_TITLE:""):
            flush_prose()
            chapter_title = line[len(""## CHAPTER_TITLE:""):].strip()
            # Emit heading as a single speech item
            if chapter_number:
                heading = ""{}. {}"".format(chapter_number, chapter_title)
            else:
                heading = chapter_title
            items.append({""type"": ""chapter_heading"", ""text"": heading})
            chapter_number = None
            chapter_title  = None
        elif line.startswith(""## SCENE_BREAK""):
            flush_prose()
            items.append({""type"": ""scene_break""})
        elif line.startswith(""## ""):
            flush_prose()   # unknown directive -- skip
        elif line.strip() == """":
            flush_prose()
        else:
            prose_buffer.append(line.strip())

    flush_prose()
    return items

# ---------------------------------------------------------------------------
# TTS synthesis (one text item -> WAV bytes)
# ---------------------------------------------------------------------------
def synthesise(text, piper_bin, onnx_path, speaker_id, espeakng_data):
    args = [
        str(piper_bin),
        ""--model"",          str(onnx_path),
        ""--output-raw"",
        ""--sentence-silence"", ""0.3"",
    ]
    if speaker_id is not None:
        args += [""--speaker"", str(speaker_id)]

    env = os.environ.copy()
    env[""PIPER_ESPEAKNG_DATA""] = str(espeakng_data)

    result = subprocess.run(
        args,
        input=text.encode(""utf-8""),
        capture_output=True,
        env=env,
    )
    if result.returncode != 0:
        raise RuntimeError(
            ""Piper failed:\n{}"".format(result.stderr.decode(errors=""replace"")))
    return result.stdout  # raw PCM bytes

# ---------------------------------------------------------------------------
# WAV assembly from PCM chunks
# ---------------------------------------------------------------------------
def pcm_chunks_to_wav(chunks, sample_rate):
    """"""Concatenate raw PCM chunks and wrap in a WAV container.""""""
    import io
    all_pcm = b"""".join(chunks)
    buf = io.BytesIO()
    with wave.open(buf, ""wb"") as wf:
        wf.setnchannels(1)
        wf.setsampwidth(2)
        wf.setframerate(sample_rate)
        wf.writeframes(all_pcm)
    return buf.getvalue()

def wav_duration(wav_bytes):
    import io
    with wave.open(io.BytesIO(wav_bytes)) as wf:
        return wf.getnframes() / wf.getframerate()

# ---------------------------------------------------------------------------
# Optional MP3 conversion
# ---------------------------------------------------------------------------
def ffmpeg_available():
    return shutil.which(""ffmpeg"") is not None

def wav_to_mp3(wav_path, mp3_path):
    subprocess.run(
        [""ffmpeg"", ""-y"", ""-i"", str(wav_path),
         ""-codec:a"", ""libmp3lame"", ""-qscale:a"", ""4"", str(mp3_path)],
        check=True,
        capture_output=True,
    )

# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------
def main():
    print(""="" * 60)
    print(""  Audiobook Generator -- Book Editor Package"")
    print(""="" * 60)

    # Load config
    if not CONFIG_FILE.exists():
        print(""ERROR: config.json not found. Run this script from the"")
        print(""       extracted package directory."")
        sys.exit(1)

    with open(CONFIG_FILE, encoding=""utf-8"") as f:
        config = json.load(f)

    book_title  = config[""bookTitle""]
    voice_id    = config[""voiceId""]
    speaker_id  = config.get(""speakerId"")  # None for single-speaker voices
    chapters    = config[""chapterFiles""]

    print(""\""\nBook:  {}"".format(book_title))
    print(""Voice: {} ({}, {})"".format(
        config[""voiceLabel""], config[""accent""], config[""gender""]))
    print(""Chapters: {}"".format(len(chapters)))

    # Ensure Piper and voice
    platform_key = detect_platform()
    print(""\n--- Checking dependencies ---"")
    piper_bin  = ensure_piper(platform_key)
    onnx_path, sample_rate = ensure_voice(voice_id)

    espeakng_data = BINARY_DIR / ""espeak-ng-data""
    # Some extractions nest differently -- find espeak-ng-data if not at root
    if not espeakng_data.exists():
        for p in BINARY_DIR.rglob(""espeak-ng-data""):
            if p.is_dir():
                espeakng_data = p
                break

    use_mp3 = ffmpeg_available()
    if use_mp3:
        print(""ffmpeg found -- will produce MP3 in addition to WAV."")
    else:
        print(""ffmpeg not found -- WAV output only."")
        print(""  (Install ffmpeg and re-run to also get MP3 files.)"")

    OUTPUT_DIR.mkdir(exist_ok=True)

    print(""\n--- Generating audio ---"")
    silence_scene   = make_silence_wav(SCENE_BREAK_SILENCE_SECS, sample_rate)
    silence_chapter = make_silence_wav(CHAPTER_END_SILENCE_SECS, sample_rate)

    total_duration = 0.0
    summary_lines  = [""{:>4}  {:<40}  {:>10}  File"".format("""", ""Title"", ""Duration"")]

    for ch in chapters:
        idx      = ch[""index""]
        title    = ch[""title""]
        filename = ch[""fileName""]
        txt_path = CHAPTERS_DIR / filename

        if not txt_path.exists():
            print(""  WARNING: {} not found -- skipping"".format(filename))
            continue

        print(""  [{:>3}/{}] {}"".format(idx, len(chapters), title))

        items  = parse_chapter(txt_path)
        chunks = []  # raw PCM accumulator

        for item in items:
            if item[""type""] == ""scene_break"":
                import io
                with wave.open(io.BytesIO(silence_scene)) as wf:
                    chunks.append(wf.readframes(wf.getnframes()))
            else:
                # Both ""speech"" and ""chapter_heading""
                pcm = synthesise(
                    item[""text""], piper_bin, onnx_path,
                    speaker_id, espeakng_data)
                chunks.append(pcm)

        # Append chapter-end silence
        import io
        with wave.open(io.BytesIO(silence_chapter)) as wf:
            chunks.append(wf.readframes(wf.getnframes()))

        # Assemble WAV
        stem     = Path(filename).stem   # e.g. 01-the-signal
        wav_path = OUTPUT_DIR / ""{}.wav"".format(stem)
        wav_data = pcm_chunks_to_wav(chunks, sample_rate)

        with open(wav_path, ""wb"") as wf:
            wf.write(wav_data)

        duration = wav_duration(wav_data)
        total_duration += duration
        mins, secs = divmod(int(duration), 60)
        dur_str = ""{}:{:02d}"".format(mins, secs)
        summary_lines.append(
            ""  {:>3}.  {:<40}  {:>10}  {}"".format(
                idx, title, dur_str, wav_path.name))

        if use_mp3:
            mp3_path = OUTPUT_DIR / ""{}.mp3"".format(stem)
            wav_to_mp3(wav_path, mp3_path)
            print(""         -> {}  +  {}  ({})"".format(
                wav_path.name, mp3_path.name, dur_str))
        else:
            print(""         -> {}  ({})"".format(wav_path.name, dur_str))

    # Write tracklist
    total_mins, total_secs = divmod(int(total_duration), 60)
    total_hours = total_mins // 60
    total_mins  = total_mins % 60
    tracklist_path = OUTPUT_DIR / ""00-tracklist.txt""
    with open(tracklist_path, ""w"", encoding=""utf-8"") as f:
        f.write(""{}\n"".format(book_title))
        f.write(""Voice: {} ({})\n"".format(config[""voiceLabel""], config[""accent""]))
        f.write(""Total duration: {}h {:02d}m {:02d}s\n"".format(
            total_hours, total_mins, total_secs))
        f.write(""\n"")
        f.write(""\n"".join(summary_lines))
        f.write(""\n"")

    print(""\n--- Complete ---"")
    print(""Output files are in: {}"".format(OUTPUT_DIR))
    print(""Total duration: {}h {:02d}m {:02d}s"".format(
        total_hours, total_mins, total_secs))
    print(""Tracklist written to: {}"".format(tracklist_path.name))

if __name__ == ""__main__"":
    main()
";
    }
}
