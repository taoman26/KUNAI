"""KUNAI アイコン生成: python3 assets/gen_icon.py  →  assets/icon.png / icon.ico"""
from pathlib import Path
from PIL import Image, ImageDraw

OUT = Path(__file__).parent
S = 4096          # 作画サイズ(縮小して滑らかにする)
FINAL = 1024
BG, BG2, GREEN, LIGHT = (30, 31, 38), (44, 46, 58), (62, 207, 110), (226, 232, 240)


def kunai_layer(color):
    """縦向き(先端が上)の苦無を描いた透過レイヤー。"""
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    cx = S // 2
    u = S / 100.0
    # 刃(木の葉形)
    blade = [(cx, 6 * u), (cx + 15 * u, 38 * u), (cx + 5 * u, 60 * u),
             (cx - 5 * u, 60 * u), (cx - 15 * u, 38 * u)]
    d.polygon(blade, fill=color)
    # 柄
    d.rectangle([cx - 4 * u, 58 * u, cx + 4 * u, 82 * u], fill=color)
    # 鍔
    d.rounded_rectangle([cx - 12 * u, 56 * u, cx + 12 * u, 62 * u], radius=2 * u, fill=color)
    # 輪
    d.ellipse([cx - 11 * u, 78 * u, cx + 11 * u, 100 * u], outline=color, width=int(4.2 * u))
    return img


def main():
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    mask = Image.new("L", (S, S), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, S - 1, S - 1], radius=int(S * 0.22), fill=255)

    # 背景: 角丸タイル + 上から下への緩やかなグラデーション
    grad = Image.new("RGBA", (S, S))
    gd = ImageDraw.Draw(grad)
    for y in range(S):
        t = y / (S - 1)
        gd.line([(0, y), (S, y)], fill=tuple(int(BG2[i] + (BG[i] - BG2[i]) * t) for i in range(3)) + (255,))
    img.paste(grad, (0, 0), mask)

    # 苦無(右上向き 45°)。回転で端が欠けないよう、小さく描いてから回転する。
    def build(color, scale):
        layer = kunai_layer(color)
        size = int(S * scale)
        layer = layer.resize((size, size), Image.LANCZOS)
        canvas = Image.new("RGBA", (S, S), (0, 0, 0, 0))
        canvas.alpha_composite(layer, ((S - size) // 2, (S - size) // 2))
        return canvas.rotate(-45, resample=Image.BICUBIC, center=(S / 2, S / 2))

    body = build(LIGHT, 0.74)
    shadow = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    shadow.putalpha(body.getchannel("A").point(lambda a: int(a * 0.35)))
    img.alpha_composite(shadow, (int(S * 0.012), int(S * 0.02)))
    img.alpha_composite(body)

    # 刃の中央に緑のライン(Wii Remote の LED を思わせるアクセント)
    accent = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    ad = ImageDraw.Draw(accent)
    cx, u = S / 2, S / 100.0
    ad.polygon([(cx, 14 * u), (cx + 3.2 * u, 38 * u), (cx, 54 * u), (cx - 3.2 * u, 38 * u)], fill=GREEN)
    size = int(S * 0.74)
    accent = accent.resize((size, size), Image.LANCZOS)
    canvas = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    canvas.alpha_composite(accent, ((S - size) // 2, (S - size) // 2))
    img.alpha_composite(canvas.rotate(-45, resample=Image.BICUBIC, center=(S / 2, S / 2)))

    final = img.resize((FINAL, FINAL), Image.LANCZOS)
    final.save(OUT / "icon.png")
    final.save(OUT / "icon.ico", sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
    print("wrote", OUT / "icon.png", OUT / "icon.ico")


main()
