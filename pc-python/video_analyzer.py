import cv2
import numpy as np
from threading import Event, Lock, Thread
# from datetime import datetime
import mediapipe as mp

import vlm
import wss
import os
import glob

# Shared vars ------------------------------------------------------
new_grid_available = Event()    # To update gui
new_grid_available.clear()
grid_img_lock = Lock()              # For safely accessing grid for display in gui

# Vars -------------------------------------------------------------
grid_img = None
gui_grid_img = None

prepared_frames = []            # Numbered frames
# lh_positions = []               # 26 joint positions left hand
# lh_rotations = []               # 26 joint rotations left hand
# rh_positions = []               # 26 joint positions right hand
# rh_rotations = []               # 26 joint rotations right hand

time_to_delete = 2 # Trim captured frames on client side 

relevant_actions = ["pick and place", "press button", "rotate switch", "open/close", "use Allen", "use screwdriver", "use wrench", "fasten by hand", "other"]

# Methods ---------------------------------------------------
def prepare_frame(frame, numbered, f_number):
    """
    Adds numbering to a single frame

    Args:
        frame (np.ndarray): Sampled frame
        numbered: Should frames be numbered? If non-null, specifies position of numbering
        f_number: Number of this frame
        
    Returns:
        None
    """
    
    # Numbering
    if not numbered:
        numbered = "middle-right"

    # Frame dimensions
    h, w = frame.shape[:2]
    # Radius
    R = 80
    
    # Place numbering center based on specified position
    pos_map = {
        "top-left":     (R+40, R+40),
        "top-right":    (w-R-40, R+40),
        "bottom-left":  (R+40, h-R-40),
        "bottom-right": (w-R-40, h-R-40),
        "center":       (w//2, h//2),
        "top-center":   (w//2, R+40),
        "bottom-center":(w//2, h-R-40),
        "middle-left":  (R+100, h//2),
        "middle-right": (w-R-40, h//2),
    }
            
    # Check if given input valid
    if numbered in pos_map:
        # Blend number on to orignal frame
        cX, cY = pos_map.get(numbered)
        number_overlay = frame.copy()
        cv2.circle(number_overlay, (cX,cY), R, (255,255,255), -1)
        blended_frame = cv2.addWeighted(number_overlay,1.0,frame,0.0,0)
        cv2.circle(blended_frame, (cX,cY), R, (255,255,255), 8)

        # Put number on frame image
        sz = cv2.getTextSize(str(f_number), cv2.FONT_HERSHEY_SIMPLEX, 3.0, 8)[0]
        tx = cX - sz[0]//2
        ty = cY + sz[1]//2
        cv2.putText(blended_frame, str(f_number), (tx,ty), cv2.FONT_HERSHEY_SIMPLEX, 3.0, (0,0,0), 8, cv2.LINE_AA)
        
    # Cache numbered frame for later analysis by vlm
    prepared_frames.append(blended_frame)
    # print(f"Sampled and prepared frame {f_number}")

    return

def grid_from_prepared_frames():
    # Remove 2 seocond of frames at the end to mimic trimming (e.g. at sample interval of 15, delete 4 frames as we are capturing at 30fps)
    # frames_to_delete = 30 * time_to_delete //  wss.sample_interval
    # del prepared_frames[-frames_to_delete:]

    # Create grid from prepared frames
    prepare_grids(prepared_frames, 6, len(prepared_frames) // 6 + 1, False)

    return
    
def extract_frames_with_hands():
    """
    Extracts frames with hands from prepared frames using MediaPipe. Also adds buffer frames (2 before first and after last hand frame)

    Args:
        None
    Return:
        None
    """

    # Initialize mediapipe stuff
    mp_hands = mp.solutions.hands
    hand_detector = mp_hands.Hands(static_image_mode=True, max_num_hands=2, min_detection_confidence=0.4)

    hand_indices = []
    global prepared_frames
    for index, frame in enumerate(prepared_frames):
        # Convert to RGB for mediapipe
        rgb_frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)

        # Detect hands
        results = hand_detector.process(rgb_frame)
        if results.multi_hand_landmarks:
            hand_indices.append(index)

    # Quit hand detection
    hand_detector.close()

    # Fallback if nohands detected
    if len(hand_indices) == 0:
        print("Did not detect any hands in captured video")

        # Inform server about suitable trim length at the start and end of capturing (0)
        no_trim = wss.TrimLengths(0, 0)
        wss.send_msg(wss.MsgType.TRIMLENGTH, no_trim.to_json())

        return

    print(f"Detected hands in frame(s): {hand_indices}")

    # Inidices of buffer frames before first and after last hand frame. Being conservative for start frames
    start_index = max(hand_indices[0] - 3, 0)
    end_index = min(hand_indices[-1] + 4, len(prepared_frames))  # end index is exclusive

    # Extract the frames with hands and buffer frames
    frames_to_analyze = prepared_frames[start_index:end_index]

    print(f"Reduced frames to analyze from {len(prepared_frames)} to {len(frames_to_analyze)}")
    print(f"Removed {start_index} frames at start and {len(prepared_frames)-end_index} frames at end")
    
    # Calculate trim times from start and end
    trim_from_end = (len(prepared_frames) - end_index) * wss.sample_interval / 30
    trim_from_start = start_index * wss.sample_interval /30
    print(f"Recommending to trim captured video from start by {trim_from_start}s and end by {trim_from_end}s")

    # Assign to global prepared frames
    prepared_frames[:] = frames_to_analyze

    # Inform server about suitable trim length at the end of capturing
    trim_times = wss.TrimLengths(trim_from_start, trim_from_end)
    wss.send_msg(wss.MsgType.TRIMLENGTH, trim_times.to_json())

def analyze_frames(english, short):
    """
    Makes request to ChatGPT to analyze sampled frames

    Args:
        english: Whether to make request using English prompt. If False -> German
        short: Whether to use short prompt, only action and frame(s). If False -> long prompt with additional information
        
    Returns:
        None
    """

    # Prompt chatgpt with preprocessed image 
    try:    
        chatgpt_thread = Thread(target=vlm.request_gpt_multi_frame, args=[prepared_frames, english, short, relevant_actions])
        chatgpt_thread.daemon = True
        chatgpt_thread.start()
    except Exception as e:
        print(f"Failed to query ChatGPT: {e}")
        
    # Save frames to disk into workflow folder
    for i, frame in enumerate(prepared_frames):
        save_path = f"./Workflows/{wss.current_wf_name}/step_{wss.current_step}/frame_{i}.jpg"
        cv2.imwrite(save_path, frame)

    print("Wrote frames to disk")
    
    return

def reanalyze_frames(english, short, regen_info):
    """
    Make repeated request to ChatGPT to reanaylze frames due to false recognition. 

    Args:
        english: Whether to make request using English prompt. If False -> German
        short: Whether to use short prompt, only action and frame(s). If False -> long prompt with additional information
        regen_info: Information provided by hl2 server regarding relevant step to regenerate for and previously false recognized action (to exclude)
        
    Returns:
        None
    """

    # Load relevant frames based on specified step to regenerate for
    # Load only images and in order
    frame_list = glob.glob(os.path.join(f"./Workflows/{wss.current_wf_name}/step_{regen_info.step_number}/", "frame_*.jpg"))
    frame_list.sort(key=lambda x: int(os.path.basename(x).split('_')[1].split('.')[0]))

    # Load
    global prepared_frames
    prepared_frames.clear()
    prepared_frames = [cv2.imread(f) for f in frame_list]
    print(f"Loaded {len(prepared_frames)} frames for step {regen_info.step_number}")
    
    # Prompt chatgpt with preprocessed image and remove irrelevant action based on previous false recognition
    filtered_actions = [a for a in relevant_actions if a != regen_info.false_action]
    try:    
        chatgpt_thread = Thread(target=vlm.request_gpt_multi_frame, args=[prepared_frames, english, short, filtered_actions])
        chatgpt_thread.daemon = True
        chatgpt_thread.start()
    except Exception as e:
        print(f"Failed to query ChatGPT: {e}")
    
    return

def save_frames_as_video(frames):
    """
    Create video based on frames provided.

    Args:
        frames: Frames to combine into single video

    Returns:
        void
    """
    # Create video from frames and save in files
    # Define video properties
    height, width = frames[0].shape[:2]
    fps = 30 
    output_filename = f"./Workflows/{wss.current_wf_name}/step_{wss.current_step}/video_instruction.mp4"

    # Define codec and create video writer object
    fourcc = cv2.VideoWriter_fourcc(*"mp4v") 
    out = cv2.VideoWriter(output_filename, fourcc, fps, (width, height))

    # Write frames to video
    for frame in frames:
        out.write(frame)

    # Release video writer object
    out.release()   
    
    # Clear stored frames to prepare for next image stream
    frames.clear()
    
    return

def resize_grid_image(image, max_width=1980, max_height=760):
    """
    Resizes created grid image to the maxium processable image resolution by current vision model of openAI (2048x768).
    Keeps aspect ratio so that slightly smaller image size targetted

    Args:
        image: the grid image to be resizes
        max_width: defaults to 1980
        max_height: defaults to 760

    Returns:
        np.ndarray: The resized grid image.
    """
    h, w = image.shape[:2]
    scale_factor = min(max_width / w, max_height / h, 1.0) 
    new_width, new_height = int(w * scale_factor), int(h * scale_factor)
    
    resized_grid_img = cv2.resize(image, (new_width, new_height), interpolation=cv2.INTER_AREA)

    return resized_grid_img

def prepare_grids(frames, grid_cols, grid_rows, numbered=True, position="middle-right", colored_borders=False):
    """
    Creates a grid from a list of frames.

    Args:
        frames (list of np.ndarray): List of images (must be np.uint8 and same size).
        grid_cols (int): Number of columns in the grid.
        grid_rows (int): Number of rows in the grid.
        numbered (bool): Whether to add numbering to frames.
        position (str): Where to place the numbers (e.g., "top-right", "center", "middle-right").
        use_border (bool): Whether to separate frames with a fixed black border (default: False).

    Returns:
        None
    """
  
    with grid_img_lock:
        global grid_img  
        
        border_thickness = 10 if colored_borders else 0

        num_frames = len(frames)
        # print(f"Obtained {num_frames} in total")
        expected_frames = grid_cols * grid_rows

        # Check if sufficient number of frames provided
        if num_frames < expected_frames:
            print(f"Not enough frames to fill grid completely.")

        # Get frame size
        if not frames:
            print("No frames provided to create grid.")
            return
        frame_height, frame_width = frames[0].shape[:2]

        # Create empty grid image with optional border space
        grid_height = grid_rows * frame_height + (grid_rows - 1) * border_thickness
        grid_width = grid_cols * frame_width + (grid_cols - 1) * border_thickness
        grid_img = np.ones((grid_height, grid_width, 3), dtype=np.uint8) * 255  # White background

        # If border is enabled, set background to border color
        if colored_borders:
            border_color = (255, 59, 219) # Pink borders
            grid_img[:] = border_color

        # Select only every other frame depending on total number of frames and grid size (evenly spaced)
        step_size = len(frames) // (grid_rows * grid_cols)
    
        index = 0
        for i in range(grid_rows):
            for j in range(grid_cols):
                # Make sure we are not out of bounce (as there could be less frames than fit into a complete grid)
                if index >= len(frames):  
                    break
                frame = frames[index].copy()

                # Add numbering if enabled
                if numbered:
                    overlay = frame.copy()
                    circle_radius = 150  # Adjusted for readability
                    circle_color = (255, 255, 255)  # White
                    text_color = (0, 0, 0)  # Black
                    font_scale = 5.0
                    thickness = 4
                    edge_offset = 40

                    # Determine circle position
                    position_map = {
                        "top-left": (circle_radius + edge_offset, circle_radius + edge_offset),
                        "top-right": (frame.shape[1] - circle_radius - edge_offset, circle_radius + edge_offset),
                        "bottom-left": (circle_radius + edge_offset, frame.shape[0] - circle_radius - edge_offset),
                        "bottom-right": (frame.shape[1] - circle_radius - edge_offset, frame.shape[0] - circle_radius - edge_offset),
                        "center": (frame.shape[1] // 2, frame.shape[0] // 2),
                        "top-center": (frame.shape[1] // 2, circle_radius + 20),
                        "bottom-center": (frame.shape[1] // 2, frame.shape[0] - circle_radius - edge_offset),
                        "middle-left": (circle_radius + 100, frame.shape[0] // 2),
                        "middle-right": (frame.shape[1] - circle_radius - edge_offset, frame.shape[0] // 2),
                    }
                    circle_center = position_map.get(position, position_map["top-right"])  # Default to top-right

                    cv2.circle(overlay, circle_center, circle_radius, circle_color, -1)  # Fill circle
                    frame = cv2.addWeighted(overlay, 1.0, frame, 0.0, 0)
                    cv2.circle(frame, circle_center, circle_radius, circle_color, thickness)  # Circle border

                    # Add number (opencv starts at top left corner)
                    text_size = cv2.getTextSize(str(index // step_size), cv2.FONT_HERSHEY_SIMPLEX, font_scale, thickness)[0]
                    text_x = circle_center[0] - text_size[0] // 2
                    text_y = circle_center[1] + text_size[1] // 2
                    cv2.putText(frame, str(index // step_size), (text_x, text_y), cv2.FONT_HERSHEY_SIMPLEX, font_scale, text_color, thickness, cv2.LINE_AA)

                # Calculate position in grid
                y1, y2 = i * (frame_height + border_thickness), i * (frame_height + border_thickness) + frame_height
                x1, x2 = j * (frame_width + border_thickness), j * (frame_width + border_thickness) + frame_width
                grid_img[y1:y2, x1:x2] = frame
                
                index += 1
            
        # Resize grid image to maximum supported resolution (dictated by VLM)
        # grid_img = resize_grid_image(grid_img)
        
        print("Image stream has been processed to a grid")
    
    # Resize grid image for visualization in gui (750x288)
    with grid_img_lock:
        global gui_grid_img
        gui_grid_img = resize_grid_image(grid_img, 750, 288)    
        # Signal main thread (gui) that new grid is ready for render
        new_grid_available.set()

    return

def prepare_frames(frames, sample_interval, numbered):
    """
    Samples frames and adds optional numbering

    Args:
        frames (list of np.ndarray): List of images (must be np.uint8 and same size).
        sample_interval: At what rate should frames be sampled (e.g. 15: every 15th frame is sampled -> basically 2fps @30fps capturing)
        numbered: Should frames be numbered?
        
    Returns:
        sampled_frames: Sampled and optionally numbered frames.
    """
    
    # Frame information
    num_frames = len(frames)
    h, w = frames[0].shape[:2]
    print(f"Total number of frames in this video: {num_frames}; Resolution: {w}x{h}")
    
    # Sample frames at specified interval/rate
    sampled_frames = []
    for i in range(sample_interval, num_frames, sample_interval):
        # Get frame
        # frame = frames[i].copy()
        
        # Numbering
        if not numbered:
            numbered = "middle-right"

        # Radius
        R = 80
        # Place numbering center based on specified position
        pos_map = {
            "top-left":     (R+40, R+40),
            "top-right":    (w-R-40, R+40),
            "bottom-left":  (R+40, h-R-40),
            "bottom-right": (w-R-40, h-R-40),
            "center":       (w//2, h//2),
            "top-center":   (w//2, R+40),
            "bottom-center":(w//2, h-R-40),
            "middle-left":  (R+100, h//2),
            "middle-right": (w-R-40, h//2),
        }
            
        # Check if given input valid
        if numbered in pos_map:
            # Determine position and characteristics of number in frame
            cX, cY = pos_map.get(numbered)
            cv2.circle(frames[i], (cX,cY), R, (255,255,255), -1)
            frm = cv2.addWeighted(frames[i],1.0,frm,0.0,0)
            cv2.circle(frm, (cX,cY), R, (255,255,255), 4)

            # Put number on frame image
            sz = cv2.getTextSize(str(i), cv2.FONT_HERSHEY_SIMPLEX, 3.0, 4)[0]
            tx = cX - sz[0]//2
            ty = cY + sz[1]//2
            cv2.putText(frm, str(i), (tx,ty), cv2.FONT_HERSHEY_SIMPLEX, 3.0, (0,0,0), 8, cv2.LINE_AA)

        # Cache frame
        sampled_frames.append(frames[i])
        
    # Prompt chatgpt with preprocessed image 
    try:    
        chatgpt_thread = Thread(target=vlm.request_gpt_multi_frame, args=sampled_frames)
        chatgpt_thread.daemon = True
        chatgpt_thread.start()
    except Exception as e:
        print(f"Failed to query ChatGPT: {e}")

    return sampled_frames