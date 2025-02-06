import pystray
from PIL import Image, ImageDraw
import screen_brightness_control as sbc
import tkinter as tk
from ttkbootstrap import Style
import ctypes
from math import cos, sin, radians

window_open = False
style = None

def create_brightness_icon():
    image = Image.new('RGBA', (64, 64), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    draw.ellipse((16, 16, 48, 48), outline='white', width=4)
    for i in range(8):
        angle = i * 45
        x1, y1 = 32 + 24 * cos(radians(angle)), 32 + 24 * sin(radians(angle))
        x2, y2 = 32 + 32 * cos(radians(angle)), 32 + 32 * sin(radians(angle))
        draw.line((x1, y1, x2, y2), fill='white', width=7)
    return image

def on_clicked(icon, item):
    global window_open, style
    if window_open:
        return

    def on_closing(event=None):
        global window_open
        if root.winfo_exists():
            window_open = False
            root.destroy()

    def update_brightness(value, display, label_var):
        brightness = int(float(value))
        sbc.set_brightness(brightness, display=display)
        label_var.set(f"{display[:5]}: {brightness}%")

    def on_mouse_wheel(event, slider):
        if event.delta > 0:
            slider.set(slider.get() + 10)
        else:
            slider.set(slider.get() - 10)

    window_open = True
    root = tk.Tk()
    root.title("Adjust Brightness")
    root.overrideredirect(True)

    if style is None:
        style = Style()
        style.theme_use('darkly')

    user32 = ctypes.windll.user32
    screen_width, screen_height = user32.GetSystemMetrics(0), user32.GetSystemMetrics(1)
    root.geometry(f"+{screen_width - 420}+{screen_height - 190}")

    root.bind('<FocusOut>', on_closing)

    frame = tk.Frame(root)
    frame.pack(fill='both', expand=True, padx=10, pady=10)
    frame.pack_propagate(True)

    for monitor in sbc.list_monitors():
        current_brightness = sbc.get_brightness(display=monitor)[0]

        label_var = tk.StringVar()
        label_var.set(f"{monitor[:5]}: {current_brightness}%")
        label_frame = tk.Frame(frame)
        label_frame.pack(side='left', fill='y', expand=True, padx=1)

        tk.Label(label_frame, textvariable=label_var, width=15, anchor='center').pack(side='top')

        slider = tk.Scale(label_frame, from_=100, to=0, orient='vertical', command=lambda value, m=monitor, lv=label_var: update_brightness(value, m, lv))
        slider.set(current_brightness)
        slider.pack(side='top', fill='y', expand=True)

        slider.bind("<MouseWheel>", lambda event, s=slider: on_mouse_wheel(event, s))

    root.update_idletasks()
    root.geometry(f"{frame.winfo_reqwidth()}x{frame.winfo_reqheight()}")

    root.focus_force()
    root.mainloop()

def setup(icon):
    icon.visible = True
    icon.icon = create_brightness_icon()
    icon.run = on_clicked

icon = pystray.Icon("brightness_icon", create_brightness_icon(), "Brightness", menu=pystray.Menu(
    pystray.MenuItem("Adjust Brightness", on_clicked, default=True),
    pystray.MenuItem("Quit", lambda icon, item: icon.stop())
))

icon.run(setup=setup)