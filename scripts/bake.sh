#!/usr/bin/env bash
# Bakes a green-screen video into the frame sequence Foxy Overlay plays at runtime.
#
# WPF cannot render a MediaElement on a layered (per-pixel alpha) window, so the
# overlay is an image sequence rather than a video. Chroma keying happens here,
# once, instead of on every playback.
#
# Usage: scripts/bake.sh <input-video> <output-dir> [key-colour] [similarity] [blend]

set -euo pipefail

INPUT="${1:?usage: bake.sh <input-video> <output-dir> [key] [similarity] [blend]}"
OUTDIR="${2:?usage: bake.sh <input-video> <output-dir> [key] [similarity] [blend]}"
KEY="${3:-0x00FE00}"
SIMILARITY="${4:-0.12}"
BLEND="${5:-0.06}"
# chromakey keys in YUV, which handles the compressed green edges better than
# colorkey's RGB distance; despill then pulls the green cast out of pale surfaces
# like teeth, which otherwise come through faintly olive.
FILTER="chromakey=${KEY}:${SIMILARITY}:${BLEND},despill=type=green:mix=0.6:expand=0.4,format=rgba"

command -v ffmpeg  >/dev/null || { echo "ffmpeg not found on PATH" >&2; exit 127; }
command -v ffprobe >/dev/null || { echo "ffprobe not found on PATH" >&2; exit 127; }

probe() { ffprobe -v error -select_streams "$1" -show_entries "$2" -of default=nw=1:nk=1 "$INPUT"; }

WIDTH=$(probe v:0 stream=width)
HEIGHT=$(probe v:0 stream=height)
RATE=$(probe v:0 stream=r_frame_rate)
HAS_AUDIO=$(ffprobe -v error -select_streams a -show_entries stream=index -of csv=p=0 "$INPUT" | head -1)

rm -rf "$OUTDIR"
mkdir -p "$OUTDIR"

# -start_number 0 so frame_000.png is the first frame; the player indexes from 0.
ffmpeg -v error -y -i "$INPUT" \
  -vf "$FILTER" \
  -start_number 0 "$OUTDIR/frame_%03d.png"

FRAMES=$(find "$OUTDIR" -name 'frame_*.png' | wc -l | tr -d ' ')

if [ -n "$HAS_AUDIO" ]; then
  # 16-bit PCM: System.Media.SoundPlayer only accepts uncompressed WAV.
  ffmpeg -v error -y -i "$INPUT" -vn -acodec pcm_s16le -ar 44100 -ac 2 "$OUTDIR/audio.wav"
  AUDIO='"audio.wav"'
else
  AUDIO='null'
fi

cat > "$OUTDIR/pack.json" <<JSON
{
  "name": "$(basename "$OUTDIR")",
  "width": $WIDTH,
  "height": $HEIGHT,
  "frameCount": $FRAMES,
  "frameRate": "$RATE",
  "framePattern": "frame_{0:D3}.png",
  "audioFile": $AUDIO,
  "source": "$(basename "$INPUT")",
  "bakedWith": "$FILTER"
}
JSON

echo "Baked $FRAMES frames (${WIDTH}x${HEIGHT} @ ${RATE}) -> $OUTDIR"
