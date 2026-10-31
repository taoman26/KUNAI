from PIL import Image, ImageDraw, ImageFont

F = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"
FB = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
FM = "/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf"
def f(p, s): return ImageFont.truetype(p, s)

S = 2  # supersampling
W, H = 760, 900
img = Image.new("RGB", (W * S, H * S), "#1e1f26")
d = ImageDraw.Draw(img)

BG, PANEL, BORDER = "#1e1f26", "#2a2c36", "#3b3e4d"
TXT, DIM, ACC = "#e6e8ef", "#8b90a3", "#4f8cff"
GREEN, YELLOW, RED = "#3ecf6e", "#f2c94c", "#eb5757"

def rr(x0, y0, x1, y1, fill, outline=None, r=8):
    d.rounded_rectangle([x0*S, y0*S, x1*S, y1*S], r*S, fill=fill, outline=outline, width=S)
def t(x, y, s, font, fill=TXT, anchor="la"):
    d.text((x*S, y*S), s, font=font, fill=fill, anchor=anchor)
def circle(cx, cy, r, fill):
    d.ellipse([(cx-r)*S, (cy-r)*S, (cx+r)*S, (cy+r)*S], fill=fill)
def button(x0, y0, x1, y1, label, primary=False):
    rr(x0, y0, x1, y1, ACC if primary else "#353847", None if primary else BORDER, 6)
    t((x0+x1)/2, (y0+y1)/2, label, f(FB, 13*S), "white" if primary else TXT, "mm")

# window
rr(10, 10, W-10, H-10, BG, BORDER, 12)
rr(10, 10, W-10, 44, "#15161b", BORDER, 12)
for i, c in enumerate(["#eb5757", "#f2c94c", "#3ecf6e"]):
    circle(34 + i*20, 27, 6, c)
t(W/2, 27, "KUNAI", f(F, 12*S), DIM, "mm")

# header
t(32, 62, "KUNAI", f(FB, 30*S), TXT)
t(34, 102, "Programmable Wiimote Macro Pad", f(F, 13*S), DIM)

def section(y0, y1, title):
    rr(28, y0, W-28, y1, PANEL, BORDER)
    t(44, y0+12, title, f(FB, 13*S), ACC)

# Connection
section(130, 250, "Connection")
button(44, 168, 164, 206, "Disconnect", primary=True)
t(44, 222, "Press 1+2 on the Wiimote to pair", f(F, 10*S), DIM)
circle(230, 187, 7, GREEN)
t(246, 168, "Status  :  Connected", f(F, 14*S), GREEN)
t(246, 192, "Battery :  82%", f(F, 14*S))
t(246, 216, "Layer   :  1", f(F, 14*S))
# battery bar
rr(540, 194, 700, 208, "#15161b", BORDER, 4)
rr(541, 195, 541 + int(158*0.82), 207, GREEN, None, 3)
# legend
for i, (c, n) in enumerate([(RED, "Disconnected"), (YELLOW, "Connecting"), (GREEN, "Connected")]):
    circle(540, 232 - 0, 0, c) if False else None
lx = 440
for c, n in [(RED, "Disconnected"), (YELLOW, "Connecting"), (GREEN, "Connected")]:
    circle(lx, 168 + 7, 5, c); t(lx+10, 168, n, f(F, 10*S), DIM); lx += 0
    lx += 0
# stack legend vertically at right
d.rectangle([430*S, 160*S, 710*S, 185*S], fill=PANEL)
lx = 470
for c, n in [(RED, "Disconnected"), (YELLOW, "Connecting"), (GREEN, "Connected")]:
    circle(lx, 173, 5, c); t(lx+9, 166, n, f(F, 9*S), DIM)
    lx += 18 + int(d.textlength(n, font=f(F, 9*S)) / S) + 8

# Profile
section(266, 350, "Profile")
rr(44, 302, 330, 336, "#15161b", BORDER, 6)
t(58, 319, "coding.json", f(FM, 14*S), TXT, "lm")
t(312, 319, "▾", f(F, 14*S), DIM, "mm")
t(350, 319, "default / coding / writing / browser", f(F, 10*S), DIM, "lm")

# Mappings
section(366, 650, "Mappings")
tx = 330
for i, n in enumerate(["Layer 0", "Layer 1", "Layer 2", "Layer 3"]):
    x0 = 400 + i*72
    sel = i == 1
    rr(x0, 372, x0+66, 396, ACC if sel else "#353847", None if sel else BORDER, 5)
    t(x0+33, 384, n, f(FB if sel else F, 11*S), "white" if sel else DIM, "mm")
t(44, 372, "(B held)", f(F, 10*S), DIM, "lm") if False else None

# table
tx0, tx1, ty = 44, W-44, 408
rr(tx0, ty, tx1, ty+30, "#15161b", BORDER, 4)
t(tx0+16, ty+15, "Button", f(FB, 12*S), DIM, "lm")
t(tx0+176, ty+15, "Action", f(FB, 12*S), DIM, "lm")
rows = [("A", "Ctrl+Z"), ("One", "Ctrl+Shift+Z"), ("Two", "Ctrl+X"),
        ("Plus", "Macro: Ctrl+S; Delay(300); Alt+Tab"), ("Up", "VolumeUp")]
for i, (b, a) in enumerate(rows):
    y = ty + 30 + i*34
    sel = i == 1
    rr(tx0, y, tx1, y+34, "#2f4a7a" if sel else (PANEL if i % 2 else "#2f313d"), None, 0)
    t(tx0+16, y+17, b, f(FM, 13*S), TXT, "lm")
    t(tx0+176, y+17, a, f(FM, 13*S), TXT, "lm")
d.line([(tx0+160)*S, ty*S, (tx0+160)*S, (ty+30+5*34)*S], fill=BORDER, width=S)
button(44, 612, 124, 640, "Add")
button(134, 612, 214, 640, "Remove")
button(W-124, 612, W-44, 640, "Save", primary=True)

# Log
section(666, 868, "Log")
rr(44, 698, W-44, 856, "#12131a", BORDER, 6)
logs = ["[12:00:01] Connected", "[12:00:05] Layer1", "[12:00:06] A Pressed",
        "[12:00:06] Ctrl+Z Sent", "[12:00:09] A Released", "[12:00:10] Layer0"]
for i, l in enumerate(logs):
    col = GREEN if "Connected" in l else (YELLOW if "Layer" in l else TXT)
    t(58, 708 + i*24, l, f(FM, 13*S), col)

img = img.resize((W*2, H*2), Image.LANCZOS) if False else img.resize((W*2, H*2), Image.LANCZOS)
img.save("/home/lamat/git/KUNAI/docs/mock_main.png")
print("ok")
