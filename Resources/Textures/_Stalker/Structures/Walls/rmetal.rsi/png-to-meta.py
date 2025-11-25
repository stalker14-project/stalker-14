import json
import os
import tkinter as tk
from tkinter import simpledialog
from PIL import Image

# Генерація meta.json
def generate_meta_json(width, height, directions):
    # Збирає всі png файли в папці
    png_files = [f for f in os.listdir('.') if f.endswith('.png')]

    # Создает список states
    states = []
    for file in png_files:
        # Перевіряє роздільну здатність зображення
        with Image.open(file) as img:
            img_width, img_height = img.size

        state = {"name": file.replace('.png', '')}
        # Додає "directions" тільки якщо під час запуску їх зазначено >1 і роздільна здатність файлу не збігається із зазначеною роздільною здатністю
        if (img_width != width or img_height != height) and directions > 1:
            state["directions"] = directions

        states.append(state)

    # Генерація заповнення міти
    meta = {
        "version": 1,
        "license": "CC-BY-SA-3.0",
        "copyright": "Python generated",
        "size": {
            "x": width,
            "y": height
        },
        "states": states
    }

    # Збереження JSON
    with open('meta.json', 'w') as json_file:
        json.dump(meta, json_file, indent=4)

# Інтерфейс вся хуйня
def ask_user_input():
    root = tk.Tk()
    root.withdraw()  # Приховати сміття

    # Спросить за базар
    width = simpledialog.askinteger("Input", "Ширина в пікселях (X):", parent=root, minvalue=1)
    height = simpledialog.askinteger("Input", "Висота в пікселях (Y):", parent=root, minvalue=1)
    directions = simpledialog.askinteger("Input", "Кількість напрямків:", parent=root, minvalue=0)

    # Закінчити з генерацією
    if width is not None and height is not None and directions is not None:
        generate_meta_json(width, height, directions)

ask_user_input()

