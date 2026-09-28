from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, ImageFilter
import shutil
import subprocess

ROOT = Path(__file__).resolve().parents[1]
PUBLIC = ROOT / "site" / "public"
ASSETS = PUBLIC / "assets"
ASSETS.mkdir(parents=True, exist_ok=True)
for name in ("huginn-muninn.ico", "huginn-muninn.png", "mascot.png"):
    shutil.copy2(ROOT / "assets" / name, ASSETS / name)

FONT_BOLD = "C:/Windows/Fonts/segoeuib.ttf"
FONT = "C:/Windows/Fonts/segoeui.ttf"
def f(size, bold=False):
    return ImageFont.truetype(FONT_BOLD if bold else FONT, size)

SLIDES = [
    ("01 / PLANEJE", "Tudo em foco.", "Crie uma tarefa com início, prazo e projeto.", "preview-adjust-main.png"),
    ("02 / ACOMPANHE", "Cada etapa conta.", "Marque a checklist e veja o progresso avançar.", "preview-final-main.png"),
    ("03 / ANOTE", "Fique de olho.", "Stickers sobre a tela guardam seus lembretes e notas.", "preview-adjust-sticker.png"),
    ("04 / AVANCE", "Um aliado na tela.", "O assistente mostra os projetos e muda com o progresso.", "preview-assistant-screen.png"),
]
frames = []
for index, (tag, title, subtitle, shot_name) in enumerate(SLIDES):
    canvas = Image.new("RGB", (1280, 720), "#07111c")
    draw = ImageDraw.Draw(canvas)
    for x in range(0, 1280, 8):
        c = int(9 + x / 1280 * 7)
        draw.rectangle((x, 0, x + 8, 720), fill=(c, c + 13, c + 24))
    draw.ellipse((675, -270, 1460, 510), outline="#153c51", width=2)
    draw.ellipse((760, -190, 1350, 390), outline="#225469", width=2)
    draw.line((57, 70, 1223, 70), fill="#1d5367", width=2)
    draw.text((58, 36), "FIRAW / WORK ASSISTANT", font=f(18, True), fill="#32dfed")
    draw.text((57, 154), tag, font=f(16, True), fill="#35dceb")
    draw.text((57, 208), title, font=f(65, True), fill="#effcff")
    draw.multiline_text((61, 310), subtitle, font=f(25), fill="#a8c5d4", spacing=10)
    draw.rounded_rectangle((58, 587, 348, 635), radius=10, fill="#27dbe9")
    draw.text((79, 596), "work.firawynix.com.br", font=f(20, True), fill="#07212e")
    draw.text((59, 669), "TAREFAS  /  CHECKLISTS  /  NOTAS  /  ASSISTENTE", font=f(13, True), fill="#6e9aaa")
    draw.text((1165, 660), f"0{index+1} / 04", font=f(16, True), fill="#40dce9")
    shot = Image.open(ROOT / "tests" / shot_name).convert("RGBA")
    bounds = (610, 112, 1218, 595)
    maxw, maxh = bounds[2] - bounds[0], bounds[3] - bounds[1]
    shot.thumbnail((maxw, maxh), Image.Resampling.LANCZOS)
    x = bounds[0] + (maxw - shot.width) // 2
    y = bounds[1] + (maxh - shot.height) // 2
    shadow = Image.new("RGBA", (shot.width + 38, shot.height + 38), (0,0,0,0))
    ImageDraw.Draw(shadow).rounded_rectangle((19, 19, shot.width + 18, shot.height + 18), radius=15, fill=(0,0,0,170))
    shadow = shadow.filter(ImageFilter.GaussianBlur(15))
    canvas.paste(shadow, (x - 19, y - 19), shadow)
    canvas.paste(shot, (x, y), shot)
    draw = ImageDraw.Draw(canvas)
    draw.rounded_rectangle((x-2, y-2, x+shot.width+1, y+shot.height+1), radius=8, outline="#2b8da2", width=2)
    path = ASSETS / f"walkthrough-{index+1}.jpg"
    canvas.save(path, quality=93)
    frames.append(path)

shutil.copy2(frames[0], ASSETS / "video-poster.jpg")
concat = ASSETS / "walkthrough-list.txt"
concat.write_text("".join(f"file '{p.as_posix()}'\nduration 5\n" for p in frames) + f"file '{frames[-1].as_posix()}'\n", encoding="utf-8")
subprocess.run([
    "ffmpeg", "-y", "-hide_banner", "-loglevel", "error", "-safe", "0", "-f", "concat", "-i", str(concat),
    "-vf", "fps=30,fade=t=in:st=0:d=0.7,fade=t=out:st=19.4:d=0.6,format=yuv420p",
    "-c:v", "libx264", "-preset", "medium", "-crf", "21", "-movflags", "+faststart",
    str(ASSETS / "walkthrough.mp4")
], check=True)
concat.unlink()
print(ASSETS / "walkthrough.mp4")
