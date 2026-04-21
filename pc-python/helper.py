# Defines utility helper methods

# Imports
# from cgitb import text
# import tkinter as tk
from PIL import Image, ImageTk
import cv2

# Load, resize and create tk images. Adjusting width based on height and relative scale
def create_img(path, desired_height):
    # Load images
    img = Image.open(path)

    # Resize images fitting height
    current_width, currentHeight = img.size
    desired_width = int(current_width * (desired_height / currentHeight))

    img = img.resize((desired_width, desired_height))

    # Create tk images
    tk_img = ImageTk.PhotoImage(img)
    
    return tk_img

# Used to print messages to text widget
def print_to_text_widget(msg, text_widget, new_line=True):
    # Need to enable text widget first
    text_widget.config(state="normal")    

    # Remove any existing new lines at the end
    msg = msg.rstrip() + ("\n" if new_line else "")
    text_widget.insert("1.0", f"{msg}")

    # Disable widget again to prevent editing of messages
    text_widget.config(state="disabled")

# Convert image to tkinter representable type
def opencv_in_tkinter(img):    
    # Convert bgr to rgb
    rgb_img = cv2.cvtColor(img, cv2.COLOR_BGR2RGB)
    # Convert it to a PIL image
    pil_img = Image.fromarray(rgb_img)
    # Convert to ImageTk.PhotoImage for tkinter
    tk_img = ImageTk.PhotoImage(pil_img)

    return tk_img
