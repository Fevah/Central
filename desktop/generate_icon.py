"""Generate Central.exe application icon — modern network hub design."""
import math
from PIL import Image, ImageDraw, ImageFont

def create_icon(size):
    """Create a single icon frame at the given size."""
    img = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    cx, cy = size / 2, size / 2
    r = size * 0.46  # main circle radius

    # Background: rounded square with gradient feel (dark blue-grey)
    corner = size * 0.18
    draw.rounded_rectangle(
        [size * 0.02, size * 0.02, size * 0.98, size * 0.98],
        radius=corner,
        fill=(25, 32, 48, 255)  # dark navy
    )

    # Subtle inner glow ring
    ring_r = r * 0.92
    ring_w = max(1, size // 32)
    draw.ellipse(
        [cx - ring_r, cy - ring_r, cx + ring_r, cy + ring_r],
        outline=(45, 65, 105, 120),
        width=ring_w
    )

    # Connection lines from center to outer nodes (draw BEFORE nodes so nodes sit on top)
    num_nodes = 6
    node_r_outer = r * 0.75
    node_size_outer = size * 0.065

    for i in range(num_nodes):
        angle = math.radians(i * 60 - 90)
        nx = cx + node_r_outer * math.cos(angle)
        ny = cy + node_r_outer * math.sin(angle)
        line_w = max(1, size // 48)
        # Glowing line effect
        draw.line([(cx, cy), (nx, ny)], fill=(80, 160, 255, 100), width=line_w + max(1, size // 64))
        draw.line([(cx, cy), (nx, ny)], fill=(100, 185, 255, 200), width=line_w)

    # Cross-connection lines between adjacent nodes (subtle)
    for i in range(num_nodes):
        angle1 = math.radians(i * 60 - 90)
        angle2 = math.radians(((i + 1) % num_nodes) * 60 - 90)
        n1x = cx + node_r_outer * math.cos(angle1)
        n1y = cy + node_r_outer * math.sin(angle1)
        n2x = cx + node_r_outer * math.cos(angle2)
        n2y = cy + node_r_outer * math.sin(angle2)
        thin_w = max(1, size // 80)
        draw.line([(n1x, n1y), (n2x, n2y)], fill=(60, 130, 220, 80), width=thin_w)

    # Outer nodes (small bright circles)
    for i in range(num_nodes):
        angle = math.radians(i * 60 - 90)
        nx = cx + node_r_outer * math.cos(angle)
        ny = cy + node_r_outer * math.sin(angle)
        ns = node_size_outer
        # Glow
        draw.ellipse([nx - ns * 1.5, ny - ns * 1.5, nx + ns * 1.5, ny + ns * 1.5],
                     fill=(80, 160, 255, 40))
        # Node
        draw.ellipse([nx - ns, ny - ns, nx + ns, ny + ns],
                     fill=(100, 185, 255, 255))

    # Center hub — larger bright circle with glow
    hub_r = size * 0.14
    # Outer glow
    draw.ellipse([cx - hub_r * 1.6, cy - hub_r * 1.6, cx + hub_r * 1.6, cy + hub_r * 1.6],
                 fill=(60, 140, 255, 35))
    draw.ellipse([cx - hub_r * 1.3, cy - hub_r * 1.3, cx + hub_r * 1.3, cy + hub_r * 1.3],
                 fill=(70, 150, 255, 60))
    # Main hub circle
    draw.ellipse([cx - hub_r, cy - hub_r, cx + hub_r, cy + hub_r],
                 fill=(90, 170, 255, 255))
    # Inner highlight
    hi_r = hub_r * 0.55
    draw.ellipse([cx - hi_r, cy - hi_r - hub_r * 0.1, cx + hi_r, cy + hi_r - hub_r * 0.1],
                 fill=(160, 210, 255, 120))

    # "C" letter in the center hub
    if size >= 32:
        font_size = int(hub_r * 1.4)
        try:
            font = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", font_size)
        except:
            try:
                font = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", font_size)
            except:
                font = ImageFont.load_default()

        bbox = draw.textbbox((0, 0), "C", font=font)
        tw = bbox[2] - bbox[0]
        th = bbox[3] - bbox[1]
        tx = cx - tw / 2 - bbox[0]
        ty = cy - th / 2 - bbox[1]
        # Shadow
        draw.text((tx + 1, ty + 1), "C", fill=(20, 40, 80, 180), font=font)
        # Letter
        draw.text((tx, ty), "C", fill=(255, 255, 255, 255), font=font)

    return img


def main():
    sizes = [256, 128, 64, 48, 32, 16]
    frames = [create_icon(s) for s in sizes]

    ico_path = r"c:\Development\Central\desktop\Central.Desktop\central.ico"
    frames[0].save(
        ico_path,
        format='ICO',
        sizes=[(s, s) for s in sizes],
        append_images=frames[1:]
    )
    print(f"Created {ico_path} with sizes: {sizes}")

    # Also save a 256px PNG for preview
    png_path = r"c:\Development\Central\desktop\Central.Desktop\central_icon_preview.png"
    frames[0].save(png_path)
    print(f"Preview: {png_path}")


if __name__ == '__main__':
    main()
