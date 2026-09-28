"""Build a multiresolution Windows icon from the raven artwork."""

from io import BytesIO
from pathlib import Path
from struct import pack

from PIL import Image, ImageFilter


assets = Path(__file__).resolve().parent
art = Image.open(assets / "huginn-muninn-art.png").convert("RGBA")
art.resize((256, 256), Image.Resampling.LANCZOS).save(assets / "huginn-muninn.png")

# Compact sizes use simpler cyan shapes so both birds remain distinct in the tray.
pixels = art.load()
flat = Image.new("RGBA", art.size, (7, 17, 29, 255))
flat_pixels = flat.load()
for y in range(art.height):
    for x in range(art.width):
        red, green, blue, _ = pixels[x, y]
        if green > 78 and blue > 85 and green > red * 1.25:
            flat_pixels[x, y] = (34, 211, 238, 255) if x < art.width // 2 else (103, 232, 249, 255)

sizes = (16, 24, 32, 48, 64, 128, 256)
frames: list[tuple[int, bytes]] = []
for size in sizes:
    frame = (flat if size <= 24 else art).resize((size, size), Image.Resampling.LANCZOS)
    if size == 16:
        frame = frame.filter(ImageFilter.UnsharpMask(radius=0.6, percent=250, threshold=1))
    data = BytesIO()
    frame.save(data, format="PNG")
    frames.append((size, data.getvalue()))

offset = 6 + 16 * len(frames)
header = bytearray(pack("<HHH", 0, 1, len(frames)))
body = bytearray()
for size, data in frames:
    header.extend(pack("<BBBBHHII", size if size < 256 else 0,
                       size if size < 256 else 0, 0, 0, 1, 32, len(data), offset))
    body.extend(data)
    offset += len(data)
(assets / "huginn-muninn.ico").write_bytes(header + body)
