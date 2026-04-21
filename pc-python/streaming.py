"""
Module Name: streaming

Description:

Usage:
    1. 

Dependencies:
    - threading
    - cv2
"""

# Modules ----------------------------------------------------------------------------------------
import cv2 # opencv
from threading import Event, Thread, Lock

import hl2ss 
import hl2ss_lnm
# import hl2ss_mp
# import hl2ss_3dcv
# import hl2ss_utilities

# Custom modules ----------------------------------------------------------------------------------
from video_analyzer import save_frames_as_video, prepare_frame, analyze_frames
# import wss

# Shared vars -------------------------------------------------------------------------------------
hl2ss_connected = Event()       # Whether hl2ss is connected
hl2ss_connected.clear()
hl2ss_state_changed = Event()   # Whether hl2ss connection state has changed
hl2ss_state_changed.clear()

show_pv = Event()               # Whether streamed pv should be visualized
show_pv.clear()

user_is_capturing = Event()		# Indicates whether user is currently capturing on the hl2
user_is_capturing.clear()

assisted_mode = Event()          # Whether assisted mode is enabled (instead of manual mode)
assisted_mode.clear()

process_frames = Event()        # Flag indicating whether previously captured frames need to be processed (final video capture has been confirmed by user)
process_frames.clear()

# Class vars --------------------------------------------------------------------------------------
capture_frames = []             # All frames from capture stream from user
sample_interval = 10            # Interval at which frames are to be sampled (15: equates to about 2fps)
frame_counter = 0               # Frame counter used for sampling

# Settings for hl2ss streaming --------------------------------------------------------------------
def connect_stream(hl2_ip_string):
    """
    Establishes connection with hl2ss streaming server on hl2. 
    Captures frames if user on hl2 is authoring video instruction.
    Visualizes pv stream if specified by user in gui
    
    Args:
        hl2_ip_string: ip address of hl2 in current network received via websocket connection

    Returns:
        void
    """
    # HoloLens address
    # hl2ss_host = "192.168.178.73"

    # Operating mode
    # 0: video
    # 1: video + camera pose
    # 2: query calibration (single transfer)
    mode = hl2ss.StreamMode.MODE_0

    # Enable Mixed Reality Capture (Holograms)
    enable_mrc = True

    # Enable Shared Capture
    # If another program is already using the PV camera, you can still stream it by
    # enabling shared mode, however you cannot change the resolution and framerate
    shared = False

    # Camera parameters
    # Ignored in shared mode
    width     = 1504
    height    = 846
    framerate = 30

    # Framerate denominator (must be > 0)
    # Effective FPS is framerate / divisor
    divisor = 1

    # Video encoding profile and bitrate (None = default)
    profile = hl2ss.VideoProfile.H265_MAIN
    bitrate = None

    # Decoded format
    # Options include:
    # 'bgr24'
    # 'rgb24'
    # 'bgra'
    # 'rgba'
    # 'gray8'
    decoded_format = 'bgr24'

    # Marker properties for hand joint visualization
    # radius = 5
    # head_color  = (  0,   0, 255)
    # left_color  = (  0, 255,   0)
    # right_color = (255,   0,   0)
    # gaze_color  = (255,   0, 255)
    # thickness = -1
    
    # Spatial Input sampling delay
    # Delays spatial input readouts by the specified time (in hundreds of nanoseconds)
    # Negative values (future timestamps) enable HoloLens predictions
    # sampling_delay = hl2ss.TimeBase.HUNDREDS_OF_NANOSECONDS // hl2ss.Parameters_SI.SAMPLE_RATE

    # Set spatial input sampling delay
    # client_rc = hl2ss_lnm.ipc_rc(hl2_ip_string, hl2ss.IPCPort.REMOTE_CONFIGURATION)
    # client_rc.open()
    # client_rc.si_set_sampling_delay(sampling_delay)
    # client_rc.close()

    # Start hl2ss streaming ------------------------------------------------------------------------------
    hl2ss_lnm.start_subsystem_pv(hl2_ip_string, hl2ss.StreamPort.PERSONAL_VIDEO, enable_mrc=enable_mrc, shared=shared)
    
    client = hl2ss_lnm.rx_pv(hl2_ip_string, hl2ss.StreamPort.PERSONAL_VIDEO, mode=mode, width=width, height=height, framerate=framerate, divisor=divisor, profile=profile, bitrate=bitrate, decoded_format=decoded_format)
    client.open()

    # sink_pv = hl2ss_mp.stream(hl2ss_lnm.rx_pv(hl2_ip_string, hl2ss.StreamPort.PERSONAL_VIDEO, width=width, height=height, framerate=framerate))
    # sink_si = hl2ss_mp.stream(hl2ss_lnm.rx_si(hl2_ip_string, hl2ss.StreamPort.SPATIAL_INPUT))

    # sink_pv.open()    
    # sink_si.open()

    # Flag as connected
    hl2ss_connected.set()
    hl2ss_state_changed.set()
    
    print("Established hl2ss streaming connection")

    while (hl2ss_connected.is_set()):
        # Get pv data frames
        try:    
            data_pv = client.get_next_packet()
            # _, _, data_pv = sink_pv.get_buffered_frame(-4)
            if (data_pv is None):
                continue
            pv_img = data_pv.payload.image
            
            # if (not hl2ss.is_valid_pose(data_pv.pose)):
            #     if show_pv.is_set():
            #         cv2.imshow("Stream", pv_img)
            #     continue          
        except:
            
            continue
        
        """
        # Get spatial input data frames (hand joints, gaze, head) -> Need only hand joints
        try:
            _, data_si = sink_si.get_nearest(data_pv.timestamp)
            if (data_si is None):
                if show_pv.is_set():
                    cv2.imshow("Stream", pv_img)
                continue
            si = data_si.payload      
            
            # Update PV intrinsics ------------------------------------------------
            # PV intrinsics may change between frames due to autofocus
            # pv_intrinsics = hl2ss_3dcv.pv_create_intrinsics(data_pv.payload.focal_length, data_pv.payload.principal_point)
            # pv_extrinsics = np.eye(4, 4, dtype=np.float32)
            # pv_intrinsics, pv_extrinsics = hl2ss_3dcv.pv_fix_calibration(pv_intrinsics, pv_extrinsics)

            # Compute world to PV image transformation matrix ---------------------
            # world_to_image = hl2ss_3dcv.world_to_reference(data_pv.pose) @ hl2ss_3dcv.rignode_to_camera(pv_extrinsics) @ hl2ss_3dcv.camera_to_image(pv_intrinsics)

            # Arrays containing this frames left and right hand joint positions and rotations. 2 elements each. Each element has 26 entries (joints)
            positions = []
            rotations = []
            
            # Left hand joints -----------------------------------------------
            if (si.hand_left_valid):
                left_hand = si.hand_left
                
                # Provides position, orientation, radius (distance joint to skin), and accuracy/confidence for 26 joints (palm redundant)
                lh_pos = left_hand.position
                lh_rot = left_hand.orientation
                
                positions.append(lh_pos)
                rotations.append(lh_rot)

                # Draw onto image
                # left_image_points = hl2ss_3dcv.project(left_hand.position, world_to_image)
                # hl2ss_utilities.draw_points(pv_img, left_image_points.astype(np.int32), radius, left_color, thickness)

            # Right hand joints ----------------------------------------------
            if (si.hand_right_valid):
                right_hand = si.hand_right
                
                # Provides position, orientation, radius (distance joint to skin), and accuracy/confidence for 26 joints (palm redundant) 
                rh_pos = left_hand.position
                rh_rot = left_hand.orientation
                
                positions.append(rh_pos)
                rotations.append(rh_rot)

                # Draw onto image
                # right_image_points = hl2ss_3dcv.project(right_hand.position, world_to_image)
                # hl2ss_utilities.draw_points(pv_img, right_image_points.astype(np.int32), radius, right_color, thickness)
        except:
            print("Failed to get spatial input data frame")
            continue
        """
            
        # Process incoming frames if currently authoring/capturing on hl2
        global process_frames
        if user_is_capturing.is_set():
            # Frame sampling in assisted mode only
            if assisted_mode.is_set():

                # Sample only every (sample_interval)-th frame
                global frame_counter
                frame_counter += 1

                if not frame_counter % sample_interval:                
                    # Prepare frame (numbering)
                    prep_frame_thread = Thread(target=prepare_frame, args=[pv_img, "middle-right", frame_counter // sample_interval])
                    prep_frame_thread.daemon = True
                    prep_frame_thread.start()
            
            # Cache captured frames
            capture_frames.append(pv_img)
            
            # Flag as processing needed
            # process_frames.set()
        else:
            # If just finalized video instruction
            if process_frames.is_set():
                # Make request to VLM using sampled frames (only in assisted mode)
                if assisted_mode.is_set():
                    request_thread = Thread(target=analyze_frames, args=[True, False])
                    request_thread.daemon = True
                    request_thread.start()
                
                # Save video from frames and save (in both modes)
                save_video_thread = Thread(target=save_frames_as_video, args=[capture_frames])
                save_video_thread.daemon = True
                save_video_thread.start()
                  
                # Process frames only once
                process_frames.clear()

            # Reset frame counter
            frame_counter = 0

        # Display pv frame (if desired)
        if show_pv.is_set():
            cv2.imshow("Stream", pv_img)
        else:
            if cv2.getWindowProperty("Stream", cv2.WND_PROP_VISIBLE) == 1:
                cv2.destroyWindow("Stream")
            
        cv2.waitKey(1)

    # Close connection    
    client.close()
    # sink_pv.close()
    # sink_si.close()
    print("Closed hl2ss streaming connection")

    # hl2ss_connected.clear()
    hl2ss_state_changed.set()

    # Stop hl2ss streaming -------------------------------------------------------------------
    hl2ss_lnm.stop_subsystem_pv(hl2_ip_string, hl2ss.StreamPort.PERSONAL_VIDEO)

# Method to create and save video based on captured frames
def create_video_from_frames(frames):
    """
    Creates video from provided frames and writes to disk
    
    Args:
        frames: Frames to write to disk
        

    Returns:
        void
    """