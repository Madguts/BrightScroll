# BrightScroll

BrightScroll is a Python application that allows you to adjust the brightness of your monitors using a system tray icon. The application uses `pystray` to create a system tray icon and `screen_brightness_control` to adjust the brightness of the monitors.

## Features

- Adjust the brightness of each monitor individually.
- Use a system tray icon to access the brightness controls.
- Smooth and intuitive user interface using `tkinter` and `ttkbootstrap`.

## Requirements

- Python 3.x
- `pystray`
- `Pillow`
- `screen_brightness_control`
- `tkinter`
- `ttkbootstrap`

## Installation

1. Clone the repository:
    ```sh
    git clone https://github.com/yourusername/BrightScroll.git
    cd BrightScroll
    ```

2. Install the required packages:
    ```sh
    pip install pystray pillow screen_brightness_control ttkbootstrap
    ```

## Usage

Run the [BrightScroll.py] script:
```sh
python BrightScroll.py