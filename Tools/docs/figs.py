"""Genera diagramas, bosquejos y gráficas para los entregables (Pillow + matplotlib)."""
import math
import re
from pathlib import Path

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(r"C:\Users\Diego\ejercicios\Tools")
OUT = ROOT / "figs"
CAP = ROOT / "captures"
REN = ROOT / "renders"
OUT.mkdir(exist_ok=True)

INK = (34, 34, 34)
MUTED = (110, 110, 110)
TEAL = (11, 61, 69)
ACC = (46, 196, 182)
PAPER = (252, 252, 251)
SKETCH = (60, 60, 60)
S1, S2 = "#2a78d6", "#eb6834"


def font(size, bold=False, italic=False):
    name = "arialbd.ttf" if bold else ("ariali.ttf" if italic else "arial.ttf")
    return ImageFont.truetype(name, size)


def canvas(w, h, bg=PAPER):
    im = Image.new("RGB", (w, h), bg)
    return im, ImageDraw.Draw(im)


def box(d, xy, text, fill=(232, 244, 245), outline=TEAL, size=22, bold=False, radius=14, color=INK, width=3):
    d.rounded_rectangle(xy, radius=radius, fill=fill, outline=outline, width=width)
    cx = (xy[0] + xy[2]) / 2; cy = (xy[1] + xy[3]) / 2
    d.multiline_text((cx, cy), text, font=font(size, bold), fill=color, anchor="mm", align="center", spacing=6)


def arrow(d, a, b, color=TEAL, width=4, head=14):
    d.line([a, b], fill=color, width=width)
    ang = math.atan2(b[1] - a[1], b[0] - a[0])
    p1 = (b[0] - head * math.cos(ang - 0.45), b[1] - head * math.sin(ang - 0.45))
    p2 = (b[0] - head * math.cos(ang + 0.45), b[1] - head * math.sin(ang + 0.45))
    d.polygon([b, p1, p2], fill=color)


def dashed(d, a, b, color=MUTED, width=2, dash=12):
    L = math.dist(a, b); n = max(1, int(L // dash))
    for i in range(0, n, 2):
        t0, t1 = i / n, min(1, (i + 1) / n)
        d.line([(a[0] + (b[0] - a[0]) * t0, a[1] + (b[1] - a[1]) * t0), (a[0] + (b[0] - a[0]) * t1, a[1] + (b[1] - a[1]) * t1)], fill=color, width=width)


# ---------------- Entregable 1 ----------------
def pipeline_raster():
    im, d = canvas(1800, 560)
    stages = [("Aplicación\n(CPU)", "draw calls,\nbuffers, estados"),
              ("Vertex\nShader", "modelo → mundo →\nvista → clip"),
              ("Ensamblado\ny Clipping", "primitivas, recorte\ncontra el frustum"),
              ("División de\nperspectiva\ny Viewport", "NDC → píxeles"),
              ("Rasterización", "triángulo →\nfragmentos"),
              ("Fragment\nShader", "color, textura,\niluminación"),
              ("Pruebas y\nmezcla (ROP)", "depth, stencil,\nblending → framebuffer")]
    w, gap, x = 220, 32, 30
    for i, (t, sub) in enumerate(stages):
        prog = i in (1, 5)
        fill = (213, 238, 236) if prog else (240, 240, 238)
        box(d, (x, 120, x + w, 300), t, fill=fill, outline=TEAL if prog else MUTED, size=24, bold=True)
        d.multiline_text((x + w / 2, 360), sub, font=font(19), fill=MUTED, anchor="ma", align="center", spacing=5)
        if i < len(stages) - 1:
            arrow(d, (x + w + 4, 210), (x + w + gap - 4, 210))
        x += w + gap
    d.text((30, 40), "Pipeline gráfico de rasterización (simplificado)", font=font(30, True), fill=INK)
    d.rounded_rectangle((30, 480, 54, 504), 5, fill=(213, 238, 236), outline=TEAL, width=2)
    d.text((64, 492), "Etapa programable (shader)", font=font(20), fill=INK, anchor="lm")
    d.rounded_rectangle((420, 480, 444, 504), 5, fill=(240, 240, 238), outline=MUTED, width=2)
    d.text((454, 492), "Etapa fija o configurable del hardware", font=font(20), fill=INK, anchor="lm")
    im.save(OUT / "e1_pipeline_raster.png")


def raytracing_diagram():
    im, d = canvas(1700, 900)
    d.text((30, 30), "Trazado de rayos: rayo primario, sombra y reflexión", font=font(30, True), fill=INK)
    # cámara
    cam = (140, 470)
    d.polygon([(cam[0] - 50, cam[1] - 40), (cam[0] - 50, cam[1] + 40), (cam[0] + 10, cam[1])], fill=TEAL)
    d.text((cam[0] - 60, cam[1] + 60), "Cámara\n(ojo)", font=font(22, True), fill=INK)
    # plano de imagen con rejilla
    px0, py0, px1, py1 = 420, 260, 470, 680
    for i in range(9):
        y = py0 + i * (py1 - py0) / 8
        d.line([(px0, y), (px1, y)], fill=MUTED, width=2)
    d.rectangle((px0, py0, px1, py1), outline=INK, width=3)
    d.text((px0 - 40, py1 + 20), "Plano de imagen\n(un rayo por píxel)", font=font(20), fill=INK)
    # objeto (esfera) y plano suelo
    sph = (1000, 520, 150)
    d.ellipse((sph[0] - sph[2], sph[1] - sph[2], sph[0] + sph[2], sph[1] + sph[2]), fill=(205, 222, 226), outline=TEAL, width=4)
    d.text((sph[0], sph[1]), "Objeto\n(nodo hoja\nde la BVH)", font=font(20, True), fill=INK, anchor="mm", align="center")
    # BVH caja
    d.rectangle((830, 350, 1170, 690), outline=(235, 104, 52), width=3)
    d.text((835, 325), "Caja envolvente (BVH)", font=font(19), fill=(180, 70, 30))
    # luz
    light = (1500, 160)
    d.ellipse((light[0] - 30, light[1] - 30, light[0] + 30, light[1] + 30), fill=(245, 200, 60), outline=(180, 140, 0), width=3)
    d.text((light[0] - 50, light[1] + 40), "Luz", font=font(22, True), fill=INK)
    # rayo primario
    hit = (sph[0] - 130, sph[1] - 75)
    arrow(d, (cam[0] + 10, cam[1]), (px0 + 25, 440), color=INK, width=3)
    arrow(d, (px0 + 25, 440), hit, color=INK, width=4)
    d.text((560, 405), "Rayo primario", font=font(21, True), fill=INK)
    d.ellipse((hit[0] - 9, hit[1] - 9, hit[0] + 9, hit[1] + 9), fill=(220, 40, 40))
    d.text((hit[0] - 140, hit[1] - 60), "Intersección\nrayo–triángulo", font=font(19), fill=(170, 30, 30))
    # rayo de sombra
    dashed(d, hit, light, color=(190, 150, 0), width=4, dash=16)
    d.text((1160, 230), "Rayo de sombra:\n¿algo bloquea la luz?", font=font(20), fill=(140, 110, 0))
    # reflexión
    refl_end = (700, 150)
    arrow(d, hit, refl_end, color=(42, 120, 214), width=4)
    d.text((560, 110), "Rayo de reflexión (secundario)", font=font(20), fill=(30, 90, 170))
    # suelo
    d.line([(560, 820), (1650, 820)], fill=MUTED, width=4)
    d.text((1300, 830), "Escena", font=font(20), fill=MUTED)
    im.save(OUT / "e1_raytracing.png")


def aliasing_diagram():
    im, d = canvas(1700, 760)
    d.text((30, 25), "Muestreo de un borde: 1 muestra por píxel vs. MSAA 4x", font=font(30, True), fill=INK)
    N, cell = 8, 70
    def grid(ox, oy, samples, title):
        d.text((ox, oy - 50), title, font=font(24, True), fill=INK)
        # borde: línea y = 0.55x + 1.2 (en celdas)
        f = lambda x: 0.55 * x + 1.4
        for j in range(N):
            for i in range(N):
                cov = 0
                for sx, sy in samples:
                    x = i + sx; y = j + sy
                    if (N - y) < f(x):
                        cov += 1
                cov /= len(samples)
                g = int(250 - cov * 190)
                d.rectangle((ox + i * cell, oy + j * cell, ox + (i + 1) * cell, oy + (j + 1) * cell), fill=(g, g + 3 if g < 252 else g, min(255, g + 8)), outline=(200, 200, 200))
                for sx, sy in samples:
                    cx = ox + (i + sx) * cell; cy = oy + (j + sy) * cell
                    inside = (N - (j + sy)) < f(i + sx)
                    d.ellipse((cx - 5, cy - 5, cx + 5, cy + 5), fill=(220, 40, 40) if inside else (255, 255, 255), outline=(120, 120, 120))
        pts = [(ox, oy + (N - f(0)) * cell), (ox + N * cell, oy + (N - f(N)) * cell)]
        d.line(pts, fill=(42, 120, 214), width=4)
    grid(60, 140, [(0.5, 0.5)], "Sin AA: 1 muestra (centro)")
    grid(900, 140, [(0.375, 0.125), (0.875, 0.375), (0.125, 0.625), (0.625, 0.875)], "MSAA 4x: patrón rotado de 4 muestras")
    d.text((60, 720), "Línea azul = borde geométrico real · punto rojo = muestra cubierta · gris = intensidad final del píxel", font=font(20), fill=MUTED)
    im.save(OUT / "e1_aliasing.png")


# ---------------- Entregable 3: bosquejos ----------------
def sketch_frame(w, h, title):
    im, d = canvas(w, h, (250, 250, 247))
    d.text((24, 16), title, font=font(26, True), fill=INK)
    return im, d


def device(d, x, y, w, h):
    d.rounded_rectangle((x - 18, y - 18, x + w + 18, y + h + 18), 36, outline=SKETCH, width=4, fill=(235, 235, 232))
    d.rectangle((x, y, x + w, y + h), outline=SKETCH, width=2, fill=(255, 255, 255))


def sketch_menu():
    im, d = sketch_frame(1500, 900, "Bosquejo 1 · Menú de inicio (horizontal, 16:9)")
    x, y, w, h = 110, 110, 1280, 720
    device(d, x, y, w, h)
    # escena difuminada
    for i in range(0, w, 26):
        d.line([(x + i, y + h), (x + i + 160, y)], fill=(236, 240, 240), width=2)
    d.ellipse((x + 470, y + 230, x + 810, y + 470), outline=(190, 205, 205), width=3)
    d.text((x + w / 2, y + 140), "CENOTE VIVO", font=font(70, True), fill=INK, anchor="mm")
    d.text((x + w / 2, y + 215), "El agua que no ves también se contamina", font=font(30, italic=True), fill=TEAL, anchor="mm")
    d.line([(x + w / 2 - 90, y + 255), (x + w / 2 + 90, y + 255)], fill=ACC, width=4)
    d.rectangle((x + 220, y + 300, x + w - 220, y + 380), outline=MUTED, width=2)
    d.text((x + w / 2, y + 340), "« Frase de impacto rotativa (fundido cada 5 s) »", font=font(26), fill=MUTED, anchor="mm")
    box(d, (x + w / 2 - 220, y + 470, x + w / 2 + 220, y + 560), "Explorar el cenote", fill=(205, 240, 236), size=32, bold=True)
    d.text((x + w / 2, y + h - 30), "créditos del proyecto", font=font(20), fill=MUTED, anchor="mm")
    # anotaciones
    def note(tx, ty, px, py, text):
        d.text((tx, ty), text, font=font(20, True), fill=(200, 70, 30))
        d.line([(px, py), (tx + 5, ty + 12)], fill=(200, 70, 30), width=2)
    note(x + 960, y + 60, x + 900, y + 140, "Título grande, alto contraste")
    note(x + 960, y + 420, x + w / 2 + 220, y + 515, "Botón ≥ 90 px de alto (zona del pulgar)")
    note(x + 20, y + 600, x + 300, y + 400, "La cámara orbita la escena 3D de fondo")
    im.save(OUT / "e3_bosquejo_menu.png")


def sketch_hud():
    im, d = sketch_frame(1500, 900, "Bosquejo 2 · Pantalla principal (HUD) y zonas táctiles")
    x, y, w, h = 110, 110, 1280, 720
    device(d, x, y, w, h)
    # zonas del pulgar
    d.ellipse((x - 120, y + h - 300, x + 360, y + h + 160), fill=(225, 244, 228))
    d.ellipse((x + w - 360, y + h - 300, x + w + 120, y + h + 160), fill=(225, 244, 228))
    d.rectangle((x, y, x + w, y + h), outline=SKETCH, width=2)
    d.text((x + w / 2, y + h / 2 - 40), "Escena 3D del cenote", font=font(34), fill=MUTED, anchor="mm")
    # banner
    d.rectangle((x + 16, y + 14, x + w - 16, y + 84), fill=(214, 228, 230), outline=TEAL, width=2)
    d.text((x + 34, y + 49), "Banner: propósito de la app (texto guía)", font=font(24), fill=INK, anchor="lm")
    box(d, (x + w - 290, y + 24, x + w - 30, y + 74), "¿Por qué importa?", size=22, bold=True, radius=8)
    # barra inferior
    d.rectangle((x + 16, y + h - 175, x + w - 16, y + h - 14), fill=(214, 228, 230), outline=TEAL, width=2)
    box(d, (x + 34, y + h - 155, x + 254, y + h - 34), "Iluminación\nDía / Noche", fill=(250, 226, 160), outline=(170, 120, 20), size=24, bold=True, radius=8)
    d.text((x + 290, y + h - 150), "Estado del cenote: Limpio (0 %)", font=font(24, True), fill=INK)
    d.line([(x + 290, y + h - 95), (x + 860, y + h - 95)], fill=SKETCH, width=6)
    d.ellipse((x + 280, y + h - 117, x + 324, y + h - 73), fill=(255, 255, 255), outline=SKETCH, width=3)
    d.text((x + 290, y + h - 66), "Limpio", font=font(18), fill=MUTED)
    d.text((x + 860, y + h - 66), "Contaminado", font=font(18), fill=MUTED, anchor="ra")
    d.text((x + 290, y + h - 42), "Texto que describe el estado actual", font=font(18, italic=True), fill=MUTED)
    box(d, (x + 890, y + h - 150, x + 1040, y + h - 40), "Contaminar /\nRestaurar", fill=(240, 205, 150), outline=(170, 110, 30), size=21, bold=True, radius=8)
    box(d, (x + w - 230, y + h - 155, x + w - 34, y + h - 34), "Cambiar vista >\nPanorámica", fill=(235, 242, 244), size=22, bold=True, radius=8)
    d.text((x + 30, y + h - 220), "Zona del pulgar izquierdo", font=font(19, True), fill=(40, 130, 60))
    d.text((x + w - 30, y + h - 220), "Zona del pulgar derecho", font=font(19, True), fill=(40, 130, 60), anchor="ra")
    im.save(OUT / "e3_bosquejo_hud.png")


def sketch_flow():
    im, d = canvas(1800, 700, (250, 250, 247))
    d.text((24, 16), "Flujo de usuario y de pantallas", font=font(30, True), fill=INK)
    box(d, (40, 260, 300, 400), "Inicio de la app", fill=(240, 240, 238), outline=MUTED, size=26, bold=True)
    box(d, (380, 230, 700, 430), "Menú de inicio\n(título + frases)", fill=(213, 238, 236), size=26, bold=True)
    box(d, (800, 230, 1140, 430), "Pantalla principal\n(escena 3D + HUD)", fill=(213, 238, 236), size=26, bold=True)
    arrow(d, (300, 330), (378, 330)); arrow(d, (700, 330), (798, 330))
    d.text((712, 290), "Explorar", font=font(20), fill=TEAL)
    arrow(d, (800, 400), (700, 400), color=MUTED); d.text((712, 408), "Atrás", font=font(20), fill=MUTED)
    acts = [("Día / Noche", 70), ("Contaminación\n(slider / botón)", 230), ("Cambiar vista\n1 → 2 → 3 → 1", 400), ("Panel ¿Por qué\nimporta? (modal)", 570)]
    for t, yy in acts:
        box(d, (1300, yy, 1640, yy + 120), t, fill=(255, 255, 255), outline=TEAL, size=23, radius=10)
        arrow(d, (1140, 330), (1298, yy + 60))
    d.text((1300, 30), "Acciones disponibles en el HUD", font=font(22, True), fill=INK)
    im.save(OUT / "e3_flujo.png")


def sketch_clean_vs_polluted():
    im, d = canvas(1700, 760, (250, 250, 247))
    d.text((24, 16), "Bosquejo 3 · Estado Limpio vs. Estado Contaminado (corte del cenote)", font=font(28, True), fill=INK)
    def cenote(ox, polluted):
        title = "CONTAMINADO" if polluted else "LIMPIO"
        d.text((ox + 370, 90), title, font=font(30, True), fill=(140, 100, 20) if polluted else TEAL, anchor="mm")
        d.polygon([(ox, 150), (ox + 260, 150), (ox + 300, 260), (ox + 250, 680), (ox + 490, 680), (ox + 440, 260), (ox + 480, 150), (ox + 740, 150), (ox + 740, 720), (ox, 720)], fill=(205, 198, 186), outline=SKETCH)
        d.rectangle((ox, 120, ox + 740, 150), fill=(150, 120, 80))
        water = (95, 75, 30) if polluted else (90, 200, 205)
        d.polygon([(ox + 283, 420), (ox + 457, 420), (ox + 490, 680), (ox + 250, 680)], fill=water)
        if polluted:
            for k in range(6):
                sx = ox + 60 + k * 120
                d.line([(sx, 150), (sx + 25, 300 + (k % 3) * 60)], fill=(80, 60, 20), width=10)
            for k in range(8):
                bx = ox + 300 + k * 18
                d.rectangle((bx, 412, bx + 10, 420), fill=(240, 240, 240) if k % 2 else (200, 60, 40))
            for k in range(4):
                dx = ox + 300 + k * 40
                d.line([(dx, 300), (dx, 400)], fill=(90, 60, 20), width=3)
            d.text((ox + 10, 160), "lixiviados que bajan por fisuras", font=font(19, True), fill=(90, 60, 10))
            d.text((ox + 270, 640), "agua turbia, opaca", font=font(19, True), fill=(250, 240, 210))
            d.text((ox + 470, 395), "basura flotante", font=font(19, True), fill=(90, 60, 10))
        else:
            for k in range(3):
                y = 450 + k * 70
                d.line([(ox + 330, y), (ox + 360, y + 30)], fill=(255, 255, 255), width=3)
            d.text((ox + 290, 640), "agua transparente", font=font(19, True), fill=(10, 70, 80))
            d.text((ox + 10, 160), "roca caliza sin manchas", font=font(19, True), fill=INK)
    cenote(60, False)
    cenote(900, True)
    arrow(d, (820, 420), (890, 420), width=6)
    d.text((24, 730), "El slider interpola todos los cambios de forma continua entre ambos estados.", font=font(20), fill=MUTED)
    im.save(OUT / "e3_limpio_vs_contaminado.png")


def sketch_cameras():
    im, d = canvas(1700, 900, (250, 250, 247))
    d.text((24, 16), "Bosquejo 4 · Ubicación de las 3 vistas de cámara (corte lateral)", font=font(28, True), fill=INK)
    d.polygon([(100, 250), (560, 250), (620, 360), (540, 820), (1160, 820), (1080, 360), (1140, 250), (1600, 250), (1600, 880), (100, 880)], fill=(212, 205, 192), outline=SKETCH)
    d.polygon([(585, 560), (1115, 560), (1160, 820), (540, 820)], fill=(120, 205, 210))
    d.text((850, 700), "agua del cenote", font=font(22, True), fill=(10, 70, 80), anchor="mm")
    d.text((180, 300), "roca caliza (subsuelo)", font=font(22, True), fill=INK)
    def cam(x, y, ang, label, sub):
        a = math.radians(ang)
        for s in (-0.35, 0.35):
            d.line([(x, y), (x + 260 * math.cos(a + s), y + 260 * math.sin(a + s))], fill=(46, 196, 182), width=3)
        d.ellipse((x - 20, y - 20, x + 20, y + 20), fill=TEAL, outline=(255, 255, 255), width=3)
        d.text((x + 50, y - 70), label, font=font(24, True), fill=INK)
        d.text((x + 50, y - 40), sub, font=font(19), fill=MUTED)
    cam(560, 110, 55, "Vista 1 · Panorámica", "desde la boca del cenote, FOV 60°")
    cam(1050, 540, 170, "Vista 2 · Nivel del agua", "a ~0.9 m sobre la superficie, FOV 62°")
    cam(940, 700, 200, "Vista 3 · Subterránea", "bajo la superficie, FOV 70°, niebla")
    d.line([(560, 560), (1140, 560)], fill=(20, 110, 120), width=3)
    im.save(OUT / "e3_camaras.png")


# ---------------- Entregable 4 ----------------
def blender_lod_sheet():
    names = [("roca_LOD0.png", "LOD0 · 100 % · 9 826 triángulos"), ("roca_LOD1.png", "LOD1 · 45 % · 4 420 triángulos"), ("roca_LOD2.png", "LOD2 · 25 % · 2 456 triángulos")]
    tiles = []
    for n, label in names:
        im = Image.open(REN / n).convert("RGB").crop((120, 120, 820, 640))
        t = Image.new("RGB", (im.width, im.height + 60), "white"); t.paste(im, (0, 60))
        ImageDraw.Draw(t).text((im.width / 2, 30), label, font=font(30, True), fill=INK, anchor="mm")
        tiles.append(t)
    sheet = Image.new("RGB", (sum(t.width for t in tiles) + 40, tiles[0].height), "white")
    x = 0
    for t in tiles:
        sheet.paste(t, (x, 0)); x += t.width + 20
    sheet.save(OUT / "e4_blender_lods.png")


def lod_group_bar():
    im, d = canvas(1700, 430, (250, 250, 247))
    d.text((24, 16), "Configuración del componente LOD Group (prefab «roca»)", font=font(28, True), fill=INK)
    x0, x1, y0, y1 = 60, 1640, 110, 240
    segs = [(1.0, 0.325, "LOD 0", "roca_LOD0 · 9 826 tris", (122, 186, 98)), (0.325, 0.125, "LOD 1", "roca_LOD1 · 4 420 tris", (88, 160, 196)),
            (0.125, 0.01, "LOD 2", "roca_LOD2 · 2 456 tris", (110, 135, 200)), (0.01, 0.0, "Culled", "", (200, 90, 90))]
    # escala visual no lineal (como el inspector): reparto por bandas
    widths = [0.40, 0.25, 0.27, 0.08]
    x = x0
    for (a, b, name, sub, col), wf in zip(segs, widths):
        w = (x1 - x0) * wf
        d.rectangle((x, y0, x + w, y1), fill=col, outline=(40, 40, 40), width=2)
        d.text((x + 14, y0 + 30), name, font=font(28, True), fill=(20, 20, 20))
        d.text((x + 14, y0 + 75), sub, font=font(20), fill=(20, 20, 20))
        d.text((x + 2, y1 + 18), f"{a * 100:g} %", font=font(22, True), fill=INK)
        x += w
    d.text((x1, y1 + 18), "0 %", font=font(22, True), fill=INK, anchor="ra")
    d.text((60, 320), "Porcentaje = altura del objeto respecto a la altura de la pantalla (Screen Relative Transition Height).", font=font(21), fill=MUTED)
    d.text((60, 355), "Fade Mode: None · LOD Bias del perfil Mobile: 1.0 · Maximum LOD Level: 0", font=font(21), fill=MUTED)
    im.save(OUT / "e4_lod_group.png")


# ---------------- Entregable 5 ----------------
def shader_nodes():
    im, d = canvas(2000, 1150, (40, 42, 46))
    d.text((24, 16), "Grafo del shader CenoteWater (equivalente en nodos de Shader Graph)", font=font(30, True), fill=(235, 235, 235))
    def node(x, y, w, h, title, ports, col=(70, 120, 160)):
        d.rounded_rectangle((x, y, x + w, y + h), 10, fill=(58, 60, 66), outline=(20, 20, 20), width=2)
        d.rounded_rectangle((x, y, x + w, y + 40), 10, fill=col)
        d.rectangle((x, y + 28, x + w, y + 40), fill=col)
        d.text((x + 12, y + 20), title, font=font(21, True), fill=(255, 255, 255), anchor="lm")
        for i, p in enumerate(ports):
            d.text((x + 14, y + 58 + i * 28), p, font=font(18), fill=(215, 215, 215))
        return (x + w, y + h / 2), (x, y + h / 2)
    o1, _ = node(40, 90, 300, 150, "Position (World)", ["xz × _NormalTiling", "+ _Time × _WaveSpeed"], (110, 110, 110))
    o2, i2 = node(420, 70, 330, 190, "Sample Texture 2D ×2", ["_NormalMap (UV A)", "_NormalMap (UV B, ×1.37)", "UnpackNormal", "Promedio × _NormalStrength"])
    o3, i3 = node(40, 360, 300, 130, "Screen Position", ["xy / w → screenUV"], (110, 110, 110))
    o4, i4 = node(420, 330, 330, 210, "Depth Fade", ["Scene Depth (eye)", "− profundidad superficie", "1 − exp(−Δ / _DepthDistance)", "× turbidez"], (160, 110, 60))
    o5, i5 = node(420, 600, 330, 170, "Scene Color (Refracción)", ["screenUV + ripple ×", "_RefractionStrength", "× (1 − _PollutionLevel)"], (160, 110, 60))
    o6, i6 = node(850, 300, 360, 240, "Lerp de color", ["_WaterColor → _AbsorptionColor", "por Depth Fade", "Lerp hacia _PollutedColor /", "_PollutedDeepColor", "por _PollutionLevel"], (120, 80, 160))
    o7, i7 = node(850, 620, 360, 170, "Lerp (transmisión)", ["Scene Color → color del agua", "por opacidad = depth + turbidez"], (120, 80, 160))
    o8, i8 = node(850, 840, 360, 200, "Fresnel Effect", ["pow(1 − N·V, _FresnelPower)", "× Reflection Probe", "+ especular Blinn-Phong", "(_Smoothness)"], (60, 140, 110))
    o9, i9 = node(1300, 560, 330, 200, "Add / Lerp final", ["+ reflexión y especular", "Lerp → _ScumColor (película)", "+ bioluminiscencia ×", "_CenoteNight × (1 − P)"], (120, 80, 160))
    o10, i10 = node(1700, 590, 260, 140, "Fragment Output", ["Base Color", "Alpha = 1 (opaco tras refracción)"], (80, 80, 80))
    for a, b in [(o1, i2), (o3, i4), (o3, i5), (o2, i5), (o4, i6), (o6, i7), (o5, i7), (o7, i9), (o8, i9), (o9, i10)]:
        d.line([a, ((a[0] + b[0]) / 2, a[1]), ((a[0] + b[0]) / 2, b[1]), b], fill=(160, 200, 220), width=3)
    d.rounded_rectangle((1300, 90, 1960, 470), 12, outline=(200, 200, 200), width=2)
    d.text((1320, 105), "Propiedades expuestas (Blackboard)", font=font(22, True), fill=(240, 240, 240))
    props = ["_WaterColor · _AbsorptionColor", "_Turbidity · _DepthDistance", "_Smoothness · _NormalTiling", "_NormalStrength · _WaveSpeed", "_RefractionStrength · _FresnelPower",
             "_PollutionLevel (0–1)", "_PollutedColor · _PollutedDeepColor", "_ScumColor · _BioColor", "Global: _CenoteNight"]
    for i, p in enumerate(props):
        d.text((1320, 150 + i * 34), "• " + p, font=font(19), fill=(220, 220, 220))
    im.save(OUT / "e5_nodos_shader.png")


# ---------------- Entregable 6: gráficas ----------------
def parse_player(path):
    rows = {}
    for line in Path(path).read_text(encoding="utf-8", errors="ignore").splitlines():
        if " | " in line and not line.startswith("Vista"):
            c = [x.strip() for x in line.split("|")]
            if len(c) >= 12:
                rows[c[0]] = dict(fps=float(c[1]), ms=float(c[2]), cpu=float(c[3]), gpu=float(c[4]), batches=int(c[5]), setpass=int(c[6]),
                                  breakdown=c[7], tris=int(c[8]), verts=int(c[9]), ram=float(c[10]), gfx=float(c[11]))
    return rows


def metric_charts():
    opt = parse_player(CAP / "player_optimizado.txt")
    base = parse_player(CAP / "player_sin_optimizar.txt")
    views = list(opt.keys())
    short = ["Alejada", "Acercándose", "Acercada"]
    plt.rcParams.update({"font.family": "Arial", "font.size": 11})
    fig, axes = plt.subplots(1, 3, figsize=(13, 4.2))
    for ax, (key, title, fmt) in zip(axes, [("tris", "Triángulos por frame", "{:,.0f}"), ("batches", "Batches (draw calls)", "{:.0f}"), ("setpass", "SetPass calls", "{:.0f}")]):
        xs = range(len(views)); w = 0.38
        b1 = [base[v][key] for v in views]; b2 = [opt[v][key] for v in views]
        ax.bar([x - w / 2 - 0.01 for x in xs], b1, w, color=S1, label="Sin optimizar")
        ax.bar([x + w / 2 + 0.01 for x in xs], b2, w, color=S2, label="Optimizado")
        for x, v1, v2 in zip(xs, b1, b2):
            ax.text(x - w / 2, v1, fmt.format(v1).replace(",", " "), ha="center", va="bottom", fontsize=9, color="#52514e")
            ax.text(x + w / 2, v2, fmt.format(v2).replace(",", " "), ha="center", va="bottom", fontsize=9, color="#52514e")
        ax.set_title(title, fontsize=12, color="#0b0b0b", loc="left")
        ax.set_xticks(list(xs)); ax.set_xticklabels(short)
        ax.spines[["top", "right"]].set_visible(False)
        ax.grid(axis="y", color="#e6e6e3", linewidth=0.8); ax.set_axisbelow(True)
        ax.yaxis.set_major_formatter(matplotlib.ticker.FuncFormatter(lambda v, _: f"{v:,.0f}".replace(",", " ")))
    axes[0].legend(frameon=False, loc="upper right")
    fig.tight_layout()
    fig.savefig(OUT / "e6_comparativa.png", dpi=160)
    plt.close(fig)


def aa_chart():
    rows = []
    for line in (CAP / "player_aa.txt").read_text(encoding="utf-8").splitlines():
        c = [x.strip() for x in line.split("|")]
        if len(c) == 6 and c[0] not in ("Tecnica",):
            rows.append((c[0], float(c[3]), float(c[4])))
    plt.rcParams.update({"font.family": "Arial", "font.size": 11})
    fig, axes = plt.subplots(1, 2, figsize=(12, 3.9))
    names = [r[0] for r in rows]
    for ax, idx, title, fmt in ((axes[0], 1, "Tiempo de GPU por frame (ms)", "{:.3f}"), (axes[1], 2, "Memoria gráfica usada (MB)", "{:.1f}")):
        vals = [r[idx] for r in rows]
        ax.bar(names, vals, 0.6, color=S1)
        for i, v in enumerate(vals):
            ax.text(i, v, fmt.format(v), ha="center", va="bottom", fontsize=9, color="#52514e")
        ax.set_title(title, loc="left", fontsize=12)
        ax.spines[["top", "right"]].set_visible(False)
        ax.grid(axis="y", color="#e6e6e3", linewidth=0.8); ax.set_axisbelow(True)
        if idx == 2:
            ax.set_ylim(min(vals) * 0.9, max(vals) * 1.06)
    fig.tight_layout(); fig.savefig(OUT / "e2_aa_costos.png", dpi=160); plt.close(fig)


if __name__ == "__main__":
    pipeline_raster(); raytracing_diagram(); aliasing_diagram()
    sketch_menu(); sketch_hud(); sketch_flow(); sketch_clean_vs_polluted(); sketch_cameras()
    blender_lod_sheet(); lod_group_bar(); shader_nodes()
    metric_charts(); aa_chart()
    print("figs ok")
